using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomOnChipDebuggerConsoleApp
{
    public class GDBSession
    {
        public static class StandartAnswers
        {
            public const string Empty = "";
            public const string OK = "OK";
            public const string Error = "E00";
            public const string Breakpoint = "T05";
            public const string HaltedReason = "T05thread:00;";
            public const string Interrupt = "T02";
        }

        private readonly IDebugTarget _target;

        public GDBSession(IDebugTarget target)
        {
            _target = target;
        }

        #region Register stuff

        public enum RegisterSize { Byte, Word };

        private static readonly RegisterSize[] s_registerSize = new RegisterSize[] {
            RegisterSize.Word, RegisterSize.Word,RegisterSize.Word, RegisterSize.Word,
            RegisterSize.Word, RegisterSize.Word,RegisterSize.Word, RegisterSize.Word,
            RegisterSize.Word, RegisterSize.Word,RegisterSize.Word, RegisterSize.Word,
            RegisterSize.Word, RegisterSize.Word,RegisterSize.Word, RegisterSize.Word,
            RegisterSize.Word, RegisterSize.Word,RegisterSize.Word, RegisterSize.Word,
            RegisterSize.Word, RegisterSize.Word,RegisterSize.Word, RegisterSize.Word,
            RegisterSize.Word, RegisterSize.Word,RegisterSize.Word, RegisterSize.Word,
            RegisterSize.Word, RegisterSize.Word,RegisterSize.Word, RegisterSize.Word
        };

        private static readonly Action<CpuRegs, int>[] s_regSetters = new Action<CpuRegs, int>[] {
            (r, v) => r.zero = v,
            (r, v) => r.ra = v,
            (r, v) => r.sp = v,
            (r, v) => r.gp = v,
            (r, v) => r.tp = v,
            (r, v) => r.t0 = v,
            (r, v) => r.t1 = v,
            (r, v) => r.t2 = v,
            (r, v) => r.s0 = v,
            (r, v) => r.s1 = v,
            (r, v) => r.a0 = v,
            (r, v) => r.a1 = v,
            (r, v) => r.a2 = v,
            (r, v) => r.a3 = v,
            (r, v) => r.a4 = v,
            (r, v) => r.a5 = v,
            (r, v) => r.a6 = v,
            (r, v) => r.a7 = v,
            (r, v) => r.s2 = v,
            (r, v) => r.s3 = v,
            (r, v) => r.s4 = v,
            (r, v) => r.s5 = v,
            (r, v) => r.s6 = v,
            (r, v) => r.s7 = v,
            (r, v) => r.s8 = v,
            (r, v) => r.s9 = v,
            (r, v) => r.s10 = v,
            (r, v) => r.s11 = v,
            (r, v) => r.t3 = v,
            (r, v) => r.t4 = v,
            (r, v) => r.t5 = v,
            (r, v) => r.t6 = v,
            (r, v) => r.pc = v
        };

        private static readonly Func<CpuRegs, int>[] s_regGetters = new Func<CpuRegs, int>[] {
            r => r.zero,
            r => r.ra,
            r => r.sp,
            r => r.gp,
            r => r.tp,
            r => r.t0,
            r => r.t1,
            r => r.t2,
            r => r.s0,
            r => r.s1,
            r => r.a0,
            r => r.a1,
            r => r.a2,
            r => r.a3,
            r => r.a4,
            r => r.a5,
            r => r.a6,
            r => r.a7,
            r => r.s2,
            r => r.s3,
            r => r.s4,
            r => r.s5,
            r => r.s6,
            r => r.s7,
            r => r.s8,
            r => r.s9,
            r => r.s10,
            r => r.s11,
            r => r.t3,
            r => r.t4,
            r => r.t5,
            r => r.t6,
            r => r.pc
        };

        public static int RegistersCount { get { return s_registerSize.Length; } }

        public static RegisterSize GetRegisterSize(int i)
        {
            return s_registerSize[i];
        }

        public string GetRegisterAsHex(int reg)
        {
            int result = s_regGetters[reg](_target.CPU.regs);
            if (s_registerSize[reg] == RegisterSize.Byte)
                return ((byte)result).ToLowEndianHexString();
            else
                return ((ushort)(result)).ToLowEndianHexString();
        }

        public bool SetRegister(int reg, string hexValue)
        {
            int val = 0;
            if (hexValue.Length == 4)
                val = Convert.ToUInt16(hexValue.Substring(0, 2), 16) | (Convert.ToUInt16(hexValue.Substring(2, 2), 16) << 8);
            else
                val = Convert.ToUInt16(hexValue, 16);

            s_regSetters[reg](_target.CPU.regs, (ushort)val);

            return true;
        }

        #endregion

        public static string FormatResponse(string response)
        {
            return "+$" + response + "#" + GDBPacket.CalculateCRC(response);
        }

        public string ParseRequest(GDBPacket packet, out bool isSignal)
        {
            var result = StandartAnswers.Empty;
            isSignal = false;

            // ctrl+c is SIGINT
            if (packet.GetBytes()[0] == 0x03)
            {
                _target.DoStop();
                result = StandartAnswers.Interrupt;
                isSignal = true;
            }

            try
            {
                switch (packet.CommandName)
                {
                    case '\0': // Command is empty ("+" in 99.99% cases)
                        return null;
                    case 'q':
                        result = GeneralQueryResponse(packet); break;
                    case 'Q':
                        result = GeneralQueryResponse(packet); break;
                    case '?':
                        result = GetTargetHaltedReason(packet); break;
                    case '!': // extended connection
                        break;
                    case 'g': // read registers
                        result = ReadRegisters(packet); break;
                    case 'G': // write registers
                        result = WriteRegisters(packet); break;
                    case 'm': // read memory
                        result = ReadMemory(packet); break;
                    case 'M': // write memory
                        result = WriteMemory(packet); break;
                    case 'X': // write memory binary
                              // Not implemented yet, client shoul use M instead
                              //result = StandartAnswers.OK;
                        break;
                    case 'p': // get single register
                        result = GetRegister(packet); break;
                    case 'P': // set single register
                        result = SetRegister(packet); break;
                    case 'v': // some requests, mainly vCont
                        result = ExecutionRequest(packet); break;
                    case 's': //stepi
                        //_target.CPU.ExecCycle();
                        result = "T05";
                        break;
                    case 'z': // remove bp
                        result = RemoveBreakpoint(packet);
                        break;
                    case 'Z': // insert bp
                        result = SetBreakpoint(packet);
                        break;
                    case 'k': // Kill the target
                        break;
                    case 'H': // set thread
                        result = StandartAnswers.OK; // we do not have threads, so ignoring this command is OK
                        break;
                    case 'c': // continue
                        _target.DoRun();
                        result = null;
                        break;
                    case 'D': // Detach from client
                        _target.DoRun();
                        result = StandartAnswers.OK;
                        break;
                }
            }
            catch (Exception ex)
            {
                _target.LogException?.Invoke(ex);
                result = GetErrorAnswer(Errno.EPERM);
            }

            if (result == null)
                return "+";
            else
                return FormatResponse(result);
        }

        private static string GetErrorAnswer(Errno errno)
        {
            return string.Format("E{0:D2}", (int)errno);
        }

        private string GeneralQueryResponse(GDBPacket packet)
        {
            string command = packet.GetCommandParameters()[0];
            if (command.StartsWith("Supported"))
                return "PacketSize=4096";
            if (command.StartsWith("C"))
                return StandartAnswers.OK;
            if (command.StartsWith("Attached"))
                return "1";
            if (command.StartsWith("TStatus"))
                return StandartAnswers.Empty;
            if (command.StartsWith("Offset"))
                return "Text=0;Data=0;Bss=0";
            if (command.StartsWith("fThreadInfo"))
                return "m0";
            if (command.StartsWith("sThreadInfo"))
                return "l";
            if (command.StartsWith("Symbol"))
            {
                return "OK";
            }
            return StandartAnswers.OK;
        }

        private string GetTargetHaltedReason(GDBPacket packet)
        {
            return StandartAnswers.HaltedReason;
        }

        private string ReadRegisters(GDBPacket packet)
        {
            string registerData = "";

            // Read register values from .s file
            Dictionary<string, uint> registerValues = ParseSFileForRegisters();

            // Iterate through all registers and append their values to the registerData string
            for (int i = 0; i < 32; i++)
            {
                string registerName = $"x{i}";
                if (registerValues.ContainsKey(registerName))
                {
                    // If the register value was found in the .s file, append it to registerData
                    registerData += registerValues[registerName].ToString();
                }
                else
                {
                    // If the register value was not found in the .s file, assume it's 0 and append it to registerData
                    registerData += "00000000";
                }
            }
            return registerData;
        }

        public static Dictionary<string, uint> ParseSFileForRegisters()
        {
            Dictionary<string, uint> registerValues = new Dictionary<string, uint>();
            var filePath = @"C:\Users\nxf89429\Documents\CustomOCD\ybedre\CustomOnChipDebugger\Binaries\Hello.s";

            using (StreamReader reader = new StreamReader(filePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] tokens = line.Split(new char[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                    // Check if this line modifies a register
                    if (tokens.Length >= 2 && tokens[0].EndsWith(":") == false)
                    {
                        string instruction = tokens[0];
                        string destReg = tokens[1];

                        if (destReg.StartsWith("x") && destReg.Length == 2)
                        {
                            // The line modifies a general purpose register
                            if (instruction == "addi")
                            {
                                // Check if this is a register initialization
                                if (tokens[2].StartsWith("0x"))
                                {
                                    uint value = Convert.ToUInt32(tokens[2], 16);
                                    registerValues[destReg] = value;
                                }
                                else
                                {
                                    // Add or subtract an immediate value from the register
                                    registerValues.TryGetValue(tokens[2], out var value);
                                    uint imm = Convert.ToUInt32(tokens[3], 10);
                                    registerValues[destReg] = value + imm;
                                }
                            }
                            else if (instruction == "lw" || instruction == "sw")
                            {
                                // Load or store a word from memory
                                string[] offsetTokens = tokens[2].Split(new char[] { '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
                                uint offset = Convert.ToUInt32(offsetTokens[0], 10);
                                string baseReg = offsetTokens[1];
                                registerValues.TryGetValue(baseReg, out var baseRegValue);
                                registerValues[destReg] = baseRegValue + offset;
                            }
                            else if (instruction == "mv")
                            {
                                // Move a value from one register to another
                                registerValues.TryGetValue(tokens[2], out var value);
                                registerValues[destReg] = value;
                            }
                            // Add more cases for other instructions that modify registers as needed
                        }
                    }
                }
            }

            return registerValues;
        }



        private string WriteRegisters(GDBPacket packet)
        {
            var regsData = packet.GetCommandParameters()[0];
            for (int i = 0, pos = 0; i < RegistersCount; i++)
            {
                int currentRegisterLength = GetRegisterSize(i) == RegisterSize.Word ? 4 : 2;
                SetRegister(i, regsData.Substring(pos, currentRegisterLength));
                pos += currentRegisterLength;
            }
            return StandartAnswers.OK;
        }

        private string GetRegister(GDBPacket packet)
        {
            return ParseSFileForPC().ToString("x8");
        }

        private uint ParseSFileForPC()
        {
            uint pc = 10000;
            string filePath = @"C:\Users\nxf89429\Documents\CustomOCD\ybedre\CustomOnChipDebugger\Binaries\Hello.s";
            List<string> sFileContentlines = new List<string>();
            using (StreamReader reader = new StreamReader(filePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    sFileContentlines.Add(line);
                }
            }

            foreach (string line in sFileContentlines)
            {
                if (line.Contains("main:"))
                {
                    string[] words = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (string word in words)
                    {
                        if (word.Contains("0x"))
                        {
                            pc = Convert.ToUInt32(word.Substring(2), 16);
                            break;
                        }
                    }

                    break;
                }
            }

            return pc;
        }

        private string SetRegister(GDBPacket packet)
        {
            var parameters = packet.GetCommandParameters()[0].Split(new char[] { '=' });
            if (SetRegister(Convert.ToInt32(parameters[0], 16), parameters[1]))
                return StandartAnswers.OK;
            else
                return StandartAnswers.Error;
        }

        private string ReadMemory(GDBPacket packet)
        {
            var parameters = packet.GetCommandParameters();
            if (parameters.Length < 2)
            {
                return GetErrorAnswer(Errno.EPERM);
            }
            var arg1 = Convert.ToUInt32(parameters[0], 16);
            var arg2 = Convert.ToUInt32(parameters[1], 16);
            if (arg1 > ushort.MaxValue || arg2 > ushort.MaxValue)
            {
                return GetErrorAnswer(Errno.EPERM);
            }
            var addr = (ushort)arg1;
            var length = (ushort)arg2;
            var result = string.Empty;
            //for (var i = 0; i < length; i++)
            //{
            //    var hex = _target.CPU.RDMEM((ushort)(addr + i))
            //        .ToLowEndianHexString();
            //    result += hex;
            //}
            return "12AB";
        }

        private string WriteMemory(GDBPacket packet)
        {
            var parameters = packet.GetCommandParameters();
            if (parameters.Length < 3)
            {
                return GetErrorAnswer(Errno.ENOENT);
            }
            var arg1 = Convert.ToUInt32(parameters[0], 16);
            var arg2 = Convert.ToUInt32(parameters[1], 16);
            if (arg1 > ushort.MaxValue || arg2 > ushort.MaxValue)
            {
                return GetErrorAnswer(Errno.ENOENT);
            }
            var addr = (ushort)arg1;
            var length = (ushort)arg2;
            for (var i = 0; i < length; i++)
            {
                var hex = parameters[2].Substring(i * 2, 2);
                var value = Convert.ToByte(hex, 16);
                _target.CPU.WRMEM((ushort)(addr + i), value);
            }
            return StandartAnswers.OK;
        }

        private string ExecutionRequest(GDBPacket packet)
        {
            string command = packet.GetCommandParameters()[0];
            if (command.StartsWith("Cont?"))
                return "";
            if (command.StartsWith("Cont"))
            {

            }
            return StandartAnswers.Empty;
        }

        private string SetBreakpoint(GDBPacket packet)
        {
            string[] parameters = packet.GetCommandParameters();
            Breakpoint.BreakpointType type = Breakpoint.GetBreakpointType(int.Parse(parameters[0]));
            int address = int.Parse(parameters[1], NumberStyles.HexNumber);
            int size = int.Parse(parameters[2]);
            int kind = int.Parse(parameters[3]);
            return _target.AddBreakpoint(type, address, size, kind);
        }

        private string RemoveBreakpoint(GDBPacket packet)
        {
            string[] parameters = packet.GetCommandParameters();
            Breakpoint.BreakpointType type = Breakpoint.GetBreakpointType(int.Parse(parameters[0]));
            int address = int.Parse(parameters[1], NumberStyles.HexNumber);
            int size = int.Parse(parameters[2]);
            int kind = int.Parse(parameters[3]);
            return _target.RemoveBreakpoint(type, address, size, kind);
        }
    }
}
