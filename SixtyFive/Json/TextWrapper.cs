using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace SixtyFive.Json
{
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class TextWrapper
    {
        public string? Text { get; set; }
    }
}