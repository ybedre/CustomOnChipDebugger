using Device.Net;
using LibUsbDotNet;
using LibUsbDotNet.Main;
using System;
using System.Linq;

namespace CustomOnChipDebuggerConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            // Connect to OpenOCD server over USB
            UsbDeviceFinder finder = new UsbDeviceFinder(0x0403, 0x6010);
            IUsbDevice wholeUsbDevice = (IUsbDevice)UsbDevice.OpenUsbDevice(finder);
            var ftdi = new FtdiDevice("210249A061AE");
            if (wholeUsbDevice == null)
            {
                Console.WriteLine("Unable to connect to OpenOCD server.");
                return;
            }

            // Open JTAG interface
            UsbEndpointWriter writer = wholeUsbDevice.OpenEndpointWriter(WriteEndpointID.Ep01);
            UsbEndpointReader reader = wholeUsbDevice.OpenEndpointReader(ReadEndpointID.Ep02);
            writer.Write(new byte[] { 0x4a, 0x54, 0x41, 0x47, 0x20, 0x6d, 0x6f, 0x64, 0x65, 0x20, 0x6a, 0x74, 0x61, 0x67, 0x00 },10000,  out var transferLenght); // JTAG mode

            // Send JTAG command to read IDCODE register at address 0x01
            byte[] command = new byte[] { 0xe0, 0x00, 0x00, 0x01, 0x01 };
            writer.Write(command,10000, out transferLenght);

            // Read response from JTAG interface
            byte[] response = new byte[4];
            reader.Read(response, 1000, out int bytesRead);
            if (bytesRead == 4)
            {
                Console.WriteLine("IDCODE: 0x" + BitConverter.ToString(response).Replace("-", ""));
            }
            else
            {
                Console.WriteLine("Failed to read IDCODE.");
            }

            // Close USB connection
            wholeUsbDevice.Close();
        }
    }

    public class LibUsbJTAG
    {
        private const byte TMS = 0x01;
        private const byte TDI = 0x02;
        private const byte TDO = 0x04;
        private const byte TCK = 0x08;

        private readonly IDevice _device;
        private readonly byte[] _buffer;
        private readonly IUsbDevice myUSBDevice;
        public LibUsbJTAG(int vendorId, int productId)
        {
            // Find the FTDI4232H device with the specified VID/PID
            UsbDeviceFinder MyUsbFinder = new UsbDeviceFinder(0x0403, 0x6011);
            var MyUsbDevice = UsbDevice.OpenUsbDevice(MyUsbFinder);

            if (MyUsbDevice is IUsbDevice usbDevice)
            {
                myUSBDevice= usbDevice;
                // Claim the first interface
                myUSBDevice.GetConfiguration(out var config);
                myUSBDevice.ClaimInterface(0);

                // Configure MPSSE for JTAG operations
                byte[] InitializeMpsse = { 0x8A, 0x97, 0x00, 0x00, 0x00 };
                var usbPacket = new UsbSetupPacket((byte)UsbRequestType.TypeVendor, 0x00, 0x00, 0x00, 0x00);
                myUSBDevice.ControlTransfer(ref usbPacket, InitializeMpsse, InitializeMpsse.Length, out var lenghtTransferred);

                // Enable clock divide by 5
                byte[] ClockDivideBy5 = { 0x86, 0x00 };
                myUSBDevice.ControlTransfer(ref usbPacket, ClockDivideBy5, ClockDivideBy5.Length, out lenghtTransferred);

                // TMS, TDI and TDO pins are all outputs
                byte[] SetIoMode = { 0x80, 0x02, 0x09 };
                myUSBDevice.ControlTransfer(ref usbPacket, SetIoMode, SetIoMode.Length, out lenghtTransferred);

                // Drive TMS high and TDI low
                byte[] WriteTmsTdi = { 0x10, 0x00 };
                myUSBDevice.ControlTransfer(ref usbPacket, WriteTmsTdi, WriteTmsTdi.Length, out lenghtTransferred);

                // Clock TCK for 6 cycles to get into the reset state
                byte[] ClockTck = { 0x8F };
                myUSBDevice.ControlTransfer(ref usbPacket, ClockTck, ClockTck.Length, out lenghtTransferred);
                myUSBDevice.ControlTransfer(ref usbPacket, ClockTck, ClockTck.Length, out lenghtTransferred);
                myUSBDevice.ControlTransfer(ref usbPacket, ClockTck, ClockTck.Length, out lenghtTransferred);
                myUSBDevice.ControlTransfer(ref usbPacket, ClockTck, ClockTck.Length, out lenghtTransferred);
                myUSBDevice.ControlTransfer(ref usbPacket, ClockTck, ClockTck.Length, out lenghtTransferred);
                myUSBDevice.ControlTransfer(ref usbPacket, ClockTck, ClockTck.Length, out lenghtTransferred);

                // Set the configuration for JTAG operation
                byte[] jtagConfig = new byte[] { 0xE0, 0xE0, 0xE0, 0xE0 };
                WriteToEndpoint(0x02, jtagConfig);

                // Enter JTAG mode
                byte[] jtagModeEnter = new byte[] { 0x00, 0x00, 0x00, 0x02 };
                WriteToEndpoint(0x02, jtagModeEnter);

                // Reset the TAP controller
                byte[] jtagTapReset = new byte[] { 0x00, 0x00, 0x00, 0x0C };
                WriteToEndpoint(0x02, jtagTapReset);

                // Perform JTAG operations to read the RISCV32 IDCODE
                byte[] jtagReadIdcode = new byte[] { 0x00, 0x00, 0x00, 0x06 };
                byte[] idcodeResponse = new byte[4];
                JtagShift(jtagReadIdcode, idcodeResponse);

                // Display the RISCV32 IDCODE
                uint idcode = BitConverter.ToUInt32(idcodeResponse.Reverse().ToArray(), 0);
                Console.WriteLine("RISCV32 IDCODE: 0x{0:X8}", idcode);
            }
            else
            {
                Console.WriteLine("FTDI4232H device not found");
                return;
            }            
        }

        public void WriteToEndpoint(byte endpoint, byte[] data)
        {
            var usbSetupPacket = new UsbSetupPacket((byte)UsbRequestType.TypeVendor, 0x0B, 0, endpoint, (ushort)data.Length);
            var ec = myUSBDevice.ControlTransfer(ref usbSetupPacket, data, data.Length, out var bytesWritten);
            if (!ec)
            {
                throw new Exception("USB control transfer error: " + ec);
            }
        }

        public bool JtagShift(byte[] input, byte[] output)
        {
            if (input.Length % 8 != 0 || output.Length % 8 != 0)
            {
                // The input and output arrays must have a length that is a multiple of 8
                return false;
            }

            int inputIndex = 0;
            int outputIndex = 0;

            // Set TMS low to enter the Shift-DR state
            JtagClock(false, true);

            for (int i = 0; i < input.Length; i += 8)
            {
                byte inputByte = 0;
                byte outputByte = 0;

                // Convert the input bits to a byte
                for (int j = 0; j < 8; j++)
                {
                    inputByte |= (byte)((input[inputIndex++] & 0x01) << j);
                }

                // Shift in the input byte while shifting out the output byte
                for (int j = 0; j < 8; j++)
                {
                    // Set TDI to the current bit of the input byte
                    JtagClock((inputByte & 0x01) == 0x01, j == 7);

                    // Read TDO into the current bit of the output byte
                    //outputByte |= (byte)((_jtagPort.InputBits & 0x01) << j);

                    // Shift the input byte to the next bit
                    inputByte >>= 1;
                }

                // Add the output byte to the output array
                for (int j = 0; j < 8; j++)
                {
                    output[outputIndex++] = (byte)((outputByte >> j) & 0x01);
                }
            }

            // Set TMS high to exit the Shift-DR state
            JtagClock(true, false);

            return true;
        }

        private void JtagClock(bool state, bool bit)
        {
            // Set TMS and TDI pins to the given state
            //myUSBDevice.SetPin(myUSBDevice.Pin.TMS, state);
            //myUSBDevice.SetPin(myUSBDevice.Pin.TDI, bit);

            //// Toggle the clock
            //myUSBDevice.SetPin(myUSBDevice.Pin.TCK, true);
            //myUSBDevice.SetPin(myUSBDevice.Pin.TCK, false);
        }

        //public void SetPin(FTDI4232.Pin pin, bool state)
        //{
        //    byte mask = (byte)(1 << (int)pin);
        //    byte value = (byte)(state ? mask : 0);
        //    Write(new byte[] { (byte)(FTDI4232.Cmd.SetDataBitsLowByte | mask), value });
        //}

        public void Write(byte[] data)
        {
            if (data == null || data.Length == 0)
                return;

            // Set the command to set the lower byte of data bits
            const byte SetDataBitsLowByteCmd = 0x80;

            // Send the command and data to the device
            byte[] cmdData = new byte[data.Length + 1];
            cmdData[0] = SetDataBitsLowByteCmd;
            Array.Copy(data, 0, cmdData, 1, data.Length);

            // Send the command and data to the device
            int bytesWritten;
            //ErrorCode errorCode = myUSBDevice..Write(cmdData, cmdData.Length, out bytesWritten);

            //if (errorCode != ErrorCode.Ok)
            //{
            //    // Handle the error here
            //}
        }


    }
}