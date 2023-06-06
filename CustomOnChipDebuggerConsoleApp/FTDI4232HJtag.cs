using FTD2XX_NET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ProgressBar;

namespace CustomOnChipDebuggerConsoleApp
{
    public class Ftdi4232HJtag : FtdiDevice
    {
        private FTDI myFtdiDevice;
        private readonly RISCVDMIController myRiscvDMIController;
        private readonly JtagController myJtagStateController;
        private ushort ClkDivisor;
        private const byte TmsTdiMask = 0x00;

        public Ftdi4232HJtag(string serialNumber) : base(serialNumber)
        {
            myFtdiDevice = ftdi;
            myJtagStateController = new JtagController(myFtdiDevice);
            myRiscvDMIController = new RISCVDMIController(myJtagStateController);
        }

        public void Initialize()
        {
            myFtdiDevice.ResetDevice();

            DataReadEvent += null;
            DataWriteEvent += null;

            // Set the FTDI device parameters
            var status = myFtdiDevice.InTransferSize(0xFFFF);
            if (status != FTDI.FT_STATUS.FT_OK)
            {
                throw new Exception("Error setting FTDI InTransferSize");
            }
            status = myFtdiDevice.SetCharacters(0, false, 0, false);
            if (status != FTDI.FT_STATUS.FT_OK)
            {
                throw new Exception("Error setting FTDI SetCharacters");
            }
            myFtdiDevice.SetTimeouts(3000, 3000);
            myFtdiDevice.SetLatency(1);
            status = myFtdiDevice.SetBitMode(TmsTdiMask, FTDI.FT_BIT_MODES.FT_BIT_MODE_RESET);
            if (status != FTDI.FT_STATUS.FT_OK)
            {
                throw new Exception("Failed to set bit mode: " + status.ToString());
            }
            myFtdiDevice.SetBitMode(TmsTdiMask, FTDI.FT_BIT_MODES.FT_BIT_MODE_MPSSE);
            if (status != FTDI.FT_STATUS.FT_OK)
            {
                throw new Exception("Failed to set bit mode: " + status.ToString());
            }
            sendBadCommand(0xAA); // Synchronize the MPSSE interface by sending bad command ＆xAA＊
            sendBadCommand(0xAB); // Synchronize the MPSSE interface by sending bad command ＆xAB＊

            ClkDivisor = 0x0000;

            var isSuccess = false;
            try
            {
                myRiscvDMIController.ConnectToTarget(myFtdiDevice);
                //ResetTap();
                //RunTestIdle(100);
                ReadIDCODE();
                isSuccess = myJtagStateController.Reset();
            }
            catch (Exception exception)
            {
                Console.WriteLine($"FTDI device failed RunTestIdle and failed due to {exception.Message}");
                throw;
            }
            finally
            {
                Console.WriteLine(isSuccess
                    ? "FTDI device is Reset and passed RunTestIdle -> Ready for use"
                    : "TapResetRti failed in Reset Jtag State machine");
            }
        }

        private void ReadIDCODE()
        {
            uint idcode = 0;

            // Read IDCODE register data
            byte[] readData = { 0x20, 0x00, 0x00, 0x00, 0x00 };
            write(readData);
            Thread.Sleep(10000);
            byte[] idCodeData = new byte[4];
            idCodeData = read(4);
            // Extract IDCODE from DR data
            idcode |= (uint)(idCodeData[0] << 0);
            idcode |= (uint)(idCodeData[1] << 8);
            idcode |= (uint)(idCodeData[2] << 16);
            idcode |= (uint)(idCodeData[3] << 24);

            uint version = (idcode >> 28) & 0x0F;
            uint partNumber = (idcode >> 12) & 0xFFFF;
            uint manufacturerId = idcode & 0x7FF;
        }

        public void ResetTap()
        {
            // Clear TMS and TDI buffers
            var emptyBuffer = new byte[1];
            emptyBuffer[0] = 0x00;
            write(emptyBuffer);

            // Send TCK pulses to reset the TAP
            for (var i = 0; i < 6; i++)
            {
                // Set TMS to high (1) for the first five clock cycles
                if (i < 5)
                {
                    emptyBuffer[0] |= 0x02;
                }
                write(emptyBuffer);
                emptyBuffer[0] ^= 0x01;
                write(emptyBuffer);
            }
            // Add delay of 10000 milliseconds
            Thread.Sleep(5000);

            var readBuffer = read((uint)6);
        }

        public void RunTestIdle(int count)
        {
            if (!myFtdiDevice.IsOpen)
            {
                throw new InvalidOperationException("Device is not open.");
            }

            var writeBuffer = new byte[count];

            // Set all bits to 1
            for (var i = 0; i < count; i++)
            {
                writeBuffer[i] = 0xFF;
            }

            write(writeBuffer);

            // Add delay of 10000 milliseconds
            Thread.Sleep(30000);

            var readBuffer = read((uint)count);

            // Check that all bits are set to 1
            for (var i = 0; i < count; i++)
            {
                if (readBuffer[i] != 0xFF)
                {
                    throw new IOException("Error running test idle.");
                }
            }
        }


        private void SendData(byte[] bytes, int length)
        {

        }

        private byte[] ReadData(int length)
        {
            return new byte[length];
        }

        private int ProcessTdoBuffer(byte[] tdoData, int lastShiftBitCount)
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

        /// <summary>
        /// 3.1 BadCommands
        /// If the device detects a bad command it will send back 2 bytes to the PC.
        /// 0xFA,
        /// followed by the byte which caused the bad command.
        /// If the commands and responses that are read/written have got out of 
        /// sequence then this will tell you what the first pattern was that it 
        /// detected an error. The error may have occurred before this, (for 
        /// example sending the wrong amount of data after a write command) and 
        /// will only trigger when bit 7 of the rogue command is high.
        /// </summary>
        /// <param name="badCommand"></param>
        protected void sendBadCommand(byte badCommand)
        {
            byte[] cmd = { badCommand };
            write(cmd);

            byte[] responce = Flush();
            byte[] searchFor = { 0xFA, badCommand };
            if (0 == responce.StartingIndex(searchFor).Count())
            {
                String errMsg = "fail to synchronize MPSSE with command '" + badCommand.ToString() + "'";
                throw new FtdiException(errMsg);
            }
        }

        private byte[] Flush()
        {
            while (inputLen == 0)
            {
                Thread.Sleep(10);
            }
            return read();
        }

        public void Close()
        {
            if (myFtdiDevice.IsOpen)
            {
                myFtdiDevice.Close();
            }
        }

        public void Dispose()
        {
            if (myFtdiDevice != null)
            {
                myFtdiDevice.Close();
                myFtdiDevice = null;
            }
        }
    }

    static class ArrayExtensions
    {
        public static IEnumerable<int> StartingIndex(this byte[] x, byte[] y)
        {
            IEnumerable<int> index = Enumerable.Range(0, x.Length - y.Length + 1);
            for (int i = 0; i < y.Length; i++)
            {
                index = index.Where(n => x[n + i] == y[i]).ToArray();
            }
            return index;
        }
    }
}