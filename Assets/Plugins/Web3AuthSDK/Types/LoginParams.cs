public class LoginParams
{
    public AuthConnection authConnection { get; set; }
    public string authConnectionId { get; set; }
    public string groupedAuthConnectionId { get; set; }
    public string appState { get; set; }
    public MFALevel mfaLevel { get; set; }
    public ExtraLoginOptions extraLoginOptions { get; set; }
    public string dappShare { get; set; }
    public Curve curve { get; set; } = Curve.SECP256K1;
    public string dappUrl { get; set; }
    public string? loginHint { get; set; }
    
}