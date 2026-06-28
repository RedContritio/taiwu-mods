using UnityEngine;
using UnityEngine.UI;

namespace CricketSingGradeColor.Frontend
{
    /// <summary>
    /// Shared logic to colour a cricket sing-ripple by its sound grade: map (SingPitch, SingSize) onto the
    /// game's 品级 palette, vivify it, set RGB while preserving the current alpha (so the game's fade-out still
    /// runs), and widen the thin ring with an Outline. Used by both the catch-minigame patch
    /// (<see cref="CricketSingColorPatch"/>) and the cricket card-view patch (<see cref="CricketViewColorPatch"/>).
    /// </summary>
    internal static class RippleColorizer
    {
        public static void Colorize(Graphic image, int singPitch, int singSize)
        {
            if (image == null)
            {
                return;
            }

            if (!SoundGradeCalculator.TryGetTierColor(singPitch, singSize, out Color tier))
            {
                return;
            }

            Color vivid = Vivify(tier);
            Color current = image.color;
            image.color = new Color(vivid.r, vivid.g, vivid.b, current.a);
            ApplyOutline(image, vivid);
        }

        /// <summary>Boosts saturation/brightness (hue preserved) so the thin ripple reads clearly.</summary>
        private static Color Vivify(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            s = Mathf.Clamp01(s * SoundGradeConfig.SaturationBoost);
            v = Mathf.Clamp01(v * SoundGradeConfig.BrightnessBoost);
            Color result = Color.HSVToRGB(h, s, v);
            result.a = c.a;
            return result;
        }

        /// <summary>Widens the thin ring stroke with an idempotent UI Outline that fades with the ripple.</summary>
        private static void ApplyOutline(Graphic graphic, Color color)
        {
            if (SoundGradeConfig.OutlineThickness <= 0f)
            {
                return;
            }

            Outline outline = graphic.GetComponent<Outline>() ?? graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(color.r, color.g, color.b, 1f);
            float t = SoundGradeConfig.OutlineThickness;
            outline.effectDistance = new Vector2(t, t);
            outline.useGraphicAlpha = true;
        }
    }
}
