using System;
using System.Collections.Generic;
using Config;
using UnityEngine;

namespace CricketSingGradeColor.Frontend
{
    /// <summary>
    /// Maps a cricket's stable, audible call signature (SingPitch + SingSize) onto the game's 品级 palette
    /// by grade-anchored calibration. Once, from the CricketParts config, we reconstruct every catchable
    /// cricket the way InitCatchPlace does (a normal colour carries a part, so 品级 = max(colourLevel,
    /// partLevel) while the audible score = colourScore + partScore; Trash/King/RealColor stand alone),
    /// learn the median call score of each real 品级, and remember those anchors. A place's call is then
    /// coloured as the 品级 whose anchor score is nearest — i.e. "the 品级 this call sounds like".
    ///
    /// Deliberately ignores the cricket's true CricketLevel, so the game's deceptive crickets keep their
    /// misleading sound-grade.
    /// </summary>
    internal static class SoundGradeCalculator
    {
        private static readonly object Gate = new object();
        private static bool _calibrated;

        // The weights the current anchors were built with. When the in-game preset changes either weight,
        // these differ from SoundGradeConfig's, triggering a one-shot re-calibration (applies live).
        private static float _calibratedPitchWeight = float.NaN;
        private static float _calibratedSizeWeight = float.NaN;

        private static bool WeightsUnchanged()
            => _calibratedPitchWeight == SoundGradeConfig.PitchWeight
            && _calibratedSizeWeight == SoundGradeConfig.SizeWeight;

        // Resolved grade palette (the game's GradeColors when available, else the fallback ramp).
        private static Color[] _palette;

        // Per-品级 anchors: _anchorGrade[i] is a real CricketLevel, _anchorScore[i] its median call score.
        // Parallel arrays, one entry per 品级 present in the data.
        private static int[] _anchorGrade;
        private static float[] _anchorScore;

        private struct Item
        {
            public int Level;
            public float Score;
            public Item(int level, float score) { Level = level; Score = score; }
        }

        public static bool TryGetTierColor(int singPitch, int singSize, out Color color)
        {
            EnsureCalibrated();
            color = Color.white;
            if (!_calibrated)
            {
                return false;
            }

            int grade = NearestGrade(Score(singPitch, singSize));
            color = _palette[Mathf.Clamp(grade, 0, _palette.Length - 1)];
            return true;
        }

        /// <summary>The 品级 whose anchor score is closest to this call's score.</summary>
        private static int NearestGrade(float score)
        {
            int best = _anchorGrade[0];
            float bestDist = Mathf.Abs(score - _anchorScore[0]);
            for (int i = 1; i < _anchorScore.Length; i++)
            {
                float dist = Mathf.Abs(score - _anchorScore[i]);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = _anchorGrade[i];
                }
            }

            return best;
        }

        private static float Score(int singPitch, int singSize)
            => SoundGradeConfig.PitchWeight * singPitch + SoundGradeConfig.SizeWeight * singSize;

        private static void EnsureCalibrated()
        {
            // Re-calibrate only when never attempted at the current weights (so a preset change recolours
            // live, but a failed calibration is not retried every ripple).
            if (WeightsUnchanged())
            {
                return;
            }

            lock (Gate)
            {
                if (WeightsUnchanged())
                {
                    return;
                }

                try
                {
                    Calibrate();
                }
                catch (Exception ex)
                {
                    Debug.LogError("[CricketSingGradeColor] Calibration failed: " + ex);
                    _calibrated = false;
                }
                finally
                {
                    _calibratedPitchWeight = SoundGradeConfig.PitchWeight;
                    _calibratedSizeWeight = SoundGradeConfig.SizeWeight;
                }
            }
        }

        private static void Calibrate()
        {
            CricketParts config = CricketParts.Instance;
            if (config == null)
            {
                _calibrated = false;
                return;
            }

            // Classify config items the way InitCatchPlace uses them.
            var normalColour = new List<Item>(); // Cyan..White: each carries an optional part
            var parts = new List<Item>();         // add-on parts
            var standalone = new List<Item>();    // Trash / King / RealColor: never carry a part

            foreach (CricketPartsItem item in (IEnumerable<CricketPartsItem>)config)
            {
                if (item == null)
                {
                    continue;
                }

                var entry = new Item(item.Level, Score(item.SingPitch, item.SingSize));
                switch (item.Type)
                {
                    case ECricketPartsType.Parts:
                        parts.Add(entry);
                        break;
                    case ECricketPartsType.Cyan:
                    case ECricketPartsType.Yellow:
                    case ECricketPartsType.Purple:
                    case ECricketPartsType.Red:
                    case ECricketPartsType.Black:
                    case ECricketPartsType.White:
                        normalColour.Add(entry);
                        break;
                    case ECricketPartsType.Trash:
                    case ECricketPartsType.King:
                    case ECricketPartsType.RealColor:
                        standalone.Add(entry);
                        break;
                    default:
                        break; // Count / unknown: ignore
                }
            }

            // Reconstruct every catchable cricket: 品级 = max(colour, part), score = colour + part.
            var byGrade = new Dictionary<int, List<float>>();
            void Add(int level, float score)
            {
                if (!byGrade.TryGetValue(level, out var list))
                {
                    list = new List<float>();
                    byGrade[level] = list;
                }

                list.Add(score);
            }

            foreach (Item s in standalone)
            {
                Add(s.Level, s.Score);
            }

            if (parts.Count == 0)
            {
                foreach (Item c in normalColour)
                {
                    Add(c.Level, c.Score);
                }
            }
            else
            {
                foreach (Item c in normalColour)
                {
                    foreach (Item p in parts)
                    {
                        Add(Mathf.Max(c.Level, p.Level), c.Score + p.Score);
                    }
                }
            }

            if (byGrade.Count == 0)
            {
                _calibrated = false;
                return;
            }

            // Median call score per 品级 = its anchor.
            var grades = new List<int>(byGrade.Keys);
            grades.Sort();
            _anchorGrade = new int[grades.Count];
            _anchorScore = new float[grades.Count];
            for (int i = 0; i < grades.Count; i++)
            {
                List<float> scores = byGrade[grades[i]];
                scores.Sort();
                _anchorGrade[i] = grades[i];
                _anchorScore[i] = scores[scores.Count / 2];
            }

            bool usedGamePalette;
            _palette = ResolvePalette(out usedGamePalette);
            if (_palette == null || _palette.Length == 0)
            {
                _calibrated = false;
                return;
            }

            _calibrated = true;

            if (SoundGradeConfig.DebugLog)
            {
                var sb = new System.Text.StringBuilder();
                sb.Append("[CricketSingGradeColor] Calibrated: palette=")
                  .Append(usedGamePalette ? "game GradeColors" : "fallback")
                  .Append(" grades=").Append(_palette.Length)
                  .Append(" weights(P:S)=").Append(SoundGradeConfig.PitchWeight).Append(':').Append(SoundGradeConfig.SizeWeight)
                  .Append(" anchors[品级=medianScore]:");
                for (int i = 0; i < _anchorGrade.Length; i++)
                {
                    sb.Append(' ').Append(_anchorGrade[i]).Append('=').Append(_anchorScore[i]);
                }

                Debug.Log(sb.ToString());
            }
        }

        /// <summary>
        /// Resolves the grade palette: the game's own <c>Colors.Instance.GradeColors</c> when enabled and
        /// available (so the ripple matches the game's 品级 colours), otherwise the built-in fallback ramp.
        /// </summary>
        private static Color[] ResolvePalette(out bool usedGamePalette)
        {
            usedGamePalette = false;
            if (SoundGradeConfig.UseGameGradeColors)
            {
                try
                {
                    Color[] grade = (Colors.Instance != null) ? Colors.Instance.GradeColors : null;
                    if (grade != null && grade.Length > 0)
                    {
                        usedGamePalette = true;
                        return grade;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[CricketSingGradeColor] Could not read game GradeColors, using fallback: " + ex);
                }
            }

            return SoundGradeConfig.FallbackTierColors;
        }
    }
}
