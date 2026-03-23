using System.Collections;
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;
using UnityEngine;
using System.Collections.Generic;

public class Web3AuthApi
{
    static Web3AuthApi instance;
    static string baseAddress = "https://session.web3auth.io/v2";

    public static Web3AuthApi getInstance()
    {
        if (instance == null)
            instance = new Web3AuthApi();
        return instance;
    }

    static string getOriginFromRedirectUrl(string redirectUrl)
    {
        if (string.IsNullOrEmpty(redirectUrl))
            return redirectUrl;
        if (!Uri.TryCreate(redirectUrl, UriKind.Absolute, out var uri))
            return redirectUrl;
        return uri.GetLeftPart(UriPartial.Authority);
    }

    public IEnumerator authorizeSession(string key, string redirectUrl, Action<StoreApiResponse> callback)
    {
        var requestURL = $"{baseAddress}/store/get";

        WWWForm data = new WWWForm();
        data.AddField("key", key);

        var request = UnityWebRequest.Post(requestURL, data);
        request.SetRequestHeader("origin", getOriginFromRedirectUrl(redirectUrl));

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string result = request.downloadHandler?.text;
            if (!string.IsNullOrEmpty(result))
            {
                StoreApiResponse response = null;
                try
                {
                    response = JsonConvert.DeserializeObject<StoreApiResponse>(result);
                }
                catch
                {
                    response = null;
                }

                callback(response);
            }
            else
            {
                callback(null);
            }
        }
        else
            callback(null);
    }

    public IEnumerator logout(LogoutApiRequest logoutApiRequest, Action<JObject> callback)
    {
        WWWForm data = new WWWForm();
        data.AddField("key", logoutApiRequest.key);
        data.AddField("data", logoutApiRequest.data);
        data.AddField("signature", logoutApiRequest.signature);
        data.AddField("timeout", logoutApiRequest.timeout.ToString());

        var request = UnityWebRequest.Post($"{baseAddress}/store/set", data);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string result = request.downloadHandler.text;
            callback(JsonConvert.DeserializeObject<JObject>(result));
        }
        else
            callback(null);
    }

    public IEnumerator createSession(LogoutApiRequest logoutApiRequest, Action<JObject> callback)
    {
        WWWForm data = new WWWForm();
        data.AddField("key", logoutApiRequest.key);
        data.AddField("data", logoutApiRequest.data);
        data.AddField("signature", logoutApiRequest.signature);
        data.AddField("timeout", logoutApiRequest.timeout.ToString());
        // Debug.Log("key during create session =>" + logoutApiRequest.key);
        var request = UnityWebRequest.Post($"{baseAddress}/store/set", data);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string result = request.downloadHandler.text;
            callback(JsonConvert.DeserializeObject<JObject>(result));
        }
        else
            callback(null);
    }

    public IEnumerator fetchProjectConfig(string project_id, string network, Action<ProjectConfigResponse> callback)
    {
        string baseUrl = SIGNER_MAP[network];
        var requestURL =
            $"{baseUrl}/api/configuration?project_id={project_id}&network={network}&whitelist=true";

        var request = UnityWebRequest.Get(requestURL);

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string result = request.downloadHandler.text;
            callback(JsonConvert.DeserializeObject<ProjectConfigResponse>(result));
        }
        else
            callback(null);
    }

    public static Dictionary<string, string> SIGNER_MAP = new Dictionary<string, string>()
    {
        { "mainnet", "https://signer.web3auth.io" },
        { "testnet", "https://signer.web3auth.io" },
        { "cyan", "https://signer-polygon.web3auth.io" },
        { "aqua", "https://signer-polygon.web3auth.io" },
        { "sapphire_mainnet", "https://signer.web3auth.io" },
        { "sapphire_devnet", "https://signer.web3auth.io" }
    };
}
