using Newtonsoft.Json;

public class SessionResponse
{
    [JsonProperty("sessionId")]
    public string sessionId { get; set; }

    [JsonProperty("accessToken")]
    public string accessToken { get; set; }

    [JsonProperty("refreshToken")]
    public string refreshToken { get; set; }

    [JsonProperty("idToken")]
    public string idToken { get; set; }
}