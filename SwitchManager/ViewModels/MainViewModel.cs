using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using SwitchManager.Models;
using SwitchManager.Services;
using SwitchManager.Commands;

namespace SwitchManager.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly ISerialService _serialService;
        private readonly ConfigService _configService;
        private SwitchConfig? _currentConfig;
        private string _statusMessage = string.Empty;

        public ObservableCollection<GroupViewModel> PortGroups { get; } = new();

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
                _currentConfig = _configService.LoadConfig();
                if (_currentConfig == null)
                {
                    StatusMessage = $"Critical Error: Could not load {_configService.ConfigFileName}";
                    return;
                }

                foreach (var group in _currentConfig.Groups)
                {
                    PortGroups.Add(new GroupViewModel(group));
                }

                try
                {
                    await _serialService.ConnectAsync(_currentConfig.ComPort, _currentConfig.BaudRate);

                    // Initial hardware audit
                    await AuditHardwareAsync();
                }
                catch (Exception ex)
                {
                    HandleHardwareError(ex);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Critical Error: {ex.Message}";
            }
        }

        public async Task AuditHardwareAsync()
        {
            if (!_serialService.IsConnected)
            {
                return;
            }

            string rawData = await _serialService.GetHardwareStatusAsync();

            if (string.IsNullOrEmpty(rawData))
            {
                StatusMessage = $"No response from switch. Check connection {_currentConfig?.ComPort}.";
                return;
            }

            var lineRegex = new Regex(@"^(\S+)\s+(.*?)\s+(connected|notconnect|disabled|err-disabled)\s+(\d+|trunk)", RegexOptions.Multiline);
            var matches = lineRegex.Matches(rawData);

            if (matches.Count == 0)
            {
                StatusMessage = "Protocol Error: Unexpected data format from the switch.";
                return;
            }

            foreach (Match match in matches)
            {
                string fullName = match.Groups[1].Value;
                string status = match.Groups[3].Value;
                string vlanRaw = match.Groups[4].Value;
                int portNum = ParsePortNumber(fullName);
                bool hasLink = (status == "connected");

                // 1. Update Source ports (Buttons)
                var portVm = PortGroups.SelectMany(g => g.Ports).FirstOrDefault(p => p.Number == portNum);
                if (portVm != null)
                {
                    portVm.FullInterfaceName = fullName;
                    portVm.IsPhysicallyConnected = hasLink;

                    if (int.TryParse(vlanRaw, out int currentVlan))
                    {
                        // RULE 2: Button is "Active" (Green) if link exists and VLAN matches target
                        portVm.IsActive = (currentVlan == portVm.TargetVlanId);
                    }
                }

                // 2. Update Group header status (Target port)
                var groupVm = PortGroups.FirstOrDefault(g => g.TargetPortNumber == portNum);
                if (groupVm != null)
                {
                    groupVm.IsTargetLinkActive = hasLink;
                }
            }
            StatusMessage = $"Sync Complete: {DateTime.Now.ToShortTimeString()}";
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