using FTD2XX_NET;
using System;
using System.Linq;

namespace CustomOnChipDebuggerConsoleApp
{
    public class Ftdi4232HJtag
    {
        private FTDI myFtdiDevice;
        private uint myClockDivider;
        private readonly byte[] myWriteBuffer;
        private readonly byte[] myReadBuffer;
        private readonly IRISCVDMIInterface myRiscvDMIController;
        private readonly JtagController myJtagStateController;
        private uint myDataLength;
        private const int MaxDMIRetryCount = 5;
        private const byte TmsTdiMask = 0xFF;

        public Ftdi4232HJtag()
        {
            myFtdiDevice = new FTDI();
            myClockDivider = 30;
            myDataLength = 8;
            myWriteBuffer = new byte[1];
            myReadBuffer = new byte[1];
            myRiscvDMIController = new RISCVDMIController();
            myJtagStateController = new JtagController(myFtdiDevice);
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

            // Open device and configure for JTAG
            myFtdiDevice.OpenBySerialNumber(device.SerialNumber);
            myFtdiDevice.SetBitMode(TmsTdiMask, FTDI.FT_BIT_MODES.FT_BIT_MODE_RESET);
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

            var isSuccess = false;
            try
            {
                myRiscvDMIController.ConnectToTarget(myFtdiDevice);
                myRiscvDMIController.ResetTarget();
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

        private void SendData(byte[] bytes, int length)
        {

        }

        private byte[] ReadData(int length)
        {

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