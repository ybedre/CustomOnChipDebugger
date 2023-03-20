using System;

namespace CustomOnChipDebuggerConsoleApp
{
    public interface IJtag : IDisposable
    {
        void Initialize();
        void Open(string serialNumber);
        void SetInterfaceConfiguration(int interfaceSpeedHz, int interfaceConfig);
        void ShiftTmsTdiAndReadTdo(bool[] tmsStates, bool[] tdiStates, out bool[] tdoStates);
        bool GetTdo();
        void Close();
    }
}