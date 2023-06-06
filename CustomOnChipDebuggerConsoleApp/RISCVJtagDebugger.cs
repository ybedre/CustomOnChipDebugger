using System;
using System.Text;

namespace CustomOnChipDebuggerConsoleApp
{
    public class RiscvJtagDriver : IRiscvJtagDriver
    {
        public Ftdi4232HJtag myDebuggerDevice { get; private set; }

        public bool IsOpen { get; private set; }

        public bool IsConfigured { get; private set; }

        public RiscvJtagDriver()
        {
            // Initialize device
            myDebuggerDevice = new Ftdi4232HJtag();
        }

        public void Open()
        {
            try
            {
                myDebuggerDevice.Open("FT5NAPCKA");
            }
            catch (Exception exception)
            {
                myDebuggerDevice.Close();
                Console.WriteLine("Couldn't open the FTDI Device -> Closing the device");
                throw;
            }
            finally
            {
                IsOpen = true;
            }
        }

        public void ReadData(string command)
        {
            throw new NotImplementedException();
        }
        
        public void Initialize()
        {
            try
            {
                IsConfigured = false;
                myDebuggerDevice.Initialize();
            }
            catch (Exception exception)
            {
                Console.WriteLine("Initialize Configuration failed");
                throw;
            }
            finally
            {
                IsConfigured = true;
            }
        }

        public bool GetTdo()
        {
            return true;
        }

        public void SetInterfaceConfiguration(int interfaceSpeedHz, int interfaceConfig)
        {
            try
            {
                myDebuggerDevice.SetInterfaceConfiguration(interfaceSpeedHz, interfaceConfig);
                IsConfigured = false;
                myDebuggerDevice.Initialize();
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    $"SetInterfaceConfiguration failed for {interfaceSpeedHz}:Hz for {interfaceConfig} value");
                throw;
            }
            finally
            {
                IsConfigured = true;
            }
        }

        public string WriteData(ushort address, byte value)
        {
            var commandBytes = Encoding.ASCII.GetBytes(address.ToString());
            var tmsStates = new[] { false, true, true, true, true, true, true, true, true, true, true, true, true, true, true, false, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, false };
            var tdiStates = new bool[commandBytes.Length * 8];
            for (var i = 0; i < commandBytes.Length; i++)
            {
                var b = commandBytes[i];
                for (var j = 0; j < 8; j++)
                {
                    tdiStates[i * 8 + j] = ((b >> j) & 0x01) == 0x01;
                }
            }

            //myDebuggerDevice.ShiftTmsTdiAndReadTdo(tmsStates, tdiStates, out var tdoStates);
            bool[] tdoStates=new []{ false };
            var responseBytes = new byte[tdoStates.Length / 8];
            for (var i = 0; i < responseBytes.Length; i++)
            {
                byte b = 0x00;
                for (var j = 0; j < 8; j++)
                {
                    if (tdoStates[i * 8 + j])
                    {
                        b |= (byte)(0x01 << j);
                    }
                }
                responseBytes[i] = b;
            }
            var response = Encoding.ASCII.GetString(responseBytes);
            return response;
        }

        public void Close()
        {
            myDebuggerDevice.Close();
        }

        public void Dispose()
        {
            // Mark the object for garbage collection
            myDebuggerDevice.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}