namespace CustomOnChipDebuggerConsoleApp
{
    public enum IRISCVDmiJtagCommand : byte
    {
        TestLogicReset = 0x0,
        RunTestIdle = 0x1,
        SelectDR = 0x2,
        SelectIR = 0x3,
        CaptureDR = 0x4,
        CaptureIR = 0x5,
        ShiftDR = 0x6,
        ShiftIR = 0x7,
        Exit1DR = 0x8,
        PauseDR = 0x9,
        Exit2DR = 0xA,
        Exit1IR = 0xB,
        PauseIR = 0xC,
        Exit2IR = 0xD,
        UpdateIR = 0xE,
        DMI=0x10,
        UpdateDR = 0x11
    }
}