using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Plugins.Web3AuthSDK.Types
{
    public class WalletServicesConfig
    {
        public ConfirmationStrategy? confirmationStrategy { get; set; }

        public WhiteLabelData? whiteLabel { get; set; }

        public bool? enableKeyExport { get; set; }
    }
}

[JsonConverter(typeof(StringEnumConverter))]
public enum ConfirmationStrategy
{
    [EnumMember(Value = "popup")]
    POPUP,

    [EnumMember(Value = "modal")]
    MODAL,

    [EnumMember(Value = "auto-approve")]
    AUTO_APPROVE,

    [EnumMember(Value = "default")]
    DEFAULT
}
