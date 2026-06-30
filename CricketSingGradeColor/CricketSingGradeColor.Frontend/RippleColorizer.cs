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
        /// <summary>卡片视图用：按真实声音品级染色（不经盲盒）。</summary>
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

            ApplyColor(image, tier);
        }

        /// <summary>捕蛐蛐小游戏用：高品时按盲盒形式替换显示色，否则同真实品级。</summary>
        public static void ColorizeForCatch(Graphic image, int singPitch, int singSize)
        {
            if (image == null)
            {
                return;
            }

            if (!SoundGradeCalculator.TryGetGrade(singPitch, singSize, out int trueGrade))
            {
                return; // 校准失败：保持原版白
            }

            int displayGrade = BlindBox.ResolveDisplayGrade(
                trueGrade, singPitch, singSize,
                BlindBoxConfig.Enabled, BlindBoxConfig.ThresholdGrade, BlindBoxConfig.Mode,
                BlindBoxConfig.SessionSeed, SoundGradeCalculator.GradeCount);

            if (displayGrade < 0)
            {
                ResetToVanilla(image); // 固定默认白色
                return;
            }

            if (!SoundGradeCalculator.TryGetColorForGrade(displayGrade, out Color tier))
            {
                return;
            }

            ApplyColor(image, tier);
        }

        private static void ApplyColor(Graphic image, Color tier)
        {
            Color vivid = Vivify(tier);
            Color current = image.color;
            image.color = new Color(vivid.r, vivid.g, vivid.b, current.a);
            ApplyOutline(image, vivid);
        }

        /// <summary>还原原版白色涟漪：RGB→白、清掉我们加宽的描边、保留当前 alpha 不打断淡出。</summary>
        public static void ResetToVanilla(Graphic image)
        {
            if (image == null)
            {
                return;
            }

            Color current = image.color;
            image.color = new Color(1f, 1f, 1f, current.a);
            Outline outline = image.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectDistance = Vector2.zero;
            }
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
