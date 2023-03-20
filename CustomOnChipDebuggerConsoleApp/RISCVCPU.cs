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
            InitializeJtagDriver();
        }

        private IRiscvJtagDriver myJtagDriver;

        private void InitializeJtagDriver()
        {
            myJtagDriver = new RiscvJtagDriver();
            myJtagDriver.Open();
            myJtagDriver.Initialize();
            ReadMemory = MemoryRead;
            WriteMemory = MemoryWrite;
        }

        private byte MemoryRead(ushort address)
        {
            return new byte();
        }

        private void MemoryWrite(ushort address, byte value)
        {
            myJtagDriver.WriteData(address,value);

            Console.WriteLine("GDB command sent successfully!");
        }

        private int ProcessTdoBuffer(byte[] tdoData,int lastShiftBitCount)
        {
            // Convert the byte array to a bit array.
            var tdoBits = new bool[tdoData.Length * 8];
            for (var i = 0; i < tdoData.Length; i++)
            {
                for (var j = 0; j < 8; j++)
                {
                    tdoBits[i * 8 + j] = ((tdoData[i] >> j) & 0x01) == 0x01;
                }
            }

            // Extract the TDO data from the bit array based on the last shift bit count.
            var tdoDataBits = new bool[lastShiftBitCount];
            for (var i = 0; i < lastShiftBitCount; i++)
            {
                tdoDataBits[i] = tdoBits[i];
            }

            // Convert the TDO data from the bit array to the desired data format.
            var tdoValue = 0;
            for (var i = 0; i < lastShiftBitCount; i++)
            {
                if (tdoDataBits[i])
                {
                    tdoValue |= 1 << i;
                }
            }

            return tdoValue;
        }
    }

    public enum CpuType
    {
        RISCV32,
        RISCV64
    }
}