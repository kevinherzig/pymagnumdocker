using System.Buffers.Binary;

namespace Magnum.Serial;

public static class BigEndianReader
{
    public static byte ReadByte(byte[] data, int offset) => data[offset];

    public static sbyte ReadSByte(byte[] data, int offset) => (sbyte)data[offset];

    public static ushort ReadUInt16(byte[] data, int offset) =>
        BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset, 2));

    public static short ReadInt16(byte[] data, int offset) =>
        BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(offset, 2));
}
