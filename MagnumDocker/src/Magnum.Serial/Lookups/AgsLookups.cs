namespace Magnum.Serial.Lookups;

public static class AgsLookups
{
    public static readonly Dictionary<int, string> Statuses = new()
    {
        [0] = "Not Connected",      [1] = "Off",
        [2] = "Ready",              [3] = "Manual Run",
        [4] = "AC In",              [5] = "In quiet time",
        [6] = "Start in test mode", [7] = "Start on temperature",
        [8] = "Start on voltage",   [9] = "Fault start on test",
        [10] = "Fault start on temp",[11] = "Fault start on voltage",
        [12] = "Start TOD",         [13] = "Start SOC",
        [14] = "Start Exercise",    [15] = "Fault start TOD",
        [16] = "Fault start SOC",   [17] = "Fault start Exercise",
        [18] = "Start on Amp",      [19] = "Start on Topoff",
        [20] = "Not used",          [21] = "Fault start on Amp",
        [22] = "Fault on Topoff",   [23] = "Not used",
        [24] = "Fault max run",     [25] = "Gen Run Fault",
        [26] = "Gen in Warm up",    [27] = "Gen in Cool down",
    };

    public static readonly HashSet<int> RunningStatuses = new()
    {
        3, 6, 7, 8, 12, 13, 14, 18, 19, 26, 27
    };
}
