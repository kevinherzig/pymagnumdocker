namespace Magnum.Serial.Lookups;

public static class InverterLookups
{
    public static readonly Dictionary<int, string> Models = new()
    {
        [0x06] = "MM612",       [0x07] = "MM612-AE",    [0x08] = "MM1212",
        [0x09] = "MMS1012",     [0x0A] = "MM1012E",     [0x0B] = "MM1512",
        [0x0C] = "MMS912E",     [0x0F] = "ME1512",      [0x14] = "ME2012",
        [0x15] = "RD2212",      [0x19] = "ME2512",      [0x1E] = "ME3112",
        [0x23] = "MS2012",      [0x24] = "MS1512E",     [0x28] = "MS2012E",
        [0x2C] = "MSH3012M",    [0x2D] = "MS2812",      [0x2F] = "MS2712E",
        [0x35] = "MM1324E",     [0x36] = "MM1524",      [0x37] = "RD1824",
        [0x3B] = "RD2624E",     [0x3F] = "RD2824",      [0x45] = "RD4024E",
        [0x4A] = "RD3924",      [0x5A] = "MS4124E",     [0x5B] = "MS2024",
        [0x67] = "MSH4024M",    [0x68] = "MSH4024RE",   [0x69] = "MS4024",
        [0x6A] = "MS4024AE",    [0x6B] = "MS4024PAE",   [0x6F] = "MS4448AE",
        [0x70] = "MS3748AEJ",   [0x72] = "MS4048",      [0x73] = "MS4448PAE",
        [0x74] = "MS3748PAEJ",  [0x75] = "MS4348PE",
    };

    public static readonly Dictionary<int, string> Modes = new()
    {
        [0x00] = "Standby",         [0x01] = "EQ",
        [0x02] = "FLOAT",           [0x04] = "ABSORB",
        [0x08] = "BULK",            [0x09] = "BATSAVER",
        [0x10] = "CHARGE",          [0x20] = "Off",
        [0x40] = "INVERT",          [0x50] = "Inverter_Standby",
        [0x80] = "SEARCH",
    };

    public static readonly Dictionary<int, string> Faults = new()
    {
        [0x00] = "None",                [0x01] = "STUCK RELAY",
        [0x02] = "DC OVERLOAD",         [0x03] = "AC OVERLOAD",
        [0x04] = "DEAD BAT",            [0x05] = "BACKFEED",
        [0x08] = "LOW BAT",             [0x09] = "HIGH BAT",
        [0x0A] = "HIGH AC VOLTS",       [0x10] = "BAD_BRIDGE",
        [0x12] = "NTC_FAULT",           [0x13] = "FET_OVERLOAD",
        [0x14] = "INTERNAL_FAULT4",     [0x16] = "STACKER MODE FAULT",
        [0x17] = "STACKER NO CLK FAULT",[0x18] = "STACKER CLK PH FAULT",
        [0x19] = "STACKER PH LOSS FAULT",[0x20] = "OVER TEMP",
        [0x21] = "RELAY FAULT",         [0x80] = "CHARGER_FAULT",
        [0x81] = "High Battery Temp",   [0x90] = "OPEN SELCO TCO",
        [0x91] = "CB3 OPEN FAULT",
    };

    public static readonly Dictionary<int, string> StackModes = new()
    {
        [0x00] = "Stand Alone",
        [0x01] = "Parallel stack - master",
        [0x02] = "Parallel stack - slave",
        [0x04] = "Series stack - master",
        [0x08] = "Series stack - slave",
    };

    public static int GetVoltageMultiplier(int model)
    {
        if (model <= 50) return 1;   // 12V
        if (model <= 107) return 2;  // 24V
        return 4;                     // 48V
    }
}
