namespace Magnum.Serial;

public class MagnumReaderOptions
{
    public string Device { get; set; } = "/dev/ttyUSB0";
    public int Packets { get; set; } = 50;
    public double Timeout { get; set; } = 0.005;
    public bool Trace { get; set; }
    public bool CleanPackets { get; set; } = true;
}
