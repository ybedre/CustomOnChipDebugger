using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomOnChipDebuggerConsoleApp
{
    public interface IRiscvJtagDriver : IDisposable
    {
        bool IsOpen { get; }
        bool IsConfigured { get; }
        void Open();
        void Close();
        void Connect();
        void Configure(int instructionRegisterLength, int bypassRegisterLength, int dataRegisterLength);
        void ClearTAPController();
        void ResetTAPController();
        void ShiftData(bool[] data, int bitLength, bool lastTMS);
        void ShiftData(byte[] data, int bitLength, bool lastTMS);
        byte[] ReadData(int bitLength, bool lastTMS);
    }
}
