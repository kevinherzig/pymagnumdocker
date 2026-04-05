using Magnum.Serial.Lookups;

namespace Magnum.Serial;

public static class PacketIdentifier
{
    public static PacketType Identify(byte[] packet)
    {
        if (packet.Length == 0)
            return PacketType.Unknown;

        byte first = packet[0];
        int length = packet.Length;

        switch (length)
        {
            case 2:
                return first == 0x91 ? PacketType.Rtr91 : PacketType.Unknown;

            case 6:
                return first switch
                {
                    0xA1 => PacketType.AgsA1,
                    0xA2 => PacketType.AgsA2,
                    _ => PacketType.Unknown,
                };

            case 8:
                return first == 0xD1 ? PacketType.AcldD1 : PacketType.Unknown;

            case 13:
                return first == 0xC2 ? PacketType.PtC2 : PacketType.Unknown;

            case 14:
                return first == 0xC3 ? PacketType.PtC3 : PacketType.Unknown;

            case 16:
                if (first == 0xC1)
                    return PacketType.PtC1;
                return Identify16BytePacket(packet);

            case 18:
                return first == 0x81 ? PacketType.Bmk81 : PacketType.Unknown;

            case 21:
                return Identify21BytePacket(packet);

            default:
                return PacketType.Unknown;
        }
    }

    private static PacketType Identify16BytePacket(byte[] packet)
    {
        byte first = packet[0];

        if (packet[10] <= 0x27 && InverterLookups.Models.ContainsKey(packet[14]))
            return PacketType.InvC;

        if (first == 0x00)
            return PacketType.RemoteC;

        return PacketType.Unknown;
    }

    private static PacketType Identify21BytePacket(byte[] packet)
    {
        byte last = packet[20];

        switch (last)
        {
            case 0x80: return PacketType.Remote80;
            case 0xA0: return PacketType.RemoteA0;
            case 0xA1: return PacketType.RemoteA1;
            case 0xA2: return PacketType.RemoteA2;
            case 0xA3: return PacketType.RemoteA3;
            case 0xA4: return PacketType.RemoteA4;
            case 0xC0: return PacketType.RemoteC0;
            case 0xC1: return PacketType.RemoteC1;
            case 0xC2: return PacketType.RemoteC2;
            case 0xC3: return PacketType.RemoteC3;
            case 0xD0: return PacketType.RemoteD0;
            case 0x11: return PacketType.Remote11;
        }

        if (last == 0x00)
        {
            if (InverterLookups.Models.ContainsKey(packet[14]))
                return PacketType.Inv;

            return PacketType.Remote00;
        }

        return PacketType.Unknown;
    }
}
