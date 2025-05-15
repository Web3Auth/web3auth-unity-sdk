using Newtonsoft.Json;
public class Web3AuthResponse
{
    [JsonProperty("privKey")]
    public string privateKey { get; set; }

    public string ed25519PrivKey { get; set; }
    public UserInfo userInfo { get; set; }
    public string error { get; set; }
    public string sessionId { get; set; }
    public string coreKitKey { get; set; }
    public string coreKitEd25519PrivKey { get; set; }
}