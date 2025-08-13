using Newtonsoft.Json;

namespace Plugins.Web3AuthSDK.Types
{
    public class WalletUiConfig
    {
        public bool? enablePortfolioWidget { get; set; }

        public bool? enableConfirmationModal { get; set; }
        
        public bool? enableWalletConnect { get; set; }
        
        public bool? enableTokenDisplay { get; set; }
        
        public bool? enableNftDisplay { get; set; }
        
        public bool? enableShowAllTokensButton { get; set; }
        
        public bool? enableBuyButton { get; set; }
        
        public bool? enableSendButton { get; set; }
        
        public bool? enableSwapButton { get; set; }
        
        public bool? enableReceiveButton { get; set; }
        
        public ButtonPositionType? portfolioWidgetPosition { get; set; }
        
        public DefaultPortfolioType? defaultPortfolio { get; set; }
    }
}

public enum ButtonPositionType
{
    [JsonProperty("bottom-left")]
    BOTTOM_LEFT,

    [JsonProperty("top-left")]
    TOP_LEFT,

    [JsonProperty("bottom-right")]
    BOTTOM_RIGHT,

    [JsonProperty("top-right")]
    TOP_RIGHT
}

public enum DefaultPortfolioType
{
    token , nft
}