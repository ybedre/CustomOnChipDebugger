using System.Collections.Generic;

namespace CustomOnChipDebuggerConsoleApp
{
    public class RISCV32RegisterInfo
    {
        public enum RV32Registers
        {
            zero,
            ra,
            sp,
            tp,
            gp,
            t0,
            t1,
            t2,
            s0,
            s1,
            a0,
            a1,
            a2,
            a3,
            a4,
            a5,
            a6,
            a7,
            s2,
            s3,
            s4,
            s5,
            s6,
            s7,
            s8,
            s9,
            s10,
            s11,
            t3,
            t4,
            t5,
            t6,
            pc,
        }

        public readonly Dictionary<RV32Registers, uint> RV32RegisterAddressMap =
            new Dictionary<RV32Registers, uint>();

        public RISCV32RegisterInfo()
        {
            RV32RegisterAddressMap.Add(RV32Registers.zero,0x1000);
            RV32RegisterAddressMap.Add(RV32Registers.ra, 0x1001);
            RV32RegisterAddressMap.Add(RV32Registers.sp, 0x1002);
            RV32RegisterAddressMap.Add(RV32Registers.tp, 0x1003);
            RV32RegisterAddressMap.Add(RV32Registers.gp, 0x1004);
            RV32RegisterAddressMap.Add(RV32Registers.t0, 0x1005);
            RV32RegisterAddressMap.Add(RV32Registers.t1, 0x1006);
            RV32RegisterAddressMap.Add(RV32Registers.t2, 0x1007);
            RV32RegisterAddressMap.Add(RV32Registers.s0, 0x1008);
            RV32RegisterAddressMap.Add(RV32Registers.s1, 0x1009);
            RV32RegisterAddressMap.Add(RV32Registers.a0, 0x100A);
            RV32RegisterAddressMap.Add(RV32Registers.a1, 0x100B);
            RV32RegisterAddressMap.Add(RV32Registers.a2, 0x100C);
            RV32RegisterAddressMap.Add(RV32Registers.a3, 0x100D);
            RV32RegisterAddressMap.Add(RV32Registers.a4, 0x100E);
            RV32RegisterAddressMap.Add(RV32Registers.a5, 0x100F);
            RV32RegisterAddressMap.Add(RV32Registers.a6, 0x1010);
            RV32RegisterAddressMap.Add(RV32Registers.a7, 0x1011);
            RV32RegisterAddressMap.Add(RV32Registers.s2, 0x1012);
            RV32RegisterAddressMap.Add(RV32Registers.s3, 0x1013);
            RV32RegisterAddressMap.Add(RV32Registers.s4, 0x1014);
            RV32RegisterAddressMap.Add(RV32Registers.s5, 0x1015);
            RV32RegisterAddressMap.Add(RV32Registers.s6, 0x1016);
            RV32RegisterAddressMap.Add(RV32Registers.s7, 0x1017);
            RV32RegisterAddressMap.Add(RV32Registers.s8, 0x1018);
            RV32RegisterAddressMap.Add(RV32Registers.s9, 0x1019);
            RV32RegisterAddressMap.Add(RV32Registers.s10, 0x101A);
            RV32RegisterAddressMap.Add(RV32Registers.s11, 0x101B);
            RV32RegisterAddressMap.Add(RV32Registers.t3, 0x101C);
            RV32RegisterAddressMap.Add(RV32Registers.t4, 0x101D);
            RV32RegisterAddressMap.Add(RV32Registers.t5, 0x101E);
            RV32RegisterAddressMap.Add(RV32Registers.t6, 0x101F);
            RV32RegisterAddressMap.Add(RV32Registers.pc, 0x7b1);
        }
    }
}