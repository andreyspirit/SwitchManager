namespace SwitchManager.Services
{
    public interface ISerialService
    {
        bool IsConnected { get; }
        void Connect(string portName, int baudRate);
        void Disconnect();
        void SendConfigCommand(int portNumber, int vlanId, bool isEnable);
    }
}