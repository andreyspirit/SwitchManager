using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using SwitchManager.Models;
using SwitchManager.Services;
using SwitchManager.Commands;
using System.Diagnostics;
using System.Windows;

namespace SwitchManager.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly ISerialService _serialService;
        private readonly ConfigService _configService;
        private SwitchConfig? _currentConfig;
        private string _statusMessage = string.Empty;

        public ObservableCollection<GroupViewModel> PortGroups { get; }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        // Using the updated asynchronous-capable RelayCommand
        public RelayCommand<PortViewModel> ToggleCommand { get; }

        public MainViewModel() : this(null) { }

        public MainViewModel(ISerialService? serialService = null)
        {
            _serialService = serialService ?? new SerialService();
            _configService = new ConfigService();
            PortGroups = new ObservableCollection<GroupViewModel>();

            // Pass ExecuteToggleAsync AND the validation logic (CanToggle)
            ToggleCommand = new RelayCommand<PortViewModel>(ExecuteToggleAsync, CanToggle);

            InitializeApp();
        }

        // This method controls if the button is enabled or disabled
        private bool CanToggle(PortViewModel port)
        {
            // The button is ENABLED only if:
            // 1. Port is not null
            // 2. Physical link is active (IsPhysicallyConnected)
            return port != null && port.IsPhysicallyConnected;
        }

        private async void InitializeApp()
        {
            try
            {
                // 1. Load and validate configuration from config.json
                // This performs strict checks on unique VLANs, port numbers, and mandatory fields
                _currentConfig = _configService.LoadAndValidateConfig();

                // Populate the UI with port groups defined in the configuration
                foreach (var group in _currentConfig.Groups)
                {
                    PortGroups.Add(new GroupViewModel(group));
                }

                // 2. Establish connection to the hardware via Serial Port
                // Verifies if the switch is responsive before proceeding
                await _serialService.ConnectAsync(_currentConfig.ComPort, _currentConfig.BaudRate);

                // 3. Perform initial hardware audit to sync UI state with the switch
                // This ensures the "Config is Law" principle is applied immediately
                await AuditHardwareAsync();

                StatusMessage = "Ready";
            }
            catch (Exception ex)
            {
                // Construct a detailed error message for the user
                string errorMessage = $"Critical Error during startup:\n\n{ex.Message}\n\n" +
                                     "The application cannot proceed and will now close.";

                // Show a modal error dialog to ensure the user notices the failure
                MessageBox.Show(errorMessage, "Startup Failure", MessageBoxButton.OK, MessageBoxImage.Error);

                _serialService?.Disconnect(); 
                // Best Practice: Shutdown the application if the environment is not properly initialized
                // This prevents unpredictable behavior or damage to the test setup
                Application.Current.Shutdown();
            }
        }

        public async Task AuditHardwareAsync()
        {
            if (!_serialService.IsConnected) return;

            string rawData = await _serialService.GetHardwareStatusAsync();

            if (string.IsNullOrEmpty(rawData))
            {
                StatusMessage = "No response from switch. Check connection.";
                return;
            }

            var lineRegex = new Regex(@"^(\S+)\s+(.*?)\s+(connected|notconnect|disabled|err-disabled)\s+(\d+|trunk)", RegexOptions.Multiline);
            var matches = lineRegex.Matches(rawData);

            if (matches.Count == 0)
            {
                StatusMessage = "Protocol Error: Unexpected data format.";
                return;
            }

            foreach (Match match in matches)
            {
                string fullName = match.Groups[1].Value;
                string status = match.Groups[3].Value;
                string vlanRaw = match.Groups[4].Value;
                int portNum = ParsePortNumber(fullName);
                bool hasLink = (status == "connected");
                int.TryParse(vlanRaw, out int currentVlan);

                var targetGroup = PortGroups.FirstOrDefault(g => g.TargetPortNumber == portNum);
                if (targetGroup != null)
                {
                    targetGroup.IsTargetLinkActive = hasLink;

                    if (currentVlan != targetGroup.VlanId && currentVlan != 0) 
                    {
                        await _serialService.SetPortVlanAsync(fullName, targetGroup.VlanId);
                    }
                    continue;
                }

                var portVm = PortGroups.SelectMany(g => g.Ports).FirstOrDefault(p => p.Number == portNum);
                if (portVm != null)
                {
                    portVm.FullInterfaceName = fullName;
                    portVm.IsPhysicallyConnected = hasLink;

                    if (!hasLink)
                    {

                        portVm.IsActive = false;
                    }
                    else
                    {
                        portVm.IsActive = (currentVlan == portVm.TargetVlanId);
                    }
                }
            }

            StatusMessage = $"Audit Complete: {DateTime.Now.ToLongTimeString()}";
        }

        public async Task ExecuteToggleAsync(PortViewModel clickedPort)
        {
            // RULE 1: If there is no physical link, abort execution (button should already be disabled)
            if (clickedPort == null || !clickedPort.IsPhysicallyConnected || !_serialService.IsConnected || _currentConfig == null)
                return;

            try
            {
                var group = PortGroups.FirstOrDefault(g => g.Ports.Contains(clickedPort));
                if (group == null) return;

                int isolationVlan = _currentConfig.IsolationVlanId;

                foreach (var port in group.Ports)
                {
                    if (port == clickedPort && !clickedPort.IsActive)
                    {
                        await _serialService.SetPortVlanAsync(port.FullInterfaceName, port.TargetVlanId);
                        port.IsActive = true;
                    }
                    else
                    {
                        // All other source ports in the group are moved to the isolation VLAN
                        await _serialService.SetPortVlanAsync(port.FullInterfaceName, isolationVlan);
                        port.IsActive = false;
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Command Error: {ex.Message}";
            }
        }

        private void HandleHardwareError(Exception ex)
        {
            if (ex.Message.Contains("Could not find file"))
            {
                StatusMessage = $"I/O Failure: {_currentConfig.ComPort} interface unavailable.";
            }
            else
            {
                StatusMessage = $"Hardware Fault: {ex.Message}";
            }

            // Reset all connection states using LINQ on failure
            PortGroups.SelectMany(g => g.Ports).ToList().ForEach(p => p.IsPhysicallyConnected = false);
            PortGroups.ToList().ForEach(g => g.IsTargetLinkActive = false);
        }

        private int ParsePortNumber(string name)
        {
            var match = Regex.Match(name, @"(\d+)$");
            return match.Success ? int.Parse(match.Groups[1].Value) : -1;
        }
    }
}