using System;

namespace CustomOnChipDebuggerConsoleApp
{
    public class RISCVCPU
    {
        public readonly CpuRegs Registers = new CpuRegs();

        public CpuType CpuType;

        public Func<ushort, byte> ReadMemory;

        public Action<ushort, byte> WriteMemory;

        public RISCVCPU()
        {
            InitializeCPU();
        }


        private void InitializeCPU()
        {
            ReadMemory = MemoryRead;
            WriteMemory = MemoryWrite;
        }

        private byte MemoryRead(ushort address)
        {
            return new byte();
        }

        private void MemoryWrite(ushort address, byte value)
        {

            Console.WriteLine("GDB command sent successfully!");
        }        
    }

    public enum CpuType
    {
        RISCV32,
        RISCV64
    }
}