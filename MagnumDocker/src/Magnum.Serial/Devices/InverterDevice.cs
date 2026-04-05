using Magnum.Serial.Lookups;
using static Magnum.Serial.BigEndianReader;

namespace Magnum.Serial.Devices;

public class InverterDevice : IDeviceParser
{
    public string DeviceType => "INVERTER";
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
        int invLedRaw = ReadByte(packet, 8);
        int chgLedRaw = ReadByte(packet, 9);
        int invled = invLedRaw != 0 ? 1 : 0;
        int chgled = chgLedRaw != 0 ? 1 : 0;
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
        _data["invled_text"] = invled == 0 ? "Off" : "On";
        _data["chgled"] = chgled;
        _data["chgled_text"] = chgled == 0 ? "Off" : "On";
        _data["revision"] = revision;
        _data["bat"] = battemp;
        _data["tfmr"] = tfmr;
        _data["FET"] = fet;
        _data["model"] = model;
        _data["model_text"] = InverterLookups.Models.GetValueOrDefault(model, model.ToString());

        // INV-only fields (21-byte packet)
        if (packetType == PacketType.Inv)
        {
            int stackmode = ReadByte(packet, 15);
            _data["stackmode"] = stackmode;
            _data["stackmode_text"] = InverterLookups.StackModes.GetValueOrDefault(stackmode, stackmode.ToString());
            _data["AACin"] = (double)ReadByte(packet, 16);
            _data["AACout"] = (double)ReadByte(packet, 17);
            _data["Hz"] = ReadInt16(packet, 18) / 10.0;
        }
    }

    public Dictionary<string, object> GetData() => new(_data);
}
