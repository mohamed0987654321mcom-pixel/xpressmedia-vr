using UnityEngine;

namespace XpressMediaVR
{
    /// Ports XpressMediaApp/App/DesignSystem.swift's `enum XM` color tokens
    /// so the Quest build reads as the same visual system as the iOS app:
    /// warm off-white ground, near-black ink, one red reserved for
    /// action/live/money (never used decoratively).
    public static class XM
    {
        public static readonly Color Ground = HexColor("#FBF9F7");
        public static readonly Color Ink = HexColor("#141110");
        public static readonly Color Action = HexColor("#EF2020");
        public static readonly Color StripeBase = HexColor("#181514");
        public static readonly Color Muted = HexColor("#141110", 0.55f);
        public static readonly Color Hairline = HexColor("#141110", 0.12f);

        public static Color HexColor(string hex, float alpha = 1f)
        {
            if (ColorUtility.TryParseHtmlString(hex, out var c))
            {
                c.a = alpha;
                return c;
            }
            return Color.magenta;
        }
    }
}
