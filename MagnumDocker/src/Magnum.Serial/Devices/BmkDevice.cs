using static Magnum.Serial.BigEndianReader;

namespace Magnum.Serial.Devices;

public class BmkDevice : IDeviceParser
{
    public string DeviceType => "BMK";

    private static readonly Dictionary<int, string> FaultStatuses = new()
    {
        [0] = "Reserved",
        [1] = "Normal",
        [2] = "Fault Start",
    };

    private readonly Dictionary<string, object> _data = new();

    public void Parse(byte[] packet, PacketType packetType, int voltageMultiplier)
    {
        // Format: >BbHhHHhHHBB (18 bytes)
        // [0] marker, [1] soc, [2-3] vdc, [4-5] adc, [6-7] vmin, [8-9] vmax,
        // [10-11] amph, [12-13] amphtrip, [14-15] amphout, [16] revision, [17] fault
        int soc = ReadSByte(packet, 1);
        double vdc = ReadUInt16(packet, 2) / 100.0;
        double adc = ReadInt16(packet, 4) / 10.0;
        double vmin = ReadUInt16(packet, 6) / 100.0;
        double vmax = ReadUInt16(packet, 8) / 100.0;
        int amph = ReadInt16(packet, 10);
        double amphtrip = ReadUInt16(packet, 12) / 10.0;
        double amphout = ReadUInt16(packet, 14) * 100.0;
        double revision = ReadByte(packet, 16) / 10.0;
        int fault = ReadByte(packet, 17);

        _data["soc"] = soc;
        _data["vdc"] = vdc;
        _data["adc"] = adc;
        _data["vmin"] = vmin;
        _data["vmax"] = vmax;
        _data["amph"] = amph;
        _data["amphtrip"] = amphtrip;
        _data["amphout"] = amphout;
        _data["revision"] = revision;
        _data["Fault"] = FaultStatuses.GetValueOrDefault(fault, "Unknown");
    }

    public Dictionary<string, object> GetData() => new(_data);
}
