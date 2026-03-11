using System.Threading.Tasks;

namespace SwitchManager.Services
{
    public interface ISerialService
    {
        public bool IsConnected { get; }
        public void Connect(string portName, int baudRate);
        public void Disconnect();

        // Async method to get the status table
        public Task<string> GetHardwareStatusAsync();

        // Async method to change VLAN
        public Task SetPortVlanAsync(string fullInterfaceName, int vlanId);
    }
}