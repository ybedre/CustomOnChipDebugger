using FTD2XX_NET;
using System;
using System.IO;
using System.Threading;

namespace CustomOnChipDebuggerConsoleApp
{
    public class RISCVDMIController
    {
        private FTDI myJtagDevice;
        private readonly JtagController myJtagStateController;
        private readonly RISCV32RegisterInfo myRISCVRegisterInfo;
        private const int DBusDataSize = 34;
        private const int DBusDataStart = 2;
        private const int DBusAddressStart = 36;
        private const int DBusOpStart = 0;
        private const int DBusOpSize = 2;
        private const uint DBusRegisterAddress = 0x11;
        private const int CmdTypeBitPosition = 24;
        private const int AarSizeBitPosition = 20;
        private const int AarPostIncrementBitPosition = 19;
        private const int PostExecBitPosition = 18;
        private const int TransferBitPosition = 17;
        private const int ReadOrWriteBitPosition = 16;
        private const int RegNumberBitPosition = 0;
        private const int CmdErrBitPosition = 8;
        private const int DtmcsAddress = 0x10;
        private const int DmiOpBit = 0;
        private const int DmiAddressShift = 2;
        private const int DmiResetBit = 12;
        private const int DmiScanLength = 16;
        private const int SelectDmi = 0x08;

        public enum OperationType : uint
        {
            Nop = 0,
            Read = 1,
            Write = 2
        }

        public enum DmiOperationStatus
        {
            Success = 0,
            Failed = 2,
            Busy = 3
        }

        private enum RegisterSize : uint
        {
            RV32 = 2,
            RV64 = 3,
            RV128 = 4
        }

        private enum DTMRegister : uint
        {
            Bypass = 0x00,
            IDCODE = 0x01,
            DTMCS = 0x10,
            DMI = 0x11,
        }

        public enum AbstractCommand
        {
            AccessRegister,
            QuickAccess,
            AccessMemory
        }

        private enum AbstractCommandRegister : uint
        {
            AbstractData0 = 0x04,
            AbstractControlAndStatus = 0x16,
            Command = 0x17
        }

        [Flags]
        private enum DTMControlAndStatusRegister : uint
        {
            DMIRESET = 0x8000
        }

        public RISCVDMIController(JtagController jtagController)
        {
            myJtagStateController = jtagController;
            myRISCVRegisterInfo = new RISCV32RegisterInfo();
        }

        public void ConnectToTarget(FTDI jtagDevice)
        {
            myJtagDevice = jtagDevice;
        }

        public void DisconnectFromTarget()
        {
            throw new NotImplementedException();
        }

        public bool ResetTarget()
        {
            var response = new byte[5];
            // Initialize JTAG interface
            ResetTap();
            RunTestIdle(100);

            // Send 0x00000001 to the target device to initiate a soft reset
            byte[] writeData = new byte[] { 0x07, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            myJtagStateController.TapShiftDRBits(writeData, 32, response);

            // Check if DMI is available
            if (response[0] != 0x11 || response[1] != 0x10 || response[2] != 0x1 || response[3] != 0x0 || response[4] != 0x1)
            {
                return false;
            }

            return true;
        }

        public void ResetTap()
        {
            // Clear TMS and TDI buffers
            var emptyBuffer = new byte[1];
            emptyBuffer[0] = 0x00;
            uint bytesWritten = 0;
            myJtagDevice.Write(emptyBuffer, 1, ref bytesWritten);

            // Send TCK pulses to reset the TAP
            for (var i = 0; i < 6; i++)
            {
                // Set TMS to high (1) for the first five clock cycles
                if (i < 5)
                {
                    emptyBuffer[0] |= 0x02;
                }
                myJtagDevice.Write(emptyBuffer, 1, ref bytesWritten);
                emptyBuffer[0] ^= 0x01;
                myJtagDevice.Write(emptyBuffer, 1, ref bytesWritten);
            }
        }

        public void RunTestIdle(int count)
        {
            if (!myJtagDevice.IsOpen)
            {
                throw new InvalidOperationException("Device is not open.");
            }

            var writeBuffer = new byte[count];
            var readBuffer = new byte[count];
            uint bytesWritten = 0, bytesRead = 0;

            // Set all bits to 1
            for (var i = 0; i < count; i++)
            {
                writeBuffer[i] = 0xFF;
            }

            var status = myJtagDevice.Write(writeBuffer, count, ref bytesWritten);
            if (status != FTDI.FT_STATUS.FT_OK || bytesWritten != count)
            {
                throw new IOException("Error writing data to device.");
            }

            // Add delay of 10000 milliseconds
            Thread.Sleep(10000);

            status = myJtagDevice.Read(readBuffer, (uint)count, ref bytesRead);
            if (status != FTDI.FT_STATUS.FT_OK || bytesRead != count)
            {
                throw new IOException("Error reading data from device.");
            }

            // Check that all bits are set to 1
            for (var i = 0; i < count; i++)
            {
                if (readBuffer[i] != 0xFF)
                {
                    throw new IOException("Error running test idle.");
                }
            }
        }

        public bool IsTargetConnected()
        {
            var isConnected = false;
            try
            {
                // Try scanning for devices
                uint count = 0;
                myJtagDevice.GetNumberOfDevices(ref count);
                if (count == 0)
                {
                    isConnected = false;
                }

                // Check if device is open
                if (myJtagDevice.IsOpen)
                {
                    // Try resetting the TAP state
                    ResetTap();

                    // Try running the Test-Logic-Reset (TLR) state
                    RunTestIdle(100);

                    // Try selecting the DR scan state
                    //SetTapState(JtagState.SelectDRScan);

                    // If all of the above were successful, assume the target is connected
                    isConnected = true;
                }
            }
            catch (Exception)
            {
                isConnected = false;
            }

            return isConnected;
        }

        public bool ReadDMIRegister(uint address, out uint data)
        {
            // Select DMI
            SelectDTMRegister(DTMRegister.DMI);

            // Scan in value with op set to 1 and address set to desired register address
            var dmiOp = (uint)OperationType.Read;
            var regAddress = address; // DMI register address
            var dmiIn = (dmiOp) | (regAddress << 33);
            myJtagStateController.TapShiftDRBits(ConvertToByteArray(new[] { dmiIn }), 34, null);

            // Update-DR to start operation
            myJtagStateController.TapTms(1, 0);
            myJtagStateController.TapTms(0, 0);
            myJtagStateController.TapTms(1, 0);
            myJtagStateController.TapTms(1, 0);

            // Wait for operation to complete
            var dmiData = new byte[] { };
            uint dmiOpStatusValue;
            do
            {
                var dmiRegisterData = new byte[] { };
                // Capture-DR to check status
                myJtagStateController.TapTms(1, 0);
                myJtagStateController.TapTms(0, 0);
                myJtagStateController.TapShiftDRBits(null, 1, dmiRegisterData);
                dmiOpStatusValue = ConvertByteArrayToUInt32(dmiRegisterData) & 0x10;
                if (dmiOpStatusValue == 3)
                {
                    // Operation didn't complete in time, clear busy condition with dmireset
                    SelectDTMRegister(DTMRegister.DTMCS);
                    myJtagStateController.TapShiftDRBits(ConvertToByteArray(new[] { (uint)DTMControlAndStatusRegister.DMIRESET }), 32, null);
                }
            } while (dmiOpStatusValue == 3);

            if (dmiOpStatusValue == 0)
            {
                // Operation completed successfully, capture data
                myJtagStateController.TapTms(1, 0);
                myJtagStateController.TapTms(0, 0);
                myJtagStateController.TapShiftDRBits(new byte[] { }, 32, dmiData);
            }
            else
            {
                // Operation failed, ignore data
                dmiData = null;
            }

            data = dmiData == null ? 404 : (ConvertByteArrayToUInt32(dmiData) >> 2);
            return true;
        }

        private void SelectDTMRegister(DTMRegister address)
        {
            myJtagStateController.TapShiftIr((byte)address);
        }

        public uint AccessRegister(OperationType type, string regName, uint data)
        {
            if(!Enum.TryParse(regName,out RISCV32RegisterInfo.RV32Registers registerNumber))
            {
                throw new IndexOutOfRangeException($"Specified register {regName} is not available in RISCV 32 bit architecture");
            }
            var cmdType = 0 << CmdTypeBitPosition;
            var aarSize = (uint)RegisterSize.RV32 << AarSizeBitPosition;
            var aarPostIncrement = 0 << AarPostIncrementBitPosition;
            var postExec = 0 << PostExecBitPosition;
            var transfer = 1 << TransferBitPosition;
            var readOrWrite = (uint)(type == OperationType.Read ? 0 : 1) << ReadOrWriteBitPosition;
            var regNumberAddress = myRISCVRegisterInfo.RV32RegisterAddressMap[registerNumber] << RegNumberBitPosition;
            var command = (uint)cmdType | aarSize | (uint)aarPostIncrement | (uint)postExec | (uint)transfer | readOrWrite | regNumberAddress;

            uint cmdErr;
            do
            {
                SelectAbstractRegister(AbstractCommandRegister.AbstractControlAndStatus);
                var abstractStatus = new byte[] { };
                myJtagStateController.TapShiftDRBits(new byte[] { }, 32, abstractStatus);
                cmdErr = (ConvertByteArrayToUInt32(abstractStatus) >> CmdErrBitPosition) & 0xFF;
            } while (cmdErr != 0);

            var commandData = new byte[] { };
            SelectAbstractRegister(AbstractCommandRegister.Command);
            myJtagStateController.TapShiftDRBits(ConvertToByteArray(new [] { command }), 32, commandData);
            return 0;
        }

        private void SelectAbstractRegister(AbstractCommandRegister registerAddress)
        {
            myJtagStateController.TapShiftIr((byte)registerAddress);
        }

        public byte[] ConvertToByteArray(uint[] data)
        {
            var result = new byte[data.Length * 4];
            for (var i = 0; i < data.Length; i++)
            {
                var bytes = BitConverter.GetBytes(data[i]);
                Buffer.BlockCopy(bytes, 0, result, i * 4, 4);
            }
            return result;
        }

        public uint ConvertByteArrayToUInt32(byte[] data)
        {
            var result = BitConverter.ToUInt32(data, 0);
            return result;
        }
    }
}