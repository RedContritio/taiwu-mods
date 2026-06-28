using UnityEngine;

namespace CricketSingGradeColor.Frontend
{
    /// <summary>
    /// The single place to tune the mod. The "sound grade" is composed from a cricket's stable, audible
    /// call signature only — SingPitch (音高) and SingSize (圈大小/响度感) — and never from its true hidden
    /// CricketLevel. The composite score is then mapped onto the game's own 9-step 品级 palette by
    /// *grade-anchored calibration*: from the CricketParts config we learn the typical call score of each
    /// real 品级, then a place's call is coloured as the 品级 whose typical call it most resembles.
    ///
    /// So the colour shows "the 品级 this call sounds like". For most crickets that matches the true 品级,
    /// but the game's deliberate traps (a Trash that sings as loud as a Lv4, a quiet King) honestly show
    /// their deceptive sound-grade instead — by design.
    /// </summary>
    internal static class SoundGradeConfig
    {
        // Sound-grade score = PitchWeight*SingPitch + SizeWeight*SingSize. Only the *ratio* matters — the
        // calibration re-anchors to whatever scale the weights produce. Exposed in-game as a 3-way preset
        // (音高优先 / 均衡 / 音量优先 = pitch:size of 1:2 / 1:2.5 / 1:3); the FrontendPlugin writes the chosen
        // pair here. SingSize tracks the true 品级 better than SingPitch, so all three stay inside the
        // empirically-validated size-favoring band (1:2..1:3); 音量优先 (1:3) fits best and is the default.
        public static float PitchWeight = 1.0f;
        public static float SizeWeight = 3.0f;

        // The three in-game presets, ordered to match the config.lua Dropdown (index 0..2). All within the
        // tested 1:2..1:3 band — only the size weight varies (pitch stays 1).
        public static readonly (float Pitch, float Size)[] BlendPresets =
        {
            (1.0f, 2.0f), // 0 音高优先  pitch : size = 1 : 2    (tested band, least size-weighted)
            (1.0f, 2.5f), // 1 均衡      1 : 2.5
            (1.0f, 3.0f), // 2 音量优先  1 : 3    (best fit to true 品级)
        };
        public const int DefaultBlendIndex = 2;

        /// <summary>Applies a preset index (clamped) to PitchWeight/SizeWeight.</summary>
        public static void ApplyBlend(int index)
        {
            index = Mathf.Clamp(index, 0, BlendPresets.Length - 1);
            PitchWeight = BlendPresets[index].Pitch;
            SizeWeight = BlendPresets[index].Size;
        }

        // Use the game's own 品级 palette (Colors.Instance.GradeColors, GradeColor_0..8) so the ripple colour
        // matches the exact colour the game shows for that 品级 elsewhere. The number of grades follows the
        // palette length (9 in the base game). If the palette can't be read at runtime, the calculator falls
        // back to FallbackTierColors below.
        public static readonly bool UseGameGradeColors = true;

        // Emit a one-time calibration summary to the Player.log (palette source + the learned per-品级 score
        // anchors). Off by default; turn on to verify the calibration without a play session.
        public static readonly bool DebugLog = false;

        // --- Ripple emphasis ---
        // The game's 叫声圈 is a set of thin concentric ring outlines, so a bare tint is hard to read. These
        // make the colour pop without changing its hue (the hue carries the 品级 identity).
        // Multiply the grade colour's saturation / brightness before applying (1 = unchanged).
        public static float SaturationBoost = 1.35f;
        public static float BrightnessBoost = 1.2f;
        // Widen the thin stroke with a UI Outline (effectDistance in px; 0 disables).
        public static float OutlineThickness = 2.0f;

        // Fallback 9-step 品级 ramp, low -> high, used only when the game palette is unavailable. These are
        // the game's canonical tier hexes (see SetConsummateColor): grey -> white -> green -> light-blue ->
        // teal -> purple -> gold -> orange -> red.
        public static readonly Color[] FallbackTierColors =
        {
            Rgb(142, 142, 142), // 0 grey
            Rgb(251, 251, 251), // 1 white
            Rgb(109, 183, 95),  // 2 green
            Rgb(143, 186, 231), // 3 light blue
            Rgb(99, 206, 208),  // 4 teal
            Rgb(174, 90, 200),  // 5 purple
            Rgb(227, 198, 109), // 6 gold
            Rgb(242, 106, 52),  // 7 orange
            Rgb(228, 80, 77),   // 8 red
        };

        private static Color Rgb(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f, 1f);
    }
}
