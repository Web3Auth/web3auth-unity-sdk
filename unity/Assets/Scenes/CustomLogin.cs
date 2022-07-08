using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json;

public class CustomLogin : MonoBehaviour
{
    [DllImport("__Internal")]
    private static extern void Hello();

    [DllImport("__Internal")]
    private static extern string GetLocalStorage(string key);

    List<LoginVerifier> verifierList = new List<LoginVerifier> {
        new LoginVerifier("Google", Provider.GOOGLE),
        new LoginVerifier("Facebook", Provider.FACEBOOK),
        new LoginVerifier("JWT", Provider.JWT),
    };

    Web3Auth web3Auth;
    LoginConfigItem loginConfigItem;

    [SerializeField]
    Text loginResponseText;

    [SerializeField]
    Button logoutButton;

    public GameObject[] loginButtons;

    void Start()
    {
        loginConfigItem = new LoginConfigItem()
        {

        };

        web3Auth = GetComponent<Web3Auth>();
        web3Auth.setOptions(new Web3AuthOptions()
        {
            whiteLabel = new WhiteLabelData()
            {
                name = "Web3Auth Sample App",
                logoLight = null,
                logoDark = null,
                defaultLanguage = "en",
                dark = true,
                theme = new Dictionary<string, string>
                {
                    { "primary", "#123456" }
                }
            }
        });
        web3Auth.onLogin += onLogin;
        web3Auth.onLogout += onLogout;

        logoutButton.gameObject.SetActive(false);
        loginButtons = GameObject.FindGameObjectsWithTag("LoginButtons");
    }

    private void onLogin(Web3AuthResponse res)
    {
        //var res = JsonConvert.SerializeObject(response);
        var privKey = res.privKey;
        var ed25519PrivKey = res.ed25519PrivKey;
        var userInfo = res.userInfo;
        var text = $"Response:\n{privKey}\n{userInfo.email} by {userInfo.verifier}\ndappShare {userInfo.dappShare}";

        loginResponseText.text = text;
        
        foreach (var btn in loginButtons)
        {
            btn.SetActive(false);
        }
        logoutButton.gameObject.SetActive(true);
    }

    private void onLogout()
    {
        loginResponseText.text = "Select provider to login";

        foreach (var btn in loginButtons)
        {
            btn.SetActive(true);
        }
        logoutButton.gameObject.SetActive(false);
    }

    private void login(int loginSelected)
    {
        var selectedProvider = verifierList[loginSelected].loginProvider;

        var options = new LoginParams()
        {
            loginProvider = selectedProvider
        };

        web3Auth.login(options);
    }

    private void logout()
    {
        web3Auth.logout();
    }

    public void LogoutAll()
    {
        logout();
    }

    // Helper functions to attach to the button clicks. 
    
    public void LoginWithGoogle()
    {
        loginResponseText.text = "Google login selected...";
        login(0);
    }

    public void LoginWithFacebook()
    {
        loginResponseText.text = "Facebook login selected...";
        login(1);
    }

    public void LoginWithJWT()
    {
        loginResponseText.text = "Custom JWT login selected...";
        login(2);
    }

    public void GetStoredToken()
    {
        //Hello();
        var storedToken = GetLocalStorage("token");
        loginResponseText.text = $"Retrieved token:\n{storedToken}";
    }
}
