using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SwitchManager.Models
{
    public class SwitchConfig
    {
        [JsonPropertyName("ComPort")]
        public string ComPort { get; set; }

        [JsonPropertyName("BaudRate")]
        public int BaudRate { get; set; }

        // The "Black Hole" VLAN for isolated ports
        [JsonPropertyName("IsolationVlanId")]
        public int IsolationVlanId { get; set; }

        [JsonPropertyName("Groups")]
        public List<PortGroup> Groups { get; set; }
    }
}