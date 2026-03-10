using System.Text.Json.Serialization;

namespace SwitchManager.Models
{
    public enum PortType
    {
        Source, // Simulator or Real Device
        Target  // UUT (Unit Under Test)
    }

    public class PortEntry
    {
        [JsonPropertyName("Number")]
        public int Number { get; set; }

        [JsonPropertyName("Alias")]
        public string? Alias { get; set; }

        [JsonPropertyName("Type")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public PortType Type { get; set; }
    }
}