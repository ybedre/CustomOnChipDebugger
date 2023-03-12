using System;

namespace CustomOnChipDebuggerConsoleApp
{
    public class Breakpoint
    {
        public enum BreakpointType { Software, Hardware, ReadWatchpoint, AccessWatchpoint };
        public delegate void BreakPointEventHandler(Breakpoint breakpoint);

        public BreakpointType Type { get; }
        public int Address { get; }
        public int Kind { get; }

        static public BreakpointType GetBreakpointType(int type)
        {
            switch (type)
            {
                case 0:
                    return BreakpointType.Software;
                case 1:
                case 2:
                    return BreakpointType.Hardware;
                case 3:
                    return BreakpointType.ReadWatchpoint;
                case 4:
                    return BreakpointType.AccessWatchpoint;
            }

            throw new Exception("Incorrect parameter passed");
        }

        public Breakpoint(Breakpoint.BreakpointType type, int address, int kind)
        {
            this.Type = type;
            this.Address = address;
            this.Kind = kind;
        }
    }
}