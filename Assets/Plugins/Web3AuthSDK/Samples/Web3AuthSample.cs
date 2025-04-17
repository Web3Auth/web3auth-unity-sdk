using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static Web3Auth;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities.Encoders;
using Org.BouncyCastle.Crypto.Digests;

public class Web3AuthSample : MonoBehaviour
{
    List<LoginVerifier> verifierList = new List<LoginVerifier> {
        new LoginVerifier("Google", AUTH_CONNECTION.GOOGLE),
        new LoginVerifier("Facebook", AUTH_CONNECTION.FACEBOOK),
        // new LoginVerifier("CUSTOM_VERIFIER", Provider.CUSTOM_VERIFIER),
        new LoginVerifier("Twitch", AUTH_CONNECTION.TWITCH),
        new LoginVerifier("Discord", AUTH_CONNECTION.DISCORD),
        new LoginVerifier("Reddit", AUTH_CONNECTION.REDDIT),
        new LoginVerifier("Apple", AUTH_CONNECTION.APPLE),
        new LoginVerifier("Github", AUTH_CONNECTION.GITHUB),
        new LoginVerifier("LinkedIn", AUTH_CONNECTION.LINKEDIN),
        new LoginVerifier("Twitter", AUTH_CONNECTION.TWITTER),
        new LoginVerifier("Line", AUTH_CONNECTION.LINE),
        new LoginVerifier("Email Passwordless", AUTH_CONNECTION.EMAIL_PASSWORDLESS),
        new LoginVerifier("SMS Passwordless", AUTH_CONNECTION.SMS_PASSWORDLESS),
        new LoginVerifier("Farcaster", AUTH_CONNECTION.FARCASTER),
    };

    Web3Auth web3Auth;

    [SerializeField]
    InputField emailAddressField;

    [SerializeField]
    Dropdown verifierDropdown;

    [SerializeField]
    Button loginButton;

    [SerializeField]
    Text loginResponseText;

    [SerializeField]
    Button logoutButton;

    [SerializeField]
    Button mfaSetupButton;

    [SerializeField]
    Button launchWalletServicesButton;

    [SerializeField]
    Button signMessageButton;

    [SerializeField]
    Button signResponseButton;

    void Start()
    {
        var authConnectionItem = new AuthConnectionConfig()
        {
            authConnectionId = "your_verifierid_from_web3auth_dashboard", // corresponds to `verifier`
            authConnection = AuthConnection.GOOGLE,
            clientId = "your_clientId_from_web3auth_dashboard"
        };
        var authConnectionConfig = new List<AuthConnectionConfig> { authConnectionItem };

        web3Auth = GetComponent<Web3Auth>();
        web3Auth.setOptions(new Web3AuthOptions()
        {
            whiteLabel = new WhiteLabelData()
            {
                appName = "Web3Auth Sample App",
                logoLight = null,
                logoDark = null,
                defaultLanguage = Language.en,
                mode = ThemeModes.dark,
                theme = new Dictionary<string, string>
                {
                    { "primary", "#FFBF00" }
                }
            },
            // If using your own custom verifier, uncomment this code. 
            /*
            ,
            loginConfig = new Dictionary<string, LoginConfigItem>
            {
                {"CUSTOM_VERIFIER", loginConfigItem}
            }
            */
            authConnectionConfig = new List<AuthConnectionConfig>()
            {
                new AuthConnectionConfig()
                {
                    authConnectionId = "web3auth-auth0-email-passwordless-sapphire-devnet",
                    authConnection = AuthConnection.JWT,
                    clientId = "d84f6xvbdV75VTGmHiMWfZLeSPk8M07C"
                }
            },
            clientId = "BFuUqebV5I8Pz5F7a5A2ihW7YVmbv_OHXnHYDv6OltAD5NGr6e-ViNvde3U4BHdn6HvwfkgobhVu4VwC-OSJkik",
            buildEnv = BuildEnv.TESTING,
            redirectUrl = new Uri("torusapp://com.torus.Web3AuthUnity"),
            network = Web3Auth.Network.SAPPHIRE_DEVNET,
            sessionTime = 86400
        });
        web3Auth.onLogin += onLogin;
        web3Auth.onLogout += onLogout;
        web3Auth.onMFASetup += onMFASetup;
        web3Auth.onSignResponse += onSignResponse;
        web3Auth.onManageMFA += onManageMFA;

        emailAddressField.gameObject.SetActive(false);
        logoutButton.gameObject.SetActive(false);
        mfaSetupButton.gameObject.SetActive(false);
        launchWalletServicesButton.gameObject.SetActive(false);
        signMessageButton.gameObject.SetActive(false);
        signResponseButton.gameObject.SetActive(false);

        loginButton.onClick.AddListener(login);
        logoutButton.onClick.AddListener(logout);
        mfaSetupButton.onClick.AddListener(enableMFA);
        launchWalletServicesButton.onClick.AddListener(launchWalletServices);
        signMessageButton.onClick.AddListener(request);
        signResponseButton.onClick.AddListener(manageMFA);

        verifierDropdown.AddOptions(verifierList.Select(x => x.name).ToList());
        verifierDropdown.onValueChanged.AddListener(onVerifierDropDownChange);
    }

    private void onLogin(Web3AuthResponse response)
    {
        loginResponseText.text = JsonConvert.SerializeObject(response, Formatting.Indented);
        var userInfo = JsonConvert.SerializeObject(response.userInfo, Formatting.Indented);
        Debug.Log(userInfo);

        loginButton.gameObject.SetActive(false);
        verifierDropdown.gameObject.SetActive(false);
        emailAddressField.gameObject.SetActive(false);
        logoutButton.gameObject.SetActive(true);
        mfaSetupButton.gameObject.SetActive(true);
        launchWalletServicesButton.gameObject.SetActive(true);
        signMessageButton.gameObject.SetActive(true);
        signResponseButton.gameObject.SetActive(true);
    }

    private void onLogout()
    {
        loginButton.gameObject.SetActive(true);
        verifierDropdown.gameObject.SetActive(true);
        logoutButton.gameObject.SetActive(false);
        mfaSetupButton.gameObject.SetActive(false);
        launchWalletServicesButton.gameObject.SetActive(false);
        signMessageButton.gameObject.SetActive(false);
        signResponseButton.gameObject.SetActive(false);

        loginResponseText.text = "";
    }

    private void onMFASetup(bool response) {
        Debug.Log("MFA Setup: " + response);
    }

    private void onSignResponse(SignResponse signResponse)
    {
        Debug.Log("Retrieved SignResponse: " + signResponse);
    }

    private void onManageMFA(bool response) {
        Debug.Log("Manage MFA: " + response);
    }

    private void onVerifierDropDownChange(int selectedIndex)
    {
        if (verifierList[selectedIndex].authConnection == AUTH_CONNECTION.EMAIL_PASSWORDLESS)
            emailAddressField.gameObject.SetActive(true);
        else
            emailAddressField.gameObject.SetActive(false);
    }

    private void login()
    {
        var selectedProvider = verifierList[verifierDropdown.value].authConnection;

        var options = new LoginParams()
        {
            authConnection = selectedProvider
        };

        if (selectedProvider == AUTH_CONNECTION.EMAIL_PASSWORDLESS)
        {
            options.extraLoginOptions = new ExtraLoginOptions()
            {
                login_hint = emailAddressField.text
            };
        }
        if (selectedProvider == AUTH_CONNECTION.SMS_PASSWORDLESS)
        {
            options.extraLoginOptions = new ExtraLoginOptions()
            {
                login_hint = "+XX-XXXXXXXXXX"
            };
        }

        web3Auth.login(options);
    }

    private void logout()
    {
        web3Auth.logout();
    }

    private void enableMFA()
    {
        var selectedProvider = verifierList[verifierDropdown.value].authConnection;

        var options = new LoginParams()
        {
            authConnection = selectedProvider,
            mfaLevel = MFALevel.MANDATORY
        };

        if (selectedProvider == AUTH_CONNECTION.EMAIL_PASSWORDLESS)
        {
            options.extraLoginOptions = new ExtraLoginOptions()
            {
                login_hint = emailAddressField.text
            };
        }
        web3Auth.enableMFA(options);
    }

    private void manageMFA()
    {
        var selectedProvider = verifierList[verifierDropdown.value].authConnection;

        var options = new LoginParams()
        {
            authConnection = selectedProvider,
            mfaLevel = MFALevel.MANDATORY
        };

        if (selectedProvider == AUTH_CONNECTION.EMAIL_PASSWORDLESS)
        {
            options.extraLoginOptions = new ExtraLoginOptions()
            {
                login_hint = emailAddressField.text
            };
        }
        web3Auth.manageMFA(options);
    }

    private void launchWalletServices() {
        var selectedProvider = verifierList[verifierDropdown.value].authConnection;

        var chainConfig = new ChainConfig()
        {
            chainId = "0x1",
            rpcTarget = "https://mainnet.infura.io/v3/daeee53504be4cd3a997d4f2718d33e0",
            ticker = "ETH",
            chainNamespace = Web3Auth.ChainNamespace.eip155
        };
        var chainConfigList = new List<ChainConfig> { chainConfig };
        foreach (var config in chainConfigList)
        {
            Debug.Log($"Chain ID: {config.chainId}, RPC Target: {config.rpcTarget}, Ticker: {config.ticker}, Namespace: {config.chainNamespace}");
        }
        web3Auth.launchWalletServices(chainConfigList, "0x1");
    }

    private void request() {
        var selectedProvider = verifierList[verifierDropdown.value].authConnection;

        var chainConfig = new ChainConfig()
        {
            chainId = "0x89",
            rpcTarget = "https://1rpc.io/matic",
            chainNamespace = Web3Auth.ChainNamespace.eip155
        };
        var chainConfigList = new List<ChainConfig> { chainConfig };

        JArray paramsArray = new JArray
        {
            "Hello, World!",
            getPublicAddressFromPrivateKey(web3Auth.getPrivKey()),
            "Android"
        };

        web3Auth.request(chainConfigList, "0x89", "personal_sign", paramsArray);
    }

    public string getPublicAddressFromPrivateKey(string privateKeyHex)
    {
        byte[] privateKeyBytes = Hex.Decode(privateKeyHex);

        // Create the EC private key parameters
        BigInteger privateKeyInt = new BigInteger(1, privateKeyBytes);
        var ecParams = SecNamedCurves.GetByName("secp256k1");
        ECPoint q = ecParams.G.Multiply(privateKeyInt);

        // Get the public key bytes
        byte[] publicKeyBytes = q.GetEncoded(false).Skip(1).ToArray();

        // Compute the Keccak-256 hash of the public key
        var digest = new KeccakDigest(256);
        byte[] hash = new byte[digest.GetDigestSize()];
        digest.BlockUpdate(publicKeyBytes, 0, publicKeyBytes.Length);
        digest.DoFinal(hash, 0);

        // Take the last 20 bytes of the hash as the address
        byte[] addressBytes = hash.Skip(12).ToArray();
        string publicAddress = "0x" + BitConverter.ToString(addressBytes).Replace("-", "").ToLower();

        return publicAddress;
    }
}
