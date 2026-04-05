using Magnum.Serial;
using Magnum.Serial.Devices;
using Xunit;

namespace Magnum.Serial.Tests;

public class Pt100DeviceTests
{
    [Fact]
    public void Parse_PtC1_ExtractsModeAndRegulation()
    {
        var device = new Pt100Device();
        device.Parse(TestPackets.PtC1, PacketType.PtC1, 2);
        var data = device.GetData();
        Assert.Equal("Absorb", data["mode"]);
        Assert.Equal("Voltage", data["regulation"]);
    }

    [Fact]
    public void Parse_PtC1_ExtractsFault()
    {
        var device = new Pt100Device();
        device.Parse(TestPackets.PtC1, PacketType.PtC1, 2);
        var data = device.GetData();
        Assert.Equal("No Fault", data["fault"]);
    }

    [Fact]
    public void Parse_PtC1_ExtractsVoltagesAndAmps()
    {
        var device = new Pt100Device();
        device.Parse(TestPackets.PtC1, PacketType.PtC1, 2);
        var data = device.GetData();
        Assert.Equal(58.9, data["battery"]);
        Assert.Equal(13.3, data["battery_amps"]);
        Assert.Equal(146.8, data["pv_voltage"]);
    }

    [Fact]
    public void Parse_PtC1_ExtractsFlags()
    {
        var device = new Pt100Device();
        device.Parse(TestPackets.PtC1, PacketType.PtC1, 2);
        var data = device.GetData();
        Assert.Equal(0, data["relay_state"]);
        Assert.Equal(0, data["alarm_state"]);
        Assert.Equal(0, data["fan_on"]);
        Assert.Equal(1, data["day"]);
    }

    [Fact]
    public void Parse_PtC1_ExtractsTemperatures()
    {
        var device = new Pt100Device();
        device.Parse(TestPackets.PtC1, PacketType.PtC1, 2);
        var data = device.GetData();
        Assert.Equal(27.0, data["battery_temp"]);
        Assert.Equal(49.0, data["inductor_temp"]);
        Assert.Equal(62.0, data["fet_temp"]);
    }

    [Fact]
    public void Parse_PtC2_ExtractsLifetimeData()
    {
        var device = new Pt100Device();
        device.Parse(TestPackets.PtC2, PacketType.PtC2, 2);
        var data = device.GetData();
        Assert.Equal(20, data["lifetime_kwhrs"]);
        Assert.Equal(17.2, data["resettable_kwhrs"]);
    }

    [Fact]
    public void Parse_AccumulatesState_AcrossPtC1AndPtC2()
    {
        var device = new Pt100Device();
        device.Parse(TestPackets.PtC1, PacketType.PtC1, 2);
        device.Parse(TestPackets.PtC2, PacketType.PtC2, 2);
        var data = device.GetData();
        Assert.True(data.ContainsKey("battery"));
        Assert.True(data.ContainsKey("lifetime_kwhrs"));
    }
}
