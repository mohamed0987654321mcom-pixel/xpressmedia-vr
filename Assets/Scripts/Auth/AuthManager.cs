using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace XpressMediaVR
{
    /// Ports MParadiseAuthManager.swift's PKCE flow for Quest. There's no
    /// ASWebAuthenticationSession equivalent on Horizon OS (it's Android
    /// under the hood), so this opens MPARADISE's login page in Quest's
    /// system browser and gets control back via the xpressmedia://oauth-callback
    /// deep link declared in Assets/Plugins/Android/AndroidManifest.xml.
    public class AuthManager : MonoBehaviour
    {
        public static AuthManager Instance { get; private set; }

        private TaskCompletionSource<string> _pendingSignIn;
        private string _codeVerifier;
        private string _state;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject);
            Application.deepLinkActivated += OnDeepLinkActivated;
            if (!string.IsNullOrEmpty(Application.absoluteURL))
                OnDeepLinkActivated(Application.absoluteURL);
        }

        private void OnDestroy() => Application.deepLinkActivated -= OnDeepLinkActivated;

        public Task<string> SignIn()
        {
            _pendingSignIn = new TaskCompletionSource<string>();

            _codeVerifier = MakeCodeVerifier();
            var codeChallenge = CodeChallenge(_codeVerifier);
            _state = Guid.NewGuid().ToString();

            var url = $"{Config.MparadiseAppURL}/oauth/authorize" +
                      $"?client_id={Uri.EscapeDataString(Config.MparadiseClientID)}" +
                      $"&redirect_uri={Uri.EscapeDataString(Config.MparadiseRedirectURI)}" +
                      $"&state={Uri.EscapeDataString(_state)}" +
                      $"&scope={Uri.EscapeDataString(Config.MparadiseScope)}" +
                      $"&code_challenge={Uri.EscapeDataString(codeChallenge)}" +
                      "&code_challenge_method=S256";

            Application.OpenURL(url);
            return _pendingSignIn.Task;
        }

        private async void OnDeepLinkActivated(string url)
        {
            if (_pendingSignIn == null || !url.StartsWith(Config.MparadiseRedirectScheme + "://"))
                return;

            var query = ParseQuery(url);

            if (query.TryGetValue("error", out var error))
            {
                _pendingSignIn.SetException(new Exception($"MPARADISE sign-in failed: {error}"));
                _pendingSignIn = null;
                return;
            }
            if (!query.TryGetValue("state", out var returnedState) || returnedState != _state)
            {
                _pendingSignIn.SetException(new Exception("Login response didn't match this request — please try again."));
                _pendingSignIn = null;
                return;
            }
            if (!query.TryGetValue("code", out var code) || string.IsNullOrEmpty(code))
            {
                _pendingSignIn.SetException(new Exception("MPARADISE didn't return a login code."));
                _pendingSignIn = null;
                return;
            }

            var completion = _pendingSignIn;
            _pendingSignIn = null;
            try
            {
                var accessToken = await ExchangeCodeForToken(code, _codeVerifier);
                completion.SetResult(accessToken);
            }
            catch (Exception e)
            {
                completion.SetException(e);
            }
        }

        private async Task<string> ExchangeCodeForToken(string code, string codeVerifier)
        {
            var payload = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["client_id"] = Config.MparadiseClientID,
                ["redirect_uri"] = Config.MparadiseRedirectURI,
                ["code_verifier"] = codeVerifier,
            };
            var json = JsonConvert.SerializeObject(payload);

            using var www = new UnityWebRequest($"{Config.MparadiseAppURL}/oauth/token", "POST")
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
                downloadHandler = new DownloadHandlerBuffer(),
            };
            www.SetRequestHeader("Content-Type", "application/json");

            await www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
                throw new Exception($"Couldn't finish signing in: {www.error}");

            var response = JsonConvert.DeserializeObject<TokenResponse>(www.downloadHandler.text);
            if (response == null || string.IsNullOrEmpty(response.access_token))
                throw new Exception("Couldn't finish signing in: unexpected response shape");
            return response.access_token;
        }

        [Serializable]
        private class TokenResponse { public string access_token; }

        private static string MakeCodeVerifier()
        {
            var bytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Base64UrlEncode(bytes);
        }

        private static string CodeChallenge(string verifier)
        {
            using var sha256 = SHA256.Create();
            return Base64UrlEncode(sha256.ComputeHash(Encoding.UTF8.GetBytes(verifier)));
        }

        private static string Base64UrlEncode(byte[] data) =>
            Convert.ToBase64String(data).Replace('+', '-').Replace('/', '_').TrimEnd('=');

        private static Dictionary<string, string> ParseQuery(string url)
        {
            var result = new Dictionary<string, string>();
            var queryStart = url.IndexOf('?');
            if (queryStart < 0) return result;
            var query = url.Substring(queryStart + 1);
            foreach (var pair in query.Split('&'))
            {
                if (string.IsNullOrEmpty(pair)) continue;
                var idx = pair.IndexOf('=');
                if (idx < 0) { result[Uri.UnescapeDataString(pair)] = ""; continue; }
                var key = Uri.UnescapeDataString(pair.Substring(0, idx));
                var value = Uri.UnescapeDataString(pair.Substring(idx + 1));
                result[key] = value;
            }
            return result;
        }
    }
}
