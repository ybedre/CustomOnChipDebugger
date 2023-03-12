using System.Runtime.InteropServices;

namespace CustomOnChipDebuggerConsoleApp
{
    [StructLayout(LayoutKind.Explicit)]
    public sealed class CpuRegs
    {
        [FieldOffset(0)]
        public int zero;

        [FieldOffset(1)]
        public int ra;

        [FieldOffset(2)]
        public int sp;

        [FieldOffset(3)]
        public int tp;

        [FieldOffset(4)]
        public int gp;

        [FieldOffset(5)]
        public int t0;

        [FieldOffset(6)]
        public int t1;

        [FieldOffset(7)]
        public int t2;

        [FieldOffset(8)]
        public int s0;

        [FieldOffset(9)]
        public int s1;

        [FieldOffset(10)]
        public int a0;

        [FieldOffset(11)]
        public int a1;

        [FieldOffset(12)]
        public int a2;

        [FieldOffset(13)]
        public int a3;

        [FieldOffset(14)]
        public int a4;

        [FieldOffset(15)]
        public int a5;

        [FieldOffset(16)]
        public int a6;

        [FieldOffset(17)]
        public int a7;

        [FieldOffset(18)]
        public int s2;

        [FieldOffset(19)]
        public int s3;

        [FieldOffset(20)]
        public int s4;

        [FieldOffset(21)]
        public int s5;

        [FieldOffset(22)]
        public int s6;

        [FieldOffset(23)]
        public int s7;

        [FieldOffset(24)]
        public int s8;

        [FieldOffset(25)]
        public int s9;

        [FieldOffset(26)]
        public int s10;

        [FieldOffset(27)]
        public int s11;

        [FieldOffset(28)]
        public int t3;

        [FieldOffset(29)]
        public int t4;

        [FieldOffset(30)]
        public int t5;

        [FieldOffset(31)]
        public int t6;

        [FieldOffset(32)]   
        public int pc;
    }
}