using SwitchManager.Models;

namespace SwitchManager.ViewModels
{
    public class PortViewModel : ViewModelBase
    {
        // Fields for internal state
        private bool _isActive;
        private bool _isPhysicallyConnected;
        private string _fullInterfaceName;
        private PortType _type;

        // Properties from config.json
        public int Number { get; }
        public string Alias { get; }

        // The VLAN ID this port should switch to when "Active"
        public int TargetVlanId { get; set; }

        public PortType Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }

        // The actual name from the switch (e.g., "Fa0/1" or "Gi1/0/24")
        // Discovered during the Audit step
        public string FullInterfaceName
        {
            get => _fullInterfaceName;
            set => SetProperty(ref _fullInterfaceName, value);
        }

        // Physical Layer (L1): Is the cable plugged in? 
        // If false -> Button turns Grey in UI
        public bool IsPhysicallyConnected
        {
            get => _isPhysicallyConnected;
            set => SetProperty(ref _isPhysicallyConnected, value);
        }

        // Logical Layer (L2): Is the port in the correct Target VLAN?
        // True -> Green, False -> Orange
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        /// <summary>
        /// Constructor with 1 argument to fix CS1729 compilation error.
        /// Information like TargetVlanId will be assigned after creation.
        /// </summary>
        public PortViewModel(PortEntry port)
        {
            Number = port.Number;
            Alias = port.Alias;
            Type = port.Type;
            // Default values before the hardware Audit runs
            _fullInterfaceName = string.Empty;
            _isPhysicallyConnected = true;
            _isActive = false;
        }
    }
}