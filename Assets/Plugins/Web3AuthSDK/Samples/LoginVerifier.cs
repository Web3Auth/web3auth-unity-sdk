public class LoginVerifier {
    public string name { get; set; }
    public AuthConnection authConnection { get; set; }

    public LoginVerifier(string name, AuthConnection authConnection)
    {
        this.name = name;
        this.authConnection = authConnection;
    }
}