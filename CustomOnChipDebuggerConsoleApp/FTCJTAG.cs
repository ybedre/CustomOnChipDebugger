using System;
using System.Runtime.InteropServices;
using System.Text;
using static FTD2XX_NET.FTDI;

namespace CustomOnChipDebuggerConsoleApp
{
    public class FTCJTAG
    {
        uint ftStatus = FTC_SUCCESS;
        //uint numHiSpeedDevices = 0; // 32-bit unsigned integer
        byte[] byteHiSpeedDeviceName = new byte[MAX_NUM_DEVICE_NAME_CHARS];
        byte[] byteHiSpeedDeviceChannel = new byte[MAX_NUM_CHANNEL_CHARS];
        string hiSpeedChannel = null;

        uint locationID = 0;
        //UInt32 locationID = 0;

        uint hiSpeedDeviceType = 1;
        //UInt32 hiSpeedDeviceType = 1;

        //uint clk_div = 0;
        uint numBytesReturned = 0;
        private enum HI_SPEED_DEVICE_TYPES// : uint
        {
            FT2232H_DEVICE_TYPE = 1,
            FT4232H_DEVICE_TYPE = 2
        };
        string hiSpeedDeviceName = null;
        IntPtr ftHandle = IntPtr.Zero;
        public string bPin2LowHighState { get; set; }

        private const string App_Title = "FT2232/FT4232 JTAG Device C# .NET Test Application";
        private const string Dll_Version_Label = "FT2232/FT4232 JTAG DLL Version = ";
        private const string Device_Name_Label = "Device Name = ";

        private const uint FTC_SUCCESS = 0;
        private const uint FTC_DEVICE_IN_USE = 27;
        private const uint TEST_LOGIC_STATE = 1;
        private const uint RUN_TEST_IDLE_STATE = 2;
        private const uint MAX_NUM_DEVICE_NAME_CHARS = 100;
        private const uint MAX_NUM_CHANNEL_CHARS = 5;
        private const uint MAX_NUM_DLL_VERSION_CHARS = 10;
        private const uint MAX_NUM_ERROR_MESSAGE_CHARS = 100;
        private const uint WRITE_DATA_BUFFER_SIZE = 65536;
        private const uint READ_DATA_BUFFER_SIZE = 65536;
        private const uint READ_CMDS_DATA_BUFFER_SIZE = 131071;

        byte[] WriteDataBuffer = new byte[WRITE_DATA_BUFFER_SIZE];
        byte[] ReadDataBuffer = new byte[READ_DATA_BUFFER_SIZE];

        public uint clockFrequencyHz = 0;

        FTC_INPUT_OUTPUT_PINS LowInputOutputPinsData;
        FTH_INPUT_OUTPUT_PINS HighInputOutputPinsData;
        FTC_LOW_HIGH_PINS LowPinsInputData;
        FTH_LOW_HIGH_PINS HighPinsInputData;

        //**************************************************************************
        // TYPE DEFINITIONS
        //**************************************************************************

        public struct FTC_INPUT_OUTPUT_PINS
        {
            public bool bPin1InputOutputState;
            public bool bPin1LowHighState;
            public bool bPin2InputOutputState;
            public bool bPin2LowHighState;
            public bool bPin3InputOutputState;
            public bool bPin3LowHighState;
            public bool bPin4InputOutputState;
            public bool bPin4LowHighState;
        }
        public struct FTH_INPUT_OUTPUT_PINS
        {
            public bool bPin1InputOutputState;
            public bool bPin1LowHighState;
            public bool bPin2InputOutputState;
            public bool bPin2LowHighState;
            public bool bPin3InputOutputState;
            public bool bPin3LowHighState;
            public bool bPin4InputOutputState;
            public bool bPin4LowHighState;
            public bool bPin5InputOutputState;
            public bool bPin5LowHighState;
            public bool bPin6InputOutputState;
            public bool bPin6LowHighState;
            public bool bPin7InputOutputState;
            public bool bPin7LowHighState;
            public bool bPin8InputOutputState;
            public bool bPin8LowHighState;
        }
        public struct FTC_LOW_HIGH_PINS
        {
            public bool bPin1LowHighState;
            public bool bPin2LowHighState;
            public bool bPin3LowHighState;
            public bool bPin4LowHighState;
        }
        public struct FTH_LOW_HIGH_PINS
        {
            public bool bPin1LowHighState;
            public bool bPin2LowHighState;
            public bool bPin3LowHighState;
            public bool bPin4LowHighState;
            public bool bPin5LowHighState;
            public bool bPin6LowHighState;
            public bool bPin7LowHighState;
            public bool bPin8LowHighState;
        }
        public struct FTC_CLOSE_FINAL_STATE_PINS
        {
            public bool bTCKPinState;
            public bool bTCKPinActiveState;
            public bool bTDIPinState;
            public bool bTDIPinActiveState;
            public bool bTMSPinState;
            public bool bTMSPinActiveState;
        }

        //**************************************************************************
        // FUNCTION IMPORTS FROM FTCJTAG DLL
        //**************************************************************************
        [DllImport("FTCJTAG.dll", EntryPoint = "JTAG_GetDllVersion", CallingConvention = CallingConvention.Winapi)]
        static extern uint GetDllVersion(byte[] pDllVersion, uint buufferSize);
        [DllImport("FTCJTAG.dll", CallingConvention = CallingConvention.Winapi)]
        static extern uint JTAG_GetErrorCodeString(string language, uint statusCode, byte[] pErrorMessage, uint bufferSize);
        [DllImport("FTCJTAG.dll", CallingConvention = CallingConvention.Winapi)]
        static extern uint JTAG_GetNumHiSpeedDevices(ref uint NumHiSpeedDevices);
        [DllImport("FTCJTAG.dll", CallingConvention = CallingConvention.Winapi)]
        static extern uint JTAG_GetHiSpeedDeviceNameLocIDChannel(uint deviceNameIndex, byte[] pDeviceName, uint deviceNameBufferSize, ref uint locationID, byte[] pChannel, uint channelBufferSize, ref uint hiSpeedDeviceType);
        [DllImport("FTCJTAG.dll", CallingConvention = CallingConvention.Winapi)]
        static extern uint JTAG_OpenHiSpeedDevice(string DeviceName, uint locationID, string channel, ref IntPtr pftHandle);
        [DllImport("FTCJTAG.dll", CallingConvention = CallingConvention.Winapi)]
        static extern uint JTAG_GetHiSpeedDeviceType(IntPtr ftHandle, ref uint hiSpeedDeviceType);
        [DllImport("FTCJTAG.dll", CallingConvention = CallingConvention.Winapi)]
        static extern uint JTAG_Close(IntPtr ftHandle);
        [DllImport("FTCJTAG.dll", CallingConvention = CallingConvention.Winapi)]
        static extern uint JTAG_CloseDevice(IntPtr ftHandle, ref FTC_CLOSE_FINAL_STATE_PINS pCloseFinalStatePinsData);
        [DllImport("FTCJTAG.dll", CallingConvention = CallingConvention.Winapi)]
        static extern uint JTAG_InitDevice(IntPtr ftHandle, uint clockDivisor);
        [DllImport("FTCJTAG.dll", CallingConvention = CallingConvention.Winapi)]
        static extern uint JTAG_TurnOnDivideByFiveClockingHiSpeedDevice(IntPtr ftHandle);
        [DllImport("FTCJTAG.dll", CallingConvention = CallingConvention.Winapi)]
        static extern uint JTAG_TurnOffDivideByFiveClockingHiSpeedDevice(IntPtr ftHandle);
        [DllImport("FTCJTAG.dll", CallingConvention = CallingConvention.Winapi)]
        static extern uint JTAG_TurnOnAdaptiveClockingHiSpeedDevice(IntPtr ftHandle);
        [DllImport("FTCJTAG.dll", CallingConvention = CallingConvention.Winapi)]
        static extern uint JTAG_TurnOffAdaptiveClockingHiSpeedDevice(IntPtr ftHandle);
        [DllImport("FTCJTAG.dll", CallingConvention = CallingConvention.Winapi)]
        static extern uint JTAG_SetDeviceLatencyTimer(IntPtr ftHandle, byte timerValue);
        [DllImport("FTCJTAG.dll", CallingConvention = CallingConvention.Winapi)]
        static extern uint JTAG_GetDeviceLatencyTimer(IntPtr ftHandle, ref byte timerValue);
        [DllImport("FTCJTAG.dll", CallingConvention = CallingConvention.Winapi)]
        static extern uint JTAG_GetHiSpeedDeviceClock(uint ClockDivisor, ref uint clockFrequencyHz);
        [DllImport("FTCJTAG.dll", CallingConvention = CallingConvention.Winapi)]
        static extern uint JTAG_GetClock(uint clockDivisor, ref uint clockFrequencyHz);
        [DllImport("FTCJTAG.dll", CallingConvention = CallingConvention.Winapi)]
        static extern uint JTAG_SetClock(IntPtr ftHandle, uint clockDivisor, ref uint clockFrequencyHz);
        [DllImport("FTCJTAG.dll", CallingConvention = CallingConvention.Winapi)]
        static extern uint JTAG_SetLoopback(IntPtr ftHandle, bool loopBackState);
        [DllImport("FTCJTAG.dll", CallingConvention = CallingConvention.Winapi)]
        static extern uint JTAG_SetHiSpeedDeviceGPIOs(IntPtr ftHandle, bool bControlLowInputOutputPins, ref FTC_INPUT_OUTPUT_PINS pLowInputOutputPinsData, bool bControlHighInputOutputPins, ref FTH_INPUT_OUTPUT_PINS pHighInputOutputPinsData);
        [DllImport("FTCJTAG.dll", CallingConvention = CallingConvention.Winapi)]
        static extern uint JTAG_GetHiSpeedDeviceGPIOs(IntPtr ftHandle, bool bControlLowInputOutputPins, out FTC_LOW_HIGH_PINS pLowPinsInputData, bool bControlHighInputOutputPins, out FTH_LOW_HIGH_PINS pHighPinsInputData);
        [DllImport("FTCJTAG.dll", CallingConvention = CallingConvention.Winapi)]
        static extern uint JTAG_Write(IntPtr ftHandle, bool bInstructionTestData, uint numBitsToWrite, byte[] WriteDataBuffer, uint numBytesToWrite, uint tapControllerState);
        [DllImport("FTCJTAG.dll", CallingConvention = CallingConvention.Winapi)]
        static extern uint JTAG_Read(IntPtr ftHandle, bool bInstructionTestData, uint numBitsToRead, byte[] ReadDataBuffer, ref uint numBytesReturned, uint tapControllerState);
        [DllImport("FTCJTAG.dll", CallingConvention = CallingConvention.Winapi)]
        static extern uint JTAG_WriteRead(IntPtr ftHandle, bool bInstructionTestData, uint numBitsToWriteRead, byte[] WriteDataBuffer, uint numBytesToWrite, byte[] ReadDataBuffer, ref uint numBytesReturned, uint tapControllerState);
        [DllImport("FTCJTAG.dll", CallingConvention = CallingConvention.Winapi)]
        static extern uint JTAG_GenerateClockPulses(IntPtr ftHandle, uint numClockPulses);
        [DllImport("FTCJTAG.dll", CallingConvention = CallingConvention.Winapi)]
        static extern uint JTAG_ClearCmdSequence();
        [DllImport("FTCJTAG.dll", CallingConvention = CallingConvention.Winapi)]
        static extern uint JTAG_AddWriteCmd(bool bInstructionTestData, uint numBitsToWrite, byte[] WriteDataBuffer, uint numBytesToWrite, uint tapControllerState);
        [DllImport("FTCJTAG.dll", CallingConvention = CallingConvention.Winapi)]
        static extern uint JTAG_AddReadCmd(bool bInstructionTestData, uint numBitsToRead, uint tapControllerState);
        [DllImport("FTCJTAG.dll", CallingConvention = CallingConvention.Winapi)]
        static extern uint JTAG_AddWriteReadCmd(bool bInstructionTestData, uint numBitsToWriteRead, byte[] WriteDataBuffer, uint numBytesToWrite, uint tapControllerState);
        [DllImport("FTCJTAG.dll", CallingConvention = CallingConvention.Winapi)]
        static extern uint JTAG_ExecuteCmdSequence(IntPtr ftHandle, byte[] ReadCmdSequenceDataBuffer, ref uint numBytesReturned);
        [DllImport("FTCJTAG.dll", CallingConvention = CallingConvention.Winapi)]
        static extern uint JTAG_ClearDeviceCmdSequence(IntPtr ftHandle);
        [DllImport("FTCJTAG.dll", CallingConvention = CallingConvention.Winapi)]
        static extern uint JTAG_AddDeviceWriteCmd(IntPtr ftHandle, bool bInstructionTestData, uint numBitsToWrite, byte[] WriteDataBuffer, uint numBytesToWrite, uint tapControllerState);
        [DllImport("FTCJTAG.dll", CallingConvention = CallingConvention.Winapi)]
        static extern uint JTAG_AddDeviceReadCmd(IntPtr ftHandle, bool bInstructionTestData, uint numBitsToRead, uint tapControllerState);
        [DllImport("FTCJTAG.dll", CallingConvention = CallingConvention.Winapi)]
        static extern uint JTAG_AddDeviceWriteReadCmd(IntPtr ftHandle, bool bInstructionTestData, uint numBitsToWriteRead, byte[] WriteDataBuffer, uint numBytesToWrite, uint tapControllerState);

        public void Load()
        {
            LowInputOutputPinsData.bPin1InputOutputState = true;    //true= Enable TCK, TMS and TDI outputs
            LowInputOutputPinsData.bPin2InputOutputState = false;
            LowInputOutputPinsData.bPin3InputOutputState = false;
            LowInputOutputPinsData.bPin4InputOutputState = false;
            LowInputOutputPinsData.bPin1LowHighState = false;       //false= Enable TCK, TMS and TDI outputs
            LowInputOutputPinsData.bPin2LowHighState = false;
            LowInputOutputPinsData.bPin3LowHighState = false;
            LowInputOutputPinsData.bPin4LowHighState = false;

            HighInputOutputPinsData.bPin1InputOutputState = false;
            HighInputOutputPinsData.bPin2InputOutputState = false;
            HighInputOutputPinsData.bPin3InputOutputState = false;
            HighInputOutputPinsData.bPin4InputOutputState = false;
            HighInputOutputPinsData.bPin5InputOutputState = false;
            HighInputOutputPinsData.bPin6InputOutputState = false;
            HighInputOutputPinsData.bPin7InputOutputState = false;
            HighInputOutputPinsData.bPin8InputOutputState = false;
            HighInputOutputPinsData.bPin1LowHighState = false;
            HighInputOutputPinsData.bPin2LowHighState = false;
            HighInputOutputPinsData.bPin3LowHighState = false;
            HighInputOutputPinsData.bPin4LowHighState = false;
            HighInputOutputPinsData.bPin5LowHighState = false;
            HighInputOutputPinsData.bPin6LowHighState = false;
            HighInputOutputPinsData.bPin7LowHighState = false;
            HighInputOutputPinsData.bPin8LowHighState = false;

            ftStatus = JTAG_GetHiSpeedDeviceNameLocIDChannel(0, byteHiSpeedDeviceName, MAX_NUM_DEVICE_NAME_CHARS, ref locationID, byteHiSpeedDeviceChannel, MAX_NUM_CHANNEL_CHARS, ref hiSpeedDeviceType);
            if (ftStatus != 0) error(Convert.ToInt32(ftStatus));
            hiSpeedChannel = Encoding.ASCII.GetString(byteHiSpeedDeviceChannel);// Trim strings to first occurrence of a null terminator character
            hiSpeedChannel = hiSpeedChannel.Substring(0, hiSpeedChannel.IndexOf("\0"));
            hiSpeedDeviceName = Encoding.ASCII.GetString(byteHiSpeedDeviceName);// Trim strings to first occurrence of a null terminator character
            hiSpeedDeviceName = hiSpeedDeviceName.Substring(0, hiSpeedDeviceName.IndexOf("\0"));// The ftHandle parameter is a pointer to a variable of type DWORD ie 32-bit unsigned integer

            ftStatus = JTAG_OpenHiSpeedDevice(hiSpeedDeviceName, locationID, hiSpeedChannel, ref ftHandle);
            if (ftStatus != 0) error(Convert.ToInt32(ftStatus));

            ftStatus = JTAG_InitDevice(ftHandle, 0);
            if (ftStatus != 0) error(Convert.ToInt32(ftStatus));

            ftStatus = JTAG_TurnOffDivideByFiveClockingHiSpeedDevice(ftHandle);
            if (ftStatus != 0) error(Convert.ToInt32(ftStatus));

            ftStatus = JTAG_SetClock(ftHandle, 6, ref clockFrequencyHz);
            if (ftStatus != 0) error(Convert.ToInt32(ftStatus));

            ftStatus = JTAG_SetHiSpeedDeviceGPIOs(ftHandle, true, ref LowInputOutputPinsData, true, ref HighInputOutputPinsData);
            if (ftStatus != 0) error(Convert.ToInt32(ftStatus));

        }

        void error(int e)
        {
            switch (e)
            {
                case 0: Console.WriteLine("FTC_SUCCESS 0 // FTC_OK"); break;
                case 1: Console.WriteLine("FTC_INVALID_HANDLE"); break;
                case 2: Console.WriteLine("FTC_DEVICE_NOT_FOUND"); break;
                case 3: Console.WriteLine("FTC_DEVICE_NOT_OPENED"); break;
                case 4: Console.WriteLine("FTC_IO_ERROR"); break;
                case 5: Console.WriteLine("FTC_INSUFFICIENT_RESOURCES"); break;
                case 20: Console.WriteLine("FTC_FAILED_TO_COMPLETE_COMMAND"); break;
                case 21: Console.WriteLine("FTC_FAILED_TO_SYNCHRONIZE_DEVICE_MPSSE"); break;
                case 22: Console.WriteLine("FTC_INVALID_DEVICE_NAME_INDEX"); break;
                case 23: Console.WriteLine("FTC_NULL_DEVICE_NAME_BUFFER_POINTER"); break;
                case 24: Console.WriteLine("FTC_DEVICE_NAME_BUFFER_TOO_SMALL"); break;
                case 25: Console.WriteLine("FTC_INVALID_DEVICE_NAME"); break;
                case 26: Console.WriteLine("FTC_INVALID_LOCATION_ID"); break;
                case 27: Console.WriteLine("FTC_DEVICE_IN_USE"); break;
                case 28: Console.WriteLine("FTC_TOO_MANY_DEVICES"); break;
                case 29: Console.WriteLine("FTC_NULL_CHANNEL_BUFFER_POINTER"); break;
                case 30: Console.WriteLine("FTC_CHANNEL_BUFFER_TOO_SMALL"); break;
                case 31: Console.WriteLine("FTC_INVALID_CHANNEL"); break;
                case 32: Console.WriteLine("FTC_INVALID_TIMER_VALUE"); break;
                case 33: Console.WriteLine("FTC_INVALID_CLOCK_DIVISOR"); break;
                case 34: Console.WriteLine("FTC_NULL_INPUT_OUTPUT_BUFFER_POINTER"); break;
                case 35: Console.WriteLine("FTC_INVALID_NUMBER_BITS"); break;
                case 36: Console.WriteLine("FTC_NULL_WRITE_DATA_BUFFER_POINTER"); break;
                case 37: Console.WriteLine("FTC_INVALID_NUMBER_BYTES"); break;
                case 38: Console.WriteLine("FTC_NUMBER_BYTES_TOO_SMALL"); break;
                case 39: Console.WriteLine("FTC_INVALID_TAP_CONTROLLER_STATE"); break;
                case 40: Console.WriteLine("FTC_NULL_READ_DATA_BUFFER_POINTER"); break;
                case 41: Console.WriteLine("FTC_COMMAND_SEQUENCE_BUFFER_FULL"); break;
                case 42: Console.WriteLine("FTC_NULL_READ_CMDS_DATA_BUFFER_POINTER"); break;
                case 43: Console.WriteLine("FTC_NO_COMMAND_SEQUENCE"); break;
                case 44: Console.WriteLine("FTC_INVALID_NUMBER_CLOCK_PULSES"); break;
                case 45: Console.WriteLine("FTC_INVALID_NUMBER_SINGLE_CLOCK_PULSES"); break;
                case 46: Console.WriteLine("FTC_INVALID_NUMBER_TIMES_EIGHT_CLOCK_PULSES"); break;
                case 47: Console.WriteLine("FTC_NULL_CLOSE_FINAL_STATE_BUFFER_POINTER"); break;
                case 48: Console.WriteLine("FTC_NULL_DLL_VERSION_BUFFER_POINTER"); break;
                case 49: Console.WriteLine("FTC_DLL_VERSION_BUFFER_TOO_SMALL"); break;
                case 50: Console.WriteLine("FTC_NULL_LANGUAGE_CODE_BUFFER_POINTER"); break;
                case 51: Console.WriteLine("FTC_NULL_ERROR_MESSAGE_BUFFER_POINTER"); break;
                case 52: Console.WriteLine("FTC_ERROR_MESSAGE_BUFFER_TOO_SMALL"); break;
                case 53: Console.WriteLine("FTC_INVALID_LANGUAGE_CODE"); break;
                case 54: Console.WriteLine("FTC_INVALID_STATUS_CODE"); break;
                default: Console.WriteLine("FTC: Unknown error occured."); break;
            }
            System.Environment.Exit(-1);
        }
    }
}