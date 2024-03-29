﻿using System;
using System.Collections.Generic;
using System.Globalization;

namespace CustomOnChipDebuggerConsoleApp
{
    public class DebugTarget : IDebugTarget
    {
        private const string MaxAddress = "0xFFFFFFFF";
        private const string MinAddress = "0x00000000";
        private readonly List<Breakpoint> myActiveBreakpoints;
        public RISCVCPU CPU { get; set; }

        public DebugTarget()
        {
            CPU = new RISCVCPU();
            myActiveBreakpoints = new List<Breakpoint>();
        }


        public void DoRun()
        {

        }

        public void DoStop()
        {

        }

        public string AddBreakpoint(Breakpoint.BreakpointType type, int address, int size, int kind)
        {
            string response;
            if (type == Breakpoint.BreakpointType.Software)
            {
                if (IsValidAddress(address))
                {
                    var bp = new Breakpoint(type, address, kind);
                    myActiveBreakpoints.Add(bp);
                    response = "OK";
                }
                else
                {
                    response = "E01";
                }
            }
            else
            {
                response = "E00";
            }

            return response;
        }

        private bool IsValidAddress(int address)
        {
            // Check if address is within the bounds of the target program's memory
            return (address >= int.Parse(MinAddress, NumberStyles.HexNumber) && address <= int.Parse(MaxAddress, NumberStyles.HexNumber));
        }

        public string RemoveBreakpoint(Breakpoint.BreakpointType type, int address, int size, int kind)
        {
            string response;
            if (type == Breakpoint.BreakpointType.Software)
            {
                var bp = myActiveBreakpoints.Find(b => b.Address == address && b.Kind == kind);
                if (bp != null)
                {
                    myActiveBreakpoints.Remove(bp);
                    response = "OK";
                }
                else
                {
                    response = "E01";
                }
            }
            else
            {
                response = "E00";
            }

            return response;
        }

        public void ClearBreakpoints()
        {
            myActiveBreakpoints.Clear();
        }

        // Logging
        public Action<string> LogError
        {
            get; set;
        }
        public Action<Exception> LogException
        {
            get; set;
        }

        public Action<string> Log
        {
            get; set;
        }
    }
}