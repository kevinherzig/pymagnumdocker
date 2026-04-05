using Magnum.Serial;
using Xunit;

namespace Magnum.Serial.Tests;

public class BigEndianReaderTests
{
    [Fact]
    public void ReadByte_ReturnsCorrectValue()
    {
        byte[] data = [0x40, 0xFF];
        Assert.Equal(0x40, BigEndianReader.ReadByte(data, 0));
        Assert.Equal(0xFF, BigEndianReader.ReadByte(data, 1));
    }

    [Fact]
    public void ReadSByte_ReturnsSignedValue()
    {
        byte[] data = [0x4C, 0xB4]; // 76, -76
        Assert.Equal(76, BigEndianReader.ReadSByte(data, 0));
        Assert.Equal(-76, BigEndianReader.ReadSByte(data, 1));
    }

    [Fact]
    public void ReadUInt16_BigEndian()
    {
        byte[] data = [0x09, 0xF1]; // 2545
        Assert.Equal((ushort)2545, BigEndianReader.ReadUInt16(data, 0));
    }

    [Fact]
    public void ReadInt16_BigEndian_Positive()
    {
        byte[] data = [0x00, 0xF6]; // 246
        Assert.Equal((short)246, BigEndianReader.ReadInt16(data, 0));
    }

    [Fact]
    public void ReadInt16_BigEndian_Negative()
    {
        byte[] data = [0xFF, 0x98]; // -104
        Assert.Equal((short)-104, BigEndianReader.ReadInt16(data, 0));
    }
}
