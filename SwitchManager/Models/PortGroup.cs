using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SwitchManager.Models
{
    public class PortGroup
    {
        [JsonPropertyName("GroupName")]
        public string GroupName { get; set; }

        [JsonPropertyName("VlanId")]
        public int VlanId { get; set; }

        [JsonPropertyName("Ports")]
        public List<PortEntry> Ports { get; set; }
    }
}
