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
                // 1. Close the existing port if it is already open
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    _serialPort.Close();
                }

                // 2. Initialize serial port with strict timeout and buffer settings
                _serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
                {
                    Handshake = Handshake.None,
                    ReadTimeout = 3000, // Slightly increased for slower legacy hardware
                    WriteTimeout = 1000,
                    NewLine = "\r\n"
                };

                _serialPort.Open();

                // 3. Stabilization delay after hardware handshake
                await Task.Delay(1500);

                // 4. Verification Step: Send 'Enter' and check for any prompt response
                _serialPort.DiscardInBuffer(); // Clear any boot-up noise
                _serialPort.WriteLine("");
                await Task.Delay(1000);

                string initialResponse = _serialPort.ReadExisting();

                // If the switch returns nothing, the cable or hardware is likely disconnected
                if (string.IsNullOrWhiteSpace(initialResponse))
                {
                    _serialPort.Close();
                    throw new Exception("Hardware not responding. Check the console cable and switch power.");
                }

                // 5. Setup terminal environment for automated parsing
                // We enter 'enable' first just in case we are in User EXEC mode
                _serialPort.WriteLine("enable");
                await Task.Delay(200);

                // Disable pagination to ensure the switch sends the entire output without "--More--" prompts
                _serialPort.WriteLine("terminal length 0");
                await Task.Delay(200);

                // Final buffer cleanup
                _serialPort.DiscardInBuffer();

                Debug.WriteLine($"Successfully established session on {portName} at {baudRate} bps.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Connection sequence failed: {ex.Message}");
                // Ensure port is not left in an inconsistent state
                if (_serialPort != null && _serialPort.IsOpen) _serialPort.Close();

                throw; // Re-throw with descriptive hardware error for the UI
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
                await Task.Delay(3000);

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
        public async Task SetPortVlanAsync(string interfaceName, int vlanId)
        {
            try
            {
                _serialPort.WriteLine("conf t");
                await Task.Delay(150);

                _serialPort.WriteLine($"interface {interfaceName}");
                await Task.Delay(100);

                _serialPort.WriteLine("switchport mode access");
                await Task.Delay(50);

                _serialPort.WriteLine($"switchport access vlan {vlanId}");
                await Task.Delay(400);

                _serialPort.WriteLine("end");
                await Task.Delay(150);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SerialService] Error setting VLAN: {ex.Message}");
            }
        }
    }
}