using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SwitchManager.Services
{
    public class SerialService : IDisposable
    {
        private SerialPort? _serialPort;
        private string? _detectedPrefix;
        private bool _isProcessing = false;
        private readonly Queue<string> _commandQueue = new Queue<string>();

        // Event to push logs/status messages to the ViewModel
        public event Action<string>? OnLogReceived;

        public bool IsConnected => _serialPort?.IsOpen ?? false;

        public void Connect(string portName, int baudRate = 9600)
        {
            // Close existing connection if any
            if (_serialPort?.IsOpen == true) _serialPort.Close();

            _serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = 2000,
                WriteTimeout = 2000
            };

            _serialPort.Open();
            OnLogReceived?.Invoke($"Connected to {portName}");
        }

        /// <summary>
        /// Silent sync: reads current port statuses from the switch without changing anything.
        /// </summary>
        public async Task<Dictionary<int, bool>> SyncWithHardwareAsync()
        {
            var statusMap = new Dictionary<int, bool>();
            if (!IsConnected) return statusMap;

            try
            {
                // Disable pagination to get the full output at once
                _serialPort!.WriteLine("\n");
                await Task.Delay(200);
                _serialPort.WriteLine("terminal length 0");
                await Task.Delay(200);

                // Clear buffer and request status
                _serialPort.DiscardInBuffer();
                _serialPort.WriteLine("show interfaces status");

                // Wait for the switch to dump the data
                await Task.Delay(1000);
                string response = _serialPort.ReadExisting();

                // Auto-detect prefix (e.g., Gi1/0/ or Fa0/) if not set
                if (string.IsNullOrEmpty(_detectedPrefix))
                {
                    var match = Regex.Match(response, @"(GigabitEthernet|FastEthernet|Gi|Fa|Te)(\d+/)+");
                    if (match.Success) _detectedPrefix = match.Value;
                }

                // Parse the output line by line
                var lines = response.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    // Regex looks for port number followed by status (disabled/connected/etc.)
                    var match = Regex.Match(line, @"^(\d+)\s+.*?\s+(disabled|connected|notconnect|err-disabled)");
                    if (match.Success)
                    {
                        int portNum = int.Parse(match.Groups[1].Value);
                        bool isEnabled = !line.Contains("disabled"); // 'disabled' means admin shutdown
                        statusMap[portNum] = isEnabled;
                    }
                }
            }
            catch (Exception ex)
            {
                OnLogReceived?.Invoke($"Sync Error: {ex.Message}");
            }

            return statusMap;
        }

        /// <summary>
        /// Enqueues a sequence of commands to change a port's state.
        /// </summary>
        public void SendConfigCommand(int portNumber, bool enable)
        {
            if (string.IsNullOrEmpty(_detectedPrefix)) return;

            string action = enable ? "no shutdown" : "shutdown";

            _commandQueue.Enqueue("configure terminal");
            _commandQueue.Enqueue($"interface {_detectedPrefix}{portNumber}");
            _commandQueue.Enqueue(action);
            _commandQueue.Enqueue("end");

            if (!_isProcessing) ProcessQueueAsync();
        }

        private async void ProcessQueueAsync()
        {
            _isProcessing = true;
            while (_commandQueue.Count > 0)
            {
                string cmd = _commandQueue.Dequeue();
                _serialPort?.WriteLine(cmd);
                OnLogReceived?.Invoke($"> {cmd}");

                // Small delay to prevent buffer overflow on the switch CLI
                await Task.Delay(300);
            }
            _isProcessing = false;
        }

        public void Dispose()
        {
            if (_serialPort != null)
            {
                if (_serialPort.IsOpen) _serialPort.Close();
                _serialPort.Dispose();
            }
        }
    }
}