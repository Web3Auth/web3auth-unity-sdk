public class LoginVerifier {
    public string name { get; set; }
    public AUTH_CONNECTION authConnection { get; set; }

    public LoginVerifier(string name, AUTH_CONNECTION authConnection)
    {
        this.name = name;
        this.authConnection = authConnection;
    }
}