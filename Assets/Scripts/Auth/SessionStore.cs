using System;
using System.Threading.Tasks;
using UnityEngine;

namespace XpressMediaVR
{
    /// Owns the signed-in XMUser + JWT for the running session, and persists
    /// the JWT across app restarts. Uses PlayerPrefs (not encrypted at rest —
    /// swap for Android's EncryptedSharedPreferences via a small native
    /// plugin before shipping for real).
    public class SessionStore : MonoBehaviour
    {
        public static SessionStore Instance { get; private set; }

        private const string TokenKey = "xm_auth_token";

        public XMUser CurrentUser { get; private set; }
        public bool IsSignedIn => CurrentUser != null;
        public bool IsRestoringSession { get; private set; } = true;

        public event Action OnSessionChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            // DontDestroyOnLoad only works in Play Mode — this component also
            // gets added by the Editor's scene-building script, which runs in
            // Edit Mode, so guard the call to avoid a spurious Console error there.
            if (Application.isPlaying) DontDestroyOnLoad(gameObject);
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
