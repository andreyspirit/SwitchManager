using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using SwitchManager.Models;
using SwitchManager.Services;
using SwitchManager.Commands;
using System.Diagnostics;
using System.Windows;
using System.ComponentModel;
using System.Windows.Input;
using System.IO;

namespace SwitchManager.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly ISerialService _serialService;
        private readonly ConfigService _configService;
        private SwitchConfig _currentConfig;
        private string _statusMessage = string.Empty;
        private bool _isBusy;

        public ObservableCollection<GroupViewModel> PortGroups { get; }
        public RelayCommand<PortViewModel> ToggleCommand { get; }
        public RelayCommand<object> AuditCommand { get; }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public MainViewModel() : this(null) {}

        public MainViewModel(ISerialService serialService = null)
        {
            _serialService = serialService ?? new SerialService();
            _configService = new ConfigService();
            PortGroups = new ObservableCollection<GroupViewModel>();
            ToggleCommand = new RelayCommand<PortViewModel>(ExecuteToggleAsync, _ => !IsBusy);
            AuditCommand = new RelayCommand<object>(async _ => await ExecuteAuditAsync(), _ => !IsBusy);

            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                return;
            }

            InitializeApp();
        }

        private async Task ExecuteAuditAsync()
        {
            if (IsBusy)
            {
                return;
            }

            try
            {
                IsBusy = true;
                await AuditHardwareAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void InitializeApp()
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                // 1. Load and strictly validate config.json
                _currentConfig = _configService.LoadAndValidateConfig();

                foreach (var group in _currentConfig.Groups)
                {
                    PortGroups.Add(new GroupViewModel(group));
                }

                StatusMessage = $"Connecting to {_currentConfig.ComPort} ...";

                // 2. Hardware connection sequence
                await _serialService.ConnectAsync(_currentConfig.ComPort, _currentConfig.BaudRate);

                // 3. Initial sync of hardware state
                await AuditHardwareAsync();

                StatusMessage = "Ready";
            }
            catch (Exception ex)
            {
                string errorMessage = $"Critical Error during startup:\n\n{ex.Message}\n\nApplication will exit.";
                MessageBox.Show(errorMessage, "Startup Failure", MessageBoxButton.OK, MessageBoxImage.Error);

                _serialService?.Disconnect();
                Application.Current.Shutdown();
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        public async Task AuditHardwareAsync()
        {
            try
            {
                // 1. Connection Recovery
                if (!_serialService.IsConnected)
                {
                    StatusMessage = "Reconnecting to switch...";
                    await _serialService.ConnectAsync(_currentConfig.ComPort, _currentConfig.BaudRate);
                }

                // 2. Data Retrieval
                StatusMessage = "Fetching hardware status...";
                string rawData = await _serialService.GetHardwareStatusAsync();

                if (string.IsNullOrEmpty(rawData))
                {
                    StatusMessage = "Error: No response from switch.";
                    return;
                }

                // 3. UI Reset (Prepare for fresh data)
                foreach (var group in PortGroups)
                {
                    group.ExistsOnHardware = false;
                    foreach (var port in group.Ports) port.ExistsOnHardware = false;
                }

                // 4. Parsing
                var lineRegex = new Regex(@"^(\S+)\s+(.*?)\s+(connected|notconnect|disabled|err-disabled)\s+(\d+|trunk)", RegexOptions.Multiline);
                var matches = lineRegex.Matches(rawData);

                foreach (Match match in matches)
                {
                    string fullName = match.Groups[1].Value;
                    string status = match.Groups[3].Value;
                    int.TryParse(match.Groups[4].Value, out int currentVlan);
                    int portNum = ParsePortNumber(fullName);

                    // Update Target Groups
                    var targetGroup = PortGroups.FirstOrDefault(g => g.TargetPortNumber == portNum);
                    if (targetGroup != null)
                    {
                        targetGroup.ExistsOnHardware = true;
                        targetGroup.TargetPortStatus = status;

                        if (currentVlan != targetGroup.VlanId && currentVlan != 0)
                        {
                            await _serialService.SetPortVlanAsync(fullName, targetGroup.VlanId);
                        }
                    }

                    // Update Source Ports
                    var portVm = PortGroups.SelectMany(g => g.Ports).FirstOrDefault(p => p.Number == portNum);
                    if (portVm != null)
                    {
                        portVm.ExistsOnHardware = true;
                        portVm.FullInterfaceName = fullName;
                        portVm.IsActive = (currentVlan == portVm.TargetVlanId);
                        portVm.PortStatus = status;
                    }
                }

                StatusMessage = $"Last update: {DateTime.Now:HH:mm:ss}";
            }
            catch (UnauthorizedAccessException)
            {
                StatusMessage = "Error: COM port is busy or access denied.";
            }
            catch (IOException)
            {
                StatusMessage = "Error: Connection lost during audit.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Unexpected error: {ex.Message}";
            }
        }

        /// <summary>
        /// Toggles the VLAN state for a specific port and its group members.
        /// </summary>
        public async Task ExecuteToggleAsync(PortViewModel clickedPort)
        {
            // Guard clause: Ensure we have a valid port, connection, and we aren't already busy
            if (clickedPort == null || !_serialService.IsConnected || _currentConfig == null || IsBusy)
            {
                return;
            }

            try
            {
                IsBusy = true;

                // Find the group containing the clicked port
                var group = PortGroups.FirstOrDefault(g => g.Ports.Contains(clickedPort));
                if (group == null) return;

                foreach (var port in group.Ports)
                {
                    // If this is the button we clicked AND it's not already active
                    if (port == clickedPort && !clickedPort.IsActive)
                    {
                        port.IsActive = true;
                        await _serialService.SetPortVlanAsync(port.FullInterfaceName, port.TargetVlanId);
                    }
                    else if (port != clickedPort)
                    {
                        // Move others in the group to Isolation VLAN
                        port.IsActive = false;
                        await _serialService.SetPortVlanAsync(port.FullInterfaceName, _currentConfig.IsolationVlanId);
                    }
                }

                StatusMessage = "VLAN configuration commands successfully sent to the switch.";
            }
            catch (Exception ex)
            {
                // Log the hardware error and notify the user via the UI
                StatusMessage = $"Switch Error: {ex.Message}";
            }
            finally
            {
                // Always release the UI lock, even if an error occurs
                IsBusy = false;
            }
        }

        private int ParsePortNumber(string name)
        {
            var match = Regex.Match(name, @"(\d+)$");
            return match.Success ? int.Parse(match.Groups[1].Value) : -1;
        }
    }
}