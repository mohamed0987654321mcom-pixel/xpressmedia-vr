using UnityEngine;

namespace XpressMediaVR
{
    /// Ports XpressMediaApp/App/DesignSystem.swift's `enum XM` color tokens,
    /// so materials and world-space UI in the Quest build read as the same
    /// system as the iOS app: warm off-white ground, near-black ink, one red
    /// reserved for action/live/money.
    public static class XM
    {
        public static readonly Color Ground = HexColor("#FBF9F7");
        public static readonly Color Ink = HexColor("#141110");
        public static readonly Color Action = HexColor("#EF2020");
        public static readonly Color StripeBase = HexColor("#181514");

        public static Color HexColor(string hex, float alpha = 1f)
        {
            if (ColorUtility.TryParseHtmlString(hex, out var c))
            {
                c.a = alpha;
                return c;
            }
            // Loud, obviously-wrong fallback so a bad hex shows up in the
            // Editor immediately instead of silently rendering black.
            return Color.magenta;
        }
    }
}
