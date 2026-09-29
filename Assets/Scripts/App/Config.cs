namespace XpressMediaVR
{
    /// Everything you need to edit before building the Quest client — mirrors
    /// XpressMediaApp/App/Config.swift exactly, so both clients point at the
    /// same MPARADISE app registration and the same backend.
    public static class Config
    {
        /// The MPARADISE server this app is registered with.
        public const string MparadiseAppURL = "https://accounts.mparadise.app";

        /// The `client_id` from `{MPARADISE_APP_URL}/dev-apps.html`. Public by
        /// design (per AI.md) — safe to compile into the app. You can reuse
        /// the SAME client_id the iOS app uses, as long as this redirect URI
        /// is also registered for it.
        public const string MparadiseClientID = "REPLACE_WITH_YOUR_MPARADISE_CLIENT_ID";

        /// Must exactly match a redirect URI registered for this app, and the
        /// scheme below must be declared in Assets/Plugins/Android/AndroidManifest.xml
        /// so Quest's system browser can hand control back to this app.
        public const string MparadiseRedirectURI = "xpressmedia://oauth-callback";
        public const string MparadiseRedirectScheme = "xpressmedia";

        public const string MparadiseScope = "profile email";

        /// Base URL of the XpressMedia backend (the same `backend/` folder
        /// the iOS app uses). Quest can't reach "localhost" any more than a
        /// physical iPhone can — that means the headset itself — so this has
        /// to be your backend machine's LAN IP, reachable from the headset's
        /// Wi-Fi network.
        public const string BackendBaseURL = "http://192.168.1.23:4000";

        public const float PollingIntervalSeconds = 8f;
    }
}
