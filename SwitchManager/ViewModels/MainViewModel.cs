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
        private string _statusMessage = "System Ready";

        public ObservableCollection<GroupViewModel> PortGroups { get; } = new();

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ICommand ToggleCommand { get; }

        public MainViewModel() : this(null) { }

        public MainViewModel(ISerialService? serialService = null)
        {
            _serialService = serialService ?? new SerialService();
            _configService = new ConfigService();
            ToggleCommand = new RelayCommand<PortViewModel>(async (p) => await ExecuteToggleAsync(p));
            InitializeApp();
        }

        private async void InitializeApp()
        {
            try
            {
                _currentConfig = _configService.LoadConfig();
                if (_currentConfig == null)
                {
                    StatusMessage = "Critical Error: Could not load config.json";
                    return;
                }

                foreach (var group in _currentConfig.Groups)
                {
                    PortGroups.Add(new GroupViewModel(group));
                }

                try
                {
                    _serialService.Connect(_currentConfig.ComPort, _currentConfig.BaudRate);
                    StatusMessage = $"Connected to {_currentConfig.ComPort}. Syncing...";
                    await AuditHardwareAsync();
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("Could not find file"))
                    {
                        StatusMessage = "I/O Failure: Physical layer link failure. COM5 resource not found on system bus.";
                    }
                    else
                    {
                        StatusMessage = $"Hardware Fault: {ex.Message}";
                    }

                    // LINQ FIX: Reset both Source ports AND Target Link Status in groups
                    PortGroups.SelectMany(g => g.Ports).ToList().ForEach(p => p.IsPhysicallyConnected = false);
                    PortGroups.ToList().ForEach(g => g.IsTargetLinkActive = false);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Critical Error: {ex.Message}";
            }
        }

        public async Task AuditHardwareAsync()
        {
            if (!_serialService.IsConnected) return;

            string rawData = await _serialService.GetHardwareStatusAsync();
            if (string.IsNullOrEmpty(rawData)) return;

            var lineRegex = new Regex(@"^(\S+)\s+(.*?)\s+(connected|notconnect|disabled|err-disabled)\s+(\d+|trunk)", RegexOptions.Multiline);
            var matches = lineRegex.Matches(rawData);

            foreach (Match match in matches)
            {
                string fullName = match.Groups[1].Value;
                string status = match.Groups[3].Value;
                string vlanRaw = match.Groups[4].Value;
                int portNum = ParsePortNumber(fullName);
                bool hasLink = (status == "connected");

                // 1. UPDATE SOURCE PORTS (Buttons)
                var portVm = PortGroups.SelectMany(g => g.Ports).FirstOrDefault(p => p.Number == portNum);
                if (portVm != null)
                {
                    portVm.FullInterfaceName = fullName;
                    portVm.IsPhysicallyConnected = hasLink;

                    if (int.TryParse(vlanRaw, out int currentVlan))
                    {
                        portVm.IsActive = (currentVlan == portVm.TargetVlanId);
                    }
                }

                // 2. UPDATE TARGET LINK STATUS (Group Header)
                // If the audited port number matches a group's TargetPortNumber, update that group
                var groupVm = PortGroups.FirstOrDefault(g => g.TargetPortNumber == portNum);
                if (groupVm != null)
                {
                    groupVm.IsTargetLinkActive = hasLink;
                }
            }
            StatusMessage = $"Sync Complete: {DateTime.Now.ToShortTimeString()}";
        }

        private async Task ExecuteToggleAsync(PortViewModel clickedPort)
        {
            if (clickedPort == null || !_serialService.IsConnected || _currentConfig == null) return;

            try
            {
                var group = PortGroups.FirstOrDefault(g => g.Ports.Contains(clickedPort));
                if (group == null) return;

                int isolationVlan = _currentConfig.IsolationVlanId;

                foreach (var port in group.Ports) // Simplified: we update all source ports in the group
                {
                    if (port == clickedPort && !clickedPort.IsActive)
                    {
                        await _serialService.SetPortVlanAsync(port.FullInterfaceName, port.TargetVlanId);
                        port.IsActive = true;
                    }
                    else
                    {
                        await _serialService.SetPortVlanAsync(port.FullInterfaceName, isolationVlan);
                        port.IsActive = false;
                    }
                }
                StatusMessage = $"Updated {group.GroupName} selection.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Command Error: {ex.Message}";
            }
        }

        private int ParsePortNumber(string name)
        {
            var match = Regex.Match(name, @"(\d+)$");
            return match.Success ? int.Parse(match.Groups[1].Value) : -1;
        }
    }
}