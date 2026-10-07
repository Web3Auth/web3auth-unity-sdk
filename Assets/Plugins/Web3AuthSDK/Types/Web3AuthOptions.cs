#nullable enable

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Plugins.Web3AuthSDK.Types;

public class Web3AuthOptions {
    public string clientId { get; set; }
    public Uri redirectUrl { get; set; }
    public Dictionary<string, string> originData { get; set; } = null;
    public string localRedirectHost { get; set; } = Utils.LOCAL_REDIRECT_HOST;
    
    [JsonProperty("buildEnv")]
    public Web3Auth.BuildEnv authBuildEnv { get; set; } = Web3Auth.BuildEnv.PRODUCTION;
    public string sdkUrl {
        get
        {
            return authBuildEnv switch
            {
                Web3Auth.BuildEnv.STAGING => "https://staging-auth.web3auth.io/" + AuthServiceVersion,
                Web3Auth.BuildEnv.TESTING => "https://develop-auth.web3auth.io",
                _ => "https://auth.web3auth.io/" + AuthServiceVersion
            };
        }
        set { }
    }

    public string storageServerUrl { get; set; }
    public string sessionSocketUrl { get; set; }
    public string citadelServerUrl { get; set; }
    public string sessionNamespace { get; set; }
    public bool? useAAWithExternalWallet { get; set; }
    
    public List<AuthConnectionConfig>? authConnectionConfig { get; set; } = null;
    public WhiteLabelData? whiteLabel { get; set; }
    public string dashboardUrl
    {
        get
        {
            return authBuildEnv switch
            {
                Web3Auth.BuildEnv.STAGING => $"https://staging-account.web3auth.io/{AuthDashboardVersion}/{walletAccountConstant}",
                Web3Auth.BuildEnv.TESTING => $"https://develop-account.web3auth.io/{walletAccountConstant}",
                _ => $"https://account.web3auth.io/{AuthDashboardVersion}/{walletAccountConstant}"
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
                Web3Auth.BuildEnv.STAGING => "https://staging-wallet.web3auth.io/" + WalletServicesVersion,
                Web3Auth.BuildEnv.TESTING => "https://develop-wallet.web3auth.io",
                _ => "https://wallet.web3auth.io/" + WalletServicesVersion
            };
        }
        set { }
    }
    public bool? includeUserDataInToken { get; set; } = true;
    public Chains? chains { get; set; }
    public String? defaultChainId { get; set; } = "0x1";
    public bool? enableLogging { get; set; } = false;
    /// <summary>
    /// Session lifetime in seconds. When unset, hydrated from project config,
    /// otherwise <see cref="DefaultSessionTime"/>.
    /// </summary>
    public int? sessionTime { get; set; }
    
    [JsonProperty("network")]
    public Web3Auth.Network web3AuthNetwork { get; set; }
    
    public bool? useSFAKey { get; set; } = false;
    public WalletServicesConfig? walletServicesConfig { get; set; }
    public MfaSettings? mfaSettings { get; set; } = null;

    public const string AuthServiceVersion = "v11";
    /// <summary>
    /// Auth v11 rehydrates wallet services with citadel sessionId + accessToken (wallet v6).
    /// </summary>
    public const string WalletServicesVersion = "v6";
    public const string AuthDashboardVersion = "v11";
    public const int DefaultSessionTime = 30 * 86400;
    private const string walletAccountConstant = "wallet/account";

    public int ResolvedSessionTime =>
        sessionTime.HasValue && sessionTime.Value > 0 ? sessionTime.Value : DefaultSessionTime;

    public void EnsureServerUrls()
    {
        if (string.IsNullOrWhiteSpace(storageServerUrl))
            storageServerUrl = StorageServerUrl(authBuildEnv);
        if (string.IsNullOrWhiteSpace(sessionSocketUrl))
            sessionSocketUrl = SessionSocketUrl(authBuildEnv);
        if (string.IsNullOrWhiteSpace(citadelServerUrl))
            citadelServerUrl = CitadelServerUrl(authBuildEnv);
    }

    public static string CitadelServerUrl(Web3Auth.BuildEnv buildEnv)
    {
        return buildEnv == Web3Auth.BuildEnv.TESTING
            ? "https://api-develop.web3auth.io/citadel-service"
            : "https://api.web3auth.io/citadel-service";
    }

    public static string StorageServerUrl(Web3Auth.BuildEnv buildEnv)
    {
        return buildEnv == Web3Auth.BuildEnv.TESTING
            ? "https://api-develop.web3auth.io/session-service"
            : "https://api.web3auth.io/session-service";
    }

    public static string SessionSocketUrl(Web3Auth.BuildEnv buildEnv)
    {
        return buildEnv == Web3Auth.BuildEnv.TESTING
            ? "https://develop-session.web3auth.io"
            : "https://session.web3auth.io";
    }

    public static string DashboardPublicApiUrl(Web3Auth.BuildEnv buildEnv)
    {
        return buildEnv == Web3Auth.BuildEnv.TESTING
            ? "https://api-develop.web3auth.io/signer-service"
            : "https://api.web3auth.io/signer-service";
    }
}