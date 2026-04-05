namespace Magnum.Serial;

public enum PacketType
{
    Unknown,
    Inv,        // 21-byte modern inverter
    InvC,       // 16-byte classic inverter
    RemoteC,    // 16-byte classic remote
    Remote00,   // 21-byte base remote
    Remote11,
    Remote80,   // 21-byte remote with BMK
    RemoteA0,   // 21-byte remote with AGS
    RemoteA1,
    RemoteA2,
    RemoteA3,
    RemoteA4,
    RemoteC0,
    RemoteC1,
    RemoteC2,
    RemoteC3,
    RemoteD0,
    Bmk81,      // 18-byte BMK
    AgsA1,      // 6-byte AGS status
    AgsA2,      // 6-byte AGS usage
    PtC1,       // 16-byte PT100 main
    PtC2,       // 13-byte PT100 lifetime
    PtC3,       // 14-byte PT100 daily
    Rtr91,      // 2-byte RTR
    AcldD1,     // 8-byte ACLD
}
