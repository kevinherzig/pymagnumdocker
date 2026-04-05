namespace Magnum.Serial.Devices;

public class AcldDevice : IDeviceParser
{
    public string DeviceType => "ACLD";

    private readonly Dictionary<string, object> _data = new();

    public void Parse(byte[] packet, PacketType packetType, int voltageMultiplier)
    {
        _data["raw"] = Convert.ToHexString(packet);
    }

    public Dictionary<string, object> GetData() => new(_data);
}
