using System;
using System.Collections.Generic;
using Newtonsoft.Json;
#nullable enable

public class Web3AuthOptions {
    public string clientId { get; set; }
    [JsonProperty("network")]
    public Web3Auth.Network web3AuthNetwork { get; set; }

    public Web3Auth.BuildEnv authBuildEnv { get; set; } = Web3Auth.BuildEnv.PRODUCTION;
    public Uri redirectUrl { get; set; }
    public string sdkUrl {
        get {
            if (authBuildEnv == Web3Auth.BuildEnv.STAGING)
                return "https://staging-auth.web3auth.io/v10";
            else if (authBuildEnv == Web3Auth.BuildEnv.TESTING)
                return "https://develop-auth.web3auth.io";
            else 
                return "https://auth.web3auth.io/v10";
        }
        set { }
    }

    public string walletSdkUrl {
         get {
            if (authBuildEnv == Web3Auth.BuildEnv.STAGING)
                return "https://staging-wallet.web3auth.io/v4";
            else if (authBuildEnv == Web3Auth.BuildEnv.TESTING)
                return "https://develop-wallet.web3auth.io";
            else
                return "https://wallet.web3auth.io/v4";
         }
         set { }
    }
    public WhiteLabelData? whiteLabel { get; set; }
    public List<AuthConnectionConfig>? authConnectionConfig { get; set; } = new List<AuthConnectionConfig>();
    public bool? useCoreKitKey { get; set; } = false;
    public Web3Auth.ChainNamespace? chainNamespace { get; set; } = Web3Auth.ChainNamespace.eip155;
    public MfaSettings? mfaSettings { get; set; } = null;
    public int sessionTime { get; set; } = 86400;
    public ChainConfig? chainConfig { get; set; }
    public Dictionary<string, string> originData { get; set; } = null;

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

    private const string authDashboardVersion = "v9";
    private const string walletAccountConstant = "wallet/account";
}