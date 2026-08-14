using Newtonsoft.Json;
public class Web3AuthResponse
{
    [JsonProperty("privKey")]
    public string privateKey { get; set; }

    [JsonProperty("ed25519PrivKey")]
    public string ed25519PrivateKey { get; set; }
    public UserInfo userInfo { get; set; }
    public string error { get; set; }
    public string sessionId { get; set; }
    public string coreKitKey { get; set; }
    public string coreKitEd25519PrivKey { get; set; }
}