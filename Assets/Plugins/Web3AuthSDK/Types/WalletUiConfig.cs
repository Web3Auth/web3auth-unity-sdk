using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

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

[JsonConverter(typeof(StringEnumConverter))]
public enum ButtonPositionType
{
    [EnumMember(Value = "bottom-left")]
    BOTTOM_LEFT,

    [EnumMember(Value = "top-left")]
    TOP_LEFT,

    [EnumMember(Value = "bottom-right")]
    BOTTOM_RIGHT,

    [EnumMember(Value = "top-right")]
    TOP_RIGHT
}

[JsonConverter(typeof(StringEnumConverter))]
public enum DefaultPortfolioType
{
    [EnumMember(Value = "token")]
    token,
    [EnumMember(Value = "nft")]
    nft
}
