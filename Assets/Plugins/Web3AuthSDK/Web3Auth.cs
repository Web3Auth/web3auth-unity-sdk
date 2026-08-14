using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Math;
using UnityEngine;

public class Web3Auth : MonoBehaviour
{
    public enum Network
    {
        MAINNET, TESTNET, CYAN, AQUA, SAPPHIRE_DEVNET, SAPPHIRE_MAINNET
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ChainNamespace
    {
        eip155, solana, other
    }

    public enum BuildEnv
    {
        PRODUCTION, STAGING, TESTING
    }

    public enum ThemeModes
    {
        light, dark, auto
    }

    public enum Language
    {
        en, de, ja, ko, zh, es, fr, pt, nl, tr
    }

    private Web3AuthOptions web3AuthOptions;
    private Dictionary<string, object> initParams;

    private Web3AuthResponse web3AuthResponse;
    private ProjectConfigResponse projectConfigResponse;
    private bool isRequestResponse = false;

    public event Action<Web3AuthResponse> onLogin;
    public event Action onLogout;
    public event Action<bool> onMFASetup;
    public event Action<bool> onManageMFA;
    public event Action<SignResponse> onSignResponse;

    private static SignResponse signResponse = null;

    public static void setSignResponse(SignResponse _response)
    {
        signResponse = _response;
    }

    [SerializeField]
    private string clientId;

    [SerializeField]
    private string redirectUri;

    [SerializeField]
    private Network network;
    private string redirectUrl;

#if UNITY_STANDALONE || UNITY_EDITOR
    private HttpListener localHttpListener;
#endif

    private static readonly Queue<Action> _executionQueue = new Queue<Action>();

    public void Awake()
    {
        this.initParams = new Dictionary<string, object>();

        this.initParams["clientId"] = clientId;
        this.initParams["network"] = network.ToString().ToLowerInvariant();

        if (!string.IsNullOrEmpty(redirectUri))
            this.initParams["redirectUrl"] = redirectUri;

        Application.deepLinkActivated += onDeepLinkActivated;
        if (!string.IsNullOrEmpty(Application.absoluteURL))
            onDeepLinkActivated(Application.absoluteURL);

#if UNITY_EDITOR
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;

        Web3AuthSDK.Editor.Web3AuthDebug.onURLRecieved += (Uri url) =>
        {
            this.setResultUrl(url);
        };

//#elif UNITY_WEBGL
//        var code = Utils.GetAuthCode();
//        Debug.Log("code is " + code);
//        if (Utils.GetAuthCode() != "") 
//        {
//            Debug.Log("I am here");
//            this.setResultUrl(new Uri($"http://localhost#{code}"));
//        } 
#endif
    }

#if UNITY_EDITOR
    private void OnBeforeAssemblyReload()
    {
        // Close HttpListener before domain reload to avoid "invalid GC handle" warnings.
        StopLocalWebserver();
    }
#endif

    private string getResolvedClientId()
    {
        if (!string.IsNullOrEmpty(this.web3AuthOptions?.clientId))
            return this.web3AuthOptions.clientId;

        return this.clientId;
    }

    public async void setOptions(Web3AuthOptions web3AuthOptions)
    {
        this.web3AuthOptions = web3AuthOptions;

        var resolvedClientId = getResolvedClientId();
        if (string.IsNullOrEmpty(resolvedClientId))
        {
            throw new Exception("clientId is required. Set it in Web3AuthOptions or the Web3Auth Inspector field.");
        }

        this.web3AuthOptions.clientId = resolvedClientId;
        this.initParams["clientId"] = resolvedClientId;

        bool isfetchConfigSuccess = await fetchProjectConfig();

        if (!isfetchConfigSuccess)
        {
            throw new Exception(
                "Failed to fetch project config. If responseCode was 0, the emulator/device has no working internet/DNS. " +
                "Check Wi‑Fi, cold-boot the AVD, or try a physical device.");
        } else {
            // Restore existing session if present. Empty store is normal when logged out / expired.
            var redirectUrl = KeyStoreManagerUtils.getPreferencesData(KeyStoreManagerUtils.REDIRECT_URL);
            if (string.IsNullOrEmpty(redirectUrl) && this.web3AuthOptions.redirectUrl != null)
                redirectUrl = GetRedirectUrlString(this.web3AuthOptions.redirectUrl);
#if UNITY_EDITOR || UNITY_STANDALONE
            var localHost = this.web3AuthOptions.localRedirectHost ?? Utils.LOCAL_REDIRECT_HOST;
            if (!string.IsNullOrEmpty(localHost))
                redirectUrl = $"http://{localHost}:{Utils.LOCAL_REDIRECT_PORT}";
#endif
            authorizeSession("", redirectUrl, quietIfEmpty: true);

            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                Converters = new List<JsonConverter> { new StringEnumConverter() },
                Formatting = Formatting.Indented
            };

            // Prefer OriginalString — System.Uri lowercases custom-scheme hosts.
            if (this.web3AuthOptions.redirectUrl != null)
                this.initParams["redirectUrl"] = GetRedirectUrlString(this.web3AuthOptions.redirectUrl);

            if (this.web3AuthOptions.whiteLabel != null)
                this.initParams["whiteLabel"] = JsonConvert.SerializeObject(this.web3AuthOptions.whiteLabel, settings);

            SetAuthConnectionConfigInitParam(settings);

            if (this.web3AuthOptions.walletServicesConfig != null)
                this.initParams["walletServicesConfig"] = JObject.FromObject(this.web3AuthOptions.walletServicesConfig, JsonSerializer.Create(settings));

            if (this.web3AuthOptions.authBuildEnv != null)
                this.initParams["authBuildEnv"] = this.web3AuthOptions.authBuildEnv.ToString().ToLower();

            this.initParams["network"] = this.web3AuthOptions.web3AuthNetwork.ToString().ToLower();

            if (this.web3AuthOptions.useSFAKey.HasValue)
                this.initParams["useCoreKitKey"] = this.web3AuthOptions.useSFAKey.Value;

            if (this.web3AuthOptions.mfaSettings != null)
                this.initParams["mfaSettings"] = JsonConvert.SerializeObject(this.web3AuthOptions.mfaSettings, settings);

            if (this.web3AuthOptions.sessionTime != null)
                this.initParams["sessionTime"] = this.web3AuthOptions.sessionTime;

            if (this.web3AuthOptions.dashboardUrl != null)
                this.initParams["dashboardUrl"] = this.web3AuthOptions.dashboardUrl;
        }
    }

    private void onDeepLinkActivated(string url)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!string.IsNullOrEmpty(url) && url == lastProcessedAndroidDeepLink)
            return;
        if (!string.IsNullOrEmpty(url))
            lastProcessedAndroidDeepLink = url;
#endif
        this.setResultUrl(new Uri(url));
    }

    private static string GetRedirectUrlString(Uri redirectUri)
    {
        if (redirectUri == null)
            return null;
        if (!string.IsNullOrEmpty(redirectUri.OriginalString))
            return redirectUri.OriginalString.Split('#')[0].Split('?')[0].TrimEnd('/');
        return redirectUri.AbsoluteUri.Split('#')[0].Split('?')[0].TrimEnd('/');
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private string lastProcessedAndroidDeepLink;

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
            TryConsumeAndroidDeepLink();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus)
            TryConsumeAndroidDeepLink();
    }

    private void TryConsumeAndroidDeepLink()
    {
        try
        {
            string url = null;
            using (var activityClass = new AndroidJavaClass("com.web3auth.unity.android.Web3AuthActivity"))
            {
                url = activityClass.CallStatic<string>("consumePendingDeepLinkUrl");
            }

            if (string.IsNullOrEmpty(url))
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var intent = activity.Call<AndroidJavaObject>("getIntent"))
                {
                    if (intent == null)
                        return;

                    using (var data = intent.Call<AndroidJavaObject>("getData"))
                    {
                        if (data == null)
                            return;
                        url = data.Call<string>("toString");
                        if (string.IsNullOrEmpty(url) || !url.StartsWith("torusapp://", StringComparison.OrdinalIgnoreCase))
                            return;
                        intent.Call("setData", null as AndroidJavaObject);
                    }
                }
            }

            if (string.IsNullOrEmpty(url))
                return;

            if (url == lastProcessedAndroidDeepLink)
                return;

            lastProcessedAndroidDeepLink = url;
            Debug.Log("Web3Auth Android deep link: " + url);
            onDeepLinkActivated(url);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("TryConsumeAndroidDeepLink failed: " + ex.Message);
        }
    }
#endif

#if UNITY_STANDALONE || UNITY_EDITOR
    private void StopLocalWebserver()
    {
        if (localHttpListener == null)
            return;

        try
        {
            if (localHttpListener.IsListening)
                localHttpListener.Stop();
            localHttpListener.Close();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed to stop local redirect server: " + ex.Message);
        }
        finally
        {
            localHttpListener = null;
        }
    }

    private string StartLocalWebserver()
    {
        // Always free the previous listener so a second wallet/MFA/sign flow can bind the same port.
        StopLocalWebserver();

        localHttpListener = new HttpListener();

        var redirectUrl = Utils.GetLocalRedirectBaseUrl(this.web3AuthOptions?.localRedirectHost);

        localHttpListener.Prefixes.Add($"{redirectUrl}/complete/");
        localHttpListener.Prefixes.Add($"{redirectUrl}/auth/");
        try
        {
            localHttpListener.Start();
        }
        catch (HttpListenerException ex)
        {
            localHttpListener = null;
            throw new Exception(
                $"Failed to start local redirect server at {redirectUrl}. " +
                $"Make sure the host resolves to this machine and the port is free. ({ex.Message})");
        }
        localHttpListener.BeginGetContext(new AsyncCallback(IncomingHttpRequest), localHttpListener);

        return redirectUrl + "/complete/";
    }

    private void IncomingHttpRequest(IAsyncResult result)
    {
        HttpListener httpListener = (HttpListener)result.AsyncState;

        // Listener may have been stopped/replaced by a newer StartLocalWebserver call.
        if (httpListener == null || !httpListener.IsListening)
            return;

        HttpListenerContext httpContext;
        try
        {
            httpContext = httpListener.EndGetContext(result);
        }
        catch (ObjectDisposedException)
        {
            return;
        }
        catch (HttpListenerException)
        {
            return;
        }

        HttpListenerRequest httpRequest = httpContext.Request;
        HttpListenerResponse httpResponse = httpContext.Response;

        if (httpRequest.Url.LocalPath == "/complete/")
        {
            httpListener.BeginGetContext(new AsyncCallback(IncomingHttpRequest), httpListener);

            var responseString = @"
                <!DOCTYPE html>
                <html>
                <head>
                  <meta charset=""utf-8"">
                  <meta name=""viewport"" content=""width=device-width"">
                  <title>Web3Auth</title>
                  <link href=""https://fonts.googleapis.com/css2?family=DM+Sans:wght@500&display=swap"" rel=""stylesheet"">
                </head>
                <body style=""padding:0;margin:0;font-size:10pt;font-family: 'DM Sans', sans-serif;"">
                  <div style=""display:flex;align-items:center;justify-content:center;height:100vh;display: none;"" id=""success"">
                    <div style=""text-align:center"">
                       <h2 style=""margin-bottom:0""> Authenticated successfully</h2>
                       <p> You can close this tab/window now </p>
                    </div>
                  </div>
                  <div style=""display:flex;align-items:center;justify-content:center;height:100vh;display: none;"" id=""error"">
                    <div style=""text-align:center"">
                       <h2 style=""margin-bottom:0""> Authentication failed</h2>
                       <p> Please try again </p>
                    </div>
                  </div>
                  <script>
                    if (window.location.hash.trim() == """") {
                        document.querySelector(""#error"").style.display=""flex"";
                    } else {
                        fetch(`http://${window.location.host}/auth/?code=${encodeURIComponent(window.location.hash.slice(1))}`).then(function(response) {
                          console.log(response);
                          document.querySelector(""#success"").style.display=""flex"";
                        }).catch(function(error) {
                          console.log(error);
                          document.querySelector(""#error"").style.display=""flex"";
                        });
                    }
                    
                  </script>
                </body>
                </html>
            ";

            byte[] buffer = Encoding.UTF8.GetBytes(responseString);

            httpResponse.ContentLength64 = buffer.Length;
            Stream output = httpResponse.OutputStream;
            output.Write(buffer, 0, buffer.Length);
            output.Close();

        }

        if (httpRequest.Url.LocalPath == "/auth/")
        {
            var responseString = @"ok";

            byte[] buffer = Encoding.UTF8.GetBytes(responseString);

            httpResponse.ContentLength64 = buffer.Length;
            Stream output = httpResponse.OutputStream;
            output.Write(buffer, 0, buffer.Length);
            output.Close();
            string code = httpRequest.QueryString.Get("code");
            if (!string.IsNullOrEmpty(code))
            {
                this.setResultUrl(new Uri($"http://localhost#{code}"));
            }

            if (ReferenceEquals(localHttpListener, httpListener))
                StopLocalWebserver();
            else
            {
                try { httpListener.Close(); } catch { /* ignored */ }
            }
        }
    }
#endif

    private async void processRequest(string path, LoginParams loginParams = null)
    {
        redirectUrl = this.initParams["redirectUrl"].ToString();
        if (redirectUrl.EndsWith("/"))
        {
            redirectUrl = redirectUrl.TrimEnd('/');
        }
        // Prefer casing from Web3AuthOptions when available.
        if (web3AuthOptions?.redirectUrl != null)
            redirectUrl = GetRedirectUrlString(web3AuthOptions.redirectUrl) ?? redirectUrl;

#if UNITY_STANDALONE || UNITY_EDITOR
        this.initParams["redirectUrl"] = StartLocalWebserver();
        redirectUrl = this.initParams["redirectUrl"].ToString().Replace("/complete/", "");
#elif UNITY_WEBGL
        this.initParams["redirectUrl"] = Utils.GetCurrentURL();
#endif

        var sessionId = KeyStoreManagerUtils.generateRandomSessionKey();
        if(path == "manage_mfa") {
            loginParams.dappUrl = this.initParams["redirectUrl"].ToString();
            this.initParams["redirectUrl"] = new Uri(this.initParams["dashboardUrl"].ToString());
            var loginIdObject = new Dictionary<string, string>
            {
                { "loginId", sessionId },
                { "platform", "unity" },
            };
            string loginIdBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(loginIdObject, Formatting.None,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                })));
            loginParams.appState = loginIdBase64;
        }

        Dictionary<string, object> paramMap = new Dictionary<string, object>();
        paramMap["options"] = this.initParams;
        paramMap["params"] = loginParams == null ? (object)new Dictionary<string, object>() : (object)loginParams;
        paramMap["actionType"] = path;

        if (path == "enable_mfa" || path == "manage_mfa")
        {
            string savedSessionId = KeyStoreManagerUtils.getPreferencesData(KeyStoreManagerUtils.SESSION_ID);
            paramMap["sessionId"] = savedSessionId;
        }

        string loginId = await createSession(JsonConvert.SerializeObject(paramMap, Formatting.None,
            new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            }), 600, this.initParams["redirectUrl"].ToString(), sessionId);

        if (!string.IsNullOrEmpty(loginId))
        {
            var loginIdObject = new Dictionary<string, string>
             {
                  { "loginId", loginId }
             };
            string hash = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(loginIdObject, Formatting.None,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                })));

            UriBuilder uriBuilder = new UriBuilder(this.web3AuthOptions.sdkUrl);
            if(this.web3AuthOptions.sdkUrl.Contains("develop"))
            {
                uriBuilder.Path = "/" + "start";
            }
            else
            {
                uriBuilder.Path += "/" + "start";
            }
            uriBuilder.Fragment = "b64Params=" + hash;
            isRequestResponse = false;
            Utils.LaunchUrl(uriBuilder.ToString(), this.initParams["redirectUrl"].ToString(), gameObject.name);
        }
        else
        {
            throw new Exception("Some went wrong. Please try again later.");
        }
    }

    public async void showWalletUI(string path = "wallet")
    {
            string sessionId = KeyStoreManagerUtils.getPreferencesData(KeyStoreManagerUtils.SESSION_ID);
            if (!string.IsNullOrEmpty(sessionId))
            {
                redirectUrl = this.initParams["redirectUrl"].ToString();
                if (redirectUrl.EndsWith("/"))
                {
                    redirectUrl = redirectUrl.TrimEnd('/');
                }
#if UNITY_STANDALONE || UNITY_EDITOR
                this.initParams["redirectUrl"] = StartLocalWebserver();
                redirectUrl = this.initParams["redirectUrl"].ToString().Replace("/complete/", "");
#elif UNITY_WEBGL
            this.initParams["redirectUrl"] = Utils.GetCurrentURL();
#endif

                if (projectConfigResponse?.chains != null && projectConfigResponse.chains.Count > 0)
    			{
        			string chainsJson = JsonConvert.SerializeObject(projectConfigResponse.chains, Formatting.None, new JsonSerializerSettings
        			{
            				Converters = new List<JsonConverter> { new StringEnumConverter() },
            				NullValueHandling = NullValueHandling.Ignore
        			});
    				
        			this.initParams["chains"] = chainsJson;

       				// Set defaultChainId and chainId based on the first chain
        			var firstChainId = projectConfigResponse.chains[0]?.chainId ?? web3AuthOptions.defaultChainId ?? "0x1";
        			this.initParams["defaultChainId"] = firstChainId;
        			this.initParams["chainId"] = firstChainId;
    			}
    			else
    			{
        			// Fallback to web3AuthOptions.defaultChainId or "0x1"
        			string fallbackChainId = web3AuthOptions.defaultChainId ?? "0x1";
        			this.initParams["defaultChainId"] = fallbackChainId;
        			this.initParams["chainId"] = fallbackChainId;
    			}
                
                if (projectConfigResponse?.embeddedWalletAuth != null)
                {
                    this.initParams["embeddedWalletAuth"] = projectConfigResponse.embeddedWalletAuth;
                }
                
                if (projectConfigResponse?.smartAccounts != null)
                {
                    this.initParams["smartAccounts"] = projectConfigResponse.smartAccounts;
                } 
                Dictionary<string, object> paramMap = new Dictionary<string, object>();
                paramMap["options"] = this.initParams;

                //Debug.Log("paramMap: =>" + JsonConvert.SerializeObject(paramMap));
                var newSessionId = KeyStoreManagerUtils.generateRandomSessionKey();
                string loginId = await createSession(JsonConvert.SerializeObject(paramMap, Formatting.None,
                    new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore
                    }), 600, this.initParams["redirectUrl"].ToString(), newSessionId);

                if (!string.IsNullOrEmpty(loginId))
                {
                    var loginIdObject = new Dictionary<string, string>
                    {
                        { "loginId", loginId },
                        { "sessionId", sessionId },
                        { "platform", "unity" }
                    };
                    string hash = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(loginIdObject, Formatting.None,
                        new JsonSerializerSettings
                        {
                            NullValueHandling = NullValueHandling.Ignore
                        })));

                    UriBuilder uriBuilder = new UriBuilder(this.web3AuthOptions.walletSdkUrl);
                    if(this.web3AuthOptions.sdkUrl.Contains("develop"))
                    {
                        uriBuilder.Path = "/" + path;
                    }
                    else
                    {
                        uriBuilder.Path += "/" + path;
                    }
                    uriBuilder.Fragment = "b64Params=" + hash;
                    //Debug.Log("finalUriBuilderToOpen: =>" + uriBuilder.ToString());
                    isRequestResponse = false;
                    Utils.LaunchUrl(uriBuilder.ToString(), this.initParams["redirectUrl"].ToString(), gameObject.name);
                }
                else
                {
                    throw new Exception("Some went wrong. Please try again later.");
                }
            } else
            {
                throw new Exception("SessionId not found. Please login first.");
            }
    }

    public void setResultUrl(Uri uri)
    {
        if (uri == null)
        {
            Debug.LogWarning("Web3Auth setResultUrl: uri is null");
            return;
        }

        Debug.Log("Web3Auth setResultUrl: " + uri);

        string hash = uri.Fragment;
#if !UNITY_EDITOR && UNITY_WEBGL
        if (hash == null || hash.Length == 0)
            return;
#else
        // Custom Tabs may put session data in query instead of fragment.
        if (string.IsNullOrEmpty(hash) || hash == "#")
        {
            if (!string.IsNullOrEmpty(uri.Query) && uri.Query.Length > 1)
            {
                hash = uri.Query;
            }
            else
            {
                Debug.LogError("Web3Auth setResultUrl: missing fragment/query in redirect URL: " + uri);
                return;
            }
        }
#endif
        if (hash.StartsWith("#") || hash.StartsWith("?"))
            hash = hash.Substring(1);

        Dictionary<string, string> queryParameters = Utils.ParseQuery(uri.Query);
        if (queryParameters.Keys.Contains("error"))
            throw new UnKnownException(queryParameters["error"]);

        Dictionary<string, string> hashParameters = Utils.ParseQuery("?" + hash);
        string b64Params = null;
        if (hashParameters != null && hashParameters.ContainsKey("b64Params"))
            b64Params = hashParameters["b64Params"];
        if (string.IsNullOrEmpty(b64Params))
            b64Params = getQueryParamValue(uri, "b64Params");

        string decodedString = decodeBase64Params(b64Params);
        if (string.IsNullOrEmpty(decodedString))
        {
            Debug.LogError("Web3Auth setResultUrl: failed to decode b64Params from: " + uri);
            return;
        }
        Debug.Log("Web3Auth setResultUrl decoded: " + decodedString);

        if (decodedString.Contains("actionType"))
        {
            RedirectResponse response = JsonUtility.FromJson<RedirectResponse>(decodedString);
            if (response.actionType == "manage_mfa")
            {
                this.Enqueue(() => this.onManageMFA?.Invoke(true));
                return;
            }
        }
        if(isRequestResponse) {
            try
            {
                signResponse = JsonUtility.FromJson<SignResponse>(decodedString);
                this.Enqueue(() => this.onSignResponse?.Invoke(signResponse));
            }
            catch (Exception)
            {
            }
            isRequestResponse = false;
            return;
        }
        SessionResponse sessionResponse = null;
        try
        {
            sessionResponse = JsonConvert.DeserializeObject<SessionResponse>(decodedString);
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to decode session JSON: " + e.Message + " raw=" + decodedString);
        }
        if (sessionResponse == null || string.IsNullOrEmpty(sessionResponse.sessionId))
        {
            Debug.LogError("Invalid or missing session response (sessionId is null or empty). Decoded: " + decodedString);
            return;
        }
        string sessionId = KeyStoreManagerUtils.normalizeSessionId(sessionResponse.sessionId);
        if (!KeyStoreManagerUtils.isValidSessionId(sessionId))
        {
            Debug.LogError(
                "Invalid sessionId format from redirect (expected hex private key). " +
                $"length={sessionResponse.sessionId?.Length}, decoded={decodedString}");
            return;
        }

        string resolvedRedirectUrl = redirectUrl;
        if (string.IsNullOrEmpty(resolvedRedirectUrl) && initParams != null && initParams.ContainsKey("redirectUrl") && initParams["redirectUrl"] != null)
            resolvedRedirectUrl = initParams["redirectUrl"].ToString();
        if (string.IsNullOrEmpty(resolvedRedirectUrl) && web3AuthOptions?.redirectUrl != null)
            resolvedRedirectUrl = GetRedirectUrlString(web3AuthOptions.redirectUrl);

#if UNITY_EDITOR || UNITY_STANDALONE
        // Editor uses local redirect; auth stores that origin.
        if (!string.IsNullOrEmpty(this.redirectUrl) &&
            (this.redirectUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
             this.redirectUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            resolvedRedirectUrl = this.redirectUrl.Replace("/complete/", "").TrimEnd('/');
        }
        else if (initParams != null && initParams.ContainsKey("redirectUrl") && initParams["redirectUrl"] != null)
        {
            var initRedirect = initParams["redirectUrl"].ToString();
            if (!string.IsNullOrEmpty(initRedirect) &&
                (initRedirect.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                 initRedirect.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                resolvedRedirectUrl = initRedirect.Replace("/complete/", "").TrimEnd('/');
            }
        }
#else
        if (!string.IsNullOrEmpty(resolvedRedirectUrl) && web3AuthOptions?.redirectUrl != null)
        {
            var preferred = GetRedirectUrlString(web3AuthOptions.redirectUrl);
            if (!string.IsNullOrEmpty(preferred))
                resolvedRedirectUrl = preferred;
        }
#endif

        Debug.Log("Web3Auth setResultUrl authorizing sessionId length=" + sessionId.Length + " redirect=" + resolvedRedirectUrl);


        this.Enqueue(() =>
        {
            KeyStoreManagerUtils.savePreferenceData(KeyStoreManagerUtils.SESSION_ID, sessionId);
            KeyStoreManagerUtils.savePreferenceData(KeyStoreManagerUtils.REDIRECT_URL, resolvedRedirectUrl);
            authorizeSession(sessionId, resolvedRedirectUrl);
        });

#if !UNITY_EDITOR && UNITY_WEBGL
        if (this.web3AuthResponse != null) 
        {
            Utils.RemoveAuthCodeFromURL();
        } 
#endif
    }

    private string getQueryParamValue(Uri uri, string key)
    {
        string value = "";
        if (uri.Query != null && uri.Query.Length > 0)
        {
            string[] queryParameters = uri.Query.Substring(1).Split('&');
            foreach (string queryParameter in queryParameters)
            {
                int separator = queryParameter.IndexOf('=');
                if (separator <= 0)
                    continue;

                string paramKey = Uri.UnescapeDataString(queryParameter.Substring(0, separator));
                if (paramKey != key)
                    continue;

                value = Uri.UnescapeDataString(queryParameter.Substring(separator + 1));
                break;
            }
        }
        return value;
    }

    private string decodeBase64Params(string base64Params)
    {
        if(string.IsNullOrEmpty(base64Params))
            return string.Empty;
        // Replace URL-safe characters and spaces introduced by poorly encoded query strings
        base64Params = base64Params.Replace('-', '+').Replace('_', '/').Replace(' ', '+');
        var d = base64Params.Length % 4;
        if (d != 0)
        {
            base64Params = base64Params.TrimEnd('=');
            base64Params += d % 2 > 0 ? "=" : "==";
        }
        byte[] bytes = Convert.FromBase64String(base64Params);
        var decodedString = Encoding.UTF8.GetString(bytes);
        return decodedString;
    }

    public void login(LoginParams loginParams)
    {
        if (web3AuthOptions.authConnectionConfig != null)
        {
            var authConnectionItem = web3AuthOptions.authConnectionConfig?.FirstOrDefault();
            var share = KeyStoreManagerUtils.getPreferencesData(authConnectionItem?.authConnectionId);

            if (!string.IsNullOrEmpty(share))
            {
                loginParams.dappShare = share;
            }
        }

        processRequest("login", loginParams);
    }

    public void logout(Dictionary<string, object> extraParams)
    {
        sessionTimeOutAPI();
    }

    public void logout(Uri redirectUrl = null, string appState = null)
    {
        Dictionary<string, object> extraParams = new Dictionary<string, object>();
        if (redirectUrl != null)
            extraParams["redirectUrl"] = redirectUrl.ToString();

        if (appState != null)
            extraParams["appState"] = appState;

        logout(extraParams);
    }

    public void enableMFA(LoginParams loginParams)
    {
        if(web3AuthResponse.userInfo.isMfaEnabled == true)
        {
            throw new Exception("MFA is already enabled for this user.");
        }
        string sessionId = KeyStoreManagerUtils.getPreferencesData(KeyStoreManagerUtils.SESSION_ID);
        if (!string.IsNullOrEmpty(sessionId))
        {
            if (web3AuthOptions.authConnectionConfig != null)
            {
                var authConnectionItem = web3AuthOptions.authConnectionConfig?.FirstOrDefault();
                var share = KeyStoreManagerUtils.getPreferencesData(authConnectionItem?.authConnectionId);
                if (!string.IsNullOrEmpty(share))
                   {
                       loginParams.dappShare = share;
                   }
            }
            processRequest("enable_mfa", loginParams);
        }
        else
        {
            throw new Exception("SessionId not found. Please login first.");
        }
    }

    public void manageMFA(LoginParams loginParams)
    {
        if(web3AuthResponse.userInfo.isMfaEnabled == false)
        {
            throw new Exception("MFA is not enabled. Please enable MFA first.");
        }
        string sessionId = KeyStoreManagerUtils.getPreferencesData(KeyStoreManagerUtils.SESSION_ID);
        if (!string.IsNullOrEmpty(sessionId))
        {
            if (web3AuthOptions.authConnectionConfig != null)
            {
                var authConnectionItem = web3AuthOptions.authConnectionConfig?.FirstOrDefault();
                var share = KeyStoreManagerUtils.getPreferencesData(authConnectionItem?.authConnectionId);
                if (!string.IsNullOrEmpty(share))
                   {
                       loginParams.dappShare = share;
                   }
            }
            processRequest("manage_mfa", loginParams);
        }
        else
        {
            throw new Exception("SessionId not found. Please login first.");
        }
    }

    public async void request(string method, JArray requestParams, string path = "wallet/request") {
        string sessionId = KeyStoreManagerUtils.getPreferencesData(KeyStoreManagerUtils.SESSION_ID);
        if (!string.IsNullOrEmpty(sessionId))
        {
                    redirectUrl = this.initParams["redirectUrl"].ToString();
                    if (redirectUrl.EndsWith("/"))
                    {
                        redirectUrl = redirectUrl.TrimEnd('/');
                    }
#if UNITY_STANDALONE || UNITY_EDITOR
                    this.initParams["redirectUrl"] = StartLocalWebserver();
                    redirectUrl = this.initParams["redirectUrl"].ToString().Replace("/complete/", "");
#elif UNITY_WEBGL
                    this.initParams["redirectUrl"] = Utils.GetCurrentURL();
#endif

                   if (projectConfigResponse?.chains != null && projectConfigResponse.chains.Count > 0)
    			   {
        				string chainsJson = JsonConvert.SerializeObject(projectConfigResponse.chains, Formatting.None, new JsonSerializerSettings
        				{
            				Converters = new List<JsonConverter> { new StringEnumConverter() },
            				NullValueHandling = NullValueHandling.Ignore
        				});
    					Debug.Log("Chain JSON:\n" + chainsJson);
        				this.initParams["chains"] = chainsJson;

       					// Set defaultChainId and chainId based on the first chain
        				var firstChainId = projectConfigResponse.chains[0]?.chainId ?? web3AuthOptions.defaultChainId ?? "0x1";
        				this.initParams["defaultChainId"] = firstChainId;
        				this.initParams["chainId"] = firstChainId;
    				}
    				else
    				{
        				// Fallback to web3AuthOptions.defaultChainId or "0x1"
        				string fallbackChainId = web3AuthOptions.defaultChainId ?? "0x1";
        				this.initParams["defaultChainId"] = fallbackChainId;
        				this.initParams["chainId"] = fallbackChainId;
    				}
 
                    if (projectConfigResponse?.embeddedWalletAuth != null)
                    {
                        this.initParams["embeddedWalletAuth"] = projectConfigResponse.embeddedWalletAuth;
                    }
                
                    if (projectConfigResponse?.smartAccounts != null)
                    {
                        this.initParams["smartAccounts"] = projectConfigResponse.smartAccounts;
                    }
                    Dictionary<string, object> paramMap = new Dictionary<string, object>();
                    paramMap["options"] = this.initParams;

                    foreach (KeyValuePair<string, object> entry in paramMap)
                    {
                        Debug.Log($"Key: {entry.Key}, Value: {JsonUtility.ToJson(entry.Value)}");
                    }

                    var newSessionId = KeyStoreManagerUtils.generateRandomSessionKey();
					//Debug.Log("paramMap durinr request func: =>" + JsonConvert.SerializeObject(paramMap));
                    string loginId = await createSession(JsonConvert.SerializeObject(paramMap, Formatting.None,
                        new JsonSerializerSettings
                        {
                            NullValueHandling = NullValueHandling.Ignore
                        }), 600, this.initParams["redirectUrl"].ToString(), newSessionId);

                    if (!string.IsNullOrEmpty(loginId))
                    {
                        JObject requestData = new JObject
                        {
                            { "method", method },
                            { "params", JsonConvert.SerializeObject(requestParams) }
                        };
                        JObject signMessageMap = new JObject
                        {
                            { "loginId", loginId },
                            { "sessionId", sessionId },
                            {"platform", "unity" },
                            { "request", JsonConvert.SerializeObject(requestData) }
                        };

                        string hash = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(signMessageMap, Formatting.None,
                            new JsonSerializerSettings
                            {
                                NullValueHandling = NullValueHandling.Ignore
                            })));

                        UriBuilder uriBuilder = new UriBuilder(this.web3AuthOptions.walletSdkUrl);
                        if(this.web3AuthOptions.sdkUrl.Contains("develop"))
                        {
                            uriBuilder.Path = "/" + path;
                        }
                        else
                        {
                            uriBuilder.Path += "/" + path;
                        }
                        uriBuilder.Fragment = "b64Params=" + hash;
                        isRequestResponse = true;
                        Utils.LaunchUrl(uriBuilder.ToString(), this.initParams["redirectUrl"].ToString(), gameObject.name);
                    }
                    else
                    {
                        throw new Exception("Some went wrong. Please try again later.");
                    }
        }
        else
        {
            throw new Exception("SessionId not found. Please login first.");
        }
    }

    static ShareMetadata parseShareMetadataFromStoreMessage(string message)
    {
        if (string.IsNullOrEmpty(message))
            return null;

        try
        {
            var direct = JsonConvert.DeserializeObject<ShareMetadata>(message);
            if (direct != null && !string.IsNullOrEmpty(direct.iv) && !string.IsNullOrEmpty(direct.ephemPublicKey))
                return direct;
        }
        catch
        {
            /* try wrapped form */
        }

        try
        {
            var jo = JObject.Parse(message);
            var valueToken = jo["value"];
            if (valueToken != null && valueToken.Type == JTokenType.String)
            {
                var inner = valueToken.ToString();
                return JsonConvert.DeserializeObject<ShareMetadata>(inner);
            }

            if (valueToken != null && valueToken.Type == JTokenType.Object)
                return valueToken.ToObject<ShareMetadata>();
        }
        catch
        {
            /* fall through */
        }

        return null;
    }

    private void authorizeSession(string newSessionId, string origin, bool quietIfEmpty = false)
    {
        string sessionId = "";
        if (string.IsNullOrEmpty(newSessionId))
        {
            sessionId = KeyStoreManagerUtils.getPreferencesData(KeyStoreManagerUtils.SESSION_ID);
        }
        else
        {
            sessionId = newSessionId;
        }

        if (!string.IsNullOrEmpty(sessionId))
        {
            sessionId = KeyStoreManagerUtils.normalizeSessionId(sessionId);
            if (!KeyStoreManagerUtils.isValidSessionId(sessionId))
            {
                Debug.LogError($"authorizeSession: stored sessionId is not valid hex (length={sessionId.Length}). Clearing it.");
                KeyStoreManagerUtils.deletePreferencesData(KeyStoreManagerUtils.SESSION_ID);
                return;
            }

            var pubKey = KeyStoreManagerUtils.getPubKey(sessionId);
            if (string.IsNullOrEmpty(pubKey))
            {
                Debug.LogError("authorizeSession: failed to derive public key from sessionId.");
                return;
            }
            StartCoroutine(Web3AuthApi.getInstance().authorizeSession(pubKey, origin, (response =>
            {
                try
                {
                if (response != null && !string.IsNullOrEmpty(response.message))
                {
                    var shareMetadata = parseShareMetadataFromStoreMessage(response.message);
                    if (shareMetadata == null || string.IsNullOrEmpty(shareMetadata.ephemPublicKey) ||
                        string.IsNullOrEmpty(shareMetadata.iv) || string.IsNullOrEmpty(shareMetadata.ciphertext) ||
                        string.IsNullOrEmpty(shareMetadata.mac))
                    {
                        Debug.LogError("Invalid or missing share metadata.");
                        return;
                    }

                    var aes256cbc = new AES256CBC(
                        sessionId,
                        shareMetadata.ephemPublicKey,
                        shareMetadata.iv
                    );

                    var encryptedShareBytes = AES256CBC.toByteArray(new BigInteger(shareMetadata.ciphertext, 16));
                    var share = aes256cbc.decrypt(encryptedShareBytes, shareMetadata.mac);
                    var tempJson = JsonConvert.DeserializeObject<JObject>(Encoding.UTF8.GetString(share));
                    if (tempJson == null)
                    {
                        Debug.LogError("Invalid or missing tempJson.");
                        return;
                    }

                    this.web3AuthResponse = JsonConvert.DeserializeObject<Web3AuthResponse>(tempJson.ToString());
                    if (this.web3AuthResponse != null)
                    {
                        if (this.web3AuthResponse.error != null)
                        {
                            throw new UnKnownException(this.web3AuthResponse.error ?? "Something went wrong");
                        }

                        if (!string.IsNullOrEmpty(this.web3AuthResponse.sessionId))
                        {
                            var responseSessionId = KeyStoreManagerUtils.normalizeSessionId(this.web3AuthResponse.sessionId);
                            if (KeyStoreManagerUtils.isValidSessionId(responseSessionId))
                            {
                                KeyStoreManagerUtils.savePreferenceData(KeyStoreManagerUtils.SESSION_ID, responseSessionId);
                            }
                        }

                        if (web3AuthResponse.userInfo != null && !string.IsNullOrEmpty(web3AuthResponse.userInfo.dappShare) &&
                            !string.IsNullOrEmpty(web3AuthResponse.userInfo.authConnectionId))
                        {
                            KeyStoreManagerUtils.savePreferenceData(
                                        web3AuthResponse.userInfo.authConnectionId, web3AuthResponse.userInfo.dappShare
                            );
                        }

                        bool hasKey =
                            !string.IsNullOrEmpty(this.web3AuthResponse.privateKey) &&
                            !string.IsNullOrEmpty(this.web3AuthResponse.privateKey.Trim('0'));
                        bool hasCoreKitKey =
                            !string.IsNullOrEmpty(this.web3AuthResponse.coreKitKey) &&
                            !string.IsNullOrEmpty(this.web3AuthResponse.coreKitKey.Trim('0'));
                        bool hasUser =
                            this.web3AuthResponse.userInfo != null &&
                            (!string.IsNullOrEmpty(this.web3AuthResponse.userInfo.email) ||
                             !string.IsNullOrEmpty(this.web3AuthResponse.userInfo.name) ||
                             !string.IsNullOrEmpty(this.web3AuthResponse.userInfo.userId));

                        if (!hasKey && !hasCoreKitKey && !hasUser)
                        {
                            Debug.LogError("Web3Auth authorizeSession: response had no private key/userInfo. Invoking onLogout. raw=" + tempJson);
                            this.Enqueue(() => this.onLogout?.Invoke());
                        }
                        else
                        {
                            Debug.Log("Web3Auth authorizeSession success, invoking onLogin");
                            this.Enqueue(() => this.onLogin?.Invoke(this.web3AuthResponse));
                            this.Enqueue(() => this.onMFASetup?.Invoke(true));
                        }
                    }
                    else
                    {
                        Debug.LogError("Web3Auth authorizeSession: web3AuthResponse was null after decrypt");
                    }
                }
                else
                {
                    if (quietIfEmpty)
                    {
                        Debug.Log("Web3Auth authorizeSession: no active session to restore");
                        KeyStoreManagerUtils.deletePreferencesData(KeyStoreManagerUtils.SESSION_ID);
                    }
                    else
                    {
                        Debug.LogError("Web3Auth authorizeSession: empty/null store response for session authorize");
                    }
                }
                }
                catch (Exception ex)
                {
                    Debug.LogError("Web3Auth authorizeSession callback failed: " + ex);
                }

            }), quiet: quietIfEmpty));
        }
    }

    private string resolveSessionRedirectUrl()
    {
        var redirectUrl = KeyStoreManagerUtils.getPreferencesData(KeyStoreManagerUtils.REDIRECT_URL);
        if (string.IsNullOrEmpty(redirectUrl) && web3AuthOptions?.redirectUrl != null)
            redirectUrl = GetRedirectUrlString(web3AuthOptions.redirectUrl);
#if UNITY_EDITOR || UNITY_STANDALONE
        if (!string.IsNullOrEmpty(this.redirectUrl) &&
            (this.redirectUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
             this.redirectUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            redirectUrl = this.redirectUrl.Replace("/complete/", "").TrimEnd('/');
        }
        else if (!string.IsNullOrEmpty(redirectUrl) && redirectUrl.StartsWith("torusapp://", StringComparison.OrdinalIgnoreCase))
        {
            var localHost = web3AuthOptions?.localRedirectHost ?? Utils.LOCAL_REDIRECT_HOST;
            redirectUrl = $"http://{localHost}:{Utils.LOCAL_REDIRECT_PORT}";
        }
#endif
        return redirectUrl;
    }

    private void clearLocalSessionAndNotifyLogout()
    {
        try
        {
            KeyStoreManagerUtils.deletePreferencesData(KeyStoreManagerUtils.SESSION_ID);
            KeyStoreManagerUtils.deletePreferencesData(KeyStoreManagerUtils.REDIRECT_URL);
            if (web3AuthOptions?.authConnectionConfig != null)
                KeyStoreManagerUtils.deletePreferencesData(web3AuthOptions.authConnectionConfig?.FirstOrDefault()?.authConnectionId);
            web3AuthResponse = null;
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to delete session data: " + ex.Message);
        }
        this.Enqueue(() => this.onLogout?.Invoke());
    }

    private void sessionTimeOutAPI()
    {
        string sessionId = KeyStoreManagerUtils.getPreferencesData(KeyStoreManagerUtils.SESSION_ID);
        string redirectUrl = resolveSessionRedirectUrl();

        if (string.IsNullOrEmpty(sessionId))
        {
            clearLocalSessionAndNotifyLogout();
            return;
        }

        sessionId = KeyStoreManagerUtils.normalizeSessionId(sessionId);
        var pubKey = KeyStoreManagerUtils.getPubKey(sessionId);
        if (string.IsNullOrEmpty(pubKey))
        {
            clearLocalSessionAndNotifyLogout();
            return;
        }

        StartCoroutine(Web3AuthApi.getInstance().authorizeSession(pubKey, redirectUrl, (response =>
        {
            if (response != null && !string.IsNullOrEmpty(response.message))
            {
                var shareMetadata = parseShareMetadataFromStoreMessage(response.message);
                if (shareMetadata != null &&
                    !string.IsNullOrEmpty(shareMetadata.ephemPublicKey) &&
                    !string.IsNullOrEmpty(shareMetadata.iv) &&
                    !string.IsNullOrEmpty(shareMetadata.ciphertext) &&
                    !string.IsNullOrEmpty(shareMetadata.mac))
                {
                    try
                    {
                        var aes256cbc = new AES256CBC(
                            sessionId,
                            shareMetadata.ephemPublicKey,
                            shareMetadata.iv
                        );

                        var encryptedData = aes256cbc.encrypt(Encoding.UTF8.GetBytes(""));
                        var encryptedMetadata = new ShareMetadata()
                        {
                            iv = shareMetadata.iv,
                            ephemPublicKey = shareMetadata.ephemPublicKey,
                            ciphertext = KeyStoreManagerUtils.convertByteToHexadecimal(encryptedData),
                            mac = shareMetadata.mac
                        };
                        var jsonData = JsonConvert.SerializeObject(encryptedMetadata);

                        StartCoroutine(Web3AuthApi.getInstance().logout(
                            new LogoutApiRequest()
                            {
                                key = KeyStoreManagerUtils.getPubKey(sessionId),
                                data = jsonData,
                                signature = KeyStoreManagerUtils.getECDSASignature(
                                    sessionId,
                                    jsonData
                                ),
                                timeout = 1
                            }, result =>
                            {
                                clearLocalSessionAndNotifyLogout();
                            }
                        ));
                        return;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("Web3Auth logout remote invalidate failed: " + ex.Message);
                    }
                }
            }

            clearLocalSessionAndNotifyLogout();
        }), quiet: true));
    }

    private async Task<string> createSession(string data, long sessionTime, string allowedOrigin, string sessionId)
    {
        TaskCompletionSource<string> createSessionResponse = new TaskCompletionSource<string>();
        var newSessionKey = sessionId;
        var ephemKey = KeyStoreManagerUtils.getPubKey(newSessionKey);
        var ivKey = KeyStoreManagerUtils.generateRandomBytes();

        var aes256cbc = new AES256CBC(
            newSessionKey,
            ephemKey,
            KeyStoreManagerUtils.convertByteToHexadecimal(ivKey)
        );
        var encryptedData = aes256cbc.encrypt(Encoding.UTF8.GetBytes(data));
        var mac = aes256cbc.getMac(encryptedData);
        var encryptedMetadata = new ShareMetadata()
        {
            iv = KeyStoreManagerUtils.convertByteToHexadecimal(ivKey),
            ephemPublicKey = ephemKey,
            ciphertext = KeyStoreManagerUtils.convertByteToHexadecimal(encryptedData),
            mac = KeyStoreManagerUtils.convertByteToHexadecimal(mac)
        };
        var jsonData = JsonConvert.SerializeObject(encryptedMetadata);
        StartCoroutine(Web3AuthApi.getInstance().createSession(
            new LogoutApiRequest()
            {
                key = KeyStoreManagerUtils.getPubKey(newSessionKey),
                data = jsonData,
                signature = KeyStoreManagerUtils.getECDSASignature(
                    newSessionKey,
                    jsonData
                ),
                timeout = Math.Min(sessionTime, 30 * 86400),
                allowedOrigin = allowedOrigin
            }, result =>
            {
                if (result != null)
                {
                    try
                    {
                        createSessionResponse.SetResult(newSessionKey);
                    }
                    catch (Exception)
                    {
                        createSessionResponse.SetException(new Exception("Something went wrong. Please try again later."));
                    }
                }
                else
                {
                    createSessionResponse.SetException(new Exception("Something went wrong. Please try again later."));
                }
            }
        ));
        return await createSessionResponse.Task;
    }

    private void SetAuthConnectionConfigInitParam(JsonSerializerSettings settings = null)
    {
        settings ??= new JsonSerializerSettings
        {
            Converters = new List<JsonConverter> { new StringEnumConverter() },
            NullValueHandling = NullValueHandling.Ignore
        };

        var config = this.web3AuthOptions?.authConnectionConfig;
        if (config != null && config.Count > 0)
            this.initParams["authConnectionConfig"] = JArray.FromObject(config, JsonSerializer.Create(settings));
        else
            this.initParams["authConnectionConfig"] = new JArray();
    }

    private async Task<bool> fetchProjectConfig()
    {
        TaskCompletionSource<bool> fetchProjectConfigResponse = new TaskCompletionSource<bool>();
        StartCoroutine(Web3AuthApi.getInstance().fetchProjectConfig(
            getResolvedClientId(),
            this.web3AuthOptions.web3AuthNetwork.ToString().ToLower(),
            this.web3AuthOptions.authBuildEnv.ToString().ToLower(), (response =>
        {
            projectConfigResponse = response;
            if (response != null)
            {
                this.web3AuthOptions.originData = this.web3AuthOptions.originData.mergeMaps(response.whitelist?.signed_urls);
                if (response?.whitelabel != null)
				{
    				var whitelabel = response.whitelabel;

    				this.web3AuthOptions.whiteLabel = this.web3AuthOptions.whiteLabel?.merge(whitelabel) ?? whitelabel;

    				if (this.web3AuthOptions.walletServicesConfig != null)
    				{
        				this.web3AuthOptions.walletServicesConfig.whiteLabel =
            				this.web3AuthOptions.walletServicesConfig.whiteLabel?.merge(whitelabel) ?? whitelabel;
    				}
				}

                JsonSerializerSettings settings = new JsonSerializerSettings
                {
                    Converters = new List<JsonConverter> { new StringEnumConverter() },
                    Formatting = Formatting.Indented
                };
                if (this.web3AuthOptions.whiteLabel != null)
                    this.initParams["whiteLabel"] = JsonConvert.SerializeObject(this.web3AuthOptions.whiteLabel, settings);

                if(this.web3AuthOptions.originData != null)
                    this.initParams["originData"] = JsonConvert.SerializeObject(this.web3AuthOptions.originData, settings);

                if (this.web3AuthOptions.walletServicesConfig != null)
                    this.initParams["walletServicesConfig"] = JObject.FromObject(this.web3AuthOptions.walletServicesConfig, JsonSerializer.Create(settings));

                if ((this.web3AuthOptions.authConnectionConfig == null || this.web3AuthOptions.authConnectionConfig.Count == 0)
                    && response.embeddedWalletAuth != null && response.embeddedWalletAuth.Count > 0)
                {
                    this.web3AuthOptions.authConnectionConfig = response.embeddedWalletAuth;
                }
                SetAuthConnectionConfigInitParam(settings);

                fetchProjectConfigResponse.SetResult(true);
            }
            else
            {
                fetchProjectConfigResponse.SetResult(false);
            }
        })));
        return await fetchProjectConfigResponse.Task;
    }

    public string getPrivateKey()
    {
        if (web3AuthResponse == null)
            return "";

        return web3AuthOptions.useSFAKey.Value ? web3AuthResponse.coreKitKey : web3AuthResponse.privateKey;
    }

    public string getEd25519PrivateKey()
    {
        if (web3AuthResponse == null)
            return "";

        return web3AuthOptions.useSFAKey.Value ? web3AuthResponse.coreKitEd25519PrivKey : web3AuthResponse.ed25519PrivateKey;
    }

    public UserInfo getUserInfo()
    {
        if (web3AuthResponse == null)
            throw new Exception(Web3AuthError.getError(ErrorCode.NOUSERFOUND));

        return web3AuthResponse.userInfo;
    }

    private void OnDisable()
    {
#if UNITY_STANDALONE || UNITY_EDITOR
        StopLocalWebserver();
#endif
    }

    private void OnDestroy()
    {
#if UNITY_EDITOR
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
#endif
#if UNITY_STANDALONE || UNITY_EDITOR
        StopLocalWebserver();
#endif
        Application.deepLinkActivated -= onDeepLinkActivated;
    }

    public void Update()
    {
        lock (_executionQueue)
        {
            while (_executionQueue.Count > 0)
            {
                _executionQueue.Dequeue().Invoke();
            }
        }
    }

    private void Enqueue(Action action)
    {
        lock (_executionQueue)
        {
            _executionQueue.Enqueue(() =>
            {
                StartCoroutine(ActionWrapper(action));
            });
        }
    }

    private IEnumerator ActionWrapper(Action a)
    {
        a();
        yield return null;
    }
}
