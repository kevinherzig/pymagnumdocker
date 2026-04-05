using Magnum.Serial.Lookups;
using static Magnum.Serial.BigEndianReader;

namespace Magnum.Serial.Devices;

public class Pt100Device : IDeviceParser
{
    public string DeviceType => "PT100";

    private readonly Dictionary<string, object> _data = new();

    public void Parse(byte[] packet, PacketType packetType, int voltageMultiplier)
    {
        if (packetType == PacketType.PtC1)
            ParseC1(packet, voltageMultiplier);
        else if (packetType == PacketType.PtC2)
            ParseC2(packet);
    }

    private void ParseC1(byte[] packet, int voltageMultiplier)
    {
        // Format: >BBBBHhHBBBBBB (16 bytes)
        int address = ReadByte(packet, 1) & 0x07;
        int modeByte = ReadByte(packet, 2);
        int mode = modeByte & 0x0F;
        int regulation = modeByte >> 4;
        int faultByte = ReadByte(packet, 3);
        int fault = faultByte >> 3;

        double battery = ReadUInt16(packet, 4) / 10.0;
        double batteryAmps = ReadInt16(packet, 6) / 10.0;
        double pvVoltage = ReadUInt16(packet, 8) / 10.0;
        double chargeTime = ReadByte(packet, 10) / 10.0;
        double targetBatteryVoltage = ReadByte(packet, 11) / 10.0 * voltageMultiplier;

        int flagsByte = ReadByte(packet, 12);
        int relay = flagsByte & 0x01;
        int alarm = (flagsByte >> 1) & 0x01;
        int fan = (flagsByte >> 2) & 0x01;
        int day = (flagsByte >> 4) & 0x01;

        int batteryTempRaw = ReadByte(packet, 13);
        double batteryTemp;
        if (batteryTempRaw == 0x97 || batteryTempRaw == 0x98)
            batteryTemp = double.NaN; // Sensor Fault
        else
            batteryTemp = (double)batteryTempRaw;

        double inductorTemp = ReadByte(packet, 14);
        double fetTemp = ReadByte(packet, 15);

        _data["address"] = address;
        _data["mode"] = Pt100Lookups.Modes.GetValueOrDefault(mode, "Unknown");
        _data["regulation"] = Pt100Lookups.Regulations.GetValueOrDefault(regulation, "Unknown");
        _data["fault"] = Pt100Lookups.Faults.GetValueOrDefault(fault, "Unknown");
        _data["battery"] = battery;
        _data["battery_amps"] = batteryAmps;
        _data["pv_voltage"] = pvVoltage;
        _data["charge_time"] = chargeTime;
        _data["target_battery_voltage"] = targetBatteryVoltage;
        _data["relay_state"] = relay;
        _data["alarm_state"] = alarm;
        _data["fan_on"] = fan;
        _data["day"] = day;
        _data["battery_temp"] = batteryTemp;
        _data["inductor_temp"] = inductorTemp;
        _data["fet_temp"] = fetTemp;
    }

    private void ParseC2(byte[] packet)
    {
        // Format: >BBHHBBBBBBB (13 bytes)
        int address = ReadByte(packet, 1);
        int lifetime = ReadUInt16(packet, 2) * 10;
        double resettable = ReadUInt16(packet, 4) / 10.0;
        int groundFault = ReadByte(packet, 6);
        int stacker = ReadByte(packet, 7);
        int dip = ReadByte(packet, 8);
        double revision = ReadByte(packet, 9) / 10.0;
        int outputCurrent = ReadByte(packet, 10);
        int inputVoltage = ReadByte(packet, 11) * 10;

        _data["lifetime_kwhrs"] = lifetime;
        _data["resettable_kwhrs"] = resettable;
        _data["ground_fault"] = groundFault;
        _data["stacker"] = stacker;
        _data["dip"] = Convert.ToString(dip, 2).PadLeft(8, '0');
        _data["revision"] = revision;
        _data["output_current"] = outputCurrent;
        _data["input_voltage"] = inputVoltage;
    }

    public Dictionary<string, object> GetData() => new(_data);
}
