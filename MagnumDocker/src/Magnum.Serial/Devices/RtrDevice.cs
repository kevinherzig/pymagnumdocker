using static Magnum.Serial.BigEndianReader;

namespace Magnum.Serial.Devices;

public class RtrDevice : IDeviceParser
{
    public string DeviceType => "RTR";

    private readonly Dictionary<string, object> _data = new();

    public void Parse(byte[] packet, PacketType packetType, int voltageMultiplier)
    {
        // Format: >BB (2 bytes)
        // [0] marker, [1] revision raw
        int revision = (int)(ReadByte(packet, 1) / 10.0);
        _data["revision"] = revision;
    }

    public Dictionary<string, object> GetData() => new(_data);
}
