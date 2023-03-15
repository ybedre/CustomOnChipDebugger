using LibUsbDotNet;
using LibUsbDotNet.Main;
using System;
using System.Collections.Generic;
using System.Linq;
using FTD2XX_NET;
using StilSoft.Communication.Ftdi;

namespace CustomOnChipDebuggerConsoleApp
{
    public class RiscvJtagDriver : IRiscvJtagDriver
    {
        private IUsbDevice myDevice;
        private byte[] myReadBuffer = new byte[1024];
        private int myTimeout = 1000;
        private UsbEndpointReader myReader;
        private UsbEndpointWriter myWriter;
        private int myInstructionRegisterLength;
        private int myBypassRegisterLength;
        private int myDataRegisterLength;
        private uint myTargetDeviceID;
        private const int ProductID = 0x0101;
        private const int VendorID = 0x1366;
        private FtdiDevice myFtdiDevice;
        public bool IsOpen { get; private set; }

        public bool IsConfigured { get; private set; }

        public RiscvJtagDriver()
        {
            // Initialize USB device
            FTDI.FT_DEVICE_INFO_NODE[] deviceList= new FTDI.FT_DEVICE_INFO_NODE[1024];
            var ftdi = new FTDI();
            ftdi.GetDeviceList(deviceList);
            ftdi.OpenByIndex(2);
            var usbFinder = new UsbDeviceFinder(ProductID, VendorID);
            var device = UsbDevice.AllDevices.Find(x => x.Device is IUsbDevice).Device;
            if (device is IUsbDevice wholeDevice)
            {
                myDevice = wholeDevice;
                // This is a "whole" USB device. Before it can be used, 
                // the desired configuration and interface must be selected.
                // Select config #1
                myDevice.SetConfiguration(1);

                // Claim interface #0.
                myDevice.ClaimInterface(0);
            }

            // Open endpoints
            myWriter = device.OpenEndpointWriter(WriteEndpointID.Ep01);
            myReader = device.OpenEndpointReader(ReadEndpointID.Ep01);
        }

        public void Open()
        {
            bool success;

            // Open a new instance of the USB device
            // Initialize USB device
            try
            {
                // Initialize USB device
                var usbFinder = new UsbDeviceFinder(ProductID, VendorID);
                var device = UsbDevice.AllDevices.Find(x => x.Device is IUsbDevice).Device;
                if (device is IUsbDevice wholeDevice)
                {
                    myDevice = wholeDevice;
                    // This is a "whole" USB device. Before it can be used, 
                    // the desired configuration and interface must be selected.
                    // Select config #1
                    myDevice.SetConfiguration(1);

                    // Claim interface #0.
                    myDevice.Open();
                    myDevice.ClaimInterface(0);
                }

                // Set interface altsetting
                //myDevice.SetAltInterface(InterfaceAltSetting);

                // Set endpoint direction and transfer type
                myReader = myDevice.OpenEndpointReader(ReadEndpointID.Ep01);
                myWriter = myDevice.OpenEndpointWriter(WriteEndpointID.Ep01);

                // Set the configuration flag to true
                success = true;
            }
            catch (Exception e)
            {
                // Handle any exceptions
                Console.WriteLine("Error opening device: " + e.Message);
                success = false;
            }
            IsOpen = success;
        }


        public void Close()
        {
            ResetTAPController();
            myDevice.Close();
        }

        public void Configure(int instructionRegisterLength, int bypassRegisterLength, int dataRegisterLength)
        {
            myInstructionRegisterLength = instructionRegisterLength;
            myBypassRegisterLength = bypassRegisterLength;
            myDataRegisterLength = dataRegisterLength;

            // Set the JTAG clock frequency to 1MHz
            //SetClockFrequency(1000000);

            // Clear the TMS and TDI lines
            SetTmsTdi(false, false);

            // Reset the JTAG state machine
            ResetStateMachine();

            // Move to the Test Logic Reset state
            GoToTestLogicReset();

            // Shift the BYPASS register to select the debug module
            ShiftRegister(new List<bool>(), new List<bool>(), bypassRegisterLength);

            // Move to the Run Test/Idle state
            GoToRunTestIdle();

            // Shift the IDCODE register to read the target device ID
            List<bool> idcodeData = ShiftRegister(new List<bool>(), new List<bool>(), instructionRegisterLength + dataRegisterLength);
            int numFillBits = instructionRegisterLength + bypassRegisterLength + dataRegisterLength - 32;
            myTargetDeviceID = (uint)ConvertData(idcodeData.GetRange(instructionRegisterLength, dataRegisterLength));

            // Set the target device ID
            SetTargetDeviceId(myTargetDeviceID, numFillBits);

            // Set the interface configuration to Interface #0, Alternate Setting #0
            SetInterfaceConfiguration(0, 0);
        }

        //public void SetInterfaceConfiguration(int interfaceSpeedHz, int interfaceConfig)
        //{

        //}

        public void SetInterfaceConfiguration(int configurationValue, int interfaceValue)
        {
            var result = myDevice.SetConfiguration((byte)configurationValue);
            if (!result)
            {
                throw new Exception($"Error setting configuration(ec={UsbDevice.LastErrorNumber}:{UsbDevice.LastErrorString}");
            }

            result = myDevice.ClaimInterface(interfaceValue);
            if (!result)
            {
                throw new Exception($"Error setting configuration(ec={UsbDevice.LastErrorNumber}:{UsbDevice.LastErrorString}");
            }
        }

        public uint ConvertData(List<bool> values)
        {
            if (values.Count > 32)
            {
                throw new ArgumentException("The number of bits cannot be greater than 32.");
            }

            uint result = 0;
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i])
                {
                    result |= (uint)(1 << i);
                }
            }

            return result;
        }

        public void SetTargetDeviceId(uint idcode, int numFillBits)
        {
            // Set up the JTAG IR to select the IDCODE register
            ShiftIR(JtagCommands.SelectDRScan);

            // Write the IDCODE to the DR
            ShiftDR(new List<bool>(), ConvertUintToBoolList(idcode, 32), 32);

            // Shift in 0s to fill up any remaining bits
            if (numFillBits > 0)
            {
                ShiftDR(new List<bool>(), new List<bool>(numFillBits), numFillBits);
            }

            // Set the JTAG IR back to the instruction register
            ShiftIR(JtagCommands.SelectIRScan);
        }

        public static List<bool> ConvertUintToBoolList(uint value, int numBits)
        {
            var boolList = new List<bool>();
            for (int i = 0; i < numBits; i++)
            {
                boolList.Add(((value >> i) & 0x01) != 0);
            }
            return boolList;
        }

        public void GoToRunTestIdle()
        {
            ShiftIR(JtagCommands.TestLogicReset);

            // Shift in 0x1 to enter the Run-Test/Idle state
            ShiftDR(new List<bool>() { false }, new List<bool>() { true }, 1);

            ShiftIR(JtagCommands.RunTestIdle);

            // Shift in 0x0 to exit the Run-Test/Idle state
            ShiftDR(new List<bool>() { false }, new List<bool>() { false }, 1);

            ShiftIR(JtagCommands.TestLogicReset);
        }

        public void ShiftDR(List<bool> data1, List<bool> data2, int numBits)
        {
            // Ensure that both data lists have the same length
            if (data1.Count != data2.Count)
            {
                throw new ArgumentException("Data lists must have the same length");
            }

            // Send the JTAG Shift-DR command
            ShiftIR(JtagCommands.ShiftDR);

            // Shift the data through the JTAG interface
            for (int i = 0; i < numBits; i++)
            {
                JtagIoFlags flags = JtagIoFlags.None;
                if (i == numBits - 1)
                {
                    // Set the LAST flag on the last bit
                    flags |= JtagIoFlags.LAST;
                }

                // Set the TDI and TMS values based on the data lists
                bool tdi = (i < data1.Count) ? data1[i] : false;
                bool tms = (i < data2.Count) ? data2[i] : false;

                if (tdi)
                {
                    flags |= JtagIoFlags.TDI;
                }

                if (tms)
                {
                    flags |= JtagIoFlags.TMS;
                }

                // Send the JTAG I/O command with the appropriate flags
                JtagIo(flags);
            }

            // Go back to the Run-Test/Idle state
            GoToRunTestIdle();
        }

        public void JtagIo(JtagIoFlags flag)
        {
            byte requestType = (byte)(UsbEndpointDirection.EndpointOut) | ((byte)UsbRequestType.TypeVendor) | ((byte)UsbRequestRecipient.RecipInterface);
            byte request = (byte)JtagCommands.JtagIO;
            int transfered;
            byte[] buffer = new byte[1];
            var setupPacket = new UsbSetupPacket(requestType, request, (ushort)(buffer[0] << 8), 0, 5000);
            buffer[0] = (byte)flag;

            // Send the JTAG IO command using libusb_control_transfer
            bool result = myDevice.ControlTransfer(ref setupPacket,
                buffer,
                (ushort)buffer.Length,
                out transfered);

            if (!result || transfered != buffer.Length)
            {
                throw new Exception("Failed to send JTAG IO command");
            }
        }


        public void ShiftIR(JtagCommands command)
        {
            // Set TMS high and TDI low
            SetTmsTdi(true, false);

            // Shift in the JTAG IDCODE command (MSB first)
            for (int i = 31; i >= 0; i--)
            {
                // Set TMS high if this is the last bit, low otherwise
                JtagIoFlags flags = (i == 0) ? JtagIoFlags.TMS | JtagIoFlags.LAST : 0;

                // Set TDI to the current bit of the JTAG IDCODE command
                bool tdi = (((uint)command >> i) & 1) != 0 ? true : false;
                SetTmsTdi(flags.HasFlag(JtagIoFlags.TMS), tdi);

                // Toggle clock
                //SetClockFrequency(1000000);
                SetTmsTdi(flags.HasFlag(JtagIoFlags.TMS), tdi);
            }

            // Go to Run-Test/Idle
            GoToRunTestIdle();
        }


        public List<bool> ShiftRegister(List<bool> tmsValues, List<bool> tdiValues, int registerLength)
        {
            if (tmsValues.Count != tdiValues.Count)
                throw new ArgumentException("TMS and TDI lists must have same length");
            List<bool> outputBits = new List<bool>();
            int numBits = tmsValues.Count;
            for (int i = 0; i < numBits; i++)
            {
                bool tms = tmsValues[i];
                bool tdi = tdiValues[i];

                byte requestType = (byte)(UsbEndpointDirection.EndpointOut) | ((byte)UsbRequestType.TypeClass) | ((byte)UsbRequestRecipient.RecipInterface);
                byte request = (byte)JtagCommands.ShiftDR;
                short value = (short)(tdi ? JtagIoDirections.Input : JtagIoDirections.Output);
                short index = 0; // Interface number
                byte[] buffer = new byte[] { (byte)((tms ? JtagIoFlags.TMS : 0) | (i == numBits - 1 ? JtagIoFlags.LAST : 0)) };
                var setupPacket = new UsbSetupPacket(requestType, request, value, index, 0);
                int bytesTransferred;
                var success = myDevice.ControlTransfer(ref setupPacket, buffer, buffer.Length, out bytesTransferred);
                if (success || bytesTransferred != buffer.Length)
                    throw new Exception("ControlTransfer failed");

                buffer = new byte[1];
                success = myDevice.ControlTransfer(ref setupPacket, buffer, buffer.Length, out bytesTransferred);
                if (success || bytesTransferred != buffer.Length)
                    throw new Exception("ControlTransfer failed");

                bool tdo = (buffer[0] & 0x01) != 0;
                if (i < registerLength)
                    tdiValues[i] = tdo;

                // Parse returned data and extract output bits
                foreach (byte b in buffer)
                {
                    for (int j = 0; j < 8; j++)
                    {
                        if (outputBits.Count < numBits)
                        {
                            outputBits.Add((b & (1 << i)) != 0);
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
            return outputBits;
        }

        public void SetTmsTdi(bool tms, bool tdi)
        {
            byte tms_tdi = 0;

            if (tms)
                tms_tdi |= 0x01;

            if (tdi)
                tms_tdi |= 0x02;

            byte[] data = { tms_tdi };
            int transferred;
            var setupPacket = new UsbSetupPacket((byte)(((byte)UsbEndpointDirection.EndpointOut) | ((byte)UsbRequestType.TypeClass) | ((byte)UsbRequestRecipient.RecipInterface)),
                                            0x0B, // HID SET_REPORT request
                                            (ushort)(0x0300 + 0x01), // Report ID = 1, Output report
                                            0x00, // Interface number
                                            (ushort)data.Length);
            bool success = myDevice.ControlTransfer(ref setupPacket, // Length of buffer
                                            data,
                                            data.Length,
                                            out transferred); // Timeout in milliseconds

            if (!success || transferred != data.Length)
            {
                throw new Exception($"Failed to set TMS_TDI to {tms_tdi} (ec={UsbDevice.LastErrorNumber}:{UsbDevice.LastErrorString}, transferred={transferred})");
            }
        }

        public void ResetStateMachine()
        {
            // TMS = 1, TDI = 0
            SetTmsTdi(true, false);
            // send 5 clock cycles to reset TAP controller
            for (int i = 0; i < 5; i++)
            {
                SetTmsTdi(true, false);
                SetTmsTdi(false, false);
            }
            // TMS = 1, TDI = 1
            SetTmsTdi(true, true);
            // send 1 clock cycle to enter Test-Logic-Reset state
            SetTmsTdi(false, false);
        }

        public void GoToTestLogicReset()
        {
            // Send 5 TCK pulses with TMS=1 and TDI=0 to enter Test Logic Reset state
            SetTmsTdi(true, false);
            for (int i = 0; i < 5; i++)
            {
                SetTmsTdi(true, false);
                SetTmsTdi(false, false);
            }

            // Send 1 TCK pulse with TMS=1 and TDI=0 to enter Run-Test/Idle state
            SetTmsTdi(true, false);
            SetTmsTdi(false, false);
        }


        public void ClearTAPController()
        {
            // Go to Test Logic Reset state
            GoToTestLogicReset();

            // Shift in all 1's to reset the TAP controller
            List<bool> allOnes = Enumerable.Repeat(true, 64).ToList();
            ShiftDR(allOnes, new List<bool>(), 64);

            // Go to Run Test/Idle state
            GoToRunTestIdle();
        }


        public void ResetTAPController()
        {
            // Reset the state machine
            ResetStateMachine();

            // Send 5 TCK cycles with TMS high to put the TAP controller in Test-Logic-Reset state
            SetTmsTdi(true, false);
            ShiftDR(new List<bool>(), new List<bool>(), 5);

            // Send 1 TCK cycle with TMS low to move the TAP controller to Run-Test-Idle state
            SetTmsTdi(false, false);
            ShiftDR(new List<bool>(), new List<bool>(), 1);
        }


        public void ShiftData(bool[] data, int bitLength, bool lastTMS)
        {
            // Initialize the TDI, TMS and TDO lists
            List<bool> tdi = new List<bool>();
            List<bool> tms = new List<bool>();
            List<bool> tdo = new List<bool>();

            // Add the TDI data to the TDI list
            for (int i = 0; i < bitLength; i++)
            {
                tdi.Add(data[i]);
            }

            // Add the TMS data to the TMS list
            tms.Add(lastTMS);

            // Shift the data in/out of the device
            ShiftDR(tdi, tms, bitLength);
        }


        public void ShiftData(byte[] data, int bitLength, bool lastTMS)
        {
            int byteLength = (bitLength + 7) / 8; // round up to nearest byte
            int bitRemainder = bitLength % 8; // remainder bits in last byte
            bool lastByte = (bitRemainder > 0);

            List<bool> tdiList = new List<bool>();
            List<bool> tdoList = new List<bool>();

            // Add TDI and TDO values for each bit in data
            for (int i = 0; i < byteLength; i++)
            {
                byte b = data[i];
                for (int j = 0; j < 8; j++)
                {
                    bool tdi = ((b >> j) & 1) == 1;
                    tdiList.Add(tdi);
                    tdoList.Add(false);
                }
            }

            // Add TDI and TDO values for remaining bits
            if (lastByte)
            {
                byte b = data[byteLength - 1];
                for (int j = 0; j < bitRemainder; j++)
                {
                    bool tdi = ((b >> j) & 1) == 1;
                    tdiList.Add(tdi);
                    tdoList.Add(false);
                }
            }

            // Shift the data
            ShiftDR(tdiList, tdoList, bitLength);

            // Send the last TMS value
            JtagIo(lastTMS ? JtagIoFlags.TMS : JtagIoFlags.None);
        }

        public byte[] ReadData(int bitLength, bool lastTMS)
        {
            // Create an empty buffer to store the data to be read
            int byteLength = (bitLength + 7) / 8;
            byte[] data = new byte[byteLength];

            // Perform the read operation by shifting data out of the target device
            ShiftData((byte[])null, bitLength, lastTMS);

            // Read the data from the TDI/TDO buffer
            int byteCount = bitLength / 8;
            if (bitLength % 8 != 0)
            {
                byteCount++;
            }
            byte[] buffer = new byte[byteCount];
            var setupPacket = new UsbSetupPacket((byte)(((byte)UsbEndpointDirection.EndpointOut) | ((byte)UsbRequestType.TypeClass) | ((byte)UsbRequestRecipient.RecipInterface)),
                                            (byte)JtagCommands.DataRegister,
                                            (ushort)bitLength,
                                            0,
                                            (ushort)data.Length);
            var success = myDevice.ControlTransfer(ref setupPacket,
                                                     buffer,
                                                     byteCount,
                                                     out var bytesRead);
            if (!success || bytesRead < 0)
            {
                // Error occurred while reading data
                throw new InvalidOperationException("Failed to read JTAG data");
            }

            // Convert the buffer data to a bit list
            List<bool> bitList = new List<bool>();
            for (int i = 0; i < bitLength; i++)
            {
                int byteIndex = i / 8;
                int bitIndex = i % 8;
                byte b = buffer[byteIndex];
                bool bitValue = ((b >> bitIndex) & 0x01) != 0;
                bitList.Add(bitValue);
            }

            // Convert the bit list to a byte array
            for (int i = 0; i < byteLength; i++)
            {
                byte b = 0;
                for (int j = 0; j < 8; j++)
                {
                    int bitIndex = i * 8 + j;
                    if (bitIndex >= bitLength)
                    {
                        break;
                    }
                    bool bitValue = bitList[bitIndex];
                    b |= (byte)((bitValue ? 1 : 0) << j);
                }
                data[i] = b;
            }

            // Return the read data
            return data;
        }

        public void Dispose()
        {
            // Mark the object for garbage collection
            GC.SuppressFinalize(this);
        }

        public void Connect()
        {
            // Set the JTAG clock frequency to the desired value
            //SetClockFrequency(ClockFrequency.MHz10);

            // Reset the TAP controller and go to Test-Logic-Reset state
            ResetTAPController();
            GoToTestLogicReset();

            // Set the TMS and TDI signals to enter the "Shift-IR" state
            SetTmsTdi(true, false);

            // Shift the "IR Length" instruction into the TDI/TDO buffer
            List<bool> irLengthData = ConvertUintToBoolList((uint)myInstructionRegisterLength, 5);
            ShiftDR(irLengthData, new List<bool>(), 5);

            // Set the TMS and TDI signals to enter the "Capture-IR" state
            SetTmsTdi(false, false);

            // Set the TMS and TDI signals to enter the "Shift-IR" state
            SetTmsTdi(true, false);

            // Shift the "IR IDCODE" instruction into the TDI/TDO buffer
            List<bool> irIDCodeData = ConvertUintToBoolList(myTargetDeviceID, 32);
            ShiftDR(irIDCodeData, new List<bool>(), 32);

            // Set the TMS and TDI signals to enter the "Capture-IR" state
            SetTmsTdi(false, false);

            // Set the TMS and TDI signals to enter the "Shift-DR" state
            SetTmsTdi(true, false);

            // Shift the "BYPASS" instruction into the TDI/TDO buffer
            List<bool> bypassData = new List<bool>();
            for (int i = 0; i < myBypassRegisterLength; i++)
            {
                bypassData.Add(false);
            }
            ShiftDR(bypassData, new List<bool>(), myBypassRegisterLength);

            // Set the TMS and TDI signals to enter the "Run-Test/Idle" state
            SetTmsTdi(true, false);
            GoToRunTestIdle();
        }


        //public void SetClockFrequency(int frequency)
        //{
        //    // Calculate clock divisor value based on desired frequency
        //    int divisor = (int)Math.Round(myDevice.Info.MaxClockSpeed / frequency);

        //    // Set clock divisor value using control transfer
        //    var usbSetupPacket = new UsbSetupPacket((byte)UsbRequestType.TypeVendor, (byte)0x01, divisor, 0, 0);
        //    myDevice.ControlTransfer(ref usbSetupPacket, null, 0, out var lenghth);
        //}
    }
}