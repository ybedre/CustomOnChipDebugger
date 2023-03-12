using System;
using System.Linq;

namespace CustomOnChipDebuggerConsoleApp
{
    public class RISCVCPU
    {
        public readonly CpuRegs regs = new CpuRegs();

        public CpuType CpuType;

        public Func<ushort, byte> RDMEM;

        public Action<ushort, byte> WRMEM;

        public RISCVCPU()
        {
            
        }
    }

    public enum CpuType
    {
        RISCV32,
        RISCV64
    }
}