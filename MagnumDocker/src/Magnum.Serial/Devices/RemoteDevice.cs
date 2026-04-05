using static Magnum.Serial.BigEndianReader;

namespace Magnum.Serial.Devices;

public class RemoteDevice : IDeviceParser
{
    public string DeviceType => "Remote";

    private readonly Dictionary<string, object> _data = new();

    public void Parse(byte[] packet, PacketType packetType, int voltageMultiplier)
    {
        int mult = voltageMultiplier;

        // Base fields (bytes 1-13)
        int searchwatts = ReadByte(packet, 1);
        int batterysize = ReadByte(packet, 2) * 10;
        int absorbRaw = ReadByte(packet, 3);
        int chargeramps = ReadByte(packet, 4);
        int ainput = ReadSByte(packet, 5);
        double revision = ReadByte(packet, 6) / 10.0;
        int parallel = (ReadByte(packet, 7) & 0x0F) * 10;
        // [8] skip
        double lbco = ReadByte(packet, 9) / 10.0;
        double vaccutout = (double)ReadByte(packet, 10);
        double vsfloat = ReadByte(packet, 11) * mult / 10.0;
        double absorbVoltage = absorbRaw > 100 ? absorbRaw * mult / 10.0 : 0;
        double vEQ = absorbRaw > 100
            ? absorbVoltage + ReadByte(packet, 12) / 10.0
            : ReadByte(packet, 12) / 10.0;
        double absorbtime = ReadByte(packet, 13) / 10.0;

        _data["searchwatts"] = searchwatts;
        _data["batterysize"] = batterysize;
        if (absorbRaw > 100)
            _data["absorb"] = absorbVoltage;
        else
            _data["absorb"] = absorbRaw;
        _data["chargeramps"] = chargeramps;
        _data["ainput"] = ainput;
        _data["revision"] = revision;
        _data["parallel"] = parallel;
        _data["lbco"] = lbco;
        _data["vaccutout"] = vaccutout;
        _data["vsfloat"] = vsfloat;
        _data["vEQ"] = vEQ;
        _data["absorbtime"] = absorbtime;

        // Subtype-specific fields
        switch (packetType)
        {
            case PacketType.RemoteA0:
                ParseA0(packet, mult);
                break;
            case PacketType.RemoteA1:
                ParseA1(packet, mult);
                break;
            case PacketType.RemoteA2:
                ParseA2(packet);
                break;
            case PacketType.RemoteA3:
                ParseA3(packet);
                break;
            case PacketType.RemoteA4:
                ParseA4(packet);
                break;
            case PacketType.Remote80:
                ParseRemote80(packet);
                break;
            // Remote00, RemoteC, Remote11, RemoteC0-C3, RemoteD0: base fields only
        }
    }

    private void ParseA0(byte[] packet, int mult)
    {
        _data["remotetimehours"] = (int)ReadByte(packet, 14);
        _data["remotetimemins"] = (int)ReadByte(packet, 15);
        _data["runtime"] = ReadByte(packet, 16) / 10.0;
        int startTempF = ReadSByte(packet, 17);
        _data["starttemp"] = Math.Round((startTempF - 32) * 5.0 / 9.0, 1);
        _data["startvdc"] = ReadByte(packet, 18) * mult / 10.0;
        _data["quiettime"] = (int)ReadByte(packet, 19);
    }

    private void ParseA1(byte[] packet, int mult)
    {
        _data["begintime"] = MinutesToHHMM(ReadByte(packet, 14) * 15);
        _data["stoptime"] = MinutesToHHMM(ReadByte(packet, 15) * 15);
        _data["vdcstop"] = ReadByte(packet, 16) * mult / 10.0;
        _data["voltstartdelay"] = DecodeDelay(ReadByte(packet, 17));
        _data["voltstopdelay"] = DecodeDelay(ReadByte(packet, 18));
        _data["maxrun"] = ReadByte(packet, 19) / 10.0;
    }

    private void ParseA2(byte[] packet)
    {
        _data["socstart"] = (int)ReadSByte(packet, 14);
        _data["socstop"] = (int)ReadSByte(packet, 15);
        _data["ampstart"] = (int)ReadByte(packet, 16);
        _data["ampsstartdelay"] = DecodeDelay(ReadByte(packet, 17));
        _data["ampstop"] = (int)ReadByte(packet, 18);
        _data["ampsstopdelay"] = DecodeDelay(ReadByte(packet, 19));
    }

    private void ParseA3(byte[] packet)
    {
        _data["quietbegintime"] = MinutesToHHMM(ReadByte(packet, 14) * 15);
        _data["quietendtime"] = MinutesToHHMM(ReadByte(packet, 15) * 15);
        _data["exercisestart"] = MinutesToHHMM(ReadByte(packet, 16) * 15);
        _data["runtime"] = ReadByte(packet, 17) / 10.0;
        _data["topoff"] = (int)ReadByte(packet, 18);
    }

    private void ParseA4(byte[] packet)
    {
        _data["warmup"] = DecodeDelay(ReadByte(packet, 14));
        _data["cool"] = DecodeDelay(ReadByte(packet, 15));
    }

    private void ParseRemote80(byte[] packet)
    {
        _data["remotetimehours"] = (int)ReadSByte(packet, 14);
        _data["remotetimemins"] = (int)ReadSByte(packet, 15);
        _data["batteryefficiency"] = (int)ReadSByte(packet, 16);
    }

    private static int MinutesToHHMM(int totalMinutes) =>
        (totalMinutes / 60) * 100 + (totalMinutes % 60);

    private static int DecodeDelay(int val) =>
        val > 127 ? (val & 0x0F) * 60 : val;

    public Dictionary<string, object> GetData() => new(_data);
}
