using SwitchManager.Models;

namespace SwitchManager.ViewModels
{
    public class PortViewModel : ViewModelBase
    {
        private bool _isActive;
        private bool _existsOnHardware;
        private string _portStatus;

        public int Number { get; }
        public string Alias { get; }
        public int TargetVlanId { get; set; }
        public string FullInterfaceName { get; set; } = string.Empty;

        public bool ExistsOnHardware
        {
            get => _existsOnHardware;
            set => SetProperty(ref _existsOnHardware, value);
        }

        public string PortStatus
        {
            get => _portStatus;
            set => SetProperty(ref _portStatus, value);
        }

        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public PortViewModel(PortEntry port)
        {
            Number = port.Number;
            Alias = port.Alias;
        }
    }
}