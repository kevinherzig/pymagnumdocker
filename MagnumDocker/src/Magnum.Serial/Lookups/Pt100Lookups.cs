namespace Magnum.Serial.Lookups;

public static class Pt100Lookups
{
    public static readonly Dictionary<int, string> Modes = new()
    {
        [1] = "Float",  [2] = "Bulk",
        [3] = "Absorb", [4] = "EQ",
    };

    public static readonly Dictionary<int, string> Regulations = new()
    {
        [0] = "Off",         [1] = "Voltage",
        [2] = "Current",     [3] = "Temperature",
        [4] = "Hardware",    [15] = "MPPT",
    };

    public static readonly Dictionary<int, string> Faults = new()
    {
        [0] = "No Fault",              [1] = "Input breaker Fault",
        [2] = "OPS Fault",             [3] = "PV High Fault",
        [4] = "Battery High Fault",    [5] = "BTS Shorted Fault",
        [6] = "FET Overtemp Fault",    [7] = "Battery Overtemp Fault",
        [8] = "Over Current Fault",    [9] = "Internal Phase Fault",
        [10] = "Open BTS Fault",       [11] = "Internal Fault 1",
        [12] = "GFP Fault",            [13] = "ARC Fault",
        [14] = "NTC Fault",            [15] = "HW overtemp Fault",
        [16] = "Overyemp Fault",       [17] = "USB Fault",
        [20] = "Stack Fault",          [21] = "Stack warning",
        [22] = "Stack DIP Fault",      [23] = "Stack DT Fault",
    };
}
