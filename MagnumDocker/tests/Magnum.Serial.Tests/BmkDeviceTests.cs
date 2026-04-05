using Magnum.Serial;
using Magnum.Serial.Devices;
using Xunit;

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
