// ReSharper disable InconsistentNaming

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using FTD2XX_NET;

namespace CustomOnChipDebuggerConsoleApp
{
    public enum JtagState
    {
        TestLogicReset,
        RunTestIdle,
        SelectDRScan,
        CaptureDR,
        ShiftDR,
        Exit1DR,
        PauseDR,
        Exit2DR,
        UpdateDR,
        SelectIRScan,
        CaptureIR,
        ShiftIR,
        Exit1IR,
        PauseIR,
        Exit2IR,
        UpdateIR
    }

    public class JtagStateTransitionMachine
    {
        private JtagState myCurrentState;
        private readonly FTDI myJtagDevice;
        private int myNumBitsInBuffer;
        private int myBufferSize;
        public JtagStateTransitionMachine(FTDI jtagDevice, byte[] buffer)
        {
            myJtagDevice = jtagDevice;
            myBuffer = buffer;
        }

        private const int InstructionRegisterSize = 4;

        private const int DataRegisterSize = 2;

        public void TransitionToState(JtagState toState)
        {
            // Get the number of bits to shift between the current state and the desired state
            var bitCount = GetBitCount(myCurrentState, toState);

            // If no bits need to be shifted, we're already in the desired state
            if (bitCount == 0)
            {
                myCurrentState = toState;
                return;
            }

            // Calculate the TMS value for transitioning to the desired state
            var tmsValue = GetTmsValue(myCurrentState, toState);

            // Create a buffer for the TMS and TDI data
            var buffer = new byte[(bitCount + 7) / 8];

            // Set the TMS and TDI values for the desired transition
            for (var i = 0; i < bitCount; i++)
            {
                var index = i / 8;
                var bit = i % 8;

                buffer[index] |= (byte)(((tmsValue >> i) & 0x01) << bit);
            }

            // Send the TMS and TDI data to the JTAG device
            uint bytesWritten = 0;
            var totalBytes = (uint)buffer.Length;

            // Send the data in chunks to prevent buffer overflows
            while (bytesWritten < totalBytes)
            {
                var chunkSize = Math.Min(totalBytes - bytesWritten, totalBytes);

                var chunk = new byte[chunkSize];

                Array.Copy(buffer, bytesWritten, chunk, 0, chunkSize);

                uint bytesSent = 0;

                var status = myJtagDevice.Write(chunk, chunkSize, ref bytesSent);

                if (status != FTDI.FT_STATUS.FT_OK || bytesSent != chunkSize)
                {
                    throw new Exception("Failed to write data to JTAG device");
                }

                bytesWritten++;
            }
        }

        private uint GetTmsValue(JtagState currentState, JtagState nextState)
        {
            uint tmsValue = 0;
            switch (currentState)
            {
                case JtagState.TestLogicReset:
                    switch (nextState)
                    {
                        case JtagState.TestLogicReset:
                            return 0x00;
                        case JtagState.RunTestIdle:
                            return 0x01;
                        default:
                            return 0x0F;
                    }
                case JtagState.RunTestIdle:
                    switch (nextState)
                    {
                        case JtagState.SelectDRScan:
                            return 0x03;
                        case JtagState.RunTestIdle:
                            return 0x00;
                        case JtagState.TestLogicReset:
                            return 0x02;
                        default:
                            return 0x0F;
                    }
                case JtagState.SelectDRScan:
                    switch (nextState)
                    {
                        case JtagState.CaptureDR:
                            return 0x02;
                        case JtagState.Exit1DR:
                            return 0x00;
                        case JtagState.SelectIRScan:
                            return 0x03;
                        case JtagState.TestLogicReset:
                            return 0x01;
                        default:
                            return 0x0F;
                    }
                case JtagState.CaptureDR:
                    switch (nextState)
                    {
                        case JtagState.Exit1DR:
                            return 0x01;
                        case JtagState.ShiftDR:
                            return 0x03;
                        case JtagState.TestLogicReset:
                            return 0x02;
                        default:
                            return 0x0F;
                    }
                case JtagState.ShiftDR:
                    switch (nextState)
                    {
                        case JtagState.Exit1DR:
                            return 0x01;
                        case JtagState.Exit2DR:
                            return 0x02;
                        case JtagState.PauseDR:
                            return 0x00;
                        case JtagState.ShiftDR:
                            return 0x03;
                        case JtagState.TestLogicReset:
                            return 0x02;
                        default:
                            return 0x0F;
                    }
                case JtagState.Exit1DR:
                    if (nextState == JtagState.SelectDRScan)
                    {
                        tmsValue = 0x02;
                    }
                    else if (nextState == JtagState.PauseDR)
                    {
                        tmsValue = 0x04;
                    }
                    break;
                case JtagState.UpdateDR:
                    tmsValue = nextState == JtagState.Exit1DR ? 0x01 : (uint)0x00;
                    break;
                case JtagState.SelectIRScan:
                    switch (nextState)
                    {
                        case JtagState.TestLogicReset:
                            return 0b0001;
                        case JtagState.RunTestIdle:
                            return 0b0010;
                        case JtagState.SelectDRScan:
                            return 0b0100;
                        case JtagState.CaptureIR:
                            return 0b0101;
                        case JtagState.ShiftIR:
                            return 0b1101;
                        case JtagState.Exit1IR:
                            return 0b1110;
                        case JtagState.PauseIR:
                            return 0b1111;
                        case JtagState.Exit2IR:
                            return 0b0111;
                        case JtagState.UpdateIR:
                            return 0b0011;
                        default:
                            throw new InvalidOperationException($"Invalid state transition from {currentState} to {nextState}");
                    }
                case JtagState.Exit1IR:
                    if (nextState == JtagState.SelectIRScan || nextState == JtagState.SelectDRScan)
                    {
                        tmsValue = 0x01;
                    }
                    else if (nextState == JtagState.Exit2IR)
                    {
                        tmsValue = 0x09;
                    }
                    else if (nextState == JtagState.UpdateIR)
                    {
                        tmsValue = 0xD;
                    }
                    else if (nextState == JtagState.PauseIR)
                    {
                        tmsValue = 0x5;
                    }
                    break;
                case JtagState.UpdateIR:
                    if (nextState == JtagState.SelectIRScan)
                    {
                        tmsValue = 0x01;
                    }
                    break;
                default:
                    var row = (int)currentState;
                    var col = (int)nextState;

                    tmsValue = (uint)BitCountTable[row, col];
                    break;
            }
            return tmsValue;
        }

        // 2D array representing the bit count for each state transition
        readonly int[,] BitCountTable = {
            { 1, 0, 0, 0, 0, 0, 0 }, // TestLogicReset
            { 0, 0, 0, 0, 0, 0, 0 }, // RunTestIdle
            { 3, 0, 0, 0, 0, 0, 0 }, // SelectDRScan
            { 4, 0, 0, 0, 0, 0, 0 }, // CaptureDR
            { 0, 1, 0, 0, 0, 0, 0 }, // ShiftDR
            { 0, 0, 0, 0, 0, 0, 0 }, // Exit1DR
            { 0, 2, 0, 0, 0, 0, 0 }, // PauseDR
            { 0, 0, 0, 0, 0, 0, 0 }, // Exit2DR
            { 0, 0, 0, 0, 1, 0, 0 }, // UpdateDR
            { 0, 0, 0, 0, 0, 0, 0 }, // SelectIRScan
            { 0, 0, 0, 0, 0, 0, 0 }, // CaptureIR
            { 0, 0, 0, 1, 0, 0, 0 }, // ShiftIR
            { 0, 0, 0, 0, 0, 0, 0 }, // Exit1IR
            { 0, 0, 0, 2, 0, 0, 0 }, // PauseIR
            { 0, 0, 0, 0, 0, 0, 0 }, // Exit2IR
            { 0, 0, 0, 0, 0, 1, 0 }, // UpdateIR
        };

        private byte[] myBuffer;
        private List<int> myDataBuffer;

        public int GetBitCount(JtagState currentState, JtagState nextState)
        {
            int bitCount = 0;

            if (currentState == JtagState.TestLogicReset)
            {
                switch (nextState)
                {
                    case JtagState.RunTestIdle:
                        bitCount = 1;
                        break;
                    case JtagState.TestLogicReset:
                        bitCount = 2;
                        break;
                }
            }
            else if (currentState == JtagState.RunTestIdle)
            {
                switch (nextState)
                {
                    case JtagState.SelectDRScan:
                    case JtagState.SelectIRScan:
                        bitCount = 1;
                        break;
                    case JtagState.RunTestIdle:
                        bitCount = 0;
                        break;
                }
            }
            else if (currentState == JtagState.SelectDRScan || currentState == JtagState.SelectIRScan)
            {
                switch (nextState)
                {
                    case JtagState.CaptureDR:
                    case JtagState.CaptureIR:
                        bitCount = 1;
                        break;
                    case JtagState.Exit1DR:
                    case JtagState.Exit1IR:
                        bitCount = 2;
                        break;
                    case JtagState.ShiftDR:
                    case JtagState.ShiftIR:
                        bitCount = currentState == JtagState.SelectDRScan ? DataRegisterSize : InstructionRegisterSize;
                        break;
                    case JtagState.UpdateDR:
                    case JtagState.UpdateIR:
                        bitCount = 1;
                        break;
                }
            }
            else if (currentState == JtagState.Exit1DR || currentState == JtagState.Exit1IR)
            {
                switch (nextState)
                {
                    case JtagState.UpdateDR:
                    case JtagState.UpdateIR:
                        bitCount = 1;
                        break;
                    case JtagState.SelectDRScan:
                    case JtagState.SelectIRScan:
                        bitCount = 2;
                        break;
                }
            }
            else if (currentState == JtagState.ShiftDR || currentState == JtagState.ShiftIR)
            {
                switch (nextState)
                {
                    case JtagState.Exit1DR:
                    case JtagState.Exit1IR:
                        bitCount = 1;
                        break;
                    case JtagState.ShiftDR:
                    case JtagState.ShiftIR:
                        bitCount = Math.Max(DataRegisterSize, InstructionRegisterSize);
                        break;
                    case JtagState.UpdateDR:
                    case JtagState.UpdateIR:
                        bitCount = 1;
                        break;
                }
            }

            return bitCount;
        }

        public void ShiftIR(JtagState state)
        {
            // Check that the current state is Select-DR-Scan or Select-IR-Scan
            if (myCurrentState != JtagState.SelectDRScan && myCurrentState != JtagState.SelectIRScan)
            {
                throw new InvalidOperationException("Invalid state: " + myCurrentState);
            }

            // Calculate the number of bits to shift in based on the instruction register size
            var numBits = InstructionRegisterSize;

            // Initialize the TMS value and state transition arrays
            var tmsValues = new JtagState[numBits];
            var nextState = new JtagState[numBits];

            // Determine the TMS values and next states for each bit
            for (var i = 0; i < numBits; i++)
            {
                // Determine the TMS value and next state for this bit
                var tmsValue = GetTmsValue(state, i == numBits - 1 ? JtagState.Exit1IR : JtagState.ShiftIR, i, numBits);
                var next = i == numBits - 1 ? JtagState.UpdateIR : JtagState.ShiftIR;

                // Store the TMS value and next state in the arrays
                tmsValues[i] = tmsValue;
                nextState[i] = next;
            }

            // Shift in the data using the calculated TMS values and next states
            ShiftData(true, numBits - 1, tmsValues, nextState);

            // Update the current state to the last next state
            myCurrentState = nextState[numBits - 1];
        }

        public void ShiftDR(bool lastBitTms, int value)
        {
            // Set TMS to 0 to enter the Shift-DR state
            ShiftTms(false);

            // Shift the data into the register
            var bitArray = new BitArray(new[] { value });
            for (var i = 0; i < bitArray.Count; i++)
            {
                var bit = bitArray[i];
                var nextState = i == bitArray.Count - 1 ? (lastBitTms ? JtagState.Exit1DR : JtagState.UpdateDR) : JtagState.ShiftDR;
                var tmsValue = GetTmsValue(JtagState.ShiftDR, nextState, i, bitArray.Count);
                ShiftData(bit, i, new[] { tmsValue }, new[] { nextState });
            }

            // Update the state
            var exitState = lastBitTms ? JtagState.UpdateDR : JtagState.RunTestIdle;
            ShiftTms(true);
            GetTmsValue(JtagState.ShiftDR, exitState, 0, 1);
            ShiftTms(false);
        }

        public void ShiftTms(bool bit)
        {
            var tmsBit = new[] { (byte)(bit ? 1 : 0) };
            uint bytesWritten = 0;
            var status = myJtagDevice.Write(tmsBit, 1, ref bytesWritten);
            if (status != FTDI.FT_STATUS.FT_OK)
            {
                throw new Exception("Error shifting TMS bit");
            }
        }

        public void ShiftData(bool bit, int index, JtagState[] tmsValues, JtagState[] nextState)
        {
            // Shift in the data bit
            Shift(bit);

            // Get the TMS value for this index
            var tmsValue = index == 0 ? nextState[nextState.Length - 1] : tmsValues[index - 1];

            // Update the state machine with the TMS value
            myCurrentState = GetNextState(myCurrentState, tmsValue);

            // If this is the last bit, transition to the next state
            if (index == tmsValues.Length - 1)
            {
                myCurrentState = GetNextState(myCurrentState, nextState[nextState.Length - 1]);
            }
        }

        public void Shift(bool bit)
        {
            // Shift in the bit
            myBuffer[0] <<= 1;
            if (bit)
            {
                myBuffer[0] |= 0x01;
            }

            // If the buffer is full, shift it out and reset the buffer
            if (myNumBitsInBuffer == myBufferSize)
            {
                var bufferToSend = new[] { myBuffer[0] };
                uint bytesSent = 0;
                myJtagDevice.Write(bufferToSend, 1, ref bytesSent);
                myNumBitsInBuffer = 0;
                myBuffer[0] = 0x00;
            }
            else
            {
                myNumBitsInBuffer++;
            }
        }

        public JtagState GetNextState(JtagState currentState, JtagState tmsValue)
        {
            // Lookup table for next state based on current state and TMS value
            JtagState[,] nextStateTable =
            {
                { JtagState.TestLogicReset, JtagState.RunTestIdle },
                { JtagState.SelectDRScan, JtagState.SelectIRScan },
                { JtagState.CaptureDR, JtagState.CaptureIR },
                { JtagState.ShiftDR, JtagState.ShiftIR },
                { JtagState.Exit1DR, JtagState.Exit1IR },
                { JtagState.PauseDR, JtagState.PauseIR },
                { JtagState.Exit2DR, JtagState.Exit2IR },
                { JtagState.UpdateDR, JtagState.UpdateIR }
            };

            // Determine the row and column index in the table based on the current state and TMS value
            var row = (int)currentState;
            var col = tmsValue == JtagState.TestLogicReset || tmsValue == JtagState.RunTestIdle ? 0 : 1;

            // Return the next state from the lookup table
            return nextStateTable[row, col];
        }

        public JtagState GetTmsValue(JtagState state, JtagState nextState, int bitIndex, int numBits)
        {
            var stateSequence = new JtagState[numBits + 1];

            // Populate state sequence array starting from current state
            stateSequence[0] = state;
            for (var i = 1; i <= numBits; i++)
            {
                stateSequence[i] = GetNextState(stateSequence[i - 1], (i - 1 == bitIndex) ? nextState : JtagState.RunTestIdle);
            }

            // Return last state in sequence
            return stateSequence[numBits];
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

        public void UpdateDR()
        {
            var tmsValues = new[] { JtagState.ShiftDR };
            var nextState = new[] { JtagState.Exit1DR };
            ShiftData(false, 0, tmsValues, nextState);
        }

        public void CaptureDR()
        {
            // Set TMS to 1 to enter the Update-DR state
            ShiftTms(true);

            // Set TMS to 0 to enter the Capture-DR state
            ShiftTms(false);

            // Wait for the data to be captured
            Thread.Sleep(1);

            // Read the data from the device
            var buffer = new byte[myNumBitsInBuffer / 8];
            uint numBytes = 0;
            var ftStatus = myJtagDevice.Read(buffer, (uint)(myNumBitsInBuffer / 8), ref numBytes);
            if (ftStatus != FTDI.FT_STATUS.FT_OK)
            {
                throw new Exception($"Failed to read data from device: {ftStatus}");
            }

            // Parse the captured data
            myDataBuffer.Clear();
            for (var i = 0; i < myNumBitsInBuffer; i++)
            {
                var byteIndex = i / 8;
                var bitIndex = i % 8;
                var bitValue = (buffer[byteIndex] >> bitIndex) & 0x01;
                myDataBuffer.Add(bitValue == 1 ? 1 : 0);
            }
        }
    }
}