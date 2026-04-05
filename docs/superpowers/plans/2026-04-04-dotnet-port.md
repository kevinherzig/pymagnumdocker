# MagnumDocker .NET Port — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port PyMagnumDocker (Python HTTP server + pymagnum RS485 library) to .NET 8 with identical API output.

**Architecture:** Two-project solution — `Magnum.Serial` (class library for RS485 protocol) and `Magnum.Server` (Minimal API). TDD with xUnit using known test packets from the Python project's test data.

**Tech Stack:** .NET 8, ASP.NET Minimal API, System.IO.Ports, System.CommandLine, xUnit

**Spec:** `docs/superpowers/specs/2026-04-04-dotnet-port-design.md`

---

## File Structure

```
MagnumDocker/
├── MagnumDocker.sln
├── src/
│   ├── Magnum.Serial/
│   │   ├── Magnum.Serial.csproj
│   │   ├── PacketType.cs              — Enum of all packet types
│   │   ├── DeviceData.cs              — Output model (device name + data dict)
│   │   ├── IDeviceParser.cs           — Parser interface
│   │   ├── BigEndianReader.cs         — Static helpers for big-endian byte reading
│   │   ├── PacketIdentifier.cs        — Classifies raw bytes into PacketType
│   │   ├── MagnumReaderOptions.cs     — Configuration POCO
│   │   ├── MagnumReader.cs            — Serial port reading + packet orchestration
│   │   ├── Devices/
│   │   │   ├── InverterDevice.cs      — 21/16-byte inverter parser
│   │   │   ├── BmkDevice.cs           — 18-byte BMK parser
│   │   │   ├── AgsDevice.cs           — 6-byte AGS parser (A1 + A2)
│   │   │   ├── Pt100Device.cs         — 13-16 byte PT100 parser (C1 + C2)
│   │   │   ├── RtrDevice.cs           — 2-byte RTR parser
│   │   │   ├── AcldDevice.cs          — 8-byte ACLD stub
│   │   │   └── RemoteDevice.cs        — 21/16-byte remote parser (all subtypes)
│   │   └── Lookups/
│   │       ├── InverterLookups.cs     — Models, modes, faults, stack modes
│   │       ├── AgsLookups.cs          — AGS status codes
│   │       └── Pt100Lookups.cs        — PT100 modes, regulations, faults
│   └── Magnum.Server/
│       ├── Magnum.Server.csproj
│       ├── Program.cs                 — Minimal API + CLI + config + startup
│       ├── MagnumService.cs           — Singleton service wrapping MagnumReaders
│       ├── appsettings.json           — Default configuration
│       └── wwwroot/
│           └── index.html             — Copied from existing project, unchanged
├── tests/
│   └── Magnum.Serial.Tests/
│       ├── Magnum.Serial.Tests.csproj
│       ├── TestPackets.cs             — Known packet hex data as byte arrays
│       ├── PacketIdentifierTests.cs
│       ├── InverterDeviceTests.cs
│       ├── BmkDeviceTests.cs
│       ├── AgsDeviceTests.cs
│       ├── Pt100DeviceTests.cs
│       ├── RtrDeviceTests.cs
│       ├── RemoteDeviceTests.cs
│       └── BigEndianReaderTests.cs
├── Dockerfile
└── compose.yml
```

---

## Test Packet Reference

These hex strings come from the Python project's `testdata/allpackets.txt`. All tests use these known packets with manually verified expected outputs.

```
AGS_A1:     A102343A007F
AGS_A2:     A20000000000
BMK_81:     814C09F1007407E00C08FF984FF000140A01
INVERTER:   400000F60016770001003D1133246B010005025800
PT_C1:      C1001300024D008505BC1194101B313E
PT_C2:      C200000200AC0002050B641425
RTR_91:     9120
REMOTE_00:  00002808640A280000C89B840C1412200000000000
REMOTE_80:  00002808640A280000C89B840C1412200000280080
REMOTE_A0:  00002808640A280000C89B840C14122014007300A0
```

---

### Task 1: Solution Scaffold

**Files:**
- Create: `MagnumDocker/MagnumDocker.sln`
- Create: `MagnumDocker/src/Magnum.Serial/Magnum.Serial.csproj`
- Create: `MagnumDocker/src/Magnum.Server/Magnum.Server.csproj`
- Create: `MagnumDocker/tests/Magnum.Serial.Tests/Magnum.Serial.Tests.csproj`

- [ ] **Step 1: Create solution directory and project files**

```bash
cd /home/kevin/pymagnumdocker
mkdir -p MagnumDocker/src/Magnum.Serial/Devices
mkdir -p MagnumDocker/src/Magnum.Serial/Lookups
mkdir -p MagnumDocker/src/Magnum.Server/wwwroot
mkdir -p MagnumDocker/tests/Magnum.Serial.Tests
```

- [ ] **Step 2: Create Magnum.Serial.csproj**

Create `MagnumDocker/src/Magnum.Serial/Magnum.Serial.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="System.IO.Ports" Version="8.0.0" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Create Magnum.Server.csproj**

Create `MagnumDocker/src/Magnum.Server/Magnum.Server.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />
    <ProjectReference Include="..\Magnum.Serial\Magnum.Serial.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Create test project**

Create `MagnumDocker/tests/Magnum.Serial.Tests/Magnum.Serial.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.9.0" />
    <PackageReference Include="xunit" Version="2.7.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.7" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\Magnum.Serial\Magnum.Serial.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5: Create solution file and verify build**

```bash
cd MagnumDocker
dotnet new sln
dotnet sln add src/Magnum.Serial/Magnum.Serial.csproj
dotnet sln add src/Magnum.Server/Magnum.Server.csproj
dotnet sln add tests/Magnum.Serial.Tests/Magnum.Serial.Tests.csproj
dotnet build
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add MagnumDocker/
git commit -m "feat: scaffold .NET solution with Serial library, Server, and test projects"
```

---

### Task 2: Core Types and Lookup Dictionaries

**Files:**
- Create: `MagnumDocker/src/Magnum.Serial/PacketType.cs`
- Create: `MagnumDocker/src/Magnum.Serial/DeviceData.cs`
- Create: `MagnumDocker/src/Magnum.Serial/IDeviceParser.cs`
- Create: `MagnumDocker/src/Magnum.Serial/MagnumReaderOptions.cs`
- Create: `MagnumDocker/src/Magnum.Serial/Lookups/InverterLookups.cs`
- Create: `MagnumDocker/src/Magnum.Serial/Lookups/AgsLookups.cs`
- Create: `MagnumDocker/src/Magnum.Serial/Lookups/Pt100Lookups.cs`

- [ ] **Step 1: Create PacketType enum**

Create `MagnumDocker/src/Magnum.Serial/PacketType.cs`:

```csharp
namespace Magnum.Serial;

public enum PacketType
{
    Unknown,
    Inv,        // 21-byte modern inverter
    InvC,       // 16-byte classic inverter
    RemoteC,    // 16-byte classic remote
    Remote00,   // 21-byte base remote
    Remote11,
    Remote80,   // 21-byte remote with BMK
    RemoteA0,   // 21-byte remote with AGS
    RemoteA1,
    RemoteA2,
    RemoteA3,
    RemoteA4,
    RemoteC0,
    RemoteC1,
    RemoteC2,
    RemoteC3,
    RemoteD0,
    Bmk81,      // 18-byte BMK
    AgsA1,      // 6-byte AGS status
    AgsA2,      // 6-byte AGS usage
    PtC1,       // 16-byte PT100 main
    PtC2,       // 13-byte PT100 lifetime
    PtC3,       // 14-byte PT100 daily
    Rtr91,      // 2-byte RTR
    AcldD1,     // 8-byte ACLD
}
```

- [ ] **Step 2: Create DeviceData and IDeviceParser**

Create `MagnumDocker/src/Magnum.Serial/DeviceData.cs`:

```csharp
namespace Magnum.Serial;

public class DeviceData
{
    public required string Device { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
}
```

Create `MagnumDocker/src/Magnum.Serial/IDeviceParser.cs`:

```csharp
namespace Magnum.Serial;

public interface IDeviceParser
{
    string DeviceType { get; }
    void Parse(byte[] packet, PacketType packetType, int voltageMultiplier);
    Dictionary<string, object> GetData();
}
```

- [ ] **Step 3: Create MagnumReaderOptions**

Create `MagnumDocker/src/Magnum.Serial/MagnumReaderOptions.cs`:

```csharp
namespace Magnum.Serial;

public class MagnumReaderOptions
{
    public string Device { get; set; } = "/dev/ttyUSB0";
    public int Packets { get; set; } = 50;
    public double Timeout { get; set; } = 0.005;
    public bool Trace { get; set; }
    public bool CleanPackets { get; set; } = true;
}
```

- [ ] **Step 4: Create InverterLookups**

Create `MagnumDocker/src/Magnum.Serial/Lookups/InverterLookups.cs`:

```csharp
namespace Magnum.Serial.Lookups;

public static class InverterLookups
{
    public static readonly Dictionary<int, string> Models = new()
    {
        [0x06] = "MM612",       [0x07] = "MM612-AE",    [0x08] = "MM1212",
        [0x09] = "MMS1012",     [0x0A] = "MM1012E",     [0x0B] = "MM1512",
        [0x0C] = "MMS912E",     [0x0F] = "ME1512",      [0x14] = "ME2012",
        [0x15] = "RD2212",      [0x19] = "ME2512",      [0x1E] = "ME3112",
        [0x23] = "MS2012",      [0x24] = "MS1512E",     [0x28] = "MS2012E",
        [0x2C] = "MSH3012M",    [0x2D] = "MS2812",      [0x2F] = "MS2712E",
        [0x35] = "MM1324E",     [0x36] = "MM1524",      [0x37] = "RD1824",
        [0x3B] = "RD2624E",     [0x3F] = "RD2824",      [0x45] = "RD4024E",
        [0x4A] = "RD3924",      [0x5A] = "MS4124E",     [0x5B] = "MS2024",
        [0x67] = "MSH4024M",    [0x68] = "MSH4024RE",   [0x69] = "MS4024",
        [0x6A] = "MS4024AE",    [0x6B] = "MS4024PAE",   [0x6F] = "MS4448AE",
        [0x70] = "MS3748AEJ",   [0x72] = "MS4048",      [0x73] = "MS4448PAE",
        [0x74] = "MS3748PAEJ",  [0x75] = "MS4348PE",
    };

    public static readonly Dictionary<int, string> Modes = new()
    {
        [0x00] = "Standby",         [0x01] = "EQ",
        [0x02] = "FLOAT",           [0x04] = "ABSORB",
        [0x08] = "BULK",            [0x09] = "BATSAVER",
        [0x10] = "CHARGE",          [0x20] = "Off",
        [0x40] = "INVERT",          [0x50] = "Inverter_Standby",
        [0x80] = "SEARCH",
    };

    public static readonly Dictionary<int, string> Faults = new()
    {
        [0x00] = "None",                [0x01] = "STUCK RELAY",
        [0x02] = "DC OVERLOAD",         [0x03] = "AC OVERLOAD",
        [0x04] = "DEAD BAT",            [0x05] = "BACKFEED",
        [0x08] = "LOW BAT",             [0x09] = "HIGH BAT",
        [0x0A] = "HIGH AC VOLTS",       [0x10] = "BAD_BRIDGE",
        [0x12] = "NTC_FAULT",           [0x13] = "FET_OVERLOAD",
        [0x14] = "INTERNAL_FAULT4",     [0x16] = "STACKER MODE FAULT",
        [0x17] = "STACKER NO CLK FAULT",[0x18] = "STACKER CLK PH FAULT",
        [0x19] = "STACKER PH LOSS FAULT",[0x20] = "OVER TEMP",
        [0x21] = "RELAY FAULT",         [0x80] = "CHARGER_FAULT",
        [0x81] = "High Battery Temp",   [0x90] = "OPEN SELCO TCO",
        [0x91] = "CB3 OPEN FAULT",
    };

    public static readonly Dictionary<int, string> StackModes = new()
    {
        [0x00] = "Stand Alone",
        [0x01] = "Parallel stack - master",
        [0x02] = "Parallel stack - slave",
        [0x04] = "Series stack - master",
        [0x08] = "Series stack - slave",
    };

    public static int GetVoltageMultiplier(int model)
    {
        if (model <= 50) return 1;   // 12V
        if (model <= 107) return 2;  // 24V
        return 4;                     // 48V
    }
}
```

- [ ] **Step 5: Create AgsLookups**

Create `MagnumDocker/src/Magnum.Serial/Lookups/AgsLookups.cs`:

```csharp
namespace Magnum.Serial.Lookups;

public static class AgsLookups
{
    public static readonly Dictionary<int, string> Statuses = new()
    {
        [0] = "Not Connected",      [1] = "Off",
        [2] = "Ready",              [3] = "Manual Run",
        [4] = "AC In",              [5] = "In quiet time",
        [6] = "Start in test mode", [7] = "Start on temperature",
        [8] = "Start on voltage",   [9] = "Fault start on test",
        [10] = "Fault start on temp",[11] = "Fault start on voltage",
        [12] = "Start TOD",         [13] = "Start SOC",
        [14] = "Start Exercise",    [15] = "Fault start TOD",
        [16] = "Fault start SOC",   [17] = "Fault start Exercise",
        [18] = "Start on Amp",      [19] = "Start on Topoff",
        [20] = "Not used",          [21] = "Fault start on Amp",
        [22] = "Fault on Topoff",   [23] = "Not used",
        [24] = "Fault max run",     [25] = "Gen Run Fault",
        [26] = "Gen in Warm up",    [27] = "Gen in Cool down",
    };

    public static readonly HashSet<int> RunningStatuses = new()
    {
        3, 6, 7, 8, 12, 13, 14, 18, 19, 26, 27
    };
}
```

- [ ] **Step 6: Create Pt100Lookups**

Create `MagnumDocker/src/Magnum.Serial/Lookups/Pt100Lookups.cs`:

```csharp
namespace Magnum.Serial.Lookups;

public static class Pt100Lookups
{
    public static readonly Dictionary<int, string> Modes = new()
    {
        [1] = "Float",  [2] = "Bulk",
        [3] = "Absorb", [4] = "EQ",
    };

    public static readonly Dictionary<int, string> Regulations = new()
    {
        [0] = "Off",         [1] = "Voltage",
        [2] = "Current",     [3] = "Temperature",
        [4] = "Hardware",    [15] = "MPPT",
    };

    public static readonly Dictionary<int, string> Faults = new()
    {
        [0] = "No Fault",              [1] = "Input breaker Fault",
        [2] = "OPS Fault",             [3] = "PV High Fault",
        [4] = "Battery High Fault",    [5] = "BTS Shorted Fault",
        [6] = "FET Overtemp Fault",    [7] = "Battery Overtemp Fault",
        [8] = "Over Current Fault",    [9] = "Internal Phase Fault",
        [10] = "Open BTS Fault",       [11] = "Internal Fault 1",
        [12] = "GFP Fault",            [13] = "ARC Fault",
        [14] = "NTC Fault",            [15] = "HW overtemp Fault",
        [16] = "Overyemp Fault",       [17] = "USB Fault",
        [20] = "Stack Fault",          [21] = "Stack warning",
        [22] = "Stack DIP Fault",      [23] = "Stack DT Fault",
    };
}
```

- [ ] **Step 7: Build and verify**

```bash
cd MagnumDocker && dotnet build
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 8: Commit**

```bash
git add MagnumDocker/
git commit -m "feat: add core types, enums, and lookup dictionaries"
```

---

### Task 3: BigEndianReader Helpers

**Files:**
- Create: `MagnumDocker/src/Magnum.Serial/BigEndianReader.cs`
- Create: `MagnumDocker/tests/Magnum.Serial.Tests/BigEndianReaderTests.cs`

- [ ] **Step 1: Write failing tests**

Create `MagnumDocker/tests/Magnum.Serial.Tests/BigEndianReaderTests.cs`:

```csharp
using Magnum.Serial;

namespace Magnum.Serial.Tests;

public class BigEndianReaderTests
{
    [Fact]
    public void ReadByte_ReturnsCorrectValue()
    {
        byte[] data = [0x40, 0xFF];
        Assert.Equal(0x40, BigEndianReader.ReadByte(data, 0));
        Assert.Equal(0xFF, BigEndianReader.ReadByte(data, 1));
    }

    [Fact]
    public void ReadSByte_ReturnsSignedValue()
    {
        byte[] data = [0x4C, 0xB4]; // 76, -76
        Assert.Equal(76, BigEndianReader.ReadSByte(data, 0));
        Assert.Equal(-76, BigEndianReader.ReadSByte(data, 1));
    }

    [Fact]
    public void ReadUInt16_BigEndian()
    {
        byte[] data = [0x09, 0xF1]; // 2545
        Assert.Equal((ushort)2545, BigEndianReader.ReadUInt16(data, 0));
    }

    [Fact]
    public void ReadInt16_BigEndian_Positive()
    {
        byte[] data = [0x00, 0xF6]; // 246
        Assert.Equal((short)246, BigEndianReader.ReadInt16(data, 0));
    }

    [Fact]
    public void ReadInt16_BigEndian_Negative()
    {
        byte[] data = [0xFF, 0x98]; // -104
        Assert.Equal((short)-104, BigEndianReader.ReadInt16(data, 0));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd MagnumDocker && dotnet test
```

Expected: FAIL — `BigEndianReader` does not exist.

- [ ] **Step 3: Implement BigEndianReader**

Create `MagnumDocker/src/Magnum.Serial/BigEndianReader.cs`:

```csharp
using System.Buffers.Binary;

namespace Magnum.Serial;

public static class BigEndianReader
{
    public static byte ReadByte(byte[] data, int offset) => data[offset];

    public static sbyte ReadSByte(byte[] data, int offset) => (sbyte)data[offset];

    public static ushort ReadUInt16(byte[] data, int offset) =>
        BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset, 2));

    public static short ReadInt16(byte[] data, int offset) =>
        BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(offset, 2));
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd MagnumDocker && dotnet test
```

Expected: All 5 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add MagnumDocker/
git commit -m "feat: add BigEndianReader helper for binary packet parsing"
```

---

### Task 4: Test Packets + PacketIdentifier

**Files:**
- Create: `MagnumDocker/tests/Magnum.Serial.Tests/TestPackets.cs`
- Create: `MagnumDocker/src/Magnum.Serial/PacketIdentifier.cs`
- Create: `MagnumDocker/tests/Magnum.Serial.Tests/PacketIdentifierTests.cs`

- [ ] **Step 1: Create TestPackets constants**

Create `MagnumDocker/tests/Magnum.Serial.Tests/TestPackets.cs`:

```csharp
namespace Magnum.Serial.Tests;

public static class TestPackets
{
    public static byte[] AgsA1 => Convert.FromHexString("A102343A007F");
    public static byte[] AgsA2 => Convert.FromHexString("A20000000000");
    public static byte[] Bmk81 => Convert.FromHexString("814C09F1007407E00C08FF984FF000140A01");
    public static byte[] Inverter => Convert.FromHexString("400000F60016770001003D1133246B010005025800");
    public static byte[] PtC1 => Convert.FromHexString("C1001300024D008505BC1194101B313E");
    public static byte[] PtC2 => Convert.FromHexString("C200000200AC0002050B641425");
    public static byte[] Rtr91 => Convert.FromHexString("9120");
    public static byte[] Remote00 => Convert.FromHexString("00002808640A280000C89B840C1412200000000000");
    public static byte[] Remote80 => Convert.FromHexString("00002808640A280000C89B840C1412200000280080");
    public static byte[] RemoteA0 => Convert.FromHexString("00002808640A280000C89B840C14122014007300A0");
    public static byte[] RemoteA1 => Convert.FromHexString("00002808640A280000C89B840C14600090787878A1");
    public static byte[] RemoteA2 => Convert.FromHexString("00003C04500F170601C8A5860100465500781478A2");
    public static byte[] RemoteA3 => Convert.FromHexString("00003C04500F170601C8A58601005E5F00240200A3");
    public static byte[] RemoteA4 => Convert.FromHexString("00003C04500F170601C8A58601001E1E00000000A4");
}
```

- [ ] **Step 2: Write PacketIdentifier tests**

Create `MagnumDocker/tests/Magnum.Serial.Tests/PacketIdentifierTests.cs`:

```csharp
using Magnum.Serial;

namespace Magnum.Serial.Tests;

public class PacketIdentifierTests
{
    [Fact]
    public void Identifies_Rtr91() =>
        Assert.Equal(PacketType.Rtr91, PacketIdentifier.Identify(TestPackets.Rtr91));

    [Fact]
    public void Identifies_AgsA1() =>
        Assert.Equal(PacketType.AgsA1, PacketIdentifier.Identify(TestPackets.AgsA1));

    [Fact]
    public void Identifies_AgsA2() =>
        Assert.Equal(PacketType.AgsA2, PacketIdentifier.Identify(TestPackets.AgsA2));

    [Fact]
    public void Identifies_Bmk81() =>
        Assert.Equal(PacketType.Bmk81, PacketIdentifier.Identify(TestPackets.Bmk81));

    [Fact]
    public void Identifies_Inverter() =>
        Assert.Equal(PacketType.Inv, PacketIdentifier.Identify(TestPackets.Inverter));

    [Fact]
    public void Identifies_PtC1() =>
        Assert.Equal(PacketType.PtC1, PacketIdentifier.Identify(TestPackets.PtC1));

    [Fact]
    public void Identifies_PtC2() =>
        Assert.Equal(PacketType.PtC2, PacketIdentifier.Identify(TestPackets.PtC2));

    [Fact]
    public void Identifies_Remote00() =>
        Assert.Equal(PacketType.Remote00, PacketIdentifier.Identify(TestPackets.Remote00));

    [Fact]
    public void Identifies_Remote80() =>
        Assert.Equal(PacketType.Remote80, PacketIdentifier.Identify(TestPackets.Remote80));

    [Fact]
    public void Identifies_RemoteA0() =>
        Assert.Equal(PacketType.RemoteA0, PacketIdentifier.Identify(TestPackets.RemoteA0));

    [Fact]
    public void Identifies_Unknown_EmptyPacket() =>
        Assert.Equal(PacketType.Unknown, PacketIdentifier.Identify(Array.Empty<byte>()));

    [Fact]
    public void Identifies_Unknown_UnrecognizedLength() =>
        Assert.Equal(PacketType.Unknown, PacketIdentifier.Identify(new byte[] { 0x00, 0x00, 0x00 }));
}
```

- [ ] **Step 3: Run tests to verify they fail**

```bash
cd MagnumDocker && dotnet test --filter PacketIdentifier
```

Expected: FAIL — `PacketIdentifier` does not exist.

- [ ] **Step 4: Implement PacketIdentifier**

Create `MagnumDocker/src/Magnum.Serial/PacketIdentifier.cs`:

```csharp
using Magnum.Serial.Lookups;

namespace Magnum.Serial;

public static class PacketIdentifier
{
    public static PacketType Identify(byte[] packet)
    {
        if (packet.Length == 0)
            return PacketType.Unknown;

        byte first = packet[0];
        int length = packet.Length;

        switch (length)
        {
            case 2:
                return first == 0x91 ? PacketType.Rtr91 : PacketType.Unknown;

            case 6:
                return first switch
                {
                    0xA1 => PacketType.AgsA1,
                    0xA2 => PacketType.AgsA2,
                    _ => PacketType.Unknown,
                };

            case 8:
                return first == 0xD1 ? PacketType.AcldD1 : PacketType.Unknown;

            case 13:
                return first == 0xC2 ? PacketType.PtC2 : PacketType.Unknown;

            case 14:
                return first == 0xC3 ? PacketType.PtC3 : PacketType.Unknown;

            case 16:
                if (first == 0xC1)
                    return PacketType.PtC1;
                return Identify16BytePacket(packet);

            case 18:
                return first == 0x81 ? PacketType.Bmk81 : PacketType.Unknown;

            case 21:
                return Identify21BytePacket(packet);

            default:
                return PacketType.Unknown;
        }
    }

    private static PacketType Identify16BytePacket(byte[] packet)
    {
        byte first = packet[0];

        // Classic inverter: first byte 0xC1 already handled above
        // Check if it's an inverter by model lookup
        if (packet[10] <= 0x27 && InverterLookups.Models.ContainsKey(packet[14]))
            return PacketType.InvC;

        // Classic remote: first byte 0x00
        if (first == 0x00)
            return PacketType.RemoteC;

        return PacketType.Unknown;
    }

    private static PacketType Identify21BytePacket(byte[] packet)
    {
        byte last = packet[20];

        // Check last byte for remote subtypes
        switch (last)
        {
            case 0x80:
                return PacketType.Remote80;
            case 0xA0:
                return PacketType.RemoteA0;
            case 0xA1:
                return PacketType.RemoteA1;
            case 0xA2:
                return PacketType.RemoteA2;
            case 0xA3:
                return PacketType.RemoteA3;
            case 0xA4:
                return PacketType.RemoteA4;
            case 0xC0:
                return PacketType.RemoteC0;
            case 0xC1:
                return PacketType.RemoteC1;
            case 0xC2:
                return PacketType.RemoteC2;
            case 0xC3:
                return PacketType.RemoteC3;
            case 0xD0:
                return PacketType.RemoteD0;
            case 0x11:
                return PacketType.Remote11;
        }

        // Last byte is 0x00 — could be inverter or remote
        if (last == 0x00)
        {
            // Inverter: byte[10] <= 0x27 and byte[14] is a known model
            if (packet[10] <= 0x27 && InverterLookups.Models.ContainsKey(packet[14]))
                return PacketType.Inv;

            // Check if last 7 bytes are all zero (REMOTE_00 pattern)
            bool lastSevenZero = true;
            for (int i = 14; i < 21; i++)
            {
                if (packet[i] != 0x00) { lastSevenZero = false; break; }
            }
            if (lastSevenZero)
                return PacketType.Remote00;

            return PacketType.Remote00;
        }

        return PacketType.Unknown;
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

```bash
cd MagnumDocker && dotnet test --filter PacketIdentifier
```

Expected: All 12 tests PASS.

- [ ] **Step 6: Commit**

```bash
git add MagnumDocker/
git commit -m "feat: add PacketIdentifier for classifying raw RS485 packets"
```

---

### Task 5: InverterDevice Parser

**Files:**
- Create: `MagnumDocker/src/Magnum.Serial/Devices/InverterDevice.cs`
- Create: `MagnumDocker/tests/Magnum.Serial.Tests/InverterDeviceTests.cs`

- [ ] **Step 1: Write failing tests**

Test packet `400000F60016770001003D1133246B010005025800` — manually decoded:

| Field | Bytes | Raw Value | Converted |
|-------|-------|-----------|-----------|
| mode | [0] | 0x40=64 | "INVERT" |
| fault | [1] | 0x00=0 | "None" |
| vdc | [2-3] | 246 | 24.6 |
| adc | [4-5] | 22 | 22.0 |
| VACout | [6] | 119 | 119.0 |
| VACin | [7] | 0 | 0.0 |
| revision | [10] | 61 | 6.1 |
| model | [14] | 0x6B=107 | "MS4024PAE" |
| stackmode | [15] | 1 | "Parallel stack - master" |
| Hz | [18-19] | 600 | 60.0 |

Create `MagnumDocker/tests/Magnum.Serial.Tests/InverterDeviceTests.cs`:

```csharp
using Magnum.Serial;
using Magnum.Serial.Devices;

namespace Magnum.Serial.Tests;

public class InverterDeviceTests
{
    [Fact]
    public void Parse_InvPacket_ExtractsMode()
    {
        var device = new InverterDevice();
        device.Parse(TestPackets.Inverter, PacketType.Inv, 0);
        var data = device.GetData();

        Assert.Equal(0x40, data["mode"]);
        Assert.Equal("INVERT", data["mode_text"]);
    }

    [Fact]
    public void Parse_InvPacket_ExtractsFault()
    {
        var device = new InverterDevice();
        device.Parse(TestPackets.Inverter, PacketType.Inv, 0);
        var data = device.GetData();

        Assert.Equal(0, data["fault"]);
        Assert.Equal("None", data["fault_text"]);
    }

    [Fact]
    public void Parse_InvPacket_ExtractsVoltagesAndCurrents()
    {
        var device = new InverterDevice();
        device.Parse(TestPackets.Inverter, PacketType.Inv, 0);
        var data = device.GetData();

        Assert.Equal(24.6, data["vdc"]);
        Assert.Equal(22.0, data["adc"]);
        Assert.Equal(119.0, data["VACout"]);
        Assert.Equal(0.0, data["VACin"]);
    }

    [Fact]
    public void Parse_InvPacket_ExtractsModel()
    {
        var device = new InverterDevice();
        device.Parse(TestPackets.Inverter, PacketType.Inv, 0);
        var data = device.GetData();

        Assert.Equal("MS4024PAE", data["model"]);
    }

    [Fact]
    public void Parse_InvPacket_ExtractsStackMode()
    {
        var device = new InverterDevice();
        device.Parse(TestPackets.Inverter, PacketType.Inv, 0);
        var data = device.GetData();

        Assert.Equal("Parallel stack - master", data["stackmode"]);
    }

    [Fact]
    public void Parse_InvPacket_ExtractsFrequency()
    {
        var device = new InverterDevice();
        device.Parse(TestPackets.Inverter, PacketType.Inv, 0);
        var data = device.GetData();

        Assert.Equal(60.0, data["Hz"]);
    }

    [Fact]
    public void Parse_InvPacket_ExtractsRevision()
    {
        var device = new InverterDevice();
        device.Parse(TestPackets.Inverter, PacketType.Inv, 0);
        var data = device.GetData();

        Assert.Equal(6.1, data["revision"]);
    }

    [Fact]
    public void Parse_InvPacket_ReturnsVoltageMultiplier()
    {
        var device = new InverterDevice();
        device.Parse(TestPackets.Inverter, PacketType.Inv, 0);

        // Model 107 (0x6B) => 24V => multiplier 2
        Assert.Equal(2, device.VoltageMultiplier);
    }

    [Fact]
    public void Parse_InvPacket_ExtractsTemperatures()
    {
        var device = new InverterDevice();
        device.Parse(TestPackets.Inverter, PacketType.Inv, 0);
        var data = device.GetData();

        Assert.Equal(17.0, data["battemp"]);
        Assert.Equal(51.0, data["tfmr"]);
        Assert.Equal(36.0, data["FET"]);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd MagnumDocker && dotnet test --filter InverterDevice
```

Expected: FAIL — `InverterDevice` does not exist.

- [ ] **Step 3: Implement InverterDevice**

Create `MagnumDocker/src/Magnum.Serial/Devices/InverterDevice.cs`:

```csharp
using Magnum.Serial.Lookups;

namespace Magnum.Serial.Devices;

public class InverterDevice : IDeviceParser
{
    public string DeviceType => "INVERTER";
    public int VoltageMultiplier { get; private set; } = 1;

    private readonly Dictionary<string, object> _data = new();

    public void Parse(byte[] packet, PacketType packetType, int voltageMultiplier)
    {
        // Common fields (both INV and INV_C): bytes 0-14
        int mode = BigEndianReader.ReadByte(packet, 0);
        int fault = BigEndianReader.ReadByte(packet, 1);
        double vdc = BigEndianReader.ReadInt16(packet, 2) / 10.0;
        double adc = (double)BigEndianReader.ReadInt16(packet, 4);
        double vacOut = (double)BigEndianReader.ReadByte(packet, 6);
        double vacIn = (double)BigEndianReader.ReadByte(packet, 7);
        int invLed = BigEndianReader.ReadByte(packet, 8);
        int chgLed = BigEndianReader.ReadByte(packet, 9);
        double revision = Math.Round(BigEndianReader.ReadByte(packet, 10) / 10.0, 2);
        double batTemp = (double)BigEndianReader.ReadByte(packet, 11);
        double tfmr = (double)BigEndianReader.ReadByte(packet, 12);
        double fet = (double)BigEndianReader.ReadByte(packet, 13);
        int model = BigEndianReader.ReadByte(packet, 14);

        VoltageMultiplier = InverterLookups.GetVoltageMultiplier(model);

        _data["mode"] = mode;
        _data["mode_text"] = InverterLookups.Modes.GetValueOrDefault(mode, mode.ToString());
        _data["fault"] = fault;
        _data["fault_text"] = InverterLookups.Faults.GetValueOrDefault(fault, fault.ToString());
        _data["vdc"] = vdc;
        _data["adc"] = adc;
        _data["VACout"] = vacOut;
        _data["VACin"] = vacIn;
        _data["invled"] = invLed;
        _data["chgled"] = chgLed;
        _data["revision"] = revision;
        _data["battemp"] = batTemp;
        _data["tfmr"] = tfmr;
        _data["FET"] = fet;
        _data["model"] = InverterLookups.Models.GetValueOrDefault(model, model.ToString());

        // INV-only fields (21-byte packet)
        if (packetType == PacketType.Inv)
        {
            int stackMode = BigEndianReader.ReadByte(packet, 15);
            _data["stackmode"] = InverterLookups.StackModes.GetValueOrDefault(stackMode, stackMode.ToString());
            _data["AACin"] = (double)BigEndianReader.ReadByte(packet, 16);
            _data["AACout"] = (double)BigEndianReader.ReadByte(packet, 17);
            _data["Hz"] = Math.Round(BigEndianReader.ReadInt16(packet, 18) / 10.0, 2);
        }
    }

    public Dictionary<string, object> GetData() => new(_data);
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd MagnumDocker && dotnet test --filter InverterDevice
```

Expected: All 8 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add MagnumDocker/
git commit -m "feat: add InverterDevice parser for 21/16-byte inverter packets"
```

---

### Task 6: BmkDevice Parser

**Files:**
- Create: `MagnumDocker/src/Magnum.Serial/Devices/BmkDevice.cs`
- Create: `MagnumDocker/tests/Magnum.Serial.Tests/BmkDeviceTests.cs`

- [ ] **Step 1: Write failing tests**

Test packet `814C09F1007407E00C08FF984FF000140A01` — manually decoded:

Format `>BbHhHHhHHBB`:
- [0] B=0x81 (marker), [1] b=0x4C=76 (soc), [2-3] H=0x09F1=2545 (vdc/100=25.45)
- [4-5] h=0x0074=116 (adc/10=11.6), [6-7] H=0x07E0=2016 (vmin/100=20.16)
- [8-9] H=0x0C08=3080 (vmax/100=30.80), [10-11] h=0xFF98=-104 (amph)
- [12-13] H=0x4FF0=20464 (amphtrip/10=2046.4), [14-15] H=0x0014=20 (amphout*100=2000)
- [16] B=0x0A=10 (revision/10=1.0), [17] B=0x01 (fault="Normal")

Create `MagnumDocker/tests/Magnum.Serial.Tests/BmkDeviceTests.cs`:

```csharp
using Magnum.Serial;
using Magnum.Serial.Devices;

namespace Magnum.Serial.Tests;

public class BmkDeviceTests
{
    [Fact]
    public void Parse_Bmk81_ExtractsSOC()
    {
        var device = new BmkDevice();
        device.Parse(TestPackets.Bmk81, PacketType.Bmk81, 1);
        var data = device.GetData();

        Assert.Equal(76, data["soc"]);
    }

    [Fact]
    public void Parse_Bmk81_ExtractsVoltages()
    {
        var device = new BmkDevice();
        device.Parse(TestPackets.Bmk81, PacketType.Bmk81, 1);
        var data = device.GetData();

        Assert.Equal(25.45, data["vdc"]);
        Assert.Equal(20.16, data["vmin"]);
        Assert.Equal(30.80, data["vmax"]);
    }

    [Fact]
    public void Parse_Bmk81_ExtractsCurrent()
    {
        var device = new BmkDevice();
        device.Parse(TestPackets.Bmk81, PacketType.Bmk81, 1);
        var data = device.GetData();

        Assert.Equal(11.6, data["adc"]);
    }

    [Fact]
    public void Parse_Bmk81_ExtractsAmpHours()
    {
        var device = new BmkDevice();
        device.Parse(TestPackets.Bmk81, PacketType.Bmk81, 1);
        var data = device.GetData();

        Assert.Equal(-104, data["amph"]);
        Assert.Equal(2046.4, data["amphtrip"]);
        Assert.Equal(2000.0, data["amphout"]);
    }

    [Fact]
    public void Parse_Bmk81_ExtractsRevisionAndFault()
    {
        var device = new BmkDevice();
        device.Parse(TestPackets.Bmk81, PacketType.Bmk81, 1);
        var data = device.GetData();

        Assert.Equal(1.0, data["revision"]);
        Assert.Equal("Normal", data["Fault"]);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd MagnumDocker && dotnet test --filter BmkDevice
```

Expected: FAIL — `BmkDevice` does not exist.

- [ ] **Step 3: Implement BmkDevice**

Create `MagnumDocker/src/Magnum.Serial/Devices/BmkDevice.cs`:

```csharp
namespace Magnum.Serial.Devices;

public class BmkDevice : IDeviceParser
{
    private static readonly Dictionary<int, string> FaultStatuses = new()
    {
        [0] = "Reserved", [1] = "Normal", [2] = "Fault Start",
    };

    public string DeviceType => "BMK";

    private readonly Dictionary<string, object> _data = new();

    public void Parse(byte[] packet, PacketType packetType, int voltageMultiplier)
    {
        // Format: >BbHhHHhHHBB
        // [0]=marker, [1]=soc, [2-3]=vdc, [4-5]=adc, [6-7]=vmin,
        // [8-9]=vmax, [10-11]=amph, [12-13]=amphtrip, [14-15]=amphout,
        // [16]=revision, [17]=fault

        int soc = BigEndianReader.ReadSByte(packet, 1);
        double vdc = Math.Round(BigEndianReader.ReadUInt16(packet, 2) / 100.0, 2);
        double adc = Math.Round(BigEndianReader.ReadInt16(packet, 4) / 10.0, 1);
        double vmin = Math.Round(BigEndianReader.ReadUInt16(packet, 6) / 100.0, 2);
        double vmax = Math.Round(BigEndianReader.ReadUInt16(packet, 8) / 100.0, 2);
        int amph = BigEndianReader.ReadInt16(packet, 10);
        double amphTrip = Math.Round(BigEndianReader.ReadUInt16(packet, 12) / 10.0, 1);
        double amphOut = Math.Round(BigEndianReader.ReadUInt16(packet, 14) * 100.0, 2);
        double revision = Math.Round(BigEndianReader.ReadByte(packet, 16) / 10.0, 2);
        int fault = BigEndianReader.ReadByte(packet, 17);

        _data["soc"] = soc;
        _data["vdc"] = vdc;
        _data["adc"] = adc;
        _data["vmin"] = vmin;
        _data["vmax"] = vmax;
        _data["amph"] = amph;
        _data["amphtrip"] = amphTrip;
        _data["amphout"] = amphOut;
        _data["revision"] = revision;
        _data["Fault"] = FaultStatuses.GetValueOrDefault(fault, fault.ToString());
    }

    public Dictionary<string, object> GetData() => new(_data);
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd MagnumDocker && dotnet test --filter BmkDevice
```

Expected: All 5 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add MagnumDocker/
git commit -m "feat: add BmkDevice parser for 18-byte battery monitor packets"
```

---

### Task 7: AgsDevice Parser

**Files:**
- Create: `MagnumDocker/src/Magnum.Serial/Devices/AgsDevice.cs`
- Create: `MagnumDocker/tests/Magnum.Serial.Tests/AgsDeviceTests.cs`

- [ ] **Step 1: Write failing tests**

Test packet `A102343A007F` (AGS_A1) — format `>BbBbBB`:
- [0] B=0xA1 (marker), [1] b=0x02=2 (status="Ready")
- [2] B=0x34=52 (revision=5.2), [3] b=0x3A=58 (temp=58F → 14.4C)
- [4] B=0x00=0 (runtime=0.0), [5] B=0x7F=127 (vdc=127/10*mult)

Test packet `A20000000000` (AGS_A2) — format `>BBBHB`:
- [0] B=0xA2 (marker), [1] B=0x00 (last_run), [2] B=0x00 (last_soc)
- [3-4] H=0x0000 (total_run), [5] B=0x00

Create `MagnumDocker/tests/Magnum.Serial.Tests/AgsDeviceTests.cs`:

```csharp
using Magnum.Serial;
using Magnum.Serial.Devices;

namespace Magnum.Serial.Tests;

public class AgsDeviceTests
{
    [Fact]
    public void Parse_AgsA1_ExtractsStatus()
    {
        var device = new AgsDevice();
        device.Parse(TestPackets.AgsA1, PacketType.AgsA1, 2);
        var data = device.GetData();

        Assert.Equal("Ready", data["status"]);
    }

    [Fact]
    public void Parse_AgsA1_ExtractsRevision()
    {
        var device = new AgsDevice();
        device.Parse(TestPackets.AgsA1, PacketType.AgsA1, 2);
        var data = device.GetData();

        Assert.Equal(5.2, data["revision"]);
    }

    [Fact]
    public void Parse_AgsA1_ConvertsTemperatureToC()
    {
        var device = new AgsDevice();
        device.Parse(TestPackets.AgsA1, PacketType.AgsA1, 2);
        var data = device.GetData();

        // 58F => (58-32)*5/9 = 14.4C
        Assert.Equal(14.4, (double)data["temp"], 1);
    }

    [Fact]
    public void Parse_AgsA1_ExtractsVdcWithMultiplier()
    {
        var device = new AgsDevice();
        device.Parse(TestPackets.AgsA1, PacketType.AgsA1, 2);
        var data = device.GetData();

        // 127 / 10 * 2 = 25.4
        Assert.Equal(25.4, data["vdc"]);
    }

    [Fact]
    public void Parse_AgsA1_NotRunning()
    {
        var device = new AgsDevice();
        device.Parse(TestPackets.AgsA1, PacketType.AgsA1, 2);
        var data = device.GetData();

        // Status 2 = "Ready" is not in the running set
        Assert.Equal(false, data["running"]);
    }

    [Fact]
    public void Parse_AgsA2_ExtractsUsageData()
    {
        var device = new AgsDevice();
        device.Parse(TestPackets.AgsA2, PacketType.AgsA2, 2);
        var data = device.GetData();

        Assert.Equal(0, data["gen_last_run"]);
        Assert.Equal(0, data["last_full_soc"]);
        Assert.Equal(0, data["gen_total_run"]);
    }

    [Fact]
    public void Parse_AccumulatesState_AcrossPacketTypes()
    {
        var device = new AgsDevice();
        device.Parse(TestPackets.AgsA1, PacketType.AgsA1, 2);
        device.Parse(TestPackets.AgsA2, PacketType.AgsA2, 2);
        var data = device.GetData();

        // Should have fields from both A1 and A2
        Assert.True(data.ContainsKey("status"));
        Assert.True(data.ContainsKey("gen_last_run"));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd MagnumDocker && dotnet test --filter AgsDevice
```

Expected: FAIL — `AgsDevice` does not exist.

- [ ] **Step 3: Implement AgsDevice**

Create `MagnumDocker/src/Magnum.Serial/Devices/AgsDevice.cs`:

```csharp
using Magnum.Serial.Lookups;

namespace Magnum.Serial.Devices;

public class AgsDevice : IDeviceParser
{
    public string DeviceType => "AGS";

    private readonly Dictionary<string, object> _data = new();

    public void Parse(byte[] packet, PacketType packetType, int voltageMultiplier)
    {
        if (packetType == PacketType.AgsA1)
            ParseA1(packet, voltageMultiplier);
        else if (packetType == PacketType.AgsA2)
            ParseA2(packet);
    }

    private void ParseA1(byte[] packet, int voltageMultiplier)
    {
        // Format: >BbBbBB
        int statusCode = BigEndianReader.ReadSByte(packet, 1);
        double revision = Math.Round(BigEndianReader.ReadByte(packet, 2) / 10.0, 1);
        double temp = (double)BigEndianReader.ReadSByte(packet, 3);
        double runtime = Math.Round(BigEndianReader.ReadByte(packet, 4) / 10.0, 2);
        double vdc = Math.Round(BigEndianReader.ReadByte(packet, 5) / 10.0 * voltageMultiplier, 2);

        // Convert Fahrenheit to Celsius if < 105
        if (temp < 105.0)
            temp = Math.Round((temp - 32) * 5.0 / 9.0, 1);

        bool running = AgsLookups.RunningStatuses.Contains(statusCode);

        _data["status"] = AgsLookups.Statuses.GetValueOrDefault(statusCode, statusCode.ToString());
        _data["revision"] = revision;
        _data["temp"] = temp;
        _data["runtime"] = runtime;
        _data["vdc"] = vdc;
        _data["running"] = running;
    }

    private void ParseA2(byte[] packet)
    {
        // Format: >BBBHB
        _data["gen_last_run"] = (int)BigEndianReader.ReadByte(packet, 1);
        _data["last_full_soc"] = (int)BigEndianReader.ReadByte(packet, 2);
        _data["gen_total_run"] = (int)BigEndianReader.ReadUInt16(packet, 3);
    }

    public Dictionary<string, object> GetData() => new(_data);
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd MagnumDocker && dotnet test --filter AgsDevice
```

Expected: All 7 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add MagnumDocker/
git commit -m "feat: add AgsDevice parser for AGS status and usage packets"
```

---

### Task 8: Pt100Device Parser

**Files:**
- Create: `MagnumDocker/src/Magnum.Serial/Devices/Pt100Device.cs`
- Create: `MagnumDocker/tests/Magnum.Serial.Tests/Pt100DeviceTests.cs`

- [ ] **Step 1: Write failing tests**

Test packet `C1001300024D008505BC1194101B313E` (PT_C1) — format `>BBBBHhHBBBBBB`:
- [0] B=0xC1 (marker), [1] B=0x00 → address=0x00&0x07=0
- [2] B=0x13=19 → mode=19&0x0F=3("Absorb"), regulation=19>>4=1("Voltage")
- [3] B=0x00 → fault=0>>3=0("No Fault")
- [4-5] H=0x024D=589 → battery=58.9V
- [6-7] h=0x0085=133 → battery_amps=13.3A
- [8-9] H=0x05BC=1468 → pv_voltage=146.8V
- [10] B=0x11=17 → charge_time=1.7
- [11] B=0x94=148 → target_battery_voltage=148/10*multiplier
- [12] B=0x10=16=0b00010000 → relay=0, alarm=0, fan=0, day=1
- [13] B=0x1B=27 → battery_temp=27.0
- [14] B=0x31=49 → inductor_temp=49.0
- [15] B=0x3E=62 → fet_temp=62.0

Create `MagnumDocker/tests/Magnum.Serial.Tests/Pt100DeviceTests.cs`:

```csharp
using Magnum.Serial;
using Magnum.Serial.Devices;

namespace Magnum.Serial.Tests;

public class Pt100DeviceTests
{
    [Fact]
    public void Parse_PtC1_ExtractsModeAndRegulation()
    {
        var device = new Pt100Device();
        device.Parse(TestPackets.PtC1, PacketType.PtC1, 2);
        var data = device.GetData();

        Assert.Equal("Absorb", data["mode"]);
        Assert.Equal("Voltage", data["regulation"]);
    }

    [Fact]
    public void Parse_PtC1_ExtractsFault()
    {
        var device = new Pt100Device();
        device.Parse(TestPackets.PtC1, PacketType.PtC1, 2);
        var data = device.GetData();

        Assert.Equal("No Fault", data["fault"]);
    }

    [Fact]
    public void Parse_PtC1_ExtractsVoltagesAndAmps()
    {
        var device = new Pt100Device();
        device.Parse(TestPackets.PtC1, PacketType.PtC1, 2);
        var data = device.GetData();

        Assert.Equal(58.9, data["battery"]);
        Assert.Equal(13.3, data["battery_amps"]);
        Assert.Equal(146.8, data["pv_voltage"]);
    }

    [Fact]
    public void Parse_PtC1_ExtractsFlags()
    {
        var device = new Pt100Device();
        device.Parse(TestPackets.PtC1, PacketType.PtC1, 2);
        var data = device.GetData();

        // 0x10 = 0b00010000: bit0=0(relay), bit1=0(alarm), bit3=0(fan), bit4=1(day)
        Assert.Equal(0, data["relay_state"]);
        Assert.Equal(0, data["alarm_state"]);
        Assert.Equal(0, data["fan_on"]);
        Assert.Equal(1, data["day"]);
    }

    [Fact]
    public void Parse_PtC1_ExtractsTemperatures()
    {
        var device = new Pt100Device();
        device.Parse(TestPackets.PtC1, PacketType.PtC1, 2);
        var data = device.GetData();

        Assert.Equal(27.0, data["battery_temp"]);
        Assert.Equal(49.0, data["inductor_temp"]);
        Assert.Equal(62.0, data["fet_temp"]);
    }

    [Fact]
    public void Parse_PtC2_ExtractsLifetimeData()
    {
        var device = new Pt100Device();
        device.Parse(TestPackets.PtC2, PacketType.PtC2, 2);
        var data = device.GetData();

        // C200000200AC0002050B641425
        // [0]=C2, [1]=00 (addr)
        // [2-3]=H=0x0002=2 → lifetime_kwhrs=2*10=20
        // [4-5]=H=0x00AC=172 → resettable_kwhrs=172/10=17.2
        Assert.Equal(20, data["lifetime_kwhrs"]);
        Assert.Equal(17.2, data["resettable_kwhrs"]);
    }

    [Fact]
    public void Parse_AccumulatesState_AcrossPtC1AndPtC2()
    {
        var device = new Pt100Device();
        device.Parse(TestPackets.PtC1, PacketType.PtC1, 2);
        device.Parse(TestPackets.PtC2, PacketType.PtC2, 2);
        var data = device.GetData();

        Assert.True(data.ContainsKey("battery"));
        Assert.True(data.ContainsKey("lifetime_kwhrs"));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd MagnumDocker && dotnet test --filter Pt100Device
```

Expected: FAIL — `Pt100Device` does not exist.

- [ ] **Step 3: Implement Pt100Device**

Create `MagnumDocker/src/Magnum.Serial/Devices/Pt100Device.cs`:

```csharp
using Magnum.Serial.Lookups;

namespace Magnum.Serial.Devices;

public class Pt100Device : IDeviceParser
{
    public string DeviceType => "PT100";

    private readonly Dictionary<string, object> _data = new();

    public void Parse(byte[] packet, PacketType packetType, int voltageMultiplier)
    {
        if (packetType == PacketType.PtC1)
            ParseC1(packet, voltageMultiplier);
        else if (packetType == PacketType.PtC2)
            ParseC2(packet);
        // PtC3 is a stub in Python — skip
    }

    private void ParseC1(byte[] packet, int voltageMultiplier)
    {
        // Format: >BBBBHhHBBBBBB (16 bytes)
        int address = BigEndianReader.ReadByte(packet, 1) & 0x07;
        int modeByte = BigEndianReader.ReadByte(packet, 2);
        int mode = modeByte & 0x0F;
        int regulation = modeByte >> 4;
        int fault = BigEndianReader.ReadByte(packet, 3) >> 3;

        double battery = Math.Round(BigEndianReader.ReadUInt16(packet, 4) / 10.0, 1);
        double batteryAmps = Math.Round(BigEndianReader.ReadInt16(packet, 6) / 10.0, 1);
        double pvVoltage = Math.Round(BigEndianReader.ReadUInt16(packet, 8) / 10.0, 1);
        double chargeTime = Math.Round(BigEndianReader.ReadByte(packet, 10) / 10.0, 1);
        double targetBatteryVoltage = Math.Round(BigEndianReader.ReadByte(packet, 11) / 10.0 * voltageMultiplier, 1);

        int flags = BigEndianReader.ReadByte(packet, 12);
        int relayState = flags & 1;
        int alarmState = (flags >> 1) & 1;
        int fanOn = (flags >> 3) & 1;
        int day = (flags >> 4) & 1;

        int batteryTempRaw = BigEndianReader.ReadByte(packet, 13);
        object batteryTemp = (batteryTempRaw == 0x97 || batteryTempRaw == 0x98)
            ? (object)"Sensor Fault"
            : (double)batteryTempRaw;

        double inductorTemp = (double)BigEndianReader.ReadByte(packet, 14);
        double fetTemp = (double)BigEndianReader.ReadByte(packet, 15);

        _data["address"] = address;
        _data["mode"] = Pt100Lookups.Modes.GetValueOrDefault(mode, mode.ToString());
        _data["regulation"] = Pt100Lookups.Regulations.GetValueOrDefault(regulation, regulation.ToString());
        _data["fault"] = Pt100Lookups.Faults.GetValueOrDefault(fault, fault.ToString());
        _data["battery"] = battery;
        _data["battery_amps"] = batteryAmps;
        _data["pv_voltage"] = pvVoltage;
        _data["charge_time"] = chargeTime;
        _data["target_battery_voltage"] = targetBatteryVoltage;
        _data["relay_state"] = relayState;
        _data["alarm_state"] = alarmState;
        _data["fan_on"] = fanOn;
        _data["day"] = day;
        _data["battery_temp"] = batteryTemp;
        _data["inductor_temp"] = inductorTemp;
        _data["fet_temp"] = fetTemp;
    }

    private void ParseC2(byte[] packet)
    {
        // Format: >BBHHBBBBBBB (13 bytes)
        // Actually from source: 'BBHHBBBBBBB' - let me trace byte offsets
        // [0]=C2 marker, [1]=address
        // [2-3]=H=lifetime_kwhrs, [4-5]=H=resettable_kwhrs
        // [6]=B=ground_fault, [7]=B=stacker_info
        // [8]=B=dip_switches, [9]=B=revision, [10]=B=output_current
        // [11]=B=input_voltage

        // Wait - the format is 'BBHHBBBBBBB' = B+B+H+H+B+B+B+B+B+B+B = 2+2+2+5*1... 
        // = 1+1+2+2+1+1+1+1+1+1+1 = 13 bytes ✓

        int address = BigEndianReader.ReadByte(packet, 1);
        int lifetimeKwhrs = BigEndianReader.ReadUInt16(packet, 2) * 10;
        double resettableKwhrs = Math.Round(BigEndianReader.ReadUInt16(packet, 4) / 10.0, 1);
        int groundFaultCurrent = BigEndianReader.ReadByte(packet, 6);

        int stackerByte = BigEndianReader.ReadByte(packet, 7);
        int stackerInfo = stackerByte >> 2;
        int nominalBatteryBits = stackerByte & 0x03;
        string nominalBatteryVoltage = nominalBatteryBits == 0 ? "12V" : $"{nominalBatteryBits * 24}V";

        int dipSwitches = BigEndianReader.ReadByte(packet, 8);
        string dipSwitchesStr = Convert.ToString(dipSwitches, 2).PadLeft(8, '0');

        double revision = Math.Round(BigEndianReader.ReadByte(packet, 9) / 10.0, 1);
        int outputCurrentRating = BigEndianReader.ReadByte(packet, 10);
        int inputVoltageRating = BigEndianReader.ReadByte(packet, 11) * 10;

        _data["lifetime_kwhrs"] = lifetimeKwhrs;
        _data["resettable_kwhrs"] = resettableKwhrs;
        _data["ground_fault_current"] = groundFaultCurrent;
        _data["stacker_info"] = stackerInfo;
        _data["nominal_battery_voltage"] = nominalBatteryVoltage;
        _data["dip_switches"] = dipSwitchesStr;
        _data["revision"] = revision;
        _data["output_current_rating"] = outputCurrentRating;
        _data["input_voltage_rating"] = inputVoltageRating;
    }

    public Dictionary<string, object> GetData() => new(_data);
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd MagnumDocker && dotnet test --filter Pt100Device
```

Expected: All 7 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add MagnumDocker/
git commit -m "feat: add Pt100Device parser for solar charge controller packets"
```

---

### Task 9: RtrDevice and AcldDevice Parsers

**Files:**
- Create: `MagnumDocker/src/Magnum.Serial/Devices/RtrDevice.cs`
- Create: `MagnumDocker/src/Magnum.Serial/Devices/AcldDevice.cs`
- Create: `MagnumDocker/tests/Magnum.Serial.Tests/RtrDeviceTests.cs`

- [ ] **Step 1: Write failing tests**

Test packet `9120` (RTR_91) — format `>BB`:
- [0] B=0x91 (marker), [1] B=0x20=32 → revision=32/10=3.2 → rounded to int=3

Create `MagnumDocker/tests/Magnum.Serial.Tests/RtrDeviceTests.cs`:

```csharp
using Magnum.Serial;
using Magnum.Serial.Devices;

namespace Magnum.Serial.Tests;

public class RtrDeviceTests
{
    [Fact]
    public void Parse_Rtr91_ExtractsRevision()
    {
        var device = new RtrDevice();
        device.Parse(TestPackets.Rtr91, PacketType.Rtr91, 1);
        var data = device.GetData();

        // 0x20=32, 32/10=3.2, rounded to int = 3
        Assert.Equal(3, data["revision"]);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd MagnumDocker && dotnet test --filter RtrDevice
```

Expected: FAIL — `RtrDevice` does not exist.

- [ ] **Step 3: Implement RtrDevice and AcldDevice**

Create `MagnumDocker/src/Magnum.Serial/Devices/RtrDevice.cs`:

```csharp
namespace Magnum.Serial.Devices;

public class RtrDevice : IDeviceParser
{
    public string DeviceType => "RTR";

    private readonly Dictionary<string, object> _data = new();

    public void Parse(byte[] packet, PacketType packetType, int voltageMultiplier)
    {
        // Format: >BB
        int revision = (int)(BigEndianReader.ReadByte(packet, 1) / 10.0);
        _data["revision"] = revision;
    }

    public Dictionary<string, object> GetData() => new(_data);
}
```

Create `MagnumDocker/src/Magnum.Serial/Devices/AcldDevice.cs`:

```csharp
namespace Magnum.Serial.Devices;

public class AcldDevice : IDeviceParser
{
    public string DeviceType => "ACLD";

    private readonly Dictionary<string, object> _data = new();

    public void Parse(byte[] packet, PacketType packetType, int voltageMultiplier)
    {
        // Stub — not implemented in Python source either
        // Store raw bytes for debugging
        _data["raw"] = Convert.ToHexString(packet);
    }

    public Dictionary<string, object> GetData() => new(_data);
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd MagnumDocker && dotnet test --filter RtrDevice
```

Expected: 1 test PASS.

- [ ] **Step 5: Commit**

```bash
git add MagnumDocker/
git commit -m "feat: add RtrDevice and AcldDevice parsers"
```

---

### Task 10: RemoteDevice Parser

**Files:**
- Create: `MagnumDocker/src/Magnum.Serial/Devices/RemoteDevice.cs`
- Create: `MagnumDocker/tests/Magnum.Serial.Tests/RemoteDeviceTests.cs`

- [ ] **Step 1: Write failing tests**

Test packet `00002808640A280000C89B840C14122014007300A0` (REMOTE_A0):

Base fields format `>BBBBBbBBBBBBBB` (14 bytes, indices 0-13):
- [0]=0x00 (marker), [1]=0x00 → searchwatts=0
- [2]=0x28=40 → batterysize=40*10=400
- [3]=0x08=8 → absorb: 8<=100 so battype=8
- [4]=0x64=100 → chargeramps=100
- [5]=0x0A=10(signed) → ainput=10
- [6]=0x28=40 → revision=40/10=4.0
- [7]=0x00 → parallel=0&0x0F*10=0
- skip byte [8]
- [9]=0xC8=200 → lbco=200/10=20.0
- [10]=0x9B=155 → vaccutout=155.0
- [11]=0x84=132 → vsfloat=132*multiplier/10 (with mult=2: 26.4)
- [12]=0x0C=12 → vEQ depends on absorb
- [13]=0x14=20 → absorbtime=20/10=2.0

A0-specific (bytes 14-20) format `BBBbBBB`:
- [14]=0x12=18 → remotetimehours
- [15]=0x20=32 → remotetimemins
- [16]=0x14=20 → runtime=20/10=2.0
- [17]=0x00=0(signed) → starttemp: (0-32)*5/9=-17.8C
- [18]=0x73=115 → startvdc=115*2/10=23.0
- [19]=0x00 → quiettime=0
- [20]=0xA0 → last byte (subtype marker)

Create `MagnumDocker/tests/Magnum.Serial.Tests/RemoteDeviceTests.cs`:

```csharp
using Magnum.Serial;
using Magnum.Serial.Devices;

namespace Magnum.Serial.Tests;

public class RemoteDeviceTests
{
    [Fact]
    public void Parse_RemoteA0_ExtractsBaseFields()
    {
        var device = new RemoteDevice();
        device.Parse(TestPackets.RemoteA0, PacketType.RemoteA0, 2);
        var data = device.GetData();

        Assert.Equal(0, data["searchwatts"]);
        Assert.Equal(400, data["batterysize"]);
        Assert.Equal(100, data["chargeramps"]);
        Assert.Equal(4.0, data["revision"]);
        Assert.Equal(20.0, data["lbco"]);
    }

    [Fact]
    public void Parse_RemoteA0_ExtractsAgsFields()
    {
        var device = new RemoteDevice();
        device.Parse(TestPackets.RemoteA0, PacketType.RemoteA0, 2);
        var data = device.GetData();

        Assert.Equal(18, data["remotetimehours"]);
        Assert.Equal(32, data["remotetimemins"]);
        Assert.Equal(2.0, data["runtime"]);
    }

    [Fact]
    public void Parse_Remote00_ExtractsBaseFieldsOnly()
    {
        var device = new RemoteDevice();
        device.Parse(TestPackets.Remote00, PacketType.Remote00, 2);
        var data = device.GetData();

        Assert.True(data.ContainsKey("searchwatts"));
        Assert.True(data.ContainsKey("revision"));
    }

    [Fact]
    public void Parse_Remote80_ExtractsBmkFields()
    {
        var device = new RemoteDevice();
        device.Parse(TestPackets.Remote80, PacketType.Remote80, 2);
        var data = device.GetData();

        Assert.True(data.ContainsKey("batteryefficiency"));
    }

    [Fact]
    public void Parse_AccumulatesState_AcrossSubtypes()
    {
        var device = new RemoteDevice();
        device.Parse(TestPackets.RemoteA0, PacketType.RemoteA0, 2);
        device.Parse(TestPackets.RemoteA1, PacketType.RemoteA1, 2);
        var data = device.GetData();

        // A0 fields
        Assert.True(data.ContainsKey("remotetimehours"));
        // A1 fields
        Assert.True(data.ContainsKey("begintime"));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd MagnumDocker && dotnet test --filter RemoteDevice
```

Expected: FAIL — `RemoteDevice` does not exist.

- [ ] **Step 3: Implement RemoteDevice**

Create `MagnumDocker/src/Magnum.Serial/Devices/RemoteDevice.cs`:

```csharp
namespace Magnum.Serial.Devices;

public class RemoteDevice : IDeviceParser
{
    public string DeviceType => "REMOTE";

    private readonly Dictionary<string, object> _data = new();

    public void Parse(byte[] packet, PacketType packetType, int voltageMultiplier)
    {
        SetBaseValues(packet, voltageMultiplier);

        switch (packetType)
        {
            case PacketType.RemoteA0:
                ParseA0(packet, voltageMultiplier);
                break;
            case PacketType.RemoteA1:
                ParseA1(packet, voltageMultiplier);
                break;
            case PacketType.RemoteA2:
                ParseA2(packet);
                break;
            case PacketType.RemoteA3:
                ParseA3(packet);
                break;
            case PacketType.RemoteA4:
                ParseA4(packet);
                break;
            case PacketType.Remote80:
                ParseRemote80(packet);
                break;
            // Remote00, RemoteC, Remote11, RemoteC0-C3, RemoteD0: base fields only
        }
    }

    private void SetBaseValues(byte[] packet, int voltageMultiplier)
    {
        // Base format: >BBBBBbBBBBBBBB (14 bytes at positions 0-13)
        // The base remote format uses indices into the unpacked tuple
        int searchWatts = BigEndianReader.ReadByte(packet, 1);
        int batterySize = BigEndianReader.ReadByte(packet, 2) * 10;

        int absorbRaw = BigEndianReader.ReadByte(packet, 3);
        object absorb;
        if (absorbRaw > 100)
            absorb = Math.Round(absorbRaw * voltageMultiplier / 10.0, 1);
        else
            absorb = absorbRaw; // battery type

        int chargerAmps = BigEndianReader.ReadByte(packet, 4);
        int ainput = BigEndianReader.ReadSByte(packet, 5);
        double revision = Math.Round(BigEndianReader.ReadByte(packet, 6) / 10.0, 1);
        int parallel = (BigEndianReader.ReadByte(packet, 7) & 0x0F) * 10;

        // byte[8] is skipped in base parsing
        double lbco = Math.Round(BigEndianReader.ReadByte(packet, 9) / 10.0, 1);
        double vacCutout = (double)BigEndianReader.ReadByte(packet, 10);
        double vsFloat = Math.Round(BigEndianReader.ReadByte(packet, 11) * voltageMultiplier / 10.0, 1);

        int veqRaw = BigEndianReader.ReadByte(packet, 12);
        // vEQ = absorb + veqRaw/10 (only if absorb is numeric voltage)
        double veq;
        if (absorbRaw > 100)
            veq = Math.Round(absorbRaw * voltageMultiplier / 10.0 + veqRaw / 10.0, 1);
        else
            veq = Math.Round(veqRaw / 10.0, 1);

        double absorbTime = Math.Round(BigEndianReader.ReadByte(packet, 13) / 10.0, 1);

        _data["searchwatts"] = searchWatts;
        _data["batterysize"] = batterySize;
        _data["absorb"] = absorb;
        _data["chargeramps"] = chargerAmps;
        _data["ainput"] = ainput;
        _data["revision"] = revision;
        _data["parallel"] = parallel;
        _data["lbco"] = lbco;
        _data["vaccutout"] = vacCutout;
        _data["vsfloat"] = vsFloat;
        _data["vEQ"] = veq;
        _data["absorbtime"] = absorbTime;
    }

    private void ParseA0(byte[] packet, int voltageMultiplier)
    {
        // A0 suffix format: BBBbBBB at bytes 14-20
        _data["remotetimehours"] = (int)BigEndianReader.ReadByte(packet, 14);
        _data["remotetimemins"] = (int)BigEndianReader.ReadByte(packet, 15);
        _data["runtime"] = Math.Round(BigEndianReader.ReadByte(packet, 16) / 10.0, 1);

        double startTempF = (double)BigEndianReader.ReadSByte(packet, 17);
        _data["starttemp"] = Math.Round((startTempF - 32) * 5.0 / 9.0, 1);

        _data["startvdc"] = Math.Round(BigEndianReader.ReadByte(packet, 18) * voltageMultiplier / 10.0, 1);
        _data["quiettime"] = (int)BigEndianReader.ReadByte(packet, 19);
    }

    private void ParseA1(byte[] packet, int voltageMultiplier)
    {
        // A1 suffix format: 7B at bytes 14-20
        int beginRaw = BigEndianReader.ReadByte(packet, 14);
        int stopRaw = BigEndianReader.ReadByte(packet, 15);
        _data["begintime"] = MinutesToHHMM(beginRaw * 15);
        _data["stoptime"] = MinutesToHHMM(stopRaw * 15);

        _data["vdcstop"] = Math.Round(BigEndianReader.ReadByte(packet, 16) * voltageMultiplier / 10.0, 1);

        int voltStartDelay = BigEndianReader.ReadByte(packet, 17);
        _data["voltstartdelay"] = voltStartDelay > 127 ? (voltStartDelay & 0x0F) * 60 : voltStartDelay;

        int voltStopDelay = BigEndianReader.ReadByte(packet, 18);
        _data["voltstopdelay"] = voltStopDelay > 127 ? (voltStopDelay & 0x0F) * 60 : voltStopDelay;

        _data["maxrun"] = Math.Round(BigEndianReader.ReadByte(packet, 19) / 10.0, 1);
    }

    private void ParseA2(byte[] packet)
    {
        // A2 suffix: bb5B at bytes 14-20
        _data["socstart"] = (int)BigEndianReader.ReadSByte(packet, 14);
        _data["socstop"] = (int)BigEndianReader.ReadSByte(packet, 15);
        _data["ampstart"] = (double)BigEndianReader.ReadByte(packet, 16);

        int ampsStartDelay = BigEndianReader.ReadByte(packet, 17);
        _data["ampsstartdelay"] = ampsStartDelay > 127 ? (ampsStartDelay & 0x0F) * 60 : ampsStartDelay;

        _data["ampstop"] = (int)BigEndianReader.ReadByte(packet, 18);

        int ampsStopDelay = BigEndianReader.ReadByte(packet, 19);
        _data["ampsstopdelay"] = ampsStopDelay > 127 ? (ampsStopDelay & 0x0F) * 60 : ampsStopDelay;
    }

    private void ParseA3(byte[] packet)
    {
        // A3 suffix: 7B at bytes 14-20
        int quietBegin = BigEndianReader.ReadByte(packet, 14);
        int quietEnd = BigEndianReader.ReadByte(packet, 15);
        _data["quietbegintime"] = MinutesToHHMM(quietBegin * 15);
        _data["quietendtime"] = MinutesToHHMM(quietEnd * 15);

        int exerciseStart = BigEndianReader.ReadByte(packet, 16);
        _data["exercisestart"] = MinutesToHHMM(exerciseStart * 15);

        _data["runtime"] = Math.Round(BigEndianReader.ReadByte(packet, 17) / 10.0, 1);
        _data["topoff"] = (int)BigEndianReader.ReadByte(packet, 18);
    }

    private void ParseA4(byte[] packet)
    {
        // A4 suffix: 7B at bytes 14-20
        int warmup = BigEndianReader.ReadByte(packet, 14);
        _data["warmup"] = warmup > 127 ? (warmup & 0x0F) * 60 : warmup;

        int cool = BigEndianReader.ReadByte(packet, 15);
        _data["cool"] = cool > 127 ? (cool & 0x0F) * 60 : cool;
    }

    private void ParseRemote80(byte[] packet)
    {
        // Remote80 suffix: bbbb3B at bytes 14-20
        _data["remotetimehours"] = (int)BigEndianReader.ReadSByte(packet, 14);
        _data["remotetimemins"] = (int)BigEndianReader.ReadSByte(packet, 15);
        _data["batteryefficiency"] = (int)BigEndianReader.ReadSByte(packet, 16);
    }

    private static int MinutesToHHMM(int totalMinutes)
    {
        int hours = totalMinutes / 60;
        int mins = totalMinutes % 60;
        return hours * 100 + mins;
    }

    public Dictionary<string, object> GetData() => new(_data);
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd MagnumDocker && dotnet test --filter RemoteDevice
```

Expected: All 5 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add MagnumDocker/
git commit -m "feat: add RemoteDevice parser for all remote control subtypes"
```

---

### Task 11: MagnumReader (Serial Communication + Orchestration)

**Files:**
- Create: `MagnumDocker/src/Magnum.Serial/MagnumReader.cs`

This task does not use TDD because it directly wraps hardware (serial port). The device parsers and packet identifier are already tested.

- [ ] **Step 1: Implement MagnumReader**

Create `MagnumDocker/src/Magnum.Serial/MagnumReader.cs`:

```csharp
using System.IO.Ports;
using Magnum.Serial.Devices;

namespace Magnum.Serial;

public class MagnumReader : IDisposable
{
    private readonly MagnumReaderOptions _options;
    private readonly SerialPort _reader;
    private readonly Dictionary<string, IDeviceParser> _devices = new();
    private int _voltageMultiplier = 1;

    public string CommDevice { get; }

    public MagnumReader(MagnumReaderOptions options)
    {
        _options = options;
        CommDevice = options.Device;

        _reader = new SerialPort(options.Device)
        {
            BaudRate = 19200,
            DataBits = 8,
            StopBits = StopBits.One,
            Parity = Parity.None,
            DtrEnable = false,
            ReadTimeout = Math.Max(1, (int)(options.Timeout * 1000)),
        };

        _reader.Open();
    }

    public List<DeviceData> GetDevices()
    {
        var rawPackets = ReadPackets();

        if (_options.CleanPackets)
            rawPackets = CleanupPackets(rawPackets);

        foreach (var packet in rawPackets)
        {
            var packetType = PacketIdentifier.Identify(packet);
            if (packetType == PacketType.Unknown)
                continue;

            var parser = GetOrCreateParser(packetType);
            if (parser == null)
                continue;

            parser.Parse(packet, packetType, _voltageMultiplier);

            // Update voltage multiplier if we just parsed an inverter
            if (parser is InverterDevice inv)
                _voltageMultiplier = inv.VoltageMultiplier;
        }

        return _devices.Values
            .Select(p => new DeviceData
            {
                Device = p.DeviceType,
                Data = p.GetData(),
            })
            .ToList();
    }

    private List<byte[]> ReadPackets()
    {
        var packets = new List<byte[]>();
        var buffer = new List<byte>();
        var readBuffer = new byte[256];

        for (int i = 0; i < _options.Packets; i++)
        {
            try
            {
                int bytesRead = _reader.Read(readBuffer, 0, readBuffer.Length);
                if (bytesRead > 0)
                {
                    buffer.AddRange(readBuffer.AsSpan(0, bytesRead).ToArray());
                }
                else
                {
                    // Timeout — current buffer is a complete packet
                    if (buffer.Count > 0)
                    {
                        packets.Add(buffer.ToArray());
                        buffer.Clear();
                    }
                }
            }
            catch (TimeoutException)
            {
                // Timeout — current buffer is a complete packet
                if (buffer.Count > 0)
                {
                    packets.Add(buffer.ToArray());
                    buffer.Clear();
                }
            }
        }

        // Flush remaining buffer
        if (buffer.Count > 0)
            packets.Add(buffer.ToArray());

        return packets;
    }

    private List<byte[]> CleanupPackets(List<byte[]> packets)
    {
        var result = new List<byte[]>();
        var unknownRun = new List<byte[]>();

        foreach (var packet in packets)
        {
            var type = PacketIdentifier.Identify(packet);
            if (type == PacketType.Unknown)
            {
                unknownRun.Add(packet);
            }
            else
            {
                if (unknownRun.Count >= 2)
                    TryMergeUnknowns(unknownRun, result);
                unknownRun.Clear();
                result.Add(packet);
            }
        }

        if (unknownRun.Count >= 2)
            TryMergeUnknowns(unknownRun, result);

        return result;
    }

    private static void TryMergeUnknowns(List<byte[]> unknowns, List<byte[]> output)
    {
        // Merge consecutive unknown packets and try to re-identify
        var merged = unknowns.SelectMany(p => p).ToArray();
        var mergedType = PacketIdentifier.Identify(merged);
        if (mergedType != PacketType.Unknown)
            output.Add(merged);
    }

    private IDeviceParser? GetOrCreateParser(PacketType packetType)
    {
        string key = packetType switch
        {
            PacketType.Inv or PacketType.InvC => "INVERTER",
            PacketType.Bmk81 => "BMK",
            PacketType.AgsA1 or PacketType.AgsA2 => "AGS",
            PacketType.PtC1 or PacketType.PtC2 or PacketType.PtC3 => "PT100",
            PacketType.Rtr91 => "RTR",
            PacketType.AcldD1 => "ACLD",
            PacketType.RemoteC or PacketType.Remote00 or PacketType.Remote11
                or PacketType.Remote80 or PacketType.RemoteA0 or PacketType.RemoteA1
                or PacketType.RemoteA2 or PacketType.RemoteA3 or PacketType.RemoteA4
                or PacketType.RemoteC0 or PacketType.RemoteC1 or PacketType.RemoteC2
                or PacketType.RemoteC3 or PacketType.RemoteD0 => "REMOTE",
            _ => null!,
        };

        if (key == null) return null;

        if (!_devices.TryGetValue(key, out var parser))
        {
            parser = key switch
            {
                "INVERTER" => new InverterDevice(),
                "BMK" => new BmkDevice(),
                "AGS" => new AgsDevice(),
                "PT100" => new Pt100Device(),
                "RTR" => new RtrDevice(),
                "ACLD" => new AcldDevice(),
                "REMOTE" => new RemoteDevice(),
                _ => null!,
            };
            if (parser != null)
                _devices[key] = parser;
        }

        return parser;
    }

    public void Dispose()
    {
        if (_reader.IsOpen)
            _reader.Close();
        _reader.Dispose();
    }
}
```

- [ ] **Step 2: Build and verify**

```bash
cd MagnumDocker && dotnet build
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add MagnumDocker/
git commit -m "feat: add MagnumReader for serial port communication and packet orchestration"
```

---

### Task 12: Magnum.Server — Minimal API

**Files:**
- Create: `MagnumDocker/src/Magnum.Server/MagnumService.cs`
- Create: `MagnumDocker/src/Magnum.Server/Program.cs`
- Create: `MagnumDocker/src/Magnum.Server/appsettings.json`
- Copy: `index.html` → `MagnumDocker/src/Magnum.Server/wwwroot/index.html`

- [ ] **Step 1: Create MagnumService**

Create `MagnumDocker/src/Magnum.Server/MagnumService.cs`:

```csharp
using Magnum.Serial;

namespace Magnum.Server;

public class MagnumService : IDisposable
{
    private readonly Dictionary<string, MagnumReader> _readers = new();
    private readonly ILogger<MagnumService> _logger;

    public MagnumService(ILogger<MagnumService> logger)
    {
        _logger = logger;
    }

    public void AddReader(MagnumReader reader)
    {
        _readers[reader.CommDevice] = reader;
    }

    public int ReaderCount => _readers.Count;

    public IResult GetAllDeviceData()
    {
        var response = new List<object>();
        int? errorCode = null;
        string? errorMessage = null;

        var timestamp = DateTimeOffset.UtcNow
            .ToLocalTime()
            .ToString("yyyy-MM-ddTHH:mm:sszzz");

        foreach (var (commDevice, reader) in _readers)
        {
            try
            {
                var devices = reader.GetDevices();
                if (devices.Count > 0)
                {
                    response.Add(new
                    {
                        comm_device = commDevice,
                        data = new
                        {
                            datetime = timestamp,
                            devices = devices.Select(d => new
                            {
                                device = d.Device,
                                data = d.Data,
                            }),
                        },
                    });
                }
                else
                {
                    errorMessage = "No data available";
                    errorCode = 204;
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"Exception detected attempting to read network data - {commDevice} {ex.Message}";
                errorCode = 500;
                _logger.LogError(ex, "Error reading from {Device}", commDevice);
            }
        }

        if (response.Count > 0)
            return Results.Json(response);

        return errorCode == 204
            ? Results.NoContent()
            : Results.Problem(errorMessage, statusCode: errorCode ?? 500);
    }

    public void Dispose()
    {
        foreach (var reader in _readers.Values)
            reader.Dispose();
    }
}
```

- [ ] **Step 2: Create appsettings.json**

Create `MagnumDocker/src/Magnum.Server/appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.AspNetCore.Hosting.Diagnostics": "Warning"
    }
  },
  "Magnum": {
    "Port": 17223,
    "Listen": "",
    "Devices": [],
    "Packets": 50,
    "Timeout": 0.005,
    "Trace": false,
    "CleanPackets": true,
    "Verbose": false
  }
}
```

- [ ] **Step 3: Create Program.cs**

Create `MagnumDocker/src/Magnum.Server/Program.cs`:

```csharp
using System.CommandLine;
using Magnum.Serial;
using Magnum.Server;

var builder = WebApplication.CreateBuilder(args);

// Bind config section
var magnumConfig = builder.Configuration.GetSection("Magnum");

// Parse CLI args to override config
int port = magnumConfig.GetValue("Port", 17223);
string listen = magnumConfig.GetValue("Listen", "") ?? "";
var devices = magnumConfig.GetSection("Devices").Get<string[]>() ?? [];
int packets = magnumConfig.GetValue("Packets", 50);
double timeout = magnumConfig.GetValue("Timeout", 0.005);
bool trace = magnumConfig.GetValue("Trace", false);
bool cleanPackets = magnumConfig.GetValue("CleanPackets", true);
bool verbose = magnumConfig.GetValue("Verbose", false);

// CLI overrides
var portOption = new Option<int?>("--port", "Server listening port");
var deviceOption = new Option<string[]>("--device", "Serial device paths") { AllowMultipleArgumentsPerToken = true };
var listenOption = new Option<string?>("--listen", "IP address to listen on");
var packetsOption = new Option<int?>("--packets", "Packet buffer size");
var timeoutOption = new Option<double?>("--timeout", "Serial read timeout in seconds");
var traceOption = new Option<bool>("--trace", "Include raw packet info");
var noCleanupOption = new Option<bool>("--nocleanup", "Suppress packet cleanup");
var verboseOption = new Option<bool>("--verbose", "Display options at runtime");

var rootCommand = new RootCommand("Magnum Network Reader");
rootCommand.AddOption(portOption);
rootCommand.AddOption(deviceOption);
rootCommand.AddOption(listenOption);
rootCommand.AddOption(packetsOption);
rootCommand.AddOption(timeoutOption);
rootCommand.AddOption(traceOption);
rootCommand.AddOption(noCleanupOption);
rootCommand.AddOption(verboseOption);

rootCommand.SetHandler((portVal, deviceVal, listenVal, packetsVal, timeoutVal, traceVal, noCleanupVal, verboseVal) =>
{
    if (portVal.HasValue) port = portVal.Value;
    if (deviceVal.Length > 0) devices = deviceVal;
    if (listenVal != null) listen = listenVal.ToUpperInvariant() == "ALL" ? "" : listenVal;
    if (packetsVal.HasValue) packets = packetsVal.Value;
    if (timeoutVal.HasValue) timeout = timeoutVal.Value;
    if (traceVal) trace = true;
    if (noCleanupVal) cleanPackets = false;
    if (verboseVal) verbose = true;
}, portOption, deviceOption, listenOption, packetsOption, timeoutOption, traceOption, noCleanupOption, verboseOption);

rootCommand.Invoke(args);

if (listen.Equals("ALL", StringComparison.OrdinalIgnoreCase))
    listen = "";

// Default device if none specified
if (devices.Length == 0)
    devices = ["/dev/ttyUSB0"];

if (verbose)
{
    Console.WriteLine($"Options: port={port}, listen='{listen}', devices=[{string.Join(", ", devices)}], " +
        $"packets={packets}, timeout={timeout}, trace={trace}, cleanpackets={cleanPackets}");
}

// Build services
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .WithMethods("GET", "OPTIONS")
              .WithHeaders("Content-Type");
    });
});

builder.Services.AddSingleton<MagnumService>();
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(port);
});

var app = builder.Build();
app.UseCors();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
var magnumService = app.Services.GetRequiredService<MagnumService>();

// Initialize readers
foreach (var device in devices)
{
    try
    {
        var options = new MagnumReaderOptions
        {
            Device = device,
            Packets = packets,
            Timeout = timeout,
            Trace = trace,
            CleanPackets = cleanPackets,
        };

        var reader = new MagnumReader(options);
        reader.GetDevices(); // test read
        magnumService.AddReader(reader);
        logger.LogInformation("Initialized reader for {Device}", device);
    }
    catch (Exception ex)
    {
        logger.LogError("{Device} {Error}", device, ex.Message);
    }
}

if (magnumService.ReaderCount == 0)
{
    logger.LogCritical("Error: There are no usable devices connected.");
    return;
}

// Routes
app.MapGet("/", () => Results.File("wwwroot/index.html", "text/html; charset=utf-8"));
app.MapGet("/index.html", () => Results.File("wwwroot/index.html", "text/html; charset=utf-8"));
app.MapGet("/api", (MagnumService svc) => svc.GetAllDeviceData());

Console.WriteLine($"Starting Magnum Reader on {(string.IsNullOrEmpty(listen) ? "0.0.0.0" : listen)}:{port}");
app.Run();
```

- [ ] **Step 4: Copy index.html**

```bash
cp /home/kevin/pymagnumdocker/index.html MagnumDocker/src/Magnum.Server/wwwroot/index.html
```

- [ ] **Step 5: Build and verify**

```bash
cd MagnumDocker && dotnet build
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add MagnumDocker/
git commit -m "feat: add Magnum.Server with Minimal API, configuration, and CLI support"
```

---

### Task 13: Docker and Deployment

**Files:**
- Create: `MagnumDocker/Dockerfile`
- Create: `MagnumDocker/compose.yml`

- [ ] **Step 1: Create Dockerfile**

Create `MagnumDocker/Dockerfile`:

```dockerfile
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGETARCH
WORKDIR /src

# Copy project files and restore
COPY src/Magnum.Serial/Magnum.Serial.csproj src/Magnum.Serial/
COPY src/Magnum.Server/Magnum.Server.csproj src/Magnum.Server/
RUN dotnet restore src/Magnum.Server/Magnum.Server.csproj -a $TARGETARCH

# Copy source and publish
COPY src/ src/
RUN dotnet publish src/Magnum.Server/Magnum.Server.csproj \
    -a $TARGETARCH \
    --no-restore \
    -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 17223
ENTRYPOINT ["dotnet", "Magnum.Server.dll"]
```

- [ ] **Step 2: Create compose.yml**

Create `MagnumDocker/compose.yml`:

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
      - ./src/Magnum.Server/appsettings.json:/app/appsettings.json:ro
```

- [ ] **Step 3: Verify Docker build (if Docker available)**

```bash
cd MagnumDocker && docker build -t magnum-dotnet . 2>&1 | tail -5
```

Expected: Successfully built / tagged.

- [ ] **Step 4: Commit**

```bash
git add MagnumDocker/
git commit -m "feat: add Dockerfile and compose.yml for multi-arch deployment"
```

---

### Task 14: Run Full Test Suite and Final Verification

- [ ] **Step 1: Run all tests**

```bash
cd MagnumDocker && dotnet test --verbosity normal
```

Expected: All tests pass (BigEndianReader: 5, PacketIdentifier: 12, InverterDevice: 8, BmkDevice: 5, AgsDevice: 7, Pt100Device: 7, RtrDevice: 1, RemoteDevice: 5 = ~50 tests total).

- [ ] **Step 2: Build release**

```bash
cd MagnumDocker && dotnet build -c Release
```

Expected: Build succeeded, 0 errors, 0 warnings.

- [ ] **Step 3: Final commit if any cleanup needed**

```bash
git status
# If clean, no commit needed
```
