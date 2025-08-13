using System.Collections.Generic;

namespace Plugins.Web3AuthSDK.Types
{
    public class SmartAccountsConfig
    {
        public SmartAccountType SmartAccountType { get; set; }
        
        public SmartAccountWalletScope walletScope { get; set; }
        
        public List<ChainConfig> chains { get; set; }
    }

    public class ChainConfig
    {
        public string chainId { get; set; }
        public BundlerConfig bundlerConfig { get; set; }
        public PaymasterConfig paymasterConfig { get; set; }
    }
}

public enum SmartAccountWalletScope
{
    embedded,
    all
}

public enum SmartAccountType
{
    biconomy, kernel, safe, trust, light, simple, nexus
}