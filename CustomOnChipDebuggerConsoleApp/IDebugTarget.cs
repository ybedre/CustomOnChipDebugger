using System;

namespace CustomOnChipDebuggerConsoleApp
{
    public interface IDebugTarget
    {
        RISCVCPU CPU { get; }
        void ClearBreakpoints();
        void DoRun();
        void DoStop();
        string AddBreakpoint(Breakpoint.BreakpointType type, int address, int size, int kind);
        string RemoveBreakpoint(Breakpoint.BreakpointType type, int address, int size, int kind);

        /// <summary>
        /// Optional error logging, leave null if not needed
        /// </summary>
        Action<string> LogError { get; }

        /// <summary>
        /// Optional exception logging, leave null if not needed
        /// </summary>
        Action<Exception> LogException { get; }

        /// <summary>
        /// Optional logging, leave null if not needed
        /// </summary>

        Action<string> Log { get; }
    }
}