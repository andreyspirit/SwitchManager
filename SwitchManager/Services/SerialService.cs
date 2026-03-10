using System;
using System.IO.Ports;
using System.Diagnostics;

namespace SwitchManager.Services
{
    public class SerialService : ISerialService
    {
        private SerialPort? _serialPort;
        public bool IsConnected => _serialPort?.IsOpen ?? false;

        // Connect to the switch using specified port and speed
        public void Connect(string portName, int baudRate)
        {
            try
            {
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    _serialPort.Close();
                }

                _serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One);
                _serialPort.Handshake = Handshake.None;

                // Set timeouts to prevent app freezing
                _serialPort.ReadTimeout = 1500;
                _serialPort.WriteTimeout = 1500;

                _serialPort.Open();
                Debug.WriteLine($"Connected to {portName} at {baudRate} bps.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Connection failed: {ex.Message}");
                throw; // Rethrow to handle it in ViewModel/UI layer later
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

        // Send command to enable/disable specific port via VLAN assignment
        public void SendConfigCommand(int portNumber, int vlanId, bool isEnable)
        {
            if (!IsConnected)
            {
                Debug.WriteLine("Cannot send command: Port is closed.");
                return;
            }

            try
            {
                // Example command: "conf t", "int gi1/0/X", "switchport access vlan X" or "shutdown"
                // This logic depends on your specific switch CLI
                string command = isEnable
                    ? $"interface GigabitEthernet1/0/{portNumber}\n switchport access vlan {vlanId}\n no shutdown\n"
                    : $"interface GigabitEthernet1/0/{portNumber}\n shutdown\n";

                _serialPort?.WriteLine(command);
                Debug.WriteLine($"Command sent: {command.Replace("\n", " ")}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error sending command: {ex.Message}");
            }
        }
    }
}