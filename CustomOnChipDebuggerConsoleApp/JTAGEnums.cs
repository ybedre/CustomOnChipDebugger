using System;

namespace CustomOnChipDebuggerConsoleApp
{
    public enum JtagCommands
    {
        TestLogicReset = 0x0,
        RunTestIdle = 0x1,
        SelectDRScan = 0x2,
        SelectIRScan = 0x3,
        CaptureDR = 0x4,
        CaptureIR = 0x5,
        ShiftDR = 0x6,
        ShiftIR = 0x7,
        Exit1DR = 0x8,
        Exit1IR = 0x9,
        UpdateDR = 0xA,
        UpdateIR = 0xB,
        DataRegister = 0xC,
        Bypass = 0xF,
        JtagIO = 0x1C
    }

    public enum JtagIoDirections
    {
        Input = 0,
        Output = 1
    }

    [Flags]
    public enum JtagIoFlags
    {
        None,
        TMS,
        TDI,
        TDO,
        LAST
    }
}
