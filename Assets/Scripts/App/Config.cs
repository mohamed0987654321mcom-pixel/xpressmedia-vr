namespace XpressMediaVR
{
    /// Everything to edit before building the Quest client — mirrors
    /// XpressMediaApp/App/Config.swift so both clients point at the same
    /// MPARADISE app registration and the same backend.
    public static class Config
    {
        public const string MparadiseAppURL = "https://accounts.mparadise.app";

        /// The `client_id` from `{MPARADISE_APP_URL}/dev-apps.html`. Public by
        /// design — reuse the SAME client_id the iOS app uses, as long as the
        /// redirect URI below is also registered for it.
        public const string MparadiseClientID = "REPLACE_WITH_YOUR_MPARADISE_CLIENT_ID";

        /// Must exactly match a redirect URI registered for this app, and the
        /// scheme below must be declared in Assets/Plugins/Android/AndroidManifest.xml
        /// so Quest's system browser can hand control back to this app.
        public const string MparadiseRedirectURI = "xpressmedia://oauth-callback";
        public const string MparadiseRedirectScheme = "xpressmedia";
        public const string MparadiseScope = "profile email";

        /// Base URL of the XpressMedia backend. Quest can't reach "localhost"
        /// any more than a physical iPhone can — use your backend machine's
        /// LAN IP, reachable from the headset's Wi-Fi network. Plain "http://"
        /// only works if "Allow downloads over HTTP" (Player Settings > Other
        /// Settings) is set to "Always allowed" AND the Android manifest sets
        /// usesCleartextTraffic="true" (already done in the manifest here) —
        /// switch to https:// for anything beyond local dev testing.
        public const string BackendBaseURL = "http://192.168.1.23:4000";

        public const float PollingIntervalSeconds = 8f;
    }
}
