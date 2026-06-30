using System;

namespace CricketSingGradeColor.Frontend
{
    /// <summary>
    /// 高品盲盒形式。index 与 config.lua 的 BlindBoxMode 下拉项一一对应（0-based）。
    /// </summary>
    internal enum EBlindBoxMode
    {
        RandomLow = 0,       // 随机展示为低品级
        RandomHigh = 1,      // 随机展示为高品级
        FixedTop = 2,        // 固定一品
        FixedBottom = 3,     // 固定九品
        FixedThreshold = 4,  // 固定门槛品级
        VanillaWhite = 5,    // 固定默认白色
    }

    /// <summary>
    /// 高品盲盒纯逻辑：给定真实声音品级与配置，算出「展示用品级」或「还原原版白色」。
    /// 不依赖 Unity，便于被测试工程以源码链接方式直接执行。
    /// 品级 index：0=九品(最低) .. gradeCount-1=一品(最高)。
    /// </summary>
    internal static class BlindBox
    {
        /// <summary>展示用品级 index；返回 -1 表示「还原原版白色」。</summary>
        public static int ResolveDisplayGrade(
            int trueGrade, int singPitch, int singSize,
            bool enabled, int threshold, EBlindBoxMode mode,
            int sessionSeed, int gradeCount)
        {
            if (gradeCount <= 0)
            {
                return trueGrade;
            }

            int maxGrade = gradeCount - 1;
            threshold = Clamp(threshold, 0, maxGrade);

            // 未启用 或 真实品级低于门槛 → 显示真实品级（现有行为）
            if (!enabled || trueGrade < threshold)
            {
                return Clamp(trueGrade, 0, maxGrade);
            }

            switch (mode)
            {
                case EBlindBoxMode.VanillaWhite:
                    return -1;
                case EBlindBoxMode.FixedTop:
                    return maxGrade;
                case EBlindBoxMode.FixedBottom:
                    return 0;
                case EBlindBoxMode.FixedThreshold:
                    return threshold;
                case EBlindBoxMode.RandomLow:
                    return PickInRange(0, threshold - 1, Seed(singPitch, singSize, sessionSeed));
                case EBlindBoxMode.RandomHigh:
                    return PickInRange(threshold, maxGrade, Seed(singPitch, singSize, sessionSeed));
                default:
                    return Clamp(trueGrade, 0, maxGrade);
            }
        }

        /// <summary>把本场全部草丛蛐蛐(颜色/部件 id，按位置顺序)散列成场次盐。</summary>
        public static int ComputeSessionSeed(int[] colorIds, int[] partsIds)
        {
            unchecked
            {
                int h = 17;
                int n = (colorIds != null && partsIds != null)
                    ? Math.Min(colorIds.Length, partsIds.Length)
                    : 0;
                for (int i = 0; i < n; i++)
                {
                    h = h * 31 + colorIds[i];
                    h = h * 31 + partsIds[i];
                }

                return h;
            }
        }

        private static int Seed(int a, int b, int c)
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + a;
                h = h * 31 + b;
                h = h * 31 + c;
                return h;
            }
        }

        /// <summary>[lo,hi] 内由 seed 确定性取一个；区间为空(hi&lt;lo)回退 lo(不小于 0)。</summary>
        private static int PickInRange(int lo, int hi, int seed)
        {
            if (hi < lo)
            {
                return lo < 0 ? 0 : lo;
            }

            int range = hi - lo + 1;
            int m = ((seed % range) + range) % range;
            return lo + m;
        }

        private static int Clamp(int v, int lo, int hi)
            => v < lo ? lo : (v > hi ? hi : v);
    }
}
