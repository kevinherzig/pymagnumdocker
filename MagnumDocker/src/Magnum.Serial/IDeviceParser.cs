namespace Magnum.Serial;

public interface IDeviceParser
{
    string DeviceType { get; }
    void Parse(byte[] packet, PacketType packetType, int voltageMultiplier);
    Dictionary<string, object> GetData();
}
