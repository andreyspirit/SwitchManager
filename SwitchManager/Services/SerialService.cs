using System;
using System.IO.Ports;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace SwitchManager.Services
{
    public class SerialService : ISerialService
    {
        private SerialPort? _serialPort;
        public bool IsConnected => _serialPort?.IsOpen ?? false;

        public async Task ConnectAsync(string portName, int baudRate)
        {
            try
            {
                // Close the existing port if it is already open
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    _serialPort.Close();
                }

                // Initialize serial port with standard console settings
                _serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
                {
                    Handshake = Handshake.None,
                    ReadTimeout = 2000,
                    WriteTimeout = 2000
                };

                _serialPort.Open();

                // Wait for the hardware interface to stabilize after opening the port
                await Task.Delay(2000);

                // Send an initial Enter to wake up the console prompt
                _serialPort.WriteLine("");
                await Task.Delay(1000);

                // Disable pagination to ensure the switch sends the entire output without "--More--" prompts
                _serialPort.WriteLine("terminal length 0");

                Debug.WriteLine($"Connected to {portName} at {baudRate} bps.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Connection failed: {ex.Message}");
                throw; // Re-throw to allow the ViewModel to catch and display the error
            }
        }

        public void Disconnect()
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Close();
                Debug.WriteLine("Serial port closed.");
            }
        }

        /// <summary>
        /// Asynchronously requests the interface status table from the switch.
        /// </summary>
        public async Task<string> GetHardwareStatusAsync()
        {
            if (!IsConnected) return string.Empty;

            try
            {
                _serialPort!.DiscardInBuffer();

                _serialPort.WriteLine("show interfaces status");

                // Use Task.Delay to keep the UI responsive while waiting for the buffer to fill
                await Task.Delay(1000);

                return _serialPort.ReadExisting();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading status: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Asynchronously sends commands to change a port's VLAN.
        /// </summary>
        public async Task SetPortVlanAsync(string fullInterfaceName, int vlanId)
        {
            if (!IsConnected) return;

            try
            {
                // We build a command sequence to change VLAN without using shutdown
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("conf t");
                sb.AppendLine($"interface {fullInterfaceName}");
                sb.AppendLine($"switchport access vlan {vlanId}");
                sb.AppendLine("end");

                // Writing to SerialPort is fast, but we wrap it in Task.Run for safety
                await Task.Run(() =>
                {
                    _serialPort?.Write(sb.ToString());
                });

                Debug.WriteLine($"Successfully moved {fullInterfaceName} to VLAN {vlanId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error sending VLAN command: {ex.Message}");
            }
        }
    }
}