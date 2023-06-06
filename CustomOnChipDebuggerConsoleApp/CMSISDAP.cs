using LibUsbDotNet;
using LibUsbDotNet.Descriptors;
using LibUsbDotNet.Main;
using System;
using System.Linq;
using static LibUsbDotNet.Main.UsbTransferQueue;

namespace CMSISDAP
{
    public interface IJtagAdapter
    {
        bool Initialize();
        void SetClockFrequency(int frequency);
        bool SetTargetSEL(int targetSEL);
        bool ResetTarget();
        bool WriteRegister(int registerAddress, int registerValue);
        bool ReadRegister(int registerAddress, out int registerValue);
    }

    public class CMSISDAPAdapter : IJtagAdapter
    {
        private const int VID = 0x1FC9; // Replace with the VID of the CMSIS-DAP device
        private const int PID = 0x0081; // Replace with the PID of the CMSIS-DAP device
        private const int INTERFACE_NUMBER = 0;
        private const int ENDPOINT_ADDRESS_IN = 0x81;
        private const int ENDPOINT_ADDRESS_OUT = 0x01;
        private const byte CMD_DAP_Info = 0x00;
        private const byte CMD_DAP_ReadIDCODE = 0x06;
        private const byte CMD_DAP_Connect = 0x0D;
        private const byte CMD_DAP_Disconnect = 0x0F;
        private const byte DAP_INFO_VENDOR_IDX = 0x01;
        private const byte DAP_INFO_PRODUCT_IDX = 0x02;
        private const byte DAP_INFO_SER_NUM_IDX = 0x03;
        private const byte DAP_INFO_FW_VER_IDX = 0x04;

        private byte _targetSEL;
        private IUsbDevice myUSBDevice;

        public CMSISDAPAdapter()
        {
            var device = UsbDevice.AllDevices.ToList().Find(x => x.Vid.Equals(VID) && x.Pid.Equals(PID) && x.Device != null).Device;
            if (device == null && device is IUsbDevice)
            {
                Console.WriteLine("Device not found.");
                return;
            }
            myUSBDevice = (IUsbDevice)device;

            if (!myUSBDevice.Open())
            {
                Console.WriteLine("Device failed to open");
            }

            if (!myUSBDevice.GetConfiguration(out var config))
            {
                Console.WriteLine("Device failed to GetConfiguration");
            }
            if (!myUSBDevice.GetLangIDs(out var langIDs))
            {

            }
            if (!myUSBDevice.ClaimInterface(INTERFACE_NUMBER))
            {
                Console.WriteLine("Device failed to ClaimInterface");
            }

            byte[] buffer = new byte[64];

            // Send JTAG command to read IDCODE
            buffer[0] = 0xE0; // Command byte
            buffer[1] = 0x01; // IR length
            buffer[2] = 0x00; // DR length
            buffer[3] = 0x00; // DR length
            buffer[4] = 0x00; // DR length
            var usbSetupPacket = new UsbSetupPacket((byte)(UsbEndpointDirection.EndpointIn) | (byte)UsbRequestRecipient.RecipDevice | (byte)UsbRequestType.TypeVendor, 0, 0, 0, 1);
            if (!myUSBDevice.ControlTransfer(ref usbSetupPacket, buffer, buffer.Length, out var bytesWritten))
            {
                Console.WriteLine("Device failed to ControlTransfer to Send JTAG command to read IDCODE");
            }

            // Read IDCODE
            buffer[0] = 0x81; // Command byte

            //if (!myUSBDevice.ControlTransfer(ref usbSetupPacket, buffer, buffer.Length, out var bytesRead))
            {
                Console.WriteLine("Device failed to ControlTransfer to  read IDCODE");
            }

            Console.WriteLine("IDCODE: 0x" + BitConverter.ToString(buffer, 1, 4).Replace("-", ""));
            var deviceInfo = GetDeviceInfo();
            Console.WriteLine("DeviceInfo: 0x" + BitConverter.ToString(deviceInfo, 0, deviceInfo.Length).Replace("-", ""));
        }
                
        private byte[] GetDeviceInfo()
        {
            // Send the DAP_Info command
            byte[] command = { CMD_DAP_Info };
            byte[] response = SendCommand(command);

            if (response.Length == 4)
            {
                // The response contains four bytes of device information
                return response;
            }
            else
            {
                // The command failed
                return null;
            }
        }

        public void SetClockFrequency(int frequency)
        {

        }

        bool IJtagAdapter.Initialize()
        {
            throw new NotImplementedException();
        }

        bool IJtagAdapter.SetTargetSEL(int targetSEL)
        {
            // Send the DAP_Connect command with the new target SEL value
            byte[] command = { CMD_DAP_Connect, (byte)targetSEL };
            byte[] response = SendCommand(command);

            if (response.Length == 1 && response[0] == 0)
            {
                // The command was successful
                _targetSEL = (byte)targetSEL;
                return true;
            }
            else
            {
                // The command failed
                return false;
            }
        }

        bool IJtagAdapter.ResetTarget()
        {
            throw new NotImplementedException();
        }

        bool IJtagAdapter.WriteRegister(int registerAddress, int registerValue)
        {
            throw new NotImplementedException();
        }

        bool IJtagAdapter.ReadRegister(int registerAddress, out int registerValue)
        {
            throw new NotImplementedException();
        }

        private byte[] SendCommand(byte[] command)
        {
            byte[] response = new byte[64];

            if (myUSBDevice != null)
            {
                var outUsbSetupPacket = new UsbSetupPacket((byte)(UsbCtrlFlags.Direction_Out | UsbCtrlFlags.RequestType_Class | UsbCtrlFlags.Recipient_Interface),
                                            0, 0, 0, 0);
                myUSBDevice.ControlTransfer(ref outUsbSetupPacket, command, command.Length, out var bytesWritten);

                var inUsbSetupPacket = new UsbSetupPacket((byte)(UsbCtrlFlags.Direction_In | UsbCtrlFlags.RequestType_Class | UsbCtrlFlags.Recipient_Interface),
                                            0, 0, 0, 0);
                myUSBDevice.ControlTransfer(ref inUsbSetupPacket, response, response.Length, out var bytesRead);

                byte[] trimmedResponse = new byte[bytesRead];
                Array.Copy(response, 0, trimmedResponse, 0, bytesRead);
                return trimmedResponse;
            }
            else
            {
                throw new Exception("USB device not found");
            }
        }

        public void Dispose()
        {
            myUSBDevice.ReleaseInterface(INTERFACE_NUMBER);
            myUSBDevice.Close();
        }
    }
}