using SwitchManager.Models;

namespace SwitchManager.ViewModels
{
    public class PortViewModel : ViewModelBase
    {
        // Fields for internal state
        private bool _isActive;
        private bool _isPhysicallyConnected;
        private bool _existsOnHardware = true;

        public int Number { get; }

        public string Alias { get; }

        public int TargetVlanId { get; set; }

        public string FullInterfaceName { get; set; } = string.Empty;

        public bool ExistsOnHardware
        {
            get => _existsOnHardware;
            set => SetProperty(ref _existsOnHardware, value);
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
        }
    }
}