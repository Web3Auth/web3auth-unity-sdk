using Newtonsoft.Json;

namespace Plugins.Web3AuthSDK.Types
{
    public class WalletServicesConfig
    {
        public ConfirmationStrategy? confirmationStrategy { get; set; } = ConfirmationStrategy.DEFAULT;
        
        public WhiteLabelData? whiteLabel { get; set; }
    }
}

public enum ConfirmationStrategy
{
    [JsonProperty("popup")]
    POPUP,

    [JsonProperty("modal")]
    MODAL,

    [JsonProperty("auto-approve")]
    AUTO_APPROVE,

    [JsonProperty("default")]
    DEFAULT
}