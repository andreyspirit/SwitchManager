using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using SwitchManager.Models;
using SwitchManager.Services;
using SwitchManager.Commands;
using System.IO.Ports;

namespace SwitchManager.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly ISerialService _serialService;
        private readonly ConfigService _configService;
        private string _statusMessage = "Ready";

        // Collection of groups to be displayed in the UI
        public ObservableCollection<GroupViewModel> PortGroups { get; } = new();

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ICommand ToggleCommand { get; }

        // Default constructor for XAML/Designer support
        public MainViewModel() : this(null)
        {
            // This calls the constructor with parameters using 'null' as the default service
        }

        public MainViewModel(ISerialService? serialService = null)
        {
            // Dependency Injection: Use mock for tests or create real service
            _serialService = serialService ?? new SerialService();
            _configService = new ConfigService();

            ToggleCommand = new RelayCommand<PortViewModel>(ExecuteToggle);

            InitializeApp();
        }

        private void InitializeApp()
        {
            try
            {
                var config = _configService.LoadConfig();

                // 1. First, try to connect to the hardware
                try
                {
                    _serialService.Connect(config.ComPort, config.BaudRate);
                    StatusMessage = $"Connected to {config.ComPort}";
                }
                catch (Exception portEx)
                {
                    // If port fails, show error but continue to UI generation
                    StatusMessage = $"Hardware Error: {portEx.Message}";
                }

                // 2. Generate UI groups regardless of connection status
                foreach (var group in config.Groups)
                {
                    PortGroups.Add(new GroupViewModel(group, ToggleCommand));
                }
            }
            catch (Exception ex)
            {
                // Critical error (e.g., config file missing)
                StatusMessage = $"Critical Error: {ex.Message}";
            }
        }

        private void ExecuteToggle(PortViewModel clickedPort)
        {
            // Verify connection before sending commands
            if (!_serialService.IsConnected)
            {
                StatusMessage = "Error: Switch is not connected.";
                return;
            }

            // Locate the group containing the clicked port
            var group = PortGroups.FirstOrDefault(g => g.Ports.Contains(clickedPort));
            if (group == null) return;

            if (!clickedPort.IsActive)
            {
                // Mutual exclusion logic: Turn on selected, turn off others in group
                // Comparing 'Type' as an Enum instead of a string
                foreach (var port in group.Ports.Where(p => p.Type != PortType.Target))
                {
                    if (port == clickedPort)
                    {
                        port.IsActive = true;
                        _serialService.SendConfigCommand(port.Number, port.VlanId, true);
                    }
                    else if (port.IsActive)
                    {
                        port.IsActive = false;
                        _serialService.SendConfigCommand(port.Number, port.VlanId, false);
                    }
                }
                StatusMessage = $"Group {group.GroupName}: {clickedPort.Alias} is now ACTIVE";
            }
            else
            {
                // Toggle off (Signal Loss / Manual shutdown)
                clickedPort.IsActive = false;
                _serialService.SendConfigCommand(clickedPort.Number, clickedPort.VlanId, false);
                StatusMessage = $"Group {group.GroupName}: {clickedPort.Alias} DISCONNECTED";
            }
        }
    }
}