using FTD2XX_NET;

namespace CustomOnChipDebuggerConsoleApp
{
    interface IRISCVDMIInterface
    {
        void ConnectToTarget(FTDI jtagDevice);
        void DisconnectFromTarget();
        bool ResetTarget();
        bool IsTargetConnected();
        uint ReadDTMRegister(uint address);
        void WriteDtmRegister(uint address, uint data);
        byte[] DmiScan(uint address);
    }
}