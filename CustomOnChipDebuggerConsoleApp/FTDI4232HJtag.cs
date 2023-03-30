using FTD2XX_NET;
using System;
using System.Collections;
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
        private readonly JtagStateTransitionMachine myJtagStateMachine;
        private const int TdoPin = 1;
        private const int TdiPin = 2;
        public int TckFrequencyHz { get; set; }
        private const int MaxDMIRetryCount = 5;

        public Ftdi4232HJtag()
        {
            myFtdiDevice = new FTDI();
            myClockDivider = 30;
            myDataLength = 8;
            myWriteBuffer = new byte[1];
            myReadBuffer = new byte[1];
            myRiscvDMIController = new RISCVDMIController();
            myJtagStateMachine = new JtagStateTransitionMachine(myFtdiDevice);
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

        public void DmiAccess(uint address, uint data, bool write)
        {
            // Select dmi
            myJtagStateMachine.TransitionToState(JtagState.SelectDRScan);
            myJtagStateMachine.TransitionToState(JtagState.CaptureDR);
            myJtagStateMachine.TransitionToState(JtagState.ShiftDR);

            // Scan in value with op set to 1 or 2 and address and data set to the desired register address and data respectively
            var op = write ? 2u : 1u;
            var scanValue = (op << 31) | (address << 7) | (data & 0x7F);

            for (var i = 0; i < 32; i++)
            {
                var bit = ((scanValue >> i) & 1) == 1;
                myJtagStateMachine.ShiftData(bit, i == 31);
            }

            // Update DR to start operation
            myJtagStateMachine.TransitionToState(JtagState.UpdateDR);

            // Capture DR to capture results
            myJtagStateMachine.TransitionToState(JtagState.CaptureDR);

            // Wait for operation to complete
            uint opStatus = 0;
            int retryCount = 0;
            do
            {
                // Shift in dummy bits until op status is available
                for (int i = 0; i < 32; i++)
                {
                    myJtagStateMachine.ShiftData(false, i == 31);
                }

                // Update DR to check op status
                myJtagStateMachine.TransitionToState(JtagState.UpdateDR);

                // Capture DR to get op status
                myJtagStateMachine.TransitionToState(JtagState.CaptureDR);

                // Read op status from data register
                opStatus = 0;
                for (var i = 0; i < 5; i++)
                {
                    opStatus |= (myJtagStateMachine.ShiftData(false, i == 4) ? (1u << i) : 0u);
                }

                // Increment retry count if operation didn't complete
                if (opStatus == 3)
                {
                    retryCount++;
                    if (retryCount >= MaxDMIRetryCount)
                    {
                        throw new Exception("DMI operation failed: max retry count exceeded");
                    }

                    // Clear busy condition by writing dmireset in dtmcs
                    DmiWrite(0x10u, 0x1u);
                }
            } while (opStatus != 0);

            // Shift out any remaining bits
            myJtagStateMachine.TransitionToState(JtagState.ShiftDR);
            for (int i = 0; i < 32; i++)
            {
                myJtagStateMachine.ShiftData(false, i == 31);
            }

            // Transition to Run-Test/Idle
            myJtagStateMachine.TransitionToState(JtagState.RunTestIdle);
        }

        public uint ReadDebugModuleRegister(uint address)
        {
            uint data = 0;
            const int opRead = 1;
            const int opBusy = 3;
            const int opDone = 0;

            do
            {
                // Select DMI and scan in the operation and address
                myJtagStateMachine.ShiftIR(JtagState.SelectDRScan);
                myJtagStateMachine.ShiftDR(false, 3); // DMI = 0b011
                myJtagStateMachine.ShiftDR(false, 2); // OP = 0b10 (read)
                myJtagStateMachine.ShiftDR(false, 35); // ADDRESS (32 bits)
                myJtagStateMachine.UpdateDR();

                // Start the operation
                myJtagStateMachine.ShiftIR(JtagState.Exit1DR);
                myJtagStateMachine.ShiftDR(false, 1); // TMS = 1
                myJtagStateMachine.UpdateDR();

                // Wait for the operation to complete
                do
                {
                    myJtagStateMachine.ShiftIR(JtagState.UpdateDR);
                    myJtagStateMachine.CaptureDR();
                    data = myJtagStateMachine.ShiftData(false, 32);
                } while ((data & 0x1) == opBusy);

                // Ignore results if operation didn't complete in time
                if ((data & 0x3) != opDone)
                {
                    continue;
                }

                // Capture the results
                myJtagStateMachine.ShiftIR(JtagState.CaptureDR);
                myJtagStateMachine.CaptureDR();
                data = myJtagStateMachine.ShiftData(false, 32);

            } while ((data & 0x3) != opDone);

            return data;
        }

        public uint ReadDebugModuleRegister(uint address)
        {
            // Set TMS to 1 to enter the Update-DR state
            myJtagStateMachine.ShiftTms(true);

            // Set TMS to 0 to enter the Select-DR-Scan state
            myJtagStateMachine.ShiftTms(false);

            // Set TMS to 0 to enter the Capture-DR state
            myJtagStateMachine.ShiftTms(false);

            // Set TMS to 0 to enter the Shift-DR state
            myJtagStateMachine.ShiftTms(false);

            // Scan in the value with op set to 1 and address set to the desired register address
            var tmsValues = new JtagState[] { JtagState.ShiftDR };
            var nextState = new JtagState[] { JtagState.ShiftDR };
            var dataBits = new BitArray(new[] { (int)address });
            for (var i = 0; i < 33; i++)
            {
                var bit = i == 32 || dataBits[i];
                myJtagStateMachine.Shift(bit);
            }

            // Set TMS to 1 to enter the Exit1-DR state
            myJtagStateMachine.ShiftTms(true);

            // Set TMS to 1 to enter the Pause-DR state
            myJtagStateMachine.ShiftTms(true);

            // Set TMS to 0 to enter the Update-DR state
            myJtagStateMachine.ShiftTms(false);

            // Wait for the operation to complete
            while (true)
            {
                myJtagStateMachine.UpdateDR();
                myJtagStateMachine.CaptureDR();

                var op = GetBit(0);

                if (op == false)
                {
                    break;
                }

                myShiftRegister.Clear();
                tmsValues = new [] { JtagState.ShiftDR };
                nextState = new [] { JtagState.ShiftDR };
                myShiftRegister.Shift(false, 0, tmsValues, nextState);
            }

            // Read the captured data
            var data = new byte[4];
            for (var i = 0; i < 32; i++)
            {
                myShiftRegister.Shift(false, 0, tmsValues, nextState);
                var bit = myShiftRegister.GetBit(0);
                data[i / 8] |= (byte)((bit ? 1 : 0) << (i % 8));
            }

            return BitConverter.ToUInt32(data, 0);
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