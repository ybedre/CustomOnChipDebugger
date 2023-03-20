using FTD2XX_NET;
using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace CustomOnChipDebuggerConsoleApp
{
    public class Ftdi4232HJtag : IJtag
    {
        private FTDI myFtdiDevice;
        private uint myClockDivider;
        private uint myDataLength;
        private readonly byte[] myWriteBuffer;
        private readonly byte[] myReadBuffer;
        private const byte TmsTdiMask = 0xFF;
        private byte myLastTmsTdiMask;
        private IntPtr myFtHandle;
        private readonly IRISCVDMIInterface myRiscvDMIController;
        private const int TdoPin = 1;
        private const int TdiPin = 2;
        public int TckFrequencyHz { get; set; }

        public Ftdi4232HJtag()
        {
            myFtdiDevice.se = new FTDI();
            myClockDivider = 30;
            myDataLength = 8;
            myWriteBuffer = new byte[1];
            myReadBuffer = new byte[1];
            myRiscvDMIController = new RISCVDMIController();
        }

        public void Open(string serialNumber)
        {
            if (myFtdiDevice.IsOpen)
            {
                myFtdiDevice.Close();
            }

            // Search for FTDI devices with specified serial number
            uint numDevices = 0;
            myFtdiDevice.GetNumberOfDevices(ref numDevices);
            var deviceList = new FTDI.FT_DEVICE_INFO_NODE[numDevices];
            myFtdiDevice.GetDeviceList(deviceList);
            var device = deviceList.FirstOrDefault(d => d.SerialNumber == serialNumber);

            if (device == null)
            {
                throw new Exception("FTDI device with specified serial number not found.");
            }

            myFtHandle = device.ftHandle;
            // Open device and configure for JTAG
            myFtdiDevice.OpenBySerialNumber(device.SerialNumber);
            myFtdiDevice.SetBitMode(TmsTdiMask, FTDI.FT_BIT_MODES.FT_BIT_MODE_RESET FTDI.FT_T);
            myFtdiDevice.SetBitMode(TmsTdiMask, FTDI.FT_BIT_MODES.FT_BIT_MODE_SYNC_BITBANG);
            myFtdiDevice.SetLatency(2);
            myFtdiDevice.SetTimeouts(1000, 1000);

            // Configure clock frequency and data length
            myFtdiDevice.SetBaudRate(10000000 / myClockDivider);
            myFtdiDevice.SetDataCharacteristics(FTDI.FT_DATA_BITS.FT_BITS_8, FTDI.FT_STOP_BITS.FT_STOP_BITS_1,
                FTDI.FT_PARITY.FT_PARITY_NONE);

            // Initialize write and read buffers
            myWriteBuffer[0] = 0x00;
            myReadBuffer[0] = 0x00;
        }

        public void SetInterfaceConfiguration(int interfaceSpeedHz, int interfaceConfig)
        {
            myClockDivider = (uint)(10000000 / interfaceSpeedHz);
            myDataLength = (uint)interfaceConfig;
        }

        public void Initialize()
        {
            // Set the FTDI device parameters
            var ftStatus = myFtdiDevice.SetBitMode(TmsTdiMask, FTDI.FT_BIT_MODES.FT_BIT_MODE_ASYNC_BITBANG);
            if (ftStatus != FTDI.FT_STATUS.FT_OK)
            {
                throw new Exception("Error setting FTDI bit mode");
            }

            ftStatus = myFtdiDevice.SetLatency(16);
            if (ftStatus != FTDI.FT_STATUS.FT_OK)
            {
                throw new Exception("Error setting FTDI latency timer");
            }

            // Set the USB parameters for JTAG (bit-bang) mode
            ftStatus = myFtdiDevice.SetBitMode(TmsTdiMask, FTDI.FT_BIT_MODES.FT_BIT_MODE_ASYNC_BITBANG);
            if (ftStatus != FTDI.FT_STATUS.FT_OK)
            {
                throw new Exception("Error setting FTDI USB parameters");
            }

            ftStatus = myFtdiDevice.SetTimeouts(5000, 5000);
            if (ftStatus != FTDI.FT_STATUS.FT_OK)
            {
                throw new Exception("Error setting FTDI timeouts");
            }

            try
            {
                myRiscvDMIController.ConnectToTarget(myFtdiDevice);
                myRiscvDMIController.ResetTarget();
            }
            catch (Exception exception)
            {
                Console.WriteLine($"FTDI device failed RunTestIdle and failed due to {exception.Message}");
                throw;
            }
            finally
            {
                Console.WriteLine("FTDI device is Reset and passed RunTestIdle -> Ready for use");
            }
        }

        public void SetTms(bool state)
        {
            var bitValue = state ? (byte)1 : (byte)0;
            var buffer = new[] { bitValue };
            uint bytesWritten = 0;
            myFtdiDevice.SetBitMode(TmsTdiMask, 0x02);
            var ftStatus = myFtdiDevice.Write(buffer, 1, ref bytesWritten);
            if (ftStatus != FTDI.FT_STATUS.FT_OK || bytesWritten != 1)
            {
                throw new Exception("Failed to write TMS state to JTAG interface.");
            }
        }

        public void SetTms(bool[] states)
        {
            var txBuffer = new byte[states.Length];

            for (var i = 0; i < states.Length; i++)
            {
                txBuffer[i] = (byte)(states[i] ? 1 : 0);
            }

            uint bytesWritten = 0;
            var ftStatus = myFtdiDevice.Write(txBuffer, (uint)states.Length, ref bytesWritten);

            if (ftStatus != FTDI.FT_STATUS.FT_OK || bytesWritten != states.Length)
            {
                throw new Exception("Failed to write TMS states to JTAG interface.");
            }
        }


        public void SetTdi(bool state)
        {
            var buffer = new byte[1];
            uint bytesWritten = 0;
            buffer[0] = (byte)(state ? 0x01 : 0x00);
            var ftStatus = myFtdiDevice.Write(buffer, 1, ref bytesWritten);
            if (ftStatus != FTDI.FT_STATUS.FT_OK || bytesWritten != 1)
            {
                throw new Exception("Failed to write TDI state to JTAG interface.");
            }
        }

        public void SetTdi(bool[] states)
        {
            uint bitCount = (uint)states.Length;
            byte[] buffer = new byte[bitCount / 8 + 1];
            uint byteCount = (bitCount + 7) / 8;

            // Build the buffer.
            for (uint i = 0; i < byteCount; i++)
            {
                byte b = 0;
                for (uint j = 0; j < 8; j++)
                {
                    uint bitIndex = i * 8 + j;
                    if (bitIndex < bitCount && states[bitIndex])
                    {
                        b |= (byte)(1 << (int)j);
                    }
                }
                buffer[i] = b;
            }

            // Send the buffer.
            uint bytesWritten = 0;
            FTDI.FT_STATUS status = myFtdiDevice.Write(buffer, byteCount, ref bytesWritten);
            if (status != FTDI.FT_STATUS.FT_OK || bytesWritten != byteCount)
            {
                throw new InvalidOperationException("Failed to write TDI data.");
            }
        }

        public bool GetTdo()
        {
            // Reset the JTAG state machine
            byte[] buffer = { 1, 0 };
            uint bytesWritten = 0;
            myFtdiDevice.Write(buffer, buffer.Length, ref bytesWritten);

            // write TMS, TDI, and read TDO
            byte[] dataOut = { 0x8F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            byte[] dataIn = new byte[8];
            bytesWritten = 0;
            uint bytesRead = 0;

            // write TMS, TDI, and read TDO
            myFtdiDevice.Write(dataOut, 8, ref bytesWritten);
            myFtdiDevice.Read(dataIn, 8, ref bytesRead);

            // return TDO
            return (dataIn[0] & 0x01) == 0x01;
        }

        public bool[] GetTdo(int bitCount)
        {
            var tdiData = new byte[bitCount];
            var tdoData = new byte[(bitCount + 7) / 8];

            // Set TDI pins as output and TDO pins as input
            uint portDir = TmsTdiMask;
            uint numBytesRead = 0, numBytesWritten = 0;
            const uint tdiMask = TmsTdiMask << TdiPin;
            const uint tdoMask = TmsTdiMask << TdoPin;
            portDir |= tdiMask;
            portDir &= ~tdoMask;
            myFtdiDevice.SetBitMode((byte)portDir, 0x01);

            // Shift in TDI data and shift out TDO data
            myFtdiDevice.Write(tdiData, bitCount, ref numBytesWritten);
            myFtdiDevice.Read(tdoData, (uint)tdoData.Length, ref numBytesRead);

            // Convert TDO data to bool array
            var tdoArray = new bool[bitCount];
            for (var i = 0; i < bitCount; i++)
            {
                var byteIndex = i / 8;
                var bitIndex = i % 8;
                var bitValue = ((tdoData[byteIndex] >> bitIndex) & 0x01) != 0;
                tdoArray[i] = bitValue;
            }

            return tdoArray;
        }

        public void ShiftTmsAndTdi(int bitCount)
        {
            var buffer = new byte[bitCount / 8 + 1];
            uint bytesWritten = 0;

            // Set the direction of TMS and TDI pins as output.
            myFtdiDevice.SetBitMode(TmsTdiMask, FTDI.FT_BIT_MODES.FT_BIT_MODE_SYNC_BITBANG);

            // Shift out TMS and TDI values to the device.
            myFtdiDevice.Write(buffer, bitCount, ref bytesWritten);

            // Set the direction of TMS and TDI pins as input.
            myFtdiDevice.SetBitMode(TmsTdiMask, FTDI.FT_BIT_MODES.FT_BIT_MODE_RESET);
        }

        public void ShiftTmsAndTdi(bool[] tmsStates, bool[] tdiStates)
        {
            if (tmsStates.Length != tdiStates.Length)
            {
                throw new ArgumentException("tmsStates and tdiStates must have the same length.");
            }

            // Calculate total bit count
            var bitCount = tmsStates.Length;

            // Create TMS/TDI buffer
            var tmsTdiBuffer = new byte[(bitCount + 7) / 8];

            // Fill TMS/TDI buffer
            for (var i = 0; i < bitCount; i++)
            {
                if (tmsStates[i])
                {
                    tmsTdiBuffer[i / 8] |= (byte)(0x01 << (i % 8));
                }

                if (tdiStates[i])
                {
                    tmsTdiBuffer[i / 8] |= (byte)(0x02 << (i % 8));
                }

                // Update _lastTmsTdiMask with the TMS and TDI bit values that were shifted out
                myLastTmsTdiMask <<= 1;
                if (tmsStates[i])
                    myLastTmsTdiMask |= 0x01;
                myLastTmsTdiMask <<= 1;
                if (tdiStates[i])
                    myLastTmsTdiMask |= 0x01;
            }

            // Shift TMS/TDI buffer
            uint bytesWritten = 0, bytesReturned = 0;
            var status = myFtdiDevice.Write(tmsTdiBuffer, tmsTdiBuffer.Length, ref bytesWritten);

            if (status != FTDI.FT_STATUS.FT_OK || bytesWritten != tmsTdiBuffer.Length)
            {
                throw new Exception("Failed to Write TMS/TDI buffer.");
            }

            status = myFtdiDevice.Read(tmsTdiBuffer, (uint)tmsTdiBuffer.Length, ref bytesReturned);

            if (status != FTDI.FT_STATUS.FT_OK || bytesReturned != tmsTdiBuffer.Length)
            {
                throw new Exception("Failed to Read TMS/TDI buffer.");
            }
        }

        public void ShiftTmsAndReadTdo(int bitCount, out bool[] tdoStates)
        {
            // Initialize the output TDO states array
            tdoStates = new bool[bitCount];

            // Allocate an array of bytes to hold the TMS and TDI data
            var data = new byte[(bitCount + 7) / 8];

            // Fill the data array with zeros
            Array.Clear(data, 0, data.Length);

            // Set the TMS bits in the data array
            for (var i = 0; i < bitCount; i++)
            {
                data[i / 8] |= (byte)(((TmsTdiMask & (1 << i)) != 0) ? 0x80 : 0x00);
            }

            // Use the FTDI WriteRead method to shift the TMS and TDI data and read the TDO data
            uint bytesWritten = 0, bytesReturned = 0;
            var status = myFtdiDevice.Write(data, data.Length, ref bytesWritten);

            if (status != FTDI.FT_STATUS.FT_OK || bytesWritten != data.Length)
            {
                throw new Exception("Failed to Write TMS/TDI buffer.");
            }

            status = myFtdiDevice.Read(data, (uint)data.Length, ref bytesReturned);

            if (status != FTDI.FT_STATUS.FT_OK || bytesReturned != data.Length)
            {
                throw new Exception("Failed to Read TMS/TDI buffer.");
            }

            // Extract the TDO bits from the data array
            for (var i = 0; i < bitCount; i++)
            {
                tdoStates[i] = ((data[i / 8] & (1 << (i % 8))) != 0);
            }
        }

        public void ShiftTmsAndReadTdo(bool[] tmsStates, out bool[] tdoStates)
        {
            var bitCount = tmsStates.Length;
            tdoStates = new bool[bitCount];

            var buffer = new byte[bitCount];

            for (var i = 0; i < bitCount; i++)
            {
                var val = (byte)((tmsStates[i] ? TmsTdiMask : 0) | (i == bitCount - 1 ? myLastTmsTdiMask : 0));
                buffer[i] = val;
            }

            uint bytesWritten = 0;
            uint bytesRead = 0;

            var status = myFtdiDevice.Write(buffer, bitCount, ref bytesWritten);

            if (status != FTDI.FT_STATUS.FT_OK)
            {
                throw new Exception("Error writing TMS/TDI data to FTDI device");
            }

            status = myFtdiDevice.Read(buffer, (uint)bitCount, ref bytesRead);

            if (status != FTDI.FT_STATUS.FT_OK || bytesRead != bitCount)
            {
                throw new Exception("Error reading TDO data from FTDI device");
            }

            for (var i = 0; i < bitCount; i++)
            {
                tdoStates[i] = (buffer[i] & TdoPin) != 0;
            }
        }

        public void ShiftTmsTdiAndReadTdo(bool[] tmsStates, bool[] tdiStates, out bool[] tdoStates)
        {
            if (tmsStates.Length != tdiStates.Length)
            {
                throw new ArgumentException("Length of TMS and TDI states should be same");
            }

            var bitCount = tmsStates.Length;

            // Calculate number of bytes required for TMS, TDI and TDO data
            var tmsTdiByteCount = (bitCount + 7) / 8; // Rounded up to nearest byte
            var tdoByteCount = (bitCount + 7) / 8; // Rounded up to nearest byte

            // Allocate memory for TMS, TDI and TDO data
            var tmsTdiData = new byte[tmsTdiByteCount];
            var tdoData = new byte[tdoByteCount];

            // Convert TMS and TDI states into bit values in tmsTdiData
            for (var i = 0; i < bitCount; i++)
            {
                if (tmsStates[i])
                {
                    tmsTdiData[i / 8] |= (byte)(1 << (i % 8));
                }

                if (tdiStates[i])
                {
                    tmsTdiData[i / 8] |= (byte)(1 << (i % 8));
                }
            }

            // Send TMS and TDI data and receive TDO data
            var status = WriteAndRead(tmsTdiData, tdoData);
            if (status != FTDI.FT_STATUS.FT_OK)
            {
                throw new Exception("Error reading TDO data from FTDI device");
            }

            // Convert TDO data into bit values in tdoStates
            tdoStates = new bool[bitCount];
            for (var i = 0; i < bitCount; i++)
            {
                if ((tdoData[i / 8] & (1 << (i % 8))) != 0)
                {
                    tdoStates[i] = true;
                }
            }
        }

        [DllImport("FTD2XX.dll")]
        private static extern FTDI.FT_STATUS FT_WriteRead(IntPtr ftHandle, IntPtr pWriteBuffer, uint writeBufferLength, IntPtr pReadBuffer, uint readBufferLength, ref uint pBytesReturned);

        public FTDI.FT_STATUS WriteAndRead(byte[] writeData, byte[] readData)
        {
            // initialize FTDI device handle
            var pWriteData = Marshal.AllocHGlobal(writeData.Length);
            Marshal.Copy(writeData, 0, pWriteData, writeData.Length);

            var pReadData = Marshal.AllocHGlobal(readData.Length);

            uint bytesReturned = 0;
            var status = FT_WriteRead(myFtHandle, pWriteData, (uint)writeData.Length, pReadData, (uint)readData.Length, ref bytesReturned);

            Marshal.Copy(pReadData, readData, 0, readData.Length);

            Marshal.FreeHGlobal(pWriteData);
            Marshal.FreeHGlobal(pReadData);
            return status;
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
}