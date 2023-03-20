// ReSharper disable InconsistentNaming

using System;
using FTD2XX_NET;

namespace CustomOnChipDebuggerConsoleApp
{
    public enum JtagState
    {
        TestLogicReset,
        RunTestIdle,
        SelectDR,
        CaptureDR,
        ShiftDR,
        Exit1DR,
        PauseDR,
        Exit2DR,
        UpdateDR,
        SelectIR,
        CaptureIR,
        ShiftIR,
        Exit1IR,
        PauseIR,
        Exit2IR,
        UpdateIR,
        DMI,
        SelectDRScan
    }

    public class JtagStateTransitionMachine
    {
        private JtagState myCurrentState;
        private readonly FTDI myJtagDevice;
        public JtagStateTransitionMachine(FTDI jtagDevice)
        {
            myJtagDevice = jtagDevice;
        }

        public void JtagStateTransition(JtagState nextState)
        {
            switch (myCurrentState)
            {
                case JtagState.TestLogicReset:
                    if (nextState.Equals(JtagState.TestLogicReset))
                    {
                        // Set TMS low
                        myCurrentState = nextState;
                    }
                    break;
                case JtagState.RunTestIdle:
                    if (nextState == JtagState.SelectDR)
                    {
                        // Set TMS low 
                        myCurrentState = nextState;
                    }
                    break;
                case JtagState.SelectDR:
                    if (nextState == JtagState.SelectIR)
                    {
                        // Set TMS low 
                        myCurrentState = nextState;
                    }
                    break;
                case JtagState.CaptureDR:
                    break;
                case JtagState.ShiftDR:
                    break;
                case JtagState.Exit1DR:
                    break;
                case JtagState.PauseDR:
                    break;
                case JtagState.Exit2DR:
                    break;
                case JtagState.UpdateDR:
                    break;
                case JtagState.SelectIR:
                    break;
                case JtagState.CaptureIR:
                    break;
                case JtagState.ShiftIR:
                    break;
                case JtagState.Exit1IR:
                    break;
                case JtagState.PauseIR:
                    break;
                case JtagState.Exit2IR:
                    break;
                case JtagState.UpdateIR:
                    break;
                case JtagState.DMI:
                    break;
                case JtagState.SelectDRScan:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (!myCurrentState.Equals(nextState))
            {
                ResetTap();
                switch (nextState)
                {
                    case JtagState.TestLogicReset:
                        break;
                    case JtagState.RunTestIdle:
                        break;
                    case JtagState.SelectDR:
                        break;
                    case JtagState.CaptureDR:
                        break;
                    case JtagState.ShiftDR:
                        break;
                    case JtagState.Exit1DR:
                        break;
                    case JtagState.PauseDR:
                        break;
                    case JtagState.Exit2DR:
                        break;
                    case JtagState.UpdateDR:
                        break;
                    case JtagState.SelectIR:
                        break;
                    case JtagState.CaptureIR:
                        break;
                    case JtagState.ShiftIR:
                        break;
                    case JtagState.Exit1IR:
                        break;
                    case JtagState.PauseIR:
                        break;
                    case JtagState.Exit2IR:
                        break;
                    case JtagState.UpdateIR:
                        break;
                    case JtagState.DMI:
                        break;
                    case JtagState.SelectDRScan:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(nextState), nextState, null);
                }
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
    }
}