using Magnum.Serial;
using Magnum.Serial.Devices;
using Xunit;

namespace Magnum.Serial.Tests;

public class AgsDeviceTests
{
    [Fact]
    public void Parse_AgsA1_ExtractsStatus()
    {
        var device = new AgsDevice();
        device.Parse(TestPackets.AgsA1, PacketType.AgsA1, 2);
        var data = device.GetData();
        Assert.Equal("Ready", data["status"]);
    }

    [Fact]
    public void Parse_AgsA1_ExtractsRevision()
    {
        var device = new AgsDevice();
        device.Parse(TestPackets.AgsA1, PacketType.AgsA1, 2);
        var data = device.GetData();
        Assert.Equal(5.2, data["revision"]);
    }

    [Fact]
    public void Parse_AgsA1_ConvertsTemperatureToC()
    {
        var device = new AgsDevice();
        device.Parse(TestPackets.AgsA1, PacketType.AgsA1, 2);
        var data = device.GetData();
        Assert.Equal(14.4, (double)data["temp"], 1);
    }

    [Fact]
    public void Parse_AgsA1_ExtractsVdcWithMultiplier()
    {
        var device = new AgsDevice();
        device.Parse(TestPackets.AgsA1, PacketType.AgsA1, 2);
        var data = device.GetData();
        Assert.Equal(25.4, data["vdc"]);
    }

    [Fact]
    public void Parse_AgsA1_NotRunning()
    {
        var device = new AgsDevice();
        device.Parse(TestPackets.AgsA1, PacketType.AgsA1, 2);
        var data = device.GetData();
        Assert.Equal(false, data["running"]);
    }

    [Fact]
    public void Parse_AgsA2_ExtractsUsageData()
    {
        var device = new AgsDevice();
        device.Parse(TestPackets.AgsA2, PacketType.AgsA2, 2);
        var data = device.GetData();
        Assert.Equal(0, data["gen_last_run"]);
        Assert.Equal(0, data["last_full_soc"]);
        Assert.Equal(0, data["gen_total_run"]);
    }

    [Fact]
    public void Parse_AccumulatesState_AcrossPacketTypes()
    {
        var device = new AgsDevice();
        device.Parse(TestPackets.AgsA1, PacketType.AgsA1, 2);
        device.Parse(TestPackets.AgsA2, PacketType.AgsA2, 2);
        var data = device.GetData();
        Assert.True(data.ContainsKey("status"));
        Assert.True(data.ContainsKey("gen_last_run"));
    }
}
