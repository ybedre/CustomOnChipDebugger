using FTD2XX_NET;
using System;
using System.Threading;

namespace CustomOnChipDebuggerConsoleApp
{
    public class JtagController
    {
        private readonly FTDI myFtdi;
        private const int FtdiMaxRwSize = 65532; // The max read/write size is 65536, we use 65532 here
        private const byte MPSSEWriteNeg = 0x01; //Write TDI/DO on negative TCK/SK edge
        private const byte MPSSEDoWrite = 0x10; // Write TDI/DO
        private const byte MPSSEDoRead = 0x20;   // Read TDO/DI
        private const byte MPSSELsb = 0x08;   // LSB first
        private const byte MPSSEWriteTms = 0x40;   // Write TMS/CS
        private const byte JPProgram = 0x08;

        public JtagController(FTDI ftdiDevice)
        {
            if (ftdiDevice != null)
            {
                myFtdi = ftdiDevice;
            }
        }

        public bool TapTms(int tms, byte bit7)
        {
            var buf = new byte[3];
            buf[0] = MPSSEWriteTms | MPSSELsb | FTDI.FT_BIT_MODES.FT_BIT_MODE_MPSSE | MPSSEWriteNeg;
            buf[1] = 0; // value = length - 1
            buf[2] = (byte)((tms != 0 ? 0x01 : 0x00) | ((bit7 & 0x01) << 7));
            uint bytesWritten = 0;
            var status = myFtdi.Write(buf, buf.Length, ref bytesWritten);
            if (status != FTDI.FT_STATUS.FT_OK || bytesWritten != buf.Length)
            {
                return false;
            }

            return true;
        }

        public bool TapResetRti()
        {
            var isSuccess = false;
            // Clear TMS and TDI buffers
            var emptyBuffer = new byte[1];
            emptyBuffer[0] = 0x00;
            uint bytesWritten = 0;
            // Send TCK pulses to reset the TAP
            for (var i = 0; i < 6; i++)
            {
                // Set TMS to high (1) for the first five clock cycles
                if (i < 5)
                {
                    emptyBuffer[0] |= 0x02;
                }
                var status = myFtdi.Write(emptyBuffer, 1, ref bytesWritten);
                if (status == FTDI.FT_STATUS.FT_OK || bytesWritten == 1)
                {
                    emptyBuffer[0] ^= 0x01;
                    status = myFtdi.Write(emptyBuffer, 1, ref bytesWritten);
                    if (status != FTDI.FT_STATUS.FT_OK || bytesWritten != 1)
                    {
                        Console.WriteLine($"TapResetRti failed due to {status}");
                    }

                    isSuccess = true;
                }
                else
                {
                    Console.WriteLine($"TapResetRti failed due to {status}");
                }
            }

            TapTms(0, 0); // Goto RTI
            return isSuccess;
        }

        public bool TapShiftIrOnly(byte ir)
        {
            var buf = new byte[3];
            buf[0] = MPSSEDoWrite | MPSSELsb | FTDI.FT_BIT_MODES.FT_BIT_MODE_MPSSE | MPSSEWriteNeg;
            buf[1] = 4;
            buf[2] = ir;
            uint bytesWritten = 0;
            var status = myFtdi.Write(buf, buf.Length, ref bytesWritten);
            if (status != FTDI.FT_STATUS.FT_OK || bytesWritten != buf.Length)
            {
                Console.WriteLine("Write loop failed");
                return false;
            }
            return TapTms(1, (byte)(ir >> 5));
        }

        public bool TapShiftIr(byte ir)
        {
            TapTms(1, 0); // RTI status
            TapTms(1, 0);
            TapTms(0, 0);
            TapTms(0, 0); // Goto shift IR

            var isSuccess = TapShiftIrOnly(ir);

            TapTms(1, 0);
            TapTms(0, 0); // Goto RTI

            return isSuccess;
        }

        public bool ShiftLastBits(byte[] input, byte len, byte[] output)
        {
            var buf = new byte[3];
            if (len != 0)
            {
                buf[0] = MPSSELsb | FTDI.FT_BIT_MODES.FT_BIT_MODE_MPSSE | MPSSEWriteNeg;

                if (input != null)
                    buf[0] |= MPSSEDoWrite;

                if (output != null)
                    buf[0] |= MPSSEDoRead;

                buf[1] = (byte)(len - 1);

                if (input != null)
                    buf[2] = input[0];

                uint bytesWritten = 0;
                if (myFtdi.Write(buf, 3, ref bytesWritten) != FTDI.FT_STATUS.FT_OK || bytesWritten != 3)
                {
                    Console.Error.WriteLine("FTDI write failed");
                    return false;
                }

                if (output != null)
                {
                    uint bytesRead = 0;
                    if (myFtdi.Read(output, 1, ref bytesRead) != FTDI.FT_STATUS.FT_OK || bytesRead != 1)
                    {
                        Console.Error.WriteLine("FTDI read failed");
                        return false;
                    }
                }

            }
            return true;
        }

        public bool TapShiftDrBitsOnly(byte[] input, uint inputBits, byte[] output)
        {
            var bufBytes = new byte[FtdiMaxRwSize + 3];
            var inputBytes = inputBits / 8;
            var lastBits = inputBits % 8;

            // Have to be at RTI status before calling this function
            TapTms(1, 0);

            if (inputBytes != 0)
            {
                var t = (int)(inputBytes / FtdiMaxRwSize);
                var lastBytes = (ushort)(inputBytes % FtdiMaxRwSize);

                int i;
                for (i = 0; i <= t; i++)
                {
                    var len = (ushort)(i == t ? lastBytes : FtdiMaxRwSize);

                    bufBytes[0] = MPSSELsb | MPSSEWriteNeg;

                    if (input != null)
                    {
                        bufBytes[0] |= MPSSEDoWrite;
                        Array.Copy(input, i * FtdiMaxRwSize, bufBytes, 3, len);
                    }

                    if (output != null)
                    {
                        bufBytes[0] |= MPSSEDoRead;
                    }

                    bufBytes[1] = (byte)((len - 1) & 0xff);
                    bufBytes[2] = (byte)(((len - 1) >> 8) & 0xff);

                    uint bytesWritten = 0;
                    if (myFtdi.Write(bufBytes, len + 3, ref bytesWritten) != FTDI.FT_STATUS.FT_OK || bytesWritten != len + 3)
                    {
                        Console.WriteLine("Ftdi write failed");
                        return false;
                    }

                    //Go to Update-DR state
                    TapTms(0, 0);
                    TapTms(0, 0);
                    TapTms(1, 0);
                    TapTms(1, 0);

                    // Add delay of 100 milliseconds
                    Thread.Sleep(1000);

                    if (output != null)
                    {
                        uint bytesRead = 0;
                        if (myFtdi.Read(output, len, ref bytesRead) != FTDI.FT_STATUS.FT_OK || bytesRead != len)
                        {
                            Console.WriteLine("Ftdi Read failed");
                            return false;
                        }
                    }
                }
            }

            if (lastBits != 0)
            {
                // Send last few bits
                ShiftLastBits(input, (byte)(inputBytes * 8 + lastBits - 1), output);
                if (input != null)
                {
                    TapTms(1, (byte)((input[inputBytes] >> (int)(lastBits - 1)) & 0x01));
                    return false;
                }
            }
            else
            {
                TapTms(1, 0);   // Goto DR-Scan
            }

            return true;
        }

        public bool TapShiftDRBits(byte[] inData, uint inBits, byte[] outData)
        {
            var isSuccess = TapShiftDrBitsOnly(inData, inBits, outData);
            return isSuccess;
        }

        public bool Flush()
        {
            byte[] buf = { };
            uint bytesWritten = 0;
            if (myFtdi.Write(buf, 1, ref bytesWritten) != FTDI.FT_STATUS.FT_OK || bytesWritten != 1)
            {
                Console.WriteLine("Can't SEND_IMMEDIATE");
                return false;
            }

            return true;
        }

        public bool Reset()
        {
            var isSuccess = false;
            if (TapResetRti())
            {
                if (TapShiftIr(JPProgram))
                {
                    isSuccess = TapResetRti();
                }
                else
                {
                    Console.WriteLine("TapReTapShiftIr failed in Reset Jtag State machine");
                }

            }
            else
            {
                Console.WriteLine("TapResetRti failed for the first time in Reset Jtag State machine");
            }
            return isSuccess;
        }
    }
}