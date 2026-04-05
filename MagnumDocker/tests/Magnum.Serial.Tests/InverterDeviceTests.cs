using Magnum.Serial;
using Magnum.Serial.Devices;
using Xunit;

namespace Magnum.Serial.Tests;

public class InverterDeviceTests
{
    [Fact]
    public void Parse_InvPacket_ExtractsMode()
    {
        var device = new InverterDevice();
        device.Parse(TestPackets.Inverter, PacketType.Inv, 0);
        var data = device.GetData();
        Assert.Equal(0x40, data["mode"]);
        Assert.Equal("INVERT", data["mode_text"]);
    }

    [Fact]
    public void Parse_InvPacket_ExtractsFault()
    {
        var device = new InverterDevice();
        device.Parse(TestPackets.Inverter, PacketType.Inv, 0);
        var data = device.GetData();
        Assert.Equal(0, data["fault"]);
        Assert.Equal("None", data["fault_text"]);
    }

    [Fact]
    public void Parse_InvPacket_ExtractsVoltagesAndCurrents()
    {
        var device = new InverterDevice();
        device.Parse(TestPackets.Inverter, PacketType.Inv, 0);
        var data = device.GetData();
        Assert.Equal(24.6, data["vdc"]);
        Assert.Equal(22.0, data["adc"]);
        Assert.Equal(119.0, data["VACout"]);
        Assert.Equal(0.0, data["VACin"]);
    }

    [Fact]
    public void Parse_InvPacket_ExtractsModel()
    {
        var device = new InverterDevice();
        device.Parse(TestPackets.Inverter, PacketType.Inv, 0);
        var data = device.GetData();
        Assert.Equal(107, data["model"]);
        Assert.Equal("MS4024PAE", data["model_text"]);
    }

    [Fact]
    public void Parse_InvPacket_ExtractsStackMode()
    {
        var device = new InverterDevice();
        device.Parse(TestPackets.Inverter, PacketType.Inv, 0);
        var data = device.GetData();
        Assert.Equal(1, data["stackmode"]);
        Assert.Equal("Parallel stack - master", data["stackmode_text"]);
    }

    [Fact]
    public void Parse_InvPacket_ExtractsFrequency()
    {
        var device = new InverterDevice();
        device.Parse(TestPackets.Inverter, PacketType.Inv, 0);
        var data = device.GetData();
        Assert.Equal(60.0, data["Hz"]);
    }

    [Fact]
    public void Parse_InvPacket_ExtractsRevision()
    {
        var device = new InverterDevice();
        device.Parse(TestPackets.Inverter, PacketType.Inv, 0);
        var data = device.GetData();
        Assert.Equal(6.1, data["revision"]);
    }

    [Fact]
    public void Parse_InvPacket_ReturnsVoltageMultiplier()
    {
        var device = new InverterDevice();
        device.Parse(TestPackets.Inverter, PacketType.Inv, 0);
        Assert.Equal(2, device.VoltageMultiplier);
    }

    [Fact]
    public void Parse_InvPacket_ExtractsTemperatures()
    {
        var device = new InverterDevice();
        device.Parse(TestPackets.Inverter, PacketType.Inv, 0);
        var data = device.GetData();
        Assert.Equal(17.0, data["bat"]);
        Assert.Equal(51.0, data["tfmr"]);
        Assert.Equal(36.0, data["FET"]);
    }
}
