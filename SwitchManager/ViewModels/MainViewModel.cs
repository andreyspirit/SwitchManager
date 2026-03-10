using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using SwitchManager.Models;
using SwitchManager.Services;
using SwitchManager.Commands;

namespace SwitchManager.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly ConfigService _configService;
        private readonly SerialService _serialService;

        public ObservableCollection<GroupViewModel> PortGroups { get; } = new();

        private string? _statusMessage;
        public string? StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ICommand ToggleCommand { get; }

        public MainViewModel()
        {
            _configService = new ConfigService();
            _serialService = new SerialService();
            _serialService.OnLogReceived += (msg) => StatusMessage = msg;

            // RelayCommand is in the same namespace now
            ToggleCommand = new RelayCommand(obj => {
                if (obj is PortViewModel pvm) TogglePort(pvm);
            }, _ => _serialService.IsConnected);

            InitializeApp();
        }

        private async void InitializeApp()
        {
            var config = _configService.LoadConfig();
            if (config != null)
            {
                // Load UI groups
                foreach (var g in config.Groups)
                    PortGroups.Add(new GroupViewModel(g, ToggleCommand));

                // Auto-connect using DefaultPort from JSON
                try
                {
                    _serialService.Connect(config.DefaultComPort);
                    await SyncHardwareState();
                }
                catch (Exception ex) { StatusMessage = $"Auto-connect failed: {ex.Message}"; }
            }
        }

        private async Task SyncHardwareState()
        {
            var states = await _serialService.SyncWithHardwareAsync();
            foreach (var group in PortGroups)
            {
                foreach (var pvm in group.Ports)
                {
                    if (states.TryGetValue(pvm.Number, out bool isOpen))
                        pvm.IsActive = isOpen;
                }
            }
        }

        private void TogglePort(PortViewModel clickedPort)
        {
            var group = PortGroups.FirstOrDefault(g => g.Ports.Contains(clickedPort));
            if (group == null) return;

            // 1. If active -> Turn OFF (Signal Loss)
            if (clickedPort.IsActive)
            {
                clickedPort.IsActive = false;
                _serialService.SendConfigCommand(clickedPort.Number, false);
            }
            else
            {
                // 2. Exclusive selection within the group (Sources only)
                foreach (var port in group.Ports.Where(p => p.Type == PortType.Source))
                {
                    if (port.IsActive)
                    {
                        port.IsActive = false;
                        _serialService.SendConfigCommand(port.Number, false);
                    }
                }
                clickedPort.IsActive = true;
                _serialService.SendConfigCommand(clickedPort.Number, true);
            }

            // 3. Background: Ensure Target port is always UP
            var target = group.Ports.FirstOrDefault(p => p.Type == PortType.Target);
            if (target != null) _serialService.SendConfigCommand(target.Number, true);
        }
    }
}