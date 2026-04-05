using Magnum.Serial;
using Xunit;

namespace Magnum.Serial.Tests;

public class PacketIdentifierTests
{
    [Fact]
    public void Identifies_Rtr91() =>
        Assert.Equal(PacketType.Rtr91, PacketIdentifier.Identify(TestPackets.Rtr91));

    [Fact]
    public void Identifies_AgsA1() =>
        Assert.Equal(PacketType.AgsA1, PacketIdentifier.Identify(TestPackets.AgsA1));

    [Fact]
    public void Identifies_AgsA2() =>
        Assert.Equal(PacketType.AgsA2, PacketIdentifier.Identify(TestPackets.AgsA2));

    [Fact]
    public void Identifies_Bmk81() =>
        Assert.Equal(PacketType.Bmk81, PacketIdentifier.Identify(TestPackets.Bmk81));

    [Fact]
    public void Identifies_Inverter() =>
        Assert.Equal(PacketType.Inv, PacketIdentifier.Identify(TestPackets.Inverter));

    [Fact]
    public void Identifies_PtC1() =>
        Assert.Equal(PacketType.PtC1, PacketIdentifier.Identify(TestPackets.PtC1));

    [Fact]
    public void Identifies_PtC2() =>
        Assert.Equal(PacketType.PtC2, PacketIdentifier.Identify(TestPackets.PtC2));

    [Fact]
    public void Identifies_Remote00() =>
        Assert.Equal(PacketType.Remote00, PacketIdentifier.Identify(TestPackets.Remote00));

    [Fact]
    public void Identifies_Remote80() =>
        Assert.Equal(PacketType.Remote80, PacketIdentifier.Identify(TestPackets.Remote80));

    [Fact]
    public void Identifies_RemoteA0() =>
        Assert.Equal(PacketType.RemoteA0, PacketIdentifier.Identify(TestPackets.RemoteA0));

    [Fact]
    public void Identifies_Unknown_EmptyPacket() =>
        Assert.Equal(PacketType.Unknown, PacketIdentifier.Identify(Array.Empty<byte>()));

    [Fact]
    public void Identifies_Unknown_UnrecognizedLength() =>
        Assert.Equal(PacketType.Unknown, PacketIdentifier.Identify(new byte[] { 0x00, 0x00, 0x00 }));
}
