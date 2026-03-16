using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SwitchManager.Models;

namespace SwitchManager.Services
{
    public class ConfigService
    {
        public string ConfigFileName { get; } = "config.json";

        public SwitchConfig LoadAndValidateConfig()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);

                if (!File.Exists(path))
                {
                    throw new FileNotFoundException($"Configuration file not found: {ConfigFileName}");
                }

                string jsonString = File.ReadAllText(path);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var config = JsonSerializer.Deserialize<SwitchConfig>(jsonString, options);

                // Start strict validation
                Validate(config);

                return config;
            }
            catch (Exception ex)
            {
                // In production, log the technical error and rethrow for the UI to handle
                throw new Exception($"Config Error: {ex.Message}");
            }
        }

        private void Validate(SwitchConfig config)
        {
            if (config == null)
            {
                throw new Exception("JSON is empty or corrupted.");
            }

            // 1. Global settings validation
            if (string.IsNullOrWhiteSpace(config.ComPort))
            {
                throw new Exception("COM port is not specified in config.json.");
            }

            if (config.BaudRate <= 0)
            {
                throw new Exception("BaudRate must be a positive integer.");
            }

            if (config.IsolationVlanId <= 0)
            {
                throw new Exception("IsolationVlanId must be a positive integer.");
            }

            var allVlanIds = new HashSet<int>();
            var allPortNumbers = new HashSet<int>();

            // Add IsolationVlanId to the set to ensure no group uses it as a Target Vlan
            allVlanIds.Add(config.IsolationVlanId);

            if (config.Groups == null || !config.Groups.Any())
            {
                throw new Exception("No groups defined in configuration.");
            }

            foreach (var group in config.Groups)
            {
                // 2. Group header validation
                if (string.IsNullOrWhiteSpace(group.GroupName))
                {
                    throw new Exception("Group Name is missing.");
                }

                // Check if VlanId is unique and not equal to IsolationVlanId
                if (!allVlanIds.Add(group.VlanId))
                {
                    throw new Exception($"Duplicate or reserved VlanId detected: {group.VlanId} (Group: {group.GroupName})");
                }

                // 3. Port collection validation
                if (group.Ports == null || !group.Ports.Any())
                {
                    throw new Exception($"Group '{group.GroupName}' has no ports.");
                }

                int targetCount = group.Ports.Count(p => p.Type == PortType.Target);
                int sourceCount = group.Ports.Count(p => p.Type == PortType.Source);

                if (targetCount != 1)
                {
                    throw new Exception($"Group '{group.GroupName}' must have exactly one Target port (Found: {targetCount}).");
                }

                if (sourceCount < 1)
                {
                    throw new Exception($"Group '{group.GroupName}' must have at least one Source port.");
                }

                // 4. Individual port validation
                foreach (var port in group.Ports)
                {
                    if (port.Number <= 0)
                    {
                        throw new Exception($"Invalid port number ({port.Number}) in group '{group.GroupName}'.");
                    }

                    // Ensure physical port numbers are unique across the entire switch
                    if (!allPortNumbers.Add(port.Number))
                    {
                        throw new Exception($"Duplicate physical Port Number detected: {port.Number}");
                    }

                    // Sources must have an Alias (REAL/SIM), Targets do not require it
                    if (port.Type == PortType.Source && string.IsNullOrWhiteSpace(port.Alias))
                    {
                        throw new Exception($"Source Port {port.Number} in group '{group.GroupName}' is missing an Alias.");
                    }
                }
            }
        }
    }
}