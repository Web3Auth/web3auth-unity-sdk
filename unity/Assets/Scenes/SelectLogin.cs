using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json;

public class SelectLogin : MonoBehaviour
{
    List<LoginVerifier> verifierList = new List<LoginVerifier> {
        new LoginVerifier("Google", Provider.GOOGLE),
        new LoginVerifier("Facebook", Provider.FACEBOOK),
        new LoginVerifier("Twitch", Provider.TWITCH),
        new LoginVerifier("Discord", Provider.DISCORD),
        new LoginVerifier("Apple", Provider.APPLE),
        new LoginVerifier("Github", Provider.GITHUB),
        new LoginVerifier("Twitter", Provider.TWITTER),
        new LoginVerifier("LinkedIn", Provider.LINKEDIN),
        new LoginVerifier("Line", Provider.LINE),
        new LoginVerifier("Password", Provider.EMAIL_PASSWORD),
        new LoginVerifier("Hosted Email Passwordless", Provider.EMAIL_PASSWORDLESS),
    };

    Web3Auth web3Auth;

    [SerializeField]
    Text loginResponseText;

    [SerializeField]
    Button logoutButton;

    public GameObject[] loginButtons;

    void Start()
    {
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

    private void onLogin(Web3AuthResponse response)
    {
        loginResponseText.text = JsonConvert.SerializeObject(response);
        
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

    public void LoginWithTwitch()
    {
        loginResponseText.text = "Twitch login selected...";
        login(2);
    }

    public void LoginWithDiscord()
    {
        loginResponseText.text = "Discord login selected...";
        login(3);
    }
    public void LoginWithApple()
    {
        loginResponseText.text = "Apple login selected...";
        login(4);
    }

    public void LoginWithGithub()
    {
        loginResponseText.text = "GitHub login selected...";
        login(5);
    }
    public void LoginWitTwitter()
    {
        loginResponseText.text = "Twitter login selected...";
        login(6);
    }

    public void LoginWithLinkedin()
    {
        loginResponseText.text = "LinkedIn login selected...";
        login(7);
    }
    public void LoginWithLine()
    {
        loginResponseText.text = "Line login selected...";
        login(8);
    }

    public void LoginWithPassword()
    {
        loginResponseText.text = "Password login selected...";
        login(9);
    }
    public void LoginWithEmail()
    {
        loginResponseText.text = "Email passwordless login selected...";
        login(10);
    }

    public void LoginWithSMS()
    {
        loginResponseText.text = "SMS passwordless not available in your region.";
    }
}
