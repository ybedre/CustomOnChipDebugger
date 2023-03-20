using FTD2XX_NET;
using System;
using System.IO;

namespace CustomOnChipDebuggerConsoleApp
{
    public class RISCVDMIController : IRISCVDMIInterface
    {
        private FTDI myJtagDevice;

        public enum IRISCVDmiOperationType : byte
        {
            AccessRegister = 0,
            AccessRegisterWrite = 0,
            AccessRegisterRead = 1,
            DMIRead = 0x10,
            DMIWrite = 0x02,
            PostInc = 1,
            ExecBuf = 1
        }

        private enum RegisterSize
        {
            RV32 = 32,
            RV64 = 64,
            RV128 = 128
        }

        private enum DTMRegister : uint
        {
            DMCONTROL = 0x10,
            DMSTATUS = 0x11,
            HARTINFO = 0x12,
            HAWINDOWSEL = 0x13,
            HAWINDOW = 0x14,
            ABSTRACTCS = 0x15,
            ABSTRACTAUTO = 0x16,
            CONF = 0x17,
            NEXTDM = 0x1f
        }

        [Flags]
        private enum DtmControlAndStatusRegister : uint
        {
            DMIRESET = 0x00000001
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
            // Initialize JTAG interface
            ResetTap();
            RunTestIdle(100);

            // Select Data Register (DR)
            SendJtagCommand((byte)IRISCVDmiJtagCommand.SelectDR);

            // Set control signals to capture data
            SendJtagCommand((byte)IRISCVDmiJtagCommand.CaptureDR);

            // Send 0x00000001 to the target device to initiate a soft reset
            SendJtagData(new byte[] { 0x01, 0x00, 0x00, 0x00 }, 32);

            // Set control signals to shift out the data
            SendJtagCommand((byte)IRISCVDmiJtagCommand.ShiftDR);

            // Set control signals to exit the Shift-DR state
            SendJtagCommand((byte)IRISCVDmiJtagCommand.Exit1DR);

            // Check if DMI is available
            var response = ReadJtagData(5);
            if (response[0] != 0x11 || response[1] != 0x10 || response[2] != 0x1 || response[3] != 0x0 || response[4] != 0x1)
            {
                return false;
            }

            return true;
        }

        private byte[] ReadJtagData(int length)
        {
            // Select the DTM IR
            SetTapState(JtagState.SelectDRScan);
            var data = new byte[length];
            uint bytesRead = 0;
            myJtagDevice.Read(data, (uint)data.Length, ref bytesRead);
            if (length != bytesRead)
            {
                throw new Exception("Failed to read JTAG response bytes");
            }
            return data;
        }

        private void SendJtagData(byte[] bytes, int length)
        {
            // Set DMI register address
            SetTapState(JtagState.RunTestIdle);

            // Update DR to start operation
            SetTapState(JtagState.UpdateDR);

            // Capture DR to get results
            SetTapState(JtagState.CaptureDR);

            uint bytesWritten = 0;
            myJtagDevice.Write(bytes, length, ref bytesWritten);
            if (length != bytesWritten)
            {
                throw new Exception("Failed to send JTAG response bytes");
            }
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

        public void SendJtagCommand(byte command)
        {
            var data = new[] { command };
            uint bytesWritten = 0;
            myJtagDevice.Write(data, data.Length, ref bytesWritten);
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
                    SetTapState(JtagState.SelectDRScan);

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

        public uint ReadDTMRegister(uint address)
        {
            // Select the DTM IR
            SetTapState(JtagState.SelectDRScan);

            // Scan in value with op set to 1 and address set to desired register address
            byte[] scanData = { (byte)((byte)IRISCVDmiOperationType.DMIRead | address), 0 };
            SendJtagData(scanData, 64);

            // Update DR to start operation
            SetTapState(JtagState.UpdateDR);

            // Capture DR to get results
            SetTapState(JtagState.CaptureDR);
            SendJtagData(new byte[] { 0x00 }, 1);

            SendJtagCommand((byte)IRISCVDmiJtagCommand.SelectIR);
            SendJtagData(new byte[] { 0x04 }, 4);
            var resultData = ReadJtagData(4);
            // Check if operation completed in time
            if ((resultData[0] & 0x3) == 0x3)
            {
                // Operation didn't complete in time, clear busy condition and retry
                WriteDtmRegister((uint)DTMRegister.DMCONTROL, (uint)DtmControlAndStatusRegister.DMIRESET);
                resultData = DmiScan(address);
            }

            return BitConverter.ToUInt32(resultData, 0);
        }

        public void WriteDtmRegister(uint address, uint data)
        {
            // Set DMI register address
            SetTapState(JtagState.RunTestIdle);

            // Scan in value with op set to 2, address set to desired register address, and data set to desired register data
            byte[] scanData = { (byte)((byte)IRISCVDmiOperationType.DMIWrite | address), (byte)data };
            SendJtagData(scanData, 64);

            // Update DR to start operation
            SetTapState(JtagState.UpdateDR);

            // Capture DR to get results
            SetTapState(JtagState.CaptureDR);
            SendJtagData(new byte[] { 0x00 }, 1);
            var resultData = ReadJtagData(1);

            // Check if operation completed in time
            if ((resultData[0] & 0x3) == 0x3)
            {
                // Operation didn't complete in time, clear busy condition and retry
                WriteDtmRegister((uint)DTMRegister.DMCONTROL, (uint)DtmControlAndStatusRegister.DMIRESET);
                DmiScan(address);
            }
        }

        public byte[] DmiScan(uint address)
        {
            // Select DMI data register
            SetTapState(JtagState.SelectDRScan);
            SendJtagCommand((byte)IRISCVDmiJtagCommand.DMI);
            SetTapState(JtagState.CaptureDR);
            SetTapState(JtagState.ShiftDR);

            // Scan 33 bits to shift in command and address (4-bit command + 29-bit address)
            var dmiData = new byte[5];
            dmiData[0] = (byte)(0x10 | (uint)IRISCVDmiOperationType.DMIRead); // Set DMI read command in the MSB nibble
            dmiData[1] = (byte)(address & 0xFF);
            dmiData[2] = (byte)((address >> 8) & 0xFF);
            dmiData[3] = (byte)((address >> 16) & 0xFF);
            dmiData[4] = 0;
            SendJtagData(dmiData, 33);

            // Shift out 32-bit DMI data
            var readData = new byte[4];
            for (var i = 0; i < 4; i++)
            {
                readData[i] = (byte)ReadDTMRegister(address + (uint)i);
            }

            // Reset TAP to update DMI data register with the read data
            ResetTap();

            return readData;
        }

        public void SetTapState(JtagState state)
        {
            switch (state)
            {
                case JtagState.TestLogicReset:
                    SendJtagCommand((byte)IRISCVDmiJtagCommand.TestLogicReset);
                    break;
                case JtagState.RunTestIdle:
                    RunTestIdle(100);
                    break;
                case JtagState.SelectDRScan:
                    SendJtagCommand((byte)IRISCVDmiJtagCommand.SelectDR);
                    SendJtagData(new byte[] { 0x00 }, 5); // 5-bit DR-select value for Select-DR-Scan state
                    break;
                case JtagState.SelectDR:
                    SendJtagCommand((byte)IRISCVDmiJtagCommand.SelectDR);
                    break;
                case JtagState.CaptureDR:
                    SendJtagCommand((byte)IRISCVDmiJtagCommand.CaptureDR);
                    break;
                case JtagState.ShiftDR:
                    SendJtagCommand((byte)IRISCVDmiJtagCommand.ShiftDR);
                    break;
                case JtagState.Exit1DR:
                    SendJtagCommand((byte)IRISCVDmiJtagCommand.Exit1DR);
                    break;
                case JtagState.PauseDR:
                    SendJtagCommand((byte)IRISCVDmiJtagCommand.PauseDR);
                    break;
                case JtagState.Exit2DR:
                    SendJtagCommand((byte)IRISCVDmiJtagCommand.Exit2DR);
                    break;
                case JtagState.UpdateDR:
                    SendJtagCommand((byte)IRISCVDmiJtagCommand.UpdateDR);
                    break;
                case JtagState.SelectIR:
                    SendJtagCommand((byte)IRISCVDmiJtagCommand.SelectIR);
                    break;
                case JtagState.CaptureIR:
                    SendJtagCommand((byte)IRISCVDmiJtagCommand.CaptureIR);
                    break;
                case JtagState.ShiftIR:
                    SendJtagCommand((byte)IRISCVDmiJtagCommand.ShiftIR);
                    break;
                case JtagState.Exit1IR:
                    SendJtagCommand((byte)IRISCVDmiJtagCommand.Exit1IR);
                    break;
                case JtagState.PauseIR:
                    SendJtagCommand((byte)IRISCVDmiJtagCommand.PauseIR);
                    break;
                case JtagState.Exit2IR:
                    SendJtagCommand((byte)IRISCVDmiJtagCommand.Exit2IR);
                    break;
                case JtagState.UpdateIR:
                    SendJtagCommand((byte)IRISCVDmiJtagCommand.UpdateIR);
                    break;
                case JtagState.DMI:
                    SendJtagCommand((byte)IRISCVDmiJtagCommand.DMI);
                    break;
            }
        }

        // Access Register command implementation for RISCV32 with FTDI4232 JTAG
        public bool AccessRegister(IRISCVDmiOperationType operation, int regNumber,
            IRISCVDmiOperationType accessOperationType, uint transfer, uint aarpostincrement, uint postexec,
            byte[] data, out int cmderr)
        {
            // Initialize variables
            cmderr = 0;
            var scanData = new uint[2];
            var responseBits = new uint[1];

            // Construct scan data based on the command
            if (operation.Equals(IRISCVDmiOperationType.AccessRegister))
            {
                scanData[0] = (uint)operation << 24 | (uint)RegisterSize.RV32 << 20 | aarpostincrement << 19 |
                              postexec << 18 | transfer << 17 | (uint)accessOperationType << 16 | (uint)regNumber;
                scanData[1] = accessOperationType.Equals(IRISCVDmiOperationType.AccessRegisterRead) ? (uint)0 : data[0];
            }
            else
            {
                // Invalid command
                cmderr = 1;
                return false;
            }

            // Send the JTAG data and receive the response
            var length = accessOperationType.Equals(IRISCVDmiOperationType.AccessRegisterWrite)
                ? 33
                : 32; // 32 for read, 33 for write
            SendJtagData(ConvertToByteArray(scanData), length);
            var responseBytes = ReadJtagData(length);
            responseBits[0] = ConvertByteArrayToUInt32(responseBytes);
            BitConverter.ToUInt32(responseBytes, 0);

            // Check cmderr
            cmderr = (int)((responseBits[0] >> 8) & 0x7);
            if (cmderr == 3)
            {
                Console.WriteLine($"Requested register {regNumber} does not exist");
                return false;
            }

            // Handle the response data if it's a read command
            if (accessOperationType.Equals(IRISCVDmiOperationType.AccessRegisterRead) && transfer == 1)
            {
                data[0] = (byte)responseBits[0];
            }

            return true;
        }

        public byte[] ConvertToByteArray(uint[] data)
        {
            byte[] result = new byte[data.Length * 4];
            for (int i = 0; i < data.Length; i++)
            {
                byte[] bytes = BitConverter.GetBytes(data[i]);
                Buffer.BlockCopy(bytes, 0, result, i * 4, 4);
            }
            return result;
        }

        public uint ConvertByteArrayToUInt32(byte[] data)
        {
            uint result = BitConverter.ToUInt32(data, 0);
            return result;
        }

    }
}