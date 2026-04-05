namespace Magnum.Serial;

public class DeviceData
{
    public required string Device { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
}
