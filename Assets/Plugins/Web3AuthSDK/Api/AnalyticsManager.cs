using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Lightweight Segment HTTP tracker. Mirrors the mobile SDK event names
/// (login, MFA, wallet, logout, init) without the web-only modal events.
/// </summary>
public static class AnalyticsManager
{
    public const string SegmentWriteKey = "f6LbNqCeVRf512ggdME4b6CyflhF1tsX";
    public const string SdkVersion = "11.0.0";
    public const string IntegrationType = "Native SDK";
    const string AnonymousIdKey = "w3a:anonymous_id";
    const string TrackUrl = "https://api.segment.io/v1/track";

    static readonly Dictionary<string, object> globalProperties = new Dictionary<string, object>();

    public static void SetGlobalProperties(Dictionary<string, object> properties)
    {
        if (properties == null)
            return;
        foreach (var entry in properties)
        {
            if (entry.Value == null)
                globalProperties.Remove(entry.Key);
            else
                globalProperties[entry.Key] = entry.Value;
        }
    }

    public static void Reset()
    {
        globalProperties.Clear();
    }

    public static void Track(MonoBehaviour host, string eventName, Dictionary<string, object> properties = null)
    {
        if (host == null || string.IsNullOrEmpty(eventName))
            return;

        var combined = new Dictionary<string, object>();
        foreach (var entry in globalProperties)
            combined[entry.Key] = entry.Value;
        if (properties != null)
        {
            foreach (var entry in properties)
            {
                if (entry.Value == null)
                    combined.Remove(entry.Key);
                else
                    combined[entry.Key] = entry.Value;
            }
        }

        host.StartCoroutine(SendTrack(eventName, combined));
    }

    static IEnumerator SendTrack(string eventName, Dictionary<string, object> properties)
    {
        var anonymousId = KeyStoreManagerUtils.getPreferencesData(AnonymousIdKey);
        if (string.IsNullOrEmpty(anonymousId))
        {
            anonymousId = Guid.NewGuid().ToString();
            KeyStoreManagerUtils.savePreferenceData(AnonymousIdKey, anonymousId);
        }

        var payload = new Dictionary<string, object>
        {
            { "anonymousId", anonymousId },
            { "event", eventName },
            { "properties", properties },
            { "timestamp", DateTime.UtcNow.ToString("o") },
            {
                "context", new Dictionary<string, object>
                {
                    {
                        "library", new Dictionary<string, string>
                        {
                            { "name", "web3auth-unity" },
                            { "version", SdkVersion }
                        }
                    }
                }
            }
        };

        var json = JsonConvert.SerializeObject(payload, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        });

        var request = new UnityWebRequest(TrackUrl, UnityWebRequest.kHttpVerbPOST);
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.timeout = 15;
        request.SetRequestHeader("Content-Type", "application/json");
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes(SegmentWriteKey + ":"));
        request.SetRequestHeader("Authorization", "Basic " + basic);

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
            Debug.LogWarning("Web3Auth analytics track failed: " + request.error);
    }
}

public static class AnalyticsEvents
{
    public const string SDK_INITIALIZATION_COMPLETED = "SDK Initialization Completed";
    public const string SDK_INITIALIZATION_FAILED = "SDK Initialization Failed";
    public const string CONNECTION_STARTED = "Connection Started";
    public const string CONNECTION_COMPLETED = "Connection Completed";
    public const string CONNECTION_FAILED = "Connection Failed";
    public const string MFA_ENABLEMENT_STARTED = "MFA Enablement Started";
    public const string MFA_ENABLEMENT_COMPLETED = "MFA Enablement Completed";
    public const string MFA_ENABLEMENT_FAILED = "MFA Enablement Failed";
    public const string MFA_MANAGEMENT_STARTED = "MFA Management Started";
    public const string MFA_MANAGEMENT_SELECTED = "MFA Management Selected";
    public const string MFA_MANAGEMENT_COMPLETED = "MFA Management Completed";
    public const string MFA_MANAGEMENT_FAILED = "MFA Management Failed";
    public const string WALLET_UI_CLICKED = "Wallet UI Clicked";
    public const string WALLET_SERVICES_FAILED = "Wallet Services Failed";
    public const string LOGOUT_STARTED = "Logout Started";
    public const string LOGOUT_COMPLETED = "Logout Completed";
    public const string LOGOUT_FAILED = "Logout Failed";
    public const string REQUEST_FUNCTION_STARTED = "Request Function Started";
    public const string REQUEST_FUNCTION_COMPLETED = "Request Function Completed";
    public const string REQUEST_FUNCTION_FAILED = "Request Function Failed";
    public const string IDENTITY_TOKEN_STARTED = "Identity Token Started";
    public const string IDENTITY_TOKEN_COMPLETED = "Identity Token Completed";
    public const string IDENTITY_TOKEN_FAILED = "Identity Token Failed";
}
