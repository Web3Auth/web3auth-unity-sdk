#nullable enable

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Plugins.Web3AuthSDK.Types;

public class Web3AuthOptions {
    public string clientId { get; set; }
    public Uri redirectUrl { get; set; }
    public Dictionary<string, string> originData { get; set; } = null;
    
    [JsonProperty("buildEnv")]
    public Web3Auth.BuildEnv authBuildEnv { get; set; } = Web3Auth.BuildEnv.PRODUCTION;
    public string sdkUrl {
        get
        {
            return authBuildEnv switch
            {
                Web3Auth.BuildEnv.STAGING => "https://staging-auth.web3auth.io/v10",
                Web3Auth.BuildEnv.TESTING => "https://develop-auth.web3auth.io",
                _ => "https://auth.web3auth.io/v10"
            };
        }
        set { }
    }
    
    public List<AuthConnectionConfig>? authConnectionConfig { get; set; } = new List<AuthConnectionConfig>();
    public WhiteLabelData? whiteLabel { get; set; }
    public string dashboardUrl
    {
        get
        {
            return authBuildEnv switch
            {
                Web3Auth.BuildEnv.STAGING => $"https://staging-account.web3auth.io/{authDashboardVersion}/{walletAccountConstant}",
                Web3Auth.BuildEnv.TESTING => $"https://develop-account.web3auth.io/{walletAccountConstant}",
                _ => $"https://account.web3auth.io/{authDashboardVersion}/{walletAccountConstant}"
            };
        }
        set { }
    }
    public string? accountAbstractionConfig { get; set; }
    public string walletSdkUrl {
        get
        {
            return authBuildEnv switch
            {
                Web3Auth.BuildEnv.STAGING => "https://staging-wallet.web3auth.io/v5",
                Web3Auth.BuildEnv.TESTING => "https://develop-wallet.web3auth.io",
                _ => "https://wallet.web3auth.io/v5"
            };
        }
        set { }
    }
    public bool? includeUserDataInToken { get; set; } = true;
    public Chains? chains { get; set; }
    public String? defaultChainId { get; set; } = "0x1";
    public bool? enableLogging { get; set; } = false;
    public int sessionTime { get; set; } = 86400;
    
    [JsonProperty("network")]
    public Web3Auth.Network web3AuthNetwork { get; set; }
    
    public bool? useSFAKey { get; set; } = false;
    public WalletServicesConfig? walletServicesConfig { get; set; }
    public MfaSettings? mfaSettings { get; set; } = null;

    private const string authDashboardVersion = "v9";
    private const string walletAccountConstant = "wallet/account";
}