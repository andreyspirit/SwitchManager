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
        private string _statusMessage = "Ready";

        public ObservableCollection<GroupViewModel> PortGroups { get; } = new();

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ICommand ToggleCommand { get; }

        public MainViewModel() : this(null)
        {
        }

        public MainViewModel(ISerialService? serialService = null)
        {
            _serialService = serialService ?? new SerialService();
            _configService = new ConfigService();

            // Note: RelativeSource binding in XAML is preferred, but we keep the command here
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

                // 1. Generate UI structure first
                foreach (var group in _currentConfig.Groups)
                {
                    // Using the simplified constructor we fixed earlier
                    PortGroups.Add(new GroupViewModel(group));
                }

                // 2. Connect to Hardware
                try
                {
                    _serialService.Connect(_currentConfig.ComPort, _currentConfig.BaudRate);
                    StatusMessage = $"Connected to {_currentConfig.ComPort}. Syncing...";

                    // 3. Initial Audit to see who is actually connected and in which VLAN
                    await AuditHardwareAsync();
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Hardware Error: {ex.Message}";

                    foreach (var group in PortGroups)
                    {
                        foreach (var port in group.Ports)
                        {
                            // This will trigger the LAST DataTrigger in XAML and turn buttons gray
                            port.IsPhysicallyConnected = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Critical Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Reads "show interfaces status" and syncs UI with reality using Regex.
        /// </summary>
        public async Task AuditHardwareAsync()
        {
            if (!_serialService.IsConnected) return;

            string rawData = await _serialService.GetHardwareStatusAsync();
            if (string.IsNullOrEmpty(rawData)) return;

            // Regex for: [Interface] [Name/Descr] [Status] [Vlan]
            var lineRegex = new Regex(@"^(\S+)\s+(.*?)\s+(connected|notconnect|disabled|err-disabled)\s+(\d+|trunk)", RegexOptions.Multiline);
            var matches = lineRegex.Matches(rawData);

            foreach (Match match in matches)
            {
                string fullName = match.Groups[1].Value;
                string status = match.Groups[3].Value;
                string vlanRaw = match.Groups[4].Value;

                // Find port by number (logic: last digits of the name)
                int portNum = ParsePortNumber(fullName);
                var portVm = PortGroups.SelectMany(g => g.Ports).FirstOrDefault(p => p.Number == portNum);

                if (portVm != null)
                {
                    portVm.FullInterfaceName = fullName;
                    portVm.IsPhysicallyConnected = (status == "connected");

                    if (int.TryParse(vlanRaw, out int currentVlan))
                    {
                        portVm.IsActive = (currentVlan == portVm.TargetVlanId);
                    }
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

                // Logic: One source per group. Target ports are always active in their VLAN.
                int isolationVlan = _currentConfig.IsolationVlanId;

                foreach (var port in group.Ports.Where(p => p.Type == PortType.Source))
                {
                    if (port == clickedPort && !clickedPort.IsActive)
                    {
                        // Switch ON: Move to Target VLAN
                        await _serialService.SetPortVlanAsync(port.FullInterfaceName, port.TargetVlanId);
                        port.IsActive = true;
                    }
                    else
                    {
                        // Switch OFF: Move to Black Hole (VLAN 999)
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