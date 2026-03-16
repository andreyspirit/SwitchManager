using System.Threading.Tasks;

namespace SwitchManager.Services
{
    public interface ISerialService
    {
        bool IsConnected { get; }

        void Disconnect();

        Task ConnectAsync(string portName, int baudRate);

        // Async method to get the status table
        Task<string> GetHardwareStatusAsync();

        // Async method to change VLAN
        Task SetPortVlanAsync(string fullInterfaceName, int vlanId);
    }
}