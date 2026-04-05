using Magnum.Serial.Lookups;
using static Magnum.Serial.BigEndianReader;

namespace Magnum.Serial.Devices;

public class AgsDevice : IDeviceParser
{
    public string DeviceType => "AGS";

    private readonly Dictionary<string, object> _data = new();

    public void Parse(byte[] packet, PacketType packetType, int voltageMultiplier)
    {
        if (packetType == PacketType.AgsA1)
            ParseA1(packet, voltageMultiplier);
        else if (packetType == PacketType.AgsA2)
            ParseA2(packet);
    }

    private void ParseA1(byte[] packet, int voltageMultiplier)
    {
        // Format: >BbBbBB (6 bytes)
        int statusCode = ReadSByte(packet, 1);
        double revision = ReadByte(packet, 2) / 10.0;
        int tempF = ReadSByte(packet, 3);
        double runtime = ReadByte(packet, 4);
        double vdc = ReadByte(packet, 5) / 10.0 * voltageMultiplier;

        // Temperature conversion: F to C, but only if < 105F (otherwise sensor fault)
        double temp = (double)tempF;
        if (temp < 105.0)
            temp = Math.Round((temp - 32) * 5.0 / 9.0, 1);

        _data["status"] = statusCode;
        _data["status_text"] = AgsLookups.Statuses.GetValueOrDefault(statusCode, statusCode.ToString());
        _data["revision"] = revision;
        _data["temp"] = temp;
        _data["runtime"] = runtime;
        _data["vdc"] = vdc;
        _data["running"] = AgsLookups.RunningStatuses.Contains(statusCode);
    }

    private void ParseA2(byte[] packet)
    {
        // Format: >BBBHB (6 bytes)
        // [0] marker, [1] gen_last_run, [2] last_full_soc, [3-4] gen_total_run, [5] unused
        int genLastRun = ReadByte(packet, 1);
        int lastFullSoc = ReadByte(packet, 2);
        int genTotalRun = ReadUInt16(packet, 3);

        _data["gen_last_run"] = genLastRun;
        _data["last_full_soc"] = lastFullSoc;
        _data["gen_total_run"] = genTotalRun;
    }

    public Dictionary<string, object> GetData() => new(_data);
}
