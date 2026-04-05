using Magnum.Serial.Lookups;
using static Magnum.Serial.BigEndianReader;

namespace Magnum.Serial.Devices;

public class InverterDevice : IDeviceParser
{
    public string DeviceType => "Inverter";
    public int VoltageMultiplier { get; private set; }

    private readonly Dictionary<string, object> _data = new();

    public void Parse(byte[] packet, PacketType packetType, int voltageMultiplier)
    {
        // Common fields for both Inv (21-byte) and InvC (16-byte)
        int mode = ReadByte(packet, 0);
        int fault = ReadByte(packet, 1);
        double vdc = ReadInt16(packet, 2) / 10.0;
        double adc = ReadInt16(packet, 4);
        double vacOut = ReadByte(packet, 6);
        double vacIn = ReadByte(packet, 7);
        int invled = ReadByte(packet, 8);
        int chgled = ReadByte(packet, 9);
        double revision = ReadByte(packet, 10) / 10.0;
        double battemp = ReadByte(packet, 11);
        double tfmr = ReadByte(packet, 12);
        double fet = ReadByte(packet, 13);
        int model = ReadByte(packet, 14);

        VoltageMultiplier = InverterLookups.GetVoltageMultiplier(model);

        _data["mode"] = mode;
        _data["mode_text"] = InverterLookups.Modes.GetValueOrDefault(mode, "Unknown");
        _data["fault"] = fault;
        _data["fault_text"] = InverterLookups.Faults.GetValueOrDefault(fault, "Unknown");
        _data["vdc"] = vdc;
        _data["adc"] = adc;
        _data["VACout"] = vacOut;
        _data["VACin"] = vacIn;
        _data["invled"] = invled;
        _data["chgled"] = chgled;
        _data["revision"] = revision;
        _data["battemp"] = battemp;
        _data["tfmr"] = tfmr;
        _data["FET"] = fet;
        _data["model"] = InverterLookups.Models.GetValueOrDefault(model, "Unknown");

        // INV-only fields (21-byte packet)
        if (packetType == PacketType.Inv)
        {
            int stackmode = ReadByte(packet, 15);
            _data["stackmode"] = InverterLookups.StackModes.GetValueOrDefault(stackmode, "Unknown");
            _data["AACin"] = (double)ReadByte(packet, 16);
            _data["AACout"] = (double)ReadByte(packet, 17);
            _data["Hz"] = ReadInt16(packet, 18) / 10.0;
        }
    }

    public Dictionary<string, object> GetData() => new(_data);
}
