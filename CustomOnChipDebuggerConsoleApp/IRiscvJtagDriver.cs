using System;

namespace CustomOnChipDebuggerConsoleApp
{
    public interface IRiscvJtagDriver : IDisposable
    {
        bool IsOpen { get; }
        bool IsConfigured { get; }
        Ftdi4232HJtag myDebuggerDevice { get; }
        void Open();
        void Initialize();
        bool GetTdo();
        void SetInterfaceConfiguration(int interfaceSpeedHz, int interfaceConfig);
        string WriteData(ushort address, byte value);
        void ReadData(string command);
        void Close();
    }
}