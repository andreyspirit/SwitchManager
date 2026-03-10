using System;
using System.IO;
using System.Text.Json;
using SwitchManager.Models;

namespace SwitchManager.Services
{
    public class ConfigService
    {
        private const string ConfigFileName = "config.json";

        public SwitchConfig? LoadConfig()
        {
            try
            {
                // Path to the config file in the application folder
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);

                if (!File.Exists(path))
                {
                    return null;
                }

                string jsonString = File.ReadAllText(path);

                // Deserialize into the root SwitchConfig object
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                return JsonSerializer.Deserialize<SwitchConfig>(jsonString, options);
            }
            catch (Exception)
            {
                // In a production app, log the error here
                return null;
            }
        }
    }
}