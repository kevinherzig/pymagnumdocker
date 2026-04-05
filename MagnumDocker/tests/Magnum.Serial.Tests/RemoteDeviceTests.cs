using Magnum.Serial;
using Magnum.Serial.Devices;
using Xunit;

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
        Assert.True(data.ContainsKey("remotetimehours"));
        Assert.True(data.ContainsKey("begintime"));
    }
}
