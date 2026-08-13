using System.Collections;
using System;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;
using UnityEngine;
using System.Collections.Generic;

public class Web3AuthApi
{
    static Web3AuthApi instance;
    static readonly string[] sessionHosts = new[]
    {
        "https://session.web3auth.io/v2",
        "https://session-us.web3auth.io/v2",
        "https://session-sg.web3auth.io/v2",
    };

    public static Web3AuthApi getInstance()
    {
        if (instance == null)
            instance = new Web3AuthApi();
        return instance;
    }

    // Auth stores allowedOrigin as scheme://host (no path). Avoid System.Uri for custom schemes (lowercases host).
    static string getOriginFromRedirectUrl(string redirectUrl)
    {
        if (string.IsNullOrEmpty(redirectUrl))
            return redirectUrl;

        var cleaned = redirectUrl.Split('#')[0].Split('?')[0].TrimEnd('/');

        if (cleaned.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            cleaned.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (Uri.TryCreate(cleaned, UriKind.Absolute, out var uri))
                return uri.GetLeftPart(UriPartial.Authority);
            return cleaned;
        }

        var schemeSep = cleaned.IndexOf("://", StringComparison.Ordinal);
        if (schemeSep <= 0)
            return cleaned;

        var scheme = cleaned.Substring(0, schemeSep);
        var rest = cleaned.Substring(schemeSep + 3);
        var pathSep = rest.IndexOf('/');
        var authority = pathSep >= 0 ? rest.Substring(0, pathSep) : rest;
        if (string.IsNullOrEmpty(authority))
            return cleaned;

        return scheme + "://" + authority;
    }

    static List<string> buildOriginsToTry(string redirectUrl)
    {
        var origins = new List<string>();
        void add(string value)
        {
            if (string.IsNullOrEmpty(value) || origins.Contains(value))
                return;
            origins.Add(value);
        }

        add(getOriginFromRedirectUrl(redirectUrl));

        var primary = getOriginFromRedirectUrl(redirectUrl);
        if (!string.IsNullOrEmpty(primary))
            add(primary.ToLowerInvariant());

        if (!string.IsNullOrEmpty(redirectUrl))
        {
            var full = redirectUrl.Split('#')[0].Split('?')[0].TrimEnd('/');
            add(full);
            add(full.ToLowerInvariant());
        }

        add("*");
        return origins;
    }

    static UnityWebRequest PostJson(string url, object body, string originHeader = null)
    {
        var json = JsonConvert.SerializeObject(body);
        var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        if (!string.IsNullOrEmpty(originHeader))
            request.SetRequestHeader("origin", originHeader);
        return request;
    }

    public IEnumerator authorizeSession(string key, string redirectUrl, Action<StoreApiResponse> callback, bool quiet = false)
    {
        var originsToTry = buildOriginsToTry(redirectUrl);
        var keyNormalized = (key ?? "").ToLowerInvariant();
        StoreApiResponse finalResponse = null;
        int maxAttempts = quiet ? 1 : 3;

        if (!quiet)
            Debug.Log($"Web3Auth authorizeSession origins=[{string.Join(", ", originsToTry)}] keyLength={keyNormalized.Length}");

        for (int attempt = 0; attempt < maxAttempts && finalResponse == null; attempt++)
        {
            if (attempt > 0)
                yield return new WaitForSecondsRealtime(Mathf.Min(1f * attempt, 3f));

            foreach (var host in sessionHosts)
            {
                if (finalResponse != null)
                    break;

                foreach (var origin in originsToTry)
                {
                    var requestURL = $"{host}/store/get";
                    var request = PostJson(requestURL, new JObject
                    {
                        ["key"] = keyNormalized,
                        ["namespace"] = ""
                    }, origin);

                    if (!quiet)
                        Debug.Log($"Web3Auth authorizeSession POST {requestURL} origin={origin} attempt={attempt}");

                    yield return request.SendWebRequest();

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        if (!quiet)
                        {
                            Debug.LogError(
                                $"Web3Auth authorizeSession failed ({request.responseCode}): " +
                                $"{request.downloadHandler?.text ?? request.error}");
                        }
                        continue;
                    }

                    string result = request.downloadHandler?.text;
                    if (!quiet)
                        Debug.Log("Web3Auth authorizeSession response: " + result);
                    if (string.IsNullOrEmpty(result))
                        continue;

                    StoreApiResponse response = null;
                    try
                    {
                        response = JsonConvert.DeserializeObject<StoreApiResponse>(result);
                    }
                    catch (Exception ex)
                    {
                        if (!quiet)
                            Debug.LogError("Web3Auth authorizeSession deserialize failed: " + ex.Message + " raw=" + result);
                        continue;
                    }

                    if (response != null && !string.IsNullOrEmpty(response.message))
                    {
                        finalResponse = response;
                        break;
                    }
                }
            }
        }

        if (finalResponse == null && !quiet)
            Debug.LogError("Web3Auth authorizeSession: empty/null store response for all origin/host retries");

        callback(finalResponse);
    }

    public IEnumerator logout(LogoutApiRequest logoutApiRequest, Action<JObject> callback)
    {
        var request = PostJson($"{sessionHosts[0]}/store/set", new JObject
        {
            ["key"] = (logoutApiRequest.key ?? "").ToLowerInvariant(),
            ["data"] = logoutApiRequest.data,
            ["signature"] = logoutApiRequest.signature,
            ["timeout"] = logoutApiRequest.timeout,
            ["namespace"] = logoutApiRequest.sessionNamespace ?? ""
        });

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string result = request.downloadHandler.text;
            callback(JsonConvert.DeserializeObject<JObject>(result));
        }
        else
            callback(null);
    }

    private static string DescribeWebRequestFailure(UnityWebRequest request)
    {
        if (request == null)
            return "unknown request failure";

        if (request.responseCode == 0)
        {
            return "network/DNS error (responseCode=0). Check emulator/device internet connectivity. " +
                   $"result={request.result}, error={request.error}";
        }

        return $"({request.responseCode}): {request.downloadHandler?.text ?? request.error}";
    }

    public IEnumerator createSession(LogoutApiRequest logoutApiRequest, Action<JObject> callback)
    {
        var body = new JObject
        {
            ["key"] = (logoutApiRequest.key ?? "").ToLowerInvariant(),
            ["data"] = logoutApiRequest.data,
            ["signature"] = logoutApiRequest.signature,
            ["timeout"] = logoutApiRequest.timeout,
            ["allowedOrigin"] = logoutApiRequest.allowedOrigin ?? "*",
            ["namespace"] = logoutApiRequest.sessionNamespace ?? ""
        };

        var request = PostJson($"{sessionHosts[0]}/store/set", body, logoutApiRequest.allowedOrigin);
        Debug.Log($"Web3Auth createSession store/set origin={logoutApiRequest.allowedOrigin} namespace={logoutApiRequest.sessionNamespace ?? ""}");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string result = request.downloadHandler.text;
            callback(JsonConvert.DeserializeObject<JObject>(result));
        }
        else
        {
            Debug.LogError("Web3Auth createSession failed " + DescribeWebRequestFailure(request));
            callback(null);
        }
    }

    public IEnumerator fetchProjectConfig(string project_id, string network, string build_env, Action<ProjectConfigResponse> callback)
    {
        string baseUrl = SIGNER_MAP[network];
        var requestURL = $"{baseUrl}/api/v2/configuration?project_id={project_id}&network={network}&build_env={build_env}";

        var request = UnityWebRequest.Get(requestURL);

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string result = request.downloadHandler.text;
            try
            {
                callback(JsonConvert.DeserializeObject<ProjectConfigResponse>(result));
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to deserialize project config: {ex.Message}\nRaw: {result}");
                callback(null);
            }
        }
        else
        {
            Debug.LogError($"Failed to fetch project config {DescribeWebRequestFailure(request)}\nURL: {requestURL}");
            callback(null);
        }
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
