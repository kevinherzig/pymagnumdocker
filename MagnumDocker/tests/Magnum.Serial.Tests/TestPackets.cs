namespace Magnum.Serial.Tests;

public static class TestPackets
{
    public static byte[] AgsA1 => Convert.FromHexString("A102343A007F");
    public static byte[] AgsA2 => Convert.FromHexString("A20000000000");
    public static byte[] Bmk81 => Convert.FromHexString("814C09F1007407E00C08FF984FF000140A01");
    public static byte[] Inverter => Convert.FromHexString("400000F60016770001003D1133246B010005025800");
    public static byte[] PtC1 => Convert.FromHexString("C1001300024D008505BC1194101B313E");
    public static byte[] PtC2 => Convert.FromHexString("C200000200AC0002050B641425");
    public static byte[] Rtr91 => Convert.FromHexString("9120");
    public static byte[] Remote00 => Convert.FromHexString("00002808640A280000C89B840C1412200000000000");
    public static byte[] Remote80 => Convert.FromHexString("00002808640A280000C89B840C1412200000280080");
    public static byte[] RemoteA0 => Convert.FromHexString("00002808640A280000C89B840C14122014007300A0");
    public static byte[] RemoteA1 => Convert.FromHexString("00002808640A280000C89B840C14600090787878A1");
    public static byte[] RemoteA2 => Convert.FromHexString("00003C04500F170601C8A5860100465500781478A2");
    public static byte[] RemoteA3 => Convert.FromHexString("00003C04500F170601C8A58601005E5F00240200A3");
    public static byte[] RemoteA4 => Convert.FromHexString("00003C04500F170601C8A58601001E1E00000000A4");
}
