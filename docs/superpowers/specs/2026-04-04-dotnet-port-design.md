# MagnumDocker .NET Port — Design Spec

## Overview

Port the PyMagnumDocker application from Python to .NET 8, including porting the `pymagnum` RS485 serial protocol library to C#. The result is a two-project .NET solution that produces the same HTTP API and serves the same frontend.

## Solution Structure

```
MagnumDocker/
├── MagnumDocker.sln
├── src/
│   ├── Magnum.Serial/          # Class library — RS485 protocol
│   │   ├── Magnum.Serial.csproj
│   │   ├── MagnumReader.cs
│   │   ├── PacketIdentifier.cs
│   │   ├── Devices/
│   │   │   ├── DeviceData.cs
│   │   │   ├── InverterDevice.cs
│   │   │   ├── RemoteDevice.cs
│   │   │   ├── BmkDevice.cs
│   │   │   ├── AgsDevice.cs
│   │   │   ├── Pt100Device.cs
│   │   │   ├── RtrDevice.cs
│   │   │   └── AcldDevice.cs
│   │   └── Lookups/
│   │       ├── InverterModels.cs
│   │       ├── InverterModes.cs
│   │       ├── InverterFaults.cs
│   │       ├── AgStatuses.cs
│   │       └── Pt100Lookups.cs
│   └── Magnum.Server/          # Minimal API — HTTP server
│       ├── Magnum.Server.csproj
│       ├── Program.cs
│       ├── appsettings.json
│       └── wwwroot/
│           └── index.html       # Existing frontend, unchanged
├── Dockerfile
├── compose.yml
└── docs/
```

## Magnum.Serial — Class Library

### Serial Communication (MagnumReader)

Opens `System.IO.Ports.SerialPort` with fixed parameters:
- Baud: 19200
- Data bits: 8
- Stop bits: 1
- Parity: None
- DSR/DTR: disabled
- Read timeout: configurable (default 5ms)

**Reading flow:**
1. Read available bytes from serial port
2. Accumulate into byte buffer
3. When read returns 0 bytes (timeout), treat buffer as a complete packet
4. Store packet, start new buffer
5. Repeat until configured packet count reached
6. Run cleanup/merge on consecutive unknown packets
7. Return parsed device data

### Configuration

```csharp
public class MagnumReaderOptions
{
    public string Device { get; set; } = "/dev/ttyUSB0";
    public int Packets { get; set; } = 50;
    public double Timeout { get; set; } = 0.005;
    public bool Trace { get; set; } = false;
    public bool CleanPackets { get; set; } = true;
}
```

### Packet Identification (PacketIdentifier)

Classifies raw `byte[]` by length, first byte, and last byte:

| Length | First Byte | Last Byte | Other Checks | Type |
|--------|-----------|-----------|--------------|------|
| 2 | 0x91 | — | — | RTR_91 |
| 6 | 0xA1 | — | — | AGS_A1 |
| 6 | 0xA2 | — | — | AGS_A2 |
| 8 | 0xD1 | — | — | ACLD_D1 |
| 13 | 0xC2 | — | — | PT_C2 |
| 14 | 0xC3 | — | — | PT_C3 |
| 16 | 0xC1 | — | — | PT_C1 |
| 16 | — | — | byte[0]=0xC1 or model check | INV_C or REMOTE_C |
| 18 | 0x81 | — | — | BMK_81 |
| 21 | — | 0x00 | byte[10]<=0x27, byte[14] in models | INV or REMOTE_00 |
| 21 | — | 0x80 | — | REMOTE_80 |
| 21 | — | 0xA0-0xA4 | — | REMOTE_A0-A4 |

For length 16 and 21 packets, disambiguation uses byte[10] (<=0x27 for inverter) and byte[14] (model lookup).

### Device Parsers

Each device class implements:
```csharp
public interface IDeviceParser
{
    string DeviceType { get; }
    Dictionary<string, object> Parse(byte[] packet, int voltageMultiplier);
}
```

All binary unpacking is big-endian. Use `BinaryPrimitives.ReadInt16BigEndian()` / `ReadUInt16BigEndian()` from `System.Buffers.Binary`.

#### InverterDevice (21 or 16 bytes)

Struct format: `>BBhhBBBBBBBBBBBBhb` (21-byte) or `>BBhhBBBBBBBBBB` (16-byte)

Fields: mode, fault, VDC (/10), ADC, VACout, VACin, inv LED, charge LED, revision (/10), battery temp, transformer temp, FET temp, model number, stack mode, AC input amps, AC output amps, frequency (/10).

**Voltage multiplier** derived from model: <=50 → 1 (12V), <=107 → 2 (24V), <=150 → 4 (48V). Cached and shared with other parsers.

40+ model names in lookup dictionary.

#### RemoteDevice (21 or 16 bytes, multiple subtypes)

Base fields (all subtypes): search watts, battery size (*10), absorb voltage, charger amps, AC input, revision, parallel mode, LBCO (/10), AC cutout, VS float voltage, VEQ, absorb time (/10).

Subtypes:
- REMOTE_00: base fields only
- REMOTE_80: adds BMK battery efficiency, remote time
- REMOTE_A0-A4: adds AGS config (runtime, start temp in C, start VDC, quiet/begin/stop times, SOC start/stop, amp start/stop, exercise, warm-up/cool-down)
- REMOTE_C: 16-byte classic, base fields subset

#### BmkDevice (18 bytes)

Format: `>BbHhHHhHHBB`

Fields: SOC (%), VDC (/100), ADC (/10), VMin (/100), VMax (/100), amp-hours, amp-hour trip (/10), amp-hour out (*100), revision (/10), fault status.

#### AgsDevice (6 bytes)

AGS_A1 format: `>BbBbBB` — status code (28 states), temperature (F→C if <105), runtime (/10), VDC (/10 * multiplier).

AGS_A2 format: `>BBBHB` — last run duration, last full SOC time, total run time.

#### Pt100Device (13-16 bytes)

PT_C1 (16 bytes): address, mode, regulation, fault, battery V (/10), battery A (/10), PV V (/10), charge time (/10), target V, relay/alarm/fan/day flags, temps.

PT_C2 (13 bytes): lifetime kWh (*10), resettable kWh (/10), ground fault, stacker info, DIP switches, model, revision, current/voltage ratings.

PT_C3 (14 bytes): stub (not fully implemented in Python source).

24 fault codes, mode/regulation lookups.

#### RtrDevice (2 bytes)

Format: `>BB`. Single field: revision (/10).

#### AcldDevice (8 bytes)

Format: `>7B`. Stub — not implemented in Python source. Parse and store raw bytes.

### Packet Cleanup

When 2+ consecutive UNKNOWN packets are detected, merge their bytes and re-parse. Handles RS485 fragmentation at packet boundaries.

### Cumulative State

Device instances persist across `GetDevices()` calls. PT100 and AGS send data across multiple packet types — state accumulates. Return deep copies to prevent leakage.

### Public API

```csharp
public class MagnumReader
{
    public MagnumReader(MagnumReaderOptions options);
    public string CommDevice { get; }
    public List<DeviceData> GetDevices();
}

public class DeviceData
{
    public string Device { get; set; }  // "INVERTER", "BMK", etc.
    public Dictionary<string, object> Data { get; set; }
}
```

## Magnum.Server — Minimal API

### Endpoints

```csharp
app.MapGet("/", () => Results.File("wwwroot/index.html", "text/html"));
app.MapGet("/index.html", () => Results.File("wwwroot/index.html", "text/html"));
app.MapGet("/api", (MagnumService svc) => svc.GetAllDeviceData());
```

CORS middleware: allow all origins, GET + OPTIONS, Content-Type header.

### MagnumService

Registered as singleton. Holds `Dictionary<string, MagnumReader>`. On `/api`:
1. Iterate readers
2. Call `GetDevices()` on each
3. Return JSON array matching current format:

```json
[
  {
    "comm_device": "/dev/ttyUSB0",
    "data": {
      "datetime": "2026-04-04T19:39:00-07:00",
      "devices": [
        { "device": "INVERTER", "data": { ... } },
        { "device": "BMK", "data": { ... } }
      ]
    }
  }
]
```

Timestamp: UTC with local timezone offset, no microseconds (matches Python behavior).

Error handling: 204 No Content if no data, 500 if serial exception.

### Configuration

**appsettings.json:**
```json
{
  "Magnum": {
    "Port": 17223,
    "Listen": "",
    "Devices": ["/dev/ttyUSB0"],
    "Packets": 50,
    "Timeout": 0.005,
    "Trace": false,
    "CleanPackets": true
  }
}
```

**CLI overrides** via `System.CommandLine`:
- `--port`, `--device` (repeatable), `--listen`, `--packets`, `--timeout`, `--trace`, `--nocleanup`, `--verbose`, `--version`

**Environment variables** supported automatically via .NET configuration (e.g., `Magnum__Port=8080`).

### Startup

1. Build configuration (appsettings.json → env vars → CLI args)
2. Instantiate `MagnumReader` per configured device
3. Test-read each; log and skip failures
4. If no usable devices, exit with error
5. Register `MagnumService` singleton
6. Configure Kestrel on `listen:port`
7. Run with graceful shutdown via `IHostApplicationLifetime`

### Logging

Use `ILogger<T>` instead of console prints. Suppress 2xx request logs (match Python behavior) via log filtering.

## Docker

### Dockerfile (multi-stage, multi-arch)

```dockerfile
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
# restore, publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
# copy publish output, expose 17223, entrypoint
```

### compose.yml

```yaml
services:
  magnum:
    build: .
    container_name: magnum-dotnet
    restart: unless-stopped
    ports:
      - "17223:17223"
    devices:
      - "/dev/ttyUSB0:/dev/ttyUSB0"
    volumes:
      - ./appsettings.json:/app/appsettings.json:ro  # optional config override
```

## What Does NOT Change

- `index.html` — served as static file, no modifications
- API JSON contract — same shape, same field names
- Default port — 17223
- Serial protocol behavior — identical packet parsing logic
- CORS policy — allow all origins

## Dependencies

- `System.IO.Ports` — serial communication (NuGet package for cross-platform)
- `System.CommandLine` — CLI argument parsing
- No other external dependencies
