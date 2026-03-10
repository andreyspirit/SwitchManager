using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SwitchManager.Models
{
    public class SwitchConfig
    {
        [JsonPropertyName("DefaultPort")]
        public string DefaultComPort { get; set; } = "COM1";

        [JsonPropertyName("Groups")]
        public List<PortGroup> Groups { get; set; } = new();
    }
}