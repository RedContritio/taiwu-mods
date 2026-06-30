namespace CricketSingGradeColor.Frontend
{
    /// <summary>
    /// 高品盲盒运行期配置 + 场次盐。由 FrontendPlugin 从游戏设置写入，由 RippleColorizer/SessionSeedPatch 读取。
    /// 默认值对应 config.lua：未启用 / 门槛二品(7) / 固定一品。
    /// </summary>
    internal static class BlindBoxConfig
    {
        public static bool Enabled = false;
        public static int ThresholdGrade = 7;            // 0=九品 .. 8=一品；7=二品
        public static EBlindBoxMode Mode = EBlindBoxMode.FixedTop;
        public static int SessionSeed = 0;               // 本场草丛蛐蛐组成的散列盐
    }
}
