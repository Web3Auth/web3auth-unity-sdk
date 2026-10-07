using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Math;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Citadel token session manager. Port of session-manager AuthSessionManager
/// (session-manager-android / session-manager-web v5.6.0): persist
/// sessionId + access/refresh/id tokens, authorize via POST /v1/auth/session,
/// refresh with in-flight dedupe, and logout via POST /v1/auth/logout.
/// </summary>
public class AuthSessionManager
{
    public const string SessionsEndpoint = "/v1/auth/session";
    public const string LogoutEndpoint = "/v1/auth/logout";
    public const string StorageKeyPrefix = "w3a";

    const string SessionIdKey = StorageKeyPrefix + ":session_id";
    const string AccessTokenKey = StorageKeyPrefix + ":access_token";
    const string RefreshTokenKey = StorageKeyPrefix + ":refresh_token";
    const string IdTokenKey = StorageKeyPrefix + ":id_token";

    readonly MonoBehaviour host;
    readonly string baseUrl;
    readonly object refreshGate = new object();

    string memoryAccessToken;
    string memorySessionId;
    string memorySessionData;
    Task<CitadelRefreshResponse> inflightRefresh;

    public AuthSessionManager(MonoBehaviour host, string citadelServerUrl)
    {
        if (host == null)
            throw new ArgumentNullException(nameof(host));
        if (string.IsNullOrWhiteSpace(citadelServerUrl))
            throw new ArgumentException("citadelServerUrl is required", nameof(citadelServerUrl));

        this.host = host;
        baseUrl = citadelServerUrl.TrimEnd('/');
    }

    public string GetAccessToken()
    {
        if (!string.IsNullOrEmpty(memoryAccessToken))
            return memoryAccessToken;
        memoryAccessToken = KeyStoreManagerUtils.getPreferencesData(AccessTokenKey);
        return string.IsNullOrEmpty(memoryAccessToken) ? null : memoryAccessToken;
    }

    public string GetRefreshToken()
    {
        var token = KeyStoreManagerUtils.getPreferencesData(RefreshTokenKey);
        return string.IsNullOrEmpty(token) ? null : token;
    }

    public string GetIdToken()
    {
        var token = KeyStoreManagerUtils.getPreferencesData(IdTokenKey);
        return string.IsNullOrEmpty(token) ? null : token;
    }

    public string GetSessionId()
    {
        if (!string.IsNullOrEmpty(memorySessionId))
            return memorySessionId;
        memorySessionId = KeyStoreManagerUtils.getPreferencesData(SessionIdKey);
        return string.IsNullOrEmpty(memorySessionId) ? null : memorySessionId;
    }

    public void SetTokens(AuthTokens tokens)
    {
        if (tokens == null)
            return;

        if (!string.IsNullOrEmpty(tokens.sessionId))
        {
            var normalized = NormalizeStoredSessionId(tokens.sessionId);
            memorySessionId = normalized;
            KeyStoreManagerUtils.savePreferenceData(SessionIdKey, normalized);
            KeyStoreManagerUtils.savePreferenceData(
                KeyStoreManagerUtils.SESSION_ID,
                KeyStoreManagerUtils.normalizeSessionId(normalized));
        }

        if (!string.IsNullOrEmpty(tokens.accessToken))
        {
            memoryAccessToken = tokens.accessToken;
            KeyStoreManagerUtils.savePreferenceData(AccessTokenKey, tokens.accessToken);
        }

        if (!string.IsNullOrEmpty(tokens.refreshToken))
            KeyStoreManagerUtils.savePreferenceData(RefreshTokenKey, tokens.refreshToken);

        if (!string.IsNullOrEmpty(tokens.idToken))
            KeyStoreManagerUtils.savePreferenceData(IdTokenKey, tokens.idToken);
    }

    public void ClearSessionData()
    {
        memoryAccessToken = null;
        memorySessionId = null;
        memorySessionData = null;
        KeyStoreManagerUtils.deletePreferencesData(SessionIdKey);
        KeyStoreManagerUtils.deletePreferencesData(AccessTokenKey);
        KeyStoreManagerUtils.deletePreferencesData(RefreshTokenKey);
        KeyStoreManagerUtils.deletePreferencesData(IdTokenKey);
    }

    /// <summary>
    /// Restores the session by calling citadel. Returns decrypted session JSON,
    /// or null when re-authentication is required. Failures clear stored tokens.
    /// </summary>
    public Task<string> Authorize()
    {
        var tcs = new TaskCompletionSource<string>();
        host.StartCoroutine(AuthorizeRoutine(tcs));
        return tcs.Task;
    }

    public Task<CitadelRefreshResponse> EnsureRefresh(bool skipIfFresh)
    {
        lock (refreshGate)
        {
            if (inflightRefresh != null && !inflightRefresh.IsCompleted)
                return inflightRefresh;

            var tcs = new TaskCompletionSource<CitadelRefreshResponse>();
            inflightRefresh = tcs.Task;
            host.StartCoroutine(RefreshRoutine(skipIfFresh, tcs));
            return inflightRefresh;
        }
    }

    public Task Logout()
    {
        var tcs = new TaskCompletionSource<bool>();
        host.StartCoroutine(LogoutRoutine(tcs));
        return tcs.Task;
    }

    IEnumerator AuthorizeRoutine(TaskCompletionSource<string> tcs)
    {
        var sessionId = GetSessionId();
        var access = GetAccessToken();
        var refresh = GetRefreshToken();
        if (string.IsNullOrEmpty(sessionId) || (string.IsNullOrEmpty(access) && string.IsNullOrEmpty(refresh)))
        {
            tcs.TrySetResult(null);
            yield break;
        }

        var refreshTask = EnsureRefresh(false);
        while (!refreshTask.IsCompleted)
            yield return null;

        if (refreshTask.IsFaulted || refreshTask.IsCanceled)
        {
            ClearSessionData();
            tcs.TrySetResult(null);
            yield break;
        }

        try
        {
            var sessionData = SessionDataToString(refreshTask.Result?.session_data);
            memorySessionData = sessionData;
            var decrypted = DecryptSessionData(sessionId, sessionData);
            tcs.TrySetResult(decrypted);
        }
        catch (Exception ex)
        {
            Debug.LogError("Web3Auth citadel authorize decrypt failed: " + ex.Message);
            ClearSessionData();
            tcs.TrySetResult(null);
        }
    }

    IEnumerator RefreshRoutine(bool skipIfFresh, TaskCompletionSource<CitadelRefreshResponse> tcs)
    {
        if (skipIfFresh)
        {
            var cached = TryResolveFreshFromStorage();
            if (cached != null)
            {
                tcs.TrySetResult(cached);
                ClearInflightRefresh(tcs.Task);
                yield break;
            }
        }

        var access = GetAccessToken();
        var refresh = GetRefreshToken();
        if (string.IsNullOrEmpty(access) && string.IsNullOrEmpty(refresh))
        {
            tcs.TrySetException(new Exception("No access token or refresh token available"));
            ClearInflightRefresh(tcs.Task);
            yield break;
        }

        var body = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(refresh))
            body["refresh_token"] = refresh;

        string raw = null;
        string error = null;
        yield return PostJson(
            baseUrl + SessionsEndpoint,
            body,
            access,
            (ok, text) =>
            {
                if (ok)
                    raw = text;
                else
                    error = text;
            });

        try
        {
            if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(raw))
            {
                tcs.TrySetException(new Exception(string.IsNullOrEmpty(error) ? "Token refresh failed" : error));
            }
            else
            {
                var response = JsonConvert.DeserializeObject<CitadelRefreshResponse>(raw);
                var sessionData = SessionDataToString(response?.session_data);
                if (response == null || (string.IsNullOrEmpty(response.access_token) && string.IsNullOrEmpty(sessionData)))
                {
                    tcs.TrySetException(new Exception("Token refresh failed"));
                }
                else
                {
                    if (!string.IsNullOrEmpty(response.access_token))
                    {
                        memoryAccessToken = response.access_token;
                        KeyStoreManagerUtils.savePreferenceData(AccessTokenKey, response.access_token);
                    }
                    if (!string.IsNullOrEmpty(response.refresh_token))
                        KeyStoreManagerUtils.savePreferenceData(RefreshTokenKey, response.refresh_token);
                    if (!string.IsNullOrEmpty(sessionData))
                        memorySessionData = sessionData;

                    tcs.TrySetResult(response);
                }
            }
        }
        catch (Exception ex)
        {
            tcs.TrySetException(ex);
        }
        finally
        {
            ClearInflightRefresh(tcs.Task);
        }
    }

    void ClearInflightRefresh(Task task)
    {
        lock (refreshGate)
        {
            if (ReferenceEquals(inflightRefresh, task))
                inflightRefresh = null;
        }
    }

    IEnumerator LogoutRoutine(TaskCompletionSource<bool> tcs)
    {
        var access = GetAccessToken();
        var refresh = GetRefreshToken();
        if (!string.IsNullOrEmpty(access) || !string.IsNullOrEmpty(refresh))
        {
            var body = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(refresh))
                body["refresh_token"] = refresh;

            yield return PostJson(baseUrl + LogoutEndpoint, body, access, (_, __) => { });
        }

        ClearSessionData();
        tcs.TrySetResult(true);
    }

    CitadelRefreshResponse TryResolveFreshFromStorage()
    {
        var stored = KeyStoreManagerUtils.getPreferencesData(AccessTokenKey);
        if (!string.IsNullOrEmpty(stored) && stored != memoryAccessToken)
        {
            memoryAccessToken = stored;
            return new CitadelRefreshResponse
            {
                access_token = stored,
                refresh_token = GetRefreshToken() ?? "",
                session_data = string.IsNullOrEmpty(memorySessionData) ? null : JToken.FromObject(memorySessionData)
            };
        }
        return null;
    }

    static string NormalizeStoredSessionId(string sessionId)
    {
        var stripped = KeyStoreManagerUtils.normalizeSessionId(sessionId);
        if (!KeyStoreManagerUtils.isValidSessionId(stripped))
            throw new Exception("Session id must be a hex string");

        if (stripped.Length < 64)
            stripped = stripped.PadLeft(64, '0');
        else if (stripped.Length > 64)
            stripped = stripped.Substring(0, 64);

        return "0x" + stripped;
    }

    public static string DecryptSessionData(string sessionId, string encryptedJson)
    {
        if (string.IsNullOrEmpty(encryptedJson))
            throw new Exception("There was an error decrypting data.");

        var shareMetadata = JsonConvert.DeserializeObject<ShareMetadata>(encryptedJson);
        if (shareMetadata == null ||
            string.IsNullOrEmpty(shareMetadata.ephemPublicKey) ||
            string.IsNullOrEmpty(shareMetadata.iv) ||
            string.IsNullOrEmpty(shareMetadata.ciphertext) ||
            string.IsNullOrEmpty(shareMetadata.mac))
        {
            throw new Exception("There was an error decrypting data.");
        }

        var priv = KeyStoreManagerUtils.normalizeSessionId(sessionId);
        var aes256cbc = new AES256CBC(priv, shareMetadata.ephemPublicKey, shareMetadata.iv);
        var encryptedShareBytes = AES256CBC.toByteArray(new BigInteger(shareMetadata.ciphertext, 16));
        var share = aes256cbc.decrypt(encryptedShareBytes, shareMetadata.mac);
        return Encoding.UTF8.GetString(share);
    }

    static string SessionDataToString(JToken token)
    {
        if (token == null || token.Type == JTokenType.Null)
            return null;
        if (token.Type == JTokenType.String)
            return token.ToString();
        return token.ToString(Formatting.None);
    }

    IEnumerator PostJson(string url, Dictionary<string, string> body, string bearer, Action<bool, string> done)
    {
        var json = JsonConvert.SerializeObject(body ?? new Dictionary<string, string>());
        var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.timeout = 60;
        request.SetRequestHeader("Content-Type", "application/json");
        if (!string.IsNullOrEmpty(bearer))
            request.SetRequestHeader("Authorization", "Bearer " + bearer);

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
            done(true, request.downloadHandler?.text);
        else
        {
            var message = request.downloadHandler?.text;
            if (string.IsNullOrEmpty(message))
                message = "HTTP " + request.responseCode + " " + request.error;
            done(false, message);
        }
        request.Dispose();
    }
}

public class AuthTokens
{
    public string sessionId;
    public string accessToken;
    public string refreshToken;
    public string idToken;
}

public class CitadelRefreshResponse
{
    public string access_token;
    public string refresh_token;
    public JToken session_data;
}
