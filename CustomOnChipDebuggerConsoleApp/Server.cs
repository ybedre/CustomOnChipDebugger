using LibUsbDotNet.Main;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IUsbDevice = LibUsbDotNet.IUsbDevice;
using UsbDevice = LibUsbDotNet.UsbDevice;

namespace CustomOnChipDebuggerConsoleApp
{
    internal class Server
    {
        static async Task Main(string[] args)
        {
            //var readWriter = new ReadWriteAsync();
            var debugTarget = new DebugTarget();
            var server = new GDBNetworkServer(debugTarget, 11000);

            await Task.Run(() => server.StartServer());
        }
    }

    internal class ReadWriteAsync
    {
        public static UsbDevice MyUsbDevice;

        #region SET YOUR USB Vendor and Product ID!

        public LibUsbDotNet.Main.UsbDeviceFinder MyUsbFinder { get; set; }

        #endregion

        public ReadWriteAsync()
        {
            ErrorCode ec = ErrorCode.Success;
            try
            {
                // Find and open the usb device.
                MyUsbFinder = new UsbDeviceFinder(0x1366, 0x0101);
                MyUsbDevice = UsbDevice.AllDevices.Find(x=>x.Device is IUsbDevice).Device;
                // If the device is open and ready
                if (MyUsbDevice == null)
                {
                    throw new Exception("Device Not Found.");
                }

                // If this is a "whole" usb device (libusb-win32, linux libusb)
                // it will have an IUsbDevice interface. If not (WinUSB) the 
                // variable will be null indicating this is an interface of a 
                // device.
                if (MyUsbDevice is IUsbDevice wholeUsbDevice)
                {
                    // This is a "whole" USB device. Before it can be used, 
                    // the desired configuration and interface must be selected.

                    // Select config #1
                    wholeUsbDevice.SetConfiguration(1);

                    //// Claim interface #0.
                    wholeUsbDevice.ClaimInterface(0);
                }

                // open read endpoint 1.
                var reader = MyUsbDevice.OpenEndpointReader(ReadEndpointID.Ep01);

                // open write endpoint 1.
                var writer = MyUsbDevice.OpenEndpointWriter(WriteEndpointID.Ep01);

                // the write test data.
                string testWriteString = "ABCDEFGH";
                ErrorCode ecWrite;
                ErrorCode ecRead;
                int transferredOut;
                int transferredIn;
                UsbTransfer usbWriteTransfer;
                UsbTransfer usbReadTransfer;
                byte[] bytesToSend = Encoding.Default.GetBytes(testWriteString);
                byte[] readBuffer = new byte[1024];
                int testCount = 0;
                do
                {
                    // Create and submit transfer
                    ecRead = reader.SubmitAsyncTransfer(readBuffer, 0, readBuffer.Length, 100, out usbReadTransfer);
                    if (ecRead != ErrorCode.Success) throw new Exception("Submit Async Read Failed.");

                    ecWrite = writer.SubmitAsyncTransfer(bytesToSend, 0, bytesToSend.Length, 100, out usbWriteTransfer);
                    if (ecWrite != ErrorCode.Success)
                    {
                        usbReadTransfer.Dispose();
                        throw new Exception("Submit Async Write Failed.");
                    }

                    WaitHandle.WaitAll(
                        new WaitHandle[] { usbWriteTransfer.AsyncWaitHandle, usbReadTransfer.AsyncWaitHandle }, 200,
                        false);
                    if (!usbWriteTransfer.IsCompleted) usbWriteTransfer.Cancel();
                    if (!usbReadTransfer.IsCompleted) usbReadTransfer.Cancel();

                    ecWrite = usbWriteTransfer.Wait(out transferredOut);
                    ecRead = usbReadTransfer.Wait(out transferredIn);

                    usbWriteTransfer.Dispose();
                    usbReadTransfer.Dispose();

                    Console.WriteLine("Read  :{0} ErrorCode:{1}", transferredIn, ecRead);
                    Console.WriteLine("Write :{0} ErrorCode:{1}", transferredOut, ecWrite);
                    Console.WriteLine("Data  :" + Encoding.Default.GetString(readBuffer, 0, transferredIn));
                    testCount++;
                } while (testCount < 5);

                Console.WriteLine("\r\nDone!\r\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine((ec != ErrorCode.Success ? ec + ":" : String.Empty) + ex.Message);
            }
            finally
            {
                if (MyUsbDevice != null)
                {
                    if (MyUsbDevice.IsOpen)
                    {
                        // If this is a "whole" usb device (libusb-win32, linux libusb-1.0)
                        // it exposes an IUsbDevice interface. If not (WinUSB) the 
                        // 'wholeUsbDevice' variable will be null indicating this is 
                        // an interface of a device; it does not require or support 
                        // configuration and interface selection.
                        var usbDevice = MyUsbDevice as IUsbDevice;
                        if (!ReferenceEquals(usbDevice, null))
                        {
                            // Release interface #0.
                            usbDevice.ReleaseInterface(0);
                        }

                        MyUsbDevice.Close();
                    }

                    MyUsbDevice = null;
                }

                // Wait for user input..
                Console.ReadKey();
            }
        }
    }
}