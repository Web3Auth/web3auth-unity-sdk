public class AuthConnectionConfig {
    public string authConnectionId { get; set; }
    public AuthConnection authConnection { get; set; }
    public string name { get; set; }
    public string description { get; set; }
    public string clientId { get; set; }
    public string groupedAuthConnectionId { get; set; }
    public string logoHover { get; set; }
    public string logoLight { get; set; }
    public string logoDark { get; set; }
    public bool mainOption { get; set; } = false;
    public bool showOnModal { get; set; } = true;
    public bool showOnDesktop { get; set; } = true;
    public bool showOnMobile { get; set; } = true;
}