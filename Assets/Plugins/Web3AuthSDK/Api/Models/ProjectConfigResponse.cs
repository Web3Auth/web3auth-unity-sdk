using System.Collections.Generic;
using Plugins.Web3AuthSDK.Types;

public class WhitelistResponse
{
    public List<string> urls { get; set; }
    public Dictionary<string, string> signed_urls { get; set; }
}

public class ProjectConfigResponse
{
    public bool? userDataInIdToken { get; set; } = true;
    public int? sessionTime { get; set; } = 30 * 86400;
    public bool? enableKeyExport { get; set; } = false;
    public WhitelistResponse whitelist { get; set; }
    public List<Chains> chains { get; set; }
    public SmartAccountsConfig smartAccounts { get; set; }
    public WalletUiConfig walletUiConfig { get; set; }
    public List<AuthConnectionConfig> embeddedWalletAuth { get; set; }
    public bool sms_otp_enabled { get; set; }
    public bool wallet_connect_enabled { get; set; }
    public string wallet_connect_project_id { get; set; }
    public WhiteLabelData whitelabel { get; set; }
}