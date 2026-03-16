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

namespace SwitchManager.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly ISerialService _serialService;
        private readonly ConfigService _configService;
        private SwitchConfig _currentConfig;
        private string _statusMessage = string.Empty;

        public ObservableCollection<GroupViewModel> PortGroups { get; }
        public RelayCommand<PortViewModel> ToggleCommand { get; }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public MainViewModel() : this(null) {}

        public MainViewModel(ISerialService serialService = null)
        {
            _serialService = serialService ?? new SerialService();
            _configService = new ConfigService();
            PortGroups = new ObservableCollection<GroupViewModel>();
            ToggleCommand = new RelayCommand<PortViewModel>(ExecuteToggleAsync, (p) => true);

            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                return;
            }

            InitializeApp();
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
            if (!_serialService.IsConnected) 
            { 
                return;
            }

            string rawData = await _serialService.GetHardwareStatusAsync();
            if (string.IsNullOrEmpty(rawData))
            {
                StatusMessage = "No response from switch hardware.";
                return;
            }

            // Regex for Cisco 'show interface status' output
            var lineRegex = new Regex(@"^(\S+)\s+(.*?)\s+(connected|notconnect|disabled|err-disabled)\s+(\d+|trunk)", RegexOptions.Multiline);
            var matches = lineRegex.Matches(rawData);

            var physicalPortNumbers = matches.Cast<Match>()
                .Select(m => ParsePortNumber(m.Groups[1].Value))
                .ToHashSet();

            foreach (Match match in matches)
            {
                string fullName = match.Groups[1].Value;
                bool hasLink = (match.Groups[3].Value == "connected");
                int.TryParse(match.Groups[4].Value, out int currentVlan);
                int portNum = ParsePortNumber(fullName);

                // Target Header Update
                var targetGroup = PortGroups.FirstOrDefault(g => g.TargetPortNumber == portNum);
                if (targetGroup != null)
                {
                    targetGroup.IsTargetLinkActive = hasLink;
                    // Auto-fix Target VLAN if it differs from config
                    if (currentVlan != targetGroup.VlanId && currentVlan != 0)
                        await _serialService.SetPortVlanAsync(fullName, targetGroup.VlanId);
                    continue;
                }

                // Source Ports Update
                var portVm = PortGroups.SelectMany(g => g.Ports).FirstOrDefault(p => p.Number == portNum);
                if (portVm != null)
                {
                    portVm.FullInterfaceName = fullName;
                    portVm.IsPhysicallyConnected = hasLink;

                    // REFACTOR: Color is Green only if VLAN matches TargetVlanId
                    // No dependency on physical link for button color
                    portVm.IsActive = (currentVlan == portVm.TargetVlanId);
                }
            }

            // Cross-check config against physical hardware reality
            foreach (var port in PortGroups.SelectMany(g => g.Ports))
            {
                port.ExistsOnHardware = physicalPortNumbers.Contains(port.Number);
            }
        }

        public async Task ExecuteToggleAsync(PortViewModel clickedPort)
        {
            if (clickedPort == null || !_serialService.IsConnected || _currentConfig == null)
            {
                return;
            }

            try
            {
                var group = PortGroups.FirstOrDefault(g => g.Ports.Contains(clickedPort));
                if (group == null)
                {
                    return;
                }

                foreach (var port in group.Ports)
                {
                    // If this is the button we clicked AND it's not already 'Green'
                    if (port == clickedPort && !clickedPort.IsActive)
                    {
                        port.IsActive = true;
                        await _serialService.SetPortVlanAsync(port.FullInterfaceName, port.TargetVlanId);
                    }
                    else
                    {
                        // Move others in the group to Isolation VLAN
                        port.IsActive = false;
                        await _serialService.SetPortVlanAsync(port.FullInterfaceName, _currentConfig.IsolationVlanId);
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Switch Error: {ex.Message}";
            }
        }

        private int ParsePortNumber(string name)
        {
            var match = Regex.Match(name, @"(\d+)$");
            return match.Success ? int.Parse(match.Groups[1].Value) : -1;
        }
    }
}