using LibUsbDotNet;
using LibUsbDotNet.Main;

class Program
{
    private static UsbEndpointWriter writeEndpoint;
    private static UsbEndpointReader readEnpoint;
    private const uint INFO_ID_CAPS = 0xF0;
    private const uint CMD_DAP_INFO = 0x00;
    private const uint VID = 0x1366; // Replace with the VID of the CMSIS-DAP device
    private const uint PID = 0x1061; // Replace with the PID of the CMSIS-DAP device
    public static readonly int TRANFER_MAX_OUTSTANDING_IO = 3;
    public static readonly int TRANSFER_COUNT = 30;
    public static int TRANFER_SIZE = 64;

    enum Commands : byte
    {
        /// <summary>
        /// FlashDataRead 0x01
        /// </summary>
        FlashDataRead = 0x01,
        /// <summary>
        /// FlashDataWrite 0x02
        /// </summary>
        FlashDataWrite = 0x02,
        /// <summary>
        /// FlashDataInit 0x03
        /// </summary>
        FlashDataInit = 0x03,
        /// <summary>
        /// FlashDataDeInit 0x04
        /// </summary>
        FlashDataDeInit = 0x04,
        /// <summary>
        /// FlashDataStatus 0x05
        /// </summary>
        FlashDataStatus = 0x05,
        /// <summary>
        /// FlashDataErase 0x06
        /// </summary>
        FlashDataErase = 0x06,
        /// <summary>
        /// SPI 0x07
        /// </summary>
        SPI = 0x07,
        /// <summary>
        /// Version 0x08
        /// </summary>
        Version = 0x08,
        /// <summary>
        /// JTAG 0x09
        /// </summary>
        JTAG = 0x09,
        /// <summary>
        /// POSTInit 0xA
        /// </summary>
        POSTInit = 0xA,
        /// <summary>
        /// POSTGet 0xB
        /// </summary>
        POSTGet = 0xB,
        /// <summary>
        /// PowerUp 0x10
        /// </summary>
        PowerUp = 0x10,
        /// <summary>
        /// ShutDown 0x11
        /// </summary>
        ShutDown = 0x11,
        /// <summary>
        /// ISD_1 0x30
        /// </summary>
        ISD_1 = 0x30,
        /// <summary>
        /// ISD_2 0x31
        /// </summary>
        ISD_2 = 0x31,
        /// <summary>
        /// XSVF_1 0x2E
        /// </summary>
        XSVF_1 = 0x2E,
        /// <summary>
        /// XSVF_2 0x2F
        /// </summary>
        XSVF_2 = 0x2F,
        /// <summary>
        /// Update 0xF0
        /// </summary>
        Update = 0xF0
    }
    public enum Errors
    {
        None,
        Unknown,
        DeviceInUse,
        DeviceNotFound,
        WrongVersion,
        NoFlashConfig,
        FailedGetVersion,
        FailedGetConfig,
        WrongConfig,
        WrongHeader,
        GeneralError,
        NoFile,
        WrongFile
    }

    static void Main(string[] args)
    {
        //using (var context = new UsbContext())
        {
            //context.SetDebugLevel(LogLevel.Info);

            var allDevices = UsbDevice.AllLibUsbDevices;

            //Narrow down the device by vendor and pid
            var succes = allDevices.ToList().FirstOrDefault(x=>x.Vid==(int)VID && x.Pid == (int)PID).Open(out var device);
            if (succes && device != null && device is IUsbDevice)
            {
                var selectedDevice = (IUsbDevice)device;
                //Open the device
                if(!selectedDevice.Open())
                {
                    Console.WriteLine("Device failed to open");
                }
                if (!selectedDevice.SetConfiguration(1))
                {
                    Console.WriteLine("Device failed to SetConfiguration");
                }
                //Get the first config number of the interface
                if (!selectedDevice.ClaimInterface(4))
                {
                    Console.WriteLine("Device failed to ClaimInterface");
                }
                //Open up the endpoints
                var writeEndpoint = selectedDevice.OpenEndpointWriter(WriteEndpointID.Ep01);
                var readEnpoint = selectedDevice.OpenEndpointReader(ReadEndpointID.Ep01);

                byte[] cmd = new byte[2] 
                {
                        0xc7,
                        0xfe
                };
                var errorCode = writeEndpoint.Write(cmd, 1000, out var transLength);
                byte[] readBuffer = new byte[4];
                errorCode = readEnpoint.Read(readBuffer, 1000, out var receivedLenghth);
            }
        }
    }

    private bool DeInit(UsbDevice dev)
    {
        int lengthTransfered = 0x10;
        byte[] buffer = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

        UsbSetupPacket packet = new UsbSetupPacket();

        packet.RequestType = (byte)UsbRequestType.TypeVendor;
        packet.Value = 0x00;
        packet.Index = 0x00;
        packet.Length = 0x0;
        packet.Request = (byte)Commands.FlashDataDeInit;
        return dev.ControlTransfer(ref packet, buffer, 8, out lengthTransfered);
    }

    private static Errors xsvfwrite(IUsbDevice MyUsbDevice)
    {
        try
        {
            if (MyUsbDevice == null) return Errors.DeviceNotFound;

            UsbSetupPacket packet = new UsbSetupPacket();
            packet.RequestType = (byte)UsbRequestType.TypeVendor;
            UsbEndpointReader reader = MyUsbDevice.OpenEndpointReader(ReadEndpointID.Ep02);
            UsbEndpointWriter writer = MyUsbDevice.OpenEndpointWriter(WriteEndpointID.Ep05);
            byte[] readBuffer = new byte[4];
            int bytesRead;


            packet.Value = 0x00;
            packet.Index = 0x00;
            packet.Length = 0x8;
            packet.Request = (byte)Commands.Version;
            int hello = 0x10;
            byte[] buffer = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

            buffer[4] = 0x4;
            var success = MyUsbDevice.ControlTransfer(ref packet, buffer, 8, out hello);
            var ec = reader.Read(readBuffer, 1000, out bytesRead);
            if (readBuffer[0] != 0x03)
            {
                Console.WriteLine("Wrong Arm Version");
                return Errors.WrongVersion;
            }

            MyUsbDevice.ControlTransfer(ref packet, buffer, 8, out hello);
            ec = reader.Read(readBuffer, 1000, out bytesRead);

            MyUsbDevice.ControlTransfer(ref packet, buffer, 8, out hello);
            ec = reader.Read(readBuffer, 1000, out bytesRead);
            buffer[4] = 0x0;

            buffer[4] = 0x0;
            buffer[5] = 0x0;
            buffer[6] = 0x0;
            buffer[7] = 0x0;
            packet.Request = (byte)Commands.JTAG;
            MyUsbDevice.ControlTransfer(ref packet, buffer, 8, out hello);
            ec = reader.Read(readBuffer, 1000, out bytesRead);

            buffer[4] = 0x4;
            packet.Request = (byte)Commands.FlashDataStatus;
            MyUsbDevice.ControlTransfer(ref packet, buffer, 8, out hello);
            ec = reader.Read(readBuffer, 1000, out bytesRead);

            Array.Reverse(readBuffer);
            return Errors.None;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
        return Errors.None;
    }
}