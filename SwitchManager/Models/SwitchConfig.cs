using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SwitchManager.Models
{
    public class SwitchConfig
    {
        [JsonPropertyName("ComPort")]
        public string ComPort { get; set; } = "COM1";

        [JsonPropertyName("BaudRate")]
        public int BaudRate { get; set; } = 9600;

        [JsonPropertyName("Groups")]
        public List<PortGroup> Groups { get; set; } = new();
    }
}