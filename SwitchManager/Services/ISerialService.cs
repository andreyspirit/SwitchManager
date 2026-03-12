using System.Threading.Tasks;

namespace SwitchManager.Services
{
    public interface ISerialService
    {
        public bool IsConnected { get; }

        public  void Disconnect();

        public Task ConnectAsync(string portName, int baudRate);

        // Async method to get the status table
        public Task<string> GetHardwareStatusAsync();

        // Async method to change VLAN
        public Task SetPortVlanAsync(string fullInterfaceName, int vlanId);
    }
}