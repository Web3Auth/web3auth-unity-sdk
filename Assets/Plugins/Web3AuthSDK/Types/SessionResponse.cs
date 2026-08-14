using Newtonsoft.Json;

public class SessionResponse
{
    [JsonProperty("sessionId")]
    public string sessionId { get; set; }
}