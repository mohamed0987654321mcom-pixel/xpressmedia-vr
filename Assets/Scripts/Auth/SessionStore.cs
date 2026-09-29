using System;
using System.Threading.Tasks;
using UnityEngine;

namespace XpressMediaVR
{
    /// Owns the signed-in XMUser + JWT for the running session, and persists
    /// the JWT across app restarts. Mirrors SessionStore.swift's role, but
    /// uses PlayerPrefs instead of Keychain — Quest/Android has no exact
    /// Keychain equivalent reachable from plain Unity. PlayerPrefs is NOT
    /// encrypted at rest; treat this as a placeholder to swap for Android's
    /// EncryptedSharedPreferences (via a small native plugin) before shipping
    /// for real, same spirit as KeychainHelper.swift on iOS.
    public class SessionStore : MonoBehaviour
    {
        public static SessionStore Instance { get; private set; }

        private const string TokenKey = "xm_auth_token";

        public XMUser CurrentUser { get; private set; }
        public bool IsSignedIn => CurrentUser != null;
        public bool IsRestoringSession { get; private set; } = true;

        /// Fired whenever sign-in state changes — drive scene/UI switches off
        /// this instead of polling, same role RootView's `if session.isSignedIn`
        /// plays in XpressMediaApp.swift.
        public event Action OnSessionChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private async void Start()
        {
            var savedToken = PlayerPrefs.GetString(TokenKey, null);
            if (!string.IsNullOrEmpty(savedToken))
            {
                APIClient.Shared.AuthToken = savedToken;
                try
                {
                    CurrentUser = await APIClient.Shared.FetchMe();
                }
                catch
                {
                    // Saved token no longer valid — fall back to signed-out,
                    // same as SessionStore.swift's restore-session failure path.
                    PlayerPrefs.DeleteKey(TokenKey);
                    APIClient.Shared.AuthToken = null;
                }
            }
            IsRestoringSession = false;
            OnSessionChanged?.Invoke();
        }

        public async Task SignInWithMparadise()
        {
            var mparadiseToken = await AuthManager.Instance.SignIn();
            var response = await APIClient.Shared.SignIn(mparadiseToken);
            APIClient.Shared.AuthToken = response.Token;
            PlayerPrefs.SetString(TokenKey, response.Token);
            PlayerPrefs.Save();
            CurrentUser = response.User;
            OnSessionChanged?.Invoke();
        }

        public void SignOut()
        {
            PlayerPrefs.DeleteKey(TokenKey);
            APIClient.Shared.AuthToken = null;
            CurrentUser = null;
            OnSessionChanged?.Invoke();
        }
    }
}
