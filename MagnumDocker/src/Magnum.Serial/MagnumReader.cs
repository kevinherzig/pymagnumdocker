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
                    if (buffer.Count > 0)
                    {
                        packets.Add(buffer.ToArray());
                        buffer.Clear();
                    }
                }
            }
            catch (TimeoutException)
            {
                if (buffer.Count > 0)
                {
                    packets.Add(buffer.ToArray());
                    buffer.Clear();
                }
            }
        }

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
        var merged = unknowns.SelectMany(p => p).ToArray();
        var mergedType = PacketIdentifier.Identify(merged);
        if (mergedType != PacketType.Unknown)
            output.Add(merged);
    }

    private IDeviceParser? GetOrCreateParser(PacketType packetType)
    {
        string? key = packetType switch
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
            _ => null,
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
