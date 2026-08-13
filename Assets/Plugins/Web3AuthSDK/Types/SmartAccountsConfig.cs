using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Plugins.Web3AuthSDK.Types
{
    public class SmartAccountsConfig
    {
        public SmartAccountType smartAccountType { get; set; }

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

[JsonConverter(typeof(StringEnumConverter))]
public enum SmartAccountWalletScope
{
    embedded,
    all
}

[JsonConverter(typeof(StringEnumConverter))]
public enum SmartAccountType
{
    metamask,
    biconomy,
    kernel,
    safe,
    trust,
    light,
    simple,
    nexus
}