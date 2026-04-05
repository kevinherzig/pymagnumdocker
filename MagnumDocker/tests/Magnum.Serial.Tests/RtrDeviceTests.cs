using Magnum.Serial;
using Magnum.Serial.Devices;
using Xunit;

namespace Magnum.Serial.Tests;

public class RtrDeviceTests
{
    [Fact]
    public void Parse_Rtr91_ExtractsRevision()
    {
        var device = new RtrDevice();
        device.Parse(TestPackets.Rtr91, PacketType.Rtr91, 1);
        var data = device.GetData();
        Assert.Equal(3, data["revision"]);
    }
}
