using System.Collections.Generic;
using GameData.Common;
using GameData.Domains;
using GameData.Domains.Character;

namespace EasyBridge.Backend
{
    /// <summary>
    /// 预设：一次调用即构造好满足某条 ForceEncounter 分支的 NPC，并返回预期路由，便于自动化校验。
    /// 全部在主线程单个 pump job 内执行，因此是原子的。
    /// </summary>
    internal static class Presets
    {
        public static Dictionary<string, object> Build(DataContext ctx, string name, sbyte gender, short age)
        {
            int taiwuId = DomainManager.Taiwu.GetTaiwuCharId();
            if (age <= 0) age = 25;
            string key = (name ?? "").Trim().ToLowerInvariant();

            switch (key)
            {
                case "spouse":
                    return IntimatePair(ctx, taiwuId, Spawn(ctx, gender, age),
                        rel: GameOps.RelSpouse, both: true, favorType: 6,
                        route: "accepted", reason: "spouse", delta: "0 (spouse)");

                case "mutual-lover":
                case "lover":
                    return IntimatePair(ctx, taiwuId, Spawn(ctx, gender, age),
                        rel: GameOps.RelAdored, both: true, favorType: 6,
                        route: "accepted", reason: "mutual-lover", delta: "0 (mutual lover)");

                case "adore":
                case "unilateral-adore":
                    // 仅 target->taiwu 爱慕；双向好感拉满。
                    return IntimateUnilateral(ctx, taiwuId, Spawn(ctx, gender, age), favorType: 6);

                case "deepvalley-unattached":
                case "closefriend":
                {
                    var npc = SpawnCloseFriend(ctx, gender);
                    if (!Ok(npc)) return npc;
                    int id = Id(npc);
                    GameOps.SetFavor(ctx, taiwuId, id, 6, int.MinValue);
                    GameOps.SetFavor(ctx, id, taiwuId, 6, int.MinValue);
                    return Final(id, "accepted", "deepvalley-unattached (req>=4)", "0", npc);
                }

                case "deepvalley-attached-high":
                {
                    var npc = SpawnCloseFriend(ctx, gender);
                    if (!Ok(npc)) return npc;
                    int id = Id(npc);
                    int other = AttachToOther(ctx, id, gender);
                    if (other < 0) return GameOps.Err("deepvalley-attached-high: other spawn/marry failed");
                    // 精确设 target->taiwu 好感到 type 3（16000>14000，>Favorite2）→ Branch A 接受
                    GameOps.SetFavorExact(ctx, id, taiwuId, 16000);
                    return Final(id, "accepted", "deepvalley-attached, targetFavor type3(>2)", "reduced (villager-rate)", GameOps.Snapshot(id));
                }

                case "deepvalley-attached-low":
                {
                    var npc = SpawnCloseFriend(ctx, gender);
                    if (!Ok(npc)) return npc;
                    int id = Id(npc);
                    int other = AttachToOther(ctx, id, gender);
                    if (other < 0) return GameOps.Err("deepvalley-attached-low: other spawn/marry failed");
                    // 精确设 type 2（12000）→ <=Favorite2 → 需要战斗选择（强制）
                    GameOps.SetFavorExact(ctx, id, taiwuId, 12000);
                    return Final(id, "forced", "deepvalley-attached, targetFavor type2(<=2)", "n/a", GameOps.Snapshot(id));
                }

                case "directfallen":
                {
                    // 村民（无护卫）→ 强制走直接路径；叠满伤势 → 目标无力应战 → 不开战直接成功
                    var npc = Spawn(ctx, gender, age);
                    if (!Ok(npc)) return npc;
                    int id = Id(npc);
                    GameOps.Injure(ctx, id, 6);
                    return Final(id, "forced->TargetDirectFallen success", "injured >=36 marks (无力应战)", "pre-combat success, no combat", GameOps.Snapshot(id));
                }

                case "villager":
                {
                    var npc = Spawn(ctx, gender, age);
                    if (!Ok(npc)) return npc;
                    int id = Id(npc);
                    GameOps.MakeVillager(ctx, id);
                    GameOps.SetFavor(ctx, taiwuId, id, 6, int.MinValue);
                    GameOps.SetFavor(ctx, id, taiwuId, 6, int.MinValue);
                    return Final(id, "accepted", "taiwu-villager (net req 4)", "0", GameOps.Snapshot(id));
                }

                case "plain":
                case "plain-adult":
                {
                    var npc = Spawn(ctx, gender, age);
                    if (!Ok(npc)) return npc;
                    int id = Id(npc);
                    // 转成非村民散人 → 无亲密来源 → NeedCombatChoice（强制，非村民，结仇路线）。
                    GameOps.MakeNonVillager(ctx, id);
                    return Final(id, "forced", "no intimate source (non-villager)", "forced route, enmity applies", GameOps.Snapshot(id));
                }

                case "plain-villager":
                {
                    var npc = Spawn(ctx, gender, age);
                    if (!Ok(npc)) return npc;
                    int id = Id(npc);
                    // 村民但好感低 → FavorabilityTooLow → 强制（村民路线，不结仇、折扣惩罚）。
                    return Final(id, "forced", "villager, favor too low", "forced route, villager (no enmity)", GameOps.Snapshot(id));
                }

                case "minor":
                {
                    var npc = Spawn(ctx, gender, 15);
                    if (!Ok(npc)) return npc;
                    int id = Id(npc);
                    return Final(id, "forced", "minor, no intimate source", "minor text", GameOps.Snapshot(id));
                }

                case "prisoner":
                case "already-prisoner":
                {
                    var npc = Spawn(ctx, gender, age);
                    if (!Ok(npc)) return npc;
                    int id = Id(npc);
                    GameOps.MakePrisoner(ctx, id);
                    return Final(id, "forced->TargetAlreadyPrisoner success", "kidnapperId==taiwu", "pre-combat success", GameOps.Snapshot(id));
                }

                default:
                    return GameOps.Err("unknown preset: " + name + " (try: spouse, mutual-lover, adore, deepvalley-unattached, deepvalley-attached-high, deepvalley-attached-low, villager, plain, minor, prisoner)");
            }
        }

        // 双向同类关系 + 双向好感拉满（spouse / mutual-lover）。
        private static Dictionary<string, object> IntimatePair(DataContext ctx, int taiwuId, Dictionary<string, object> npc,
            ushort rel, bool both, int favorType, string route, string reason, string delta)
        {
            if (!Ok(npc)) return npc;
            int id = Id(npc);
            GameOps.SetRelation(ctx, taiwuId, id, rel, both, clear: false);
            GameOps.SetFavor(ctx, taiwuId, id, favorType, int.MinValue);
            GameOps.SetFavor(ctx, id, taiwuId, favorType, int.MinValue);
            return Final(id, route, reason, delta, GameOps.Snapshot(id));
        }

        // target 单向爱慕太吾 + 双向好感拉满。
        private static Dictionary<string, object> IntimateUnilateral(DataContext ctx, int taiwuId, Dictionary<string, object> npc, int favorType)
        {
            if (!Ok(npc)) return npc;
            int id = Id(npc);
            GameOps.SetRelation(ctx, id, taiwuId, GameOps.RelAdored, both: false, clear: false);
            GameOps.SetFavor(ctx, taiwuId, id, favorType, int.MinValue);
            GameOps.SetFavor(ctx, id, taiwuId, favorType, int.MinValue);
            return Final(id, "accepted", "target unilaterally adores taiwu", "reduced (villager-rate)", GameOps.Snapshot(id));
        }

        // 让 target 与另一个新 NPC 结为配偶（配偶即 1024 关系位，AddRelation 已写双向），
        // 制造 targetHasExclusiveAttachmentToOther=true。失败返回 -1（不静默吞掉）。
        private static int AttachToOther(DataContext ctx, int targetId, sbyte gender)
        {
            sbyte otherGender = (sbyte)(gender == 0 ? 1 : 0);
            var other = Spawn(ctx, otherGender, 25);
            if (!Ok(other)) return -1;
            int otherId = Id(other);
            // AddRelation(target, other, 1024) 自动写双向，只调一次即可使 GetAliveSpouse(target)==other
            GameOps.SetRelation(ctx, targetId, otherId, GameOps.RelSpouse, both: false, clear: false);
            int spouse = DomainManager.Character.GetAliveSpouse(targetId);
            return (spouse == otherId) ? otherId : -1;
        }

        private static Dictionary<string, object> Spawn(DataContext ctx, sbyte gender, short age)
            => GameOps.Spawn(ctx, gender, age, settlementId: -1, grade: 4, baseAttraction: 500);

        private static Dictionary<string, object> SpawnCloseFriend(DataContext ctx, sbyte gender)
            => GameOps.SpawnCloseFriend(ctx, gender);

        private static bool Ok(Dictionary<string, object> d)
            => d != null && d.TryGetValue("ok", out var v) && v is bool b && b;

        private static int Id(Dictionary<string, object> d)
            => d.TryGetValue("id", out var v) && v is int i ? i : -1;

        private static Dictionary<string, object> Final(int id, string route, string reason, string delta, Dictionary<string, object> snapshot)
        {
            return new Dictionary<string, object>
            {
                ["ok"] = id >= 0,
                ["id"] = id,
                ["expectedRoute"] = route,
                ["expectedReason"] = reason,
                ["expectedFavorDelta"] = delta,
                ["snapshot"] = snapshot,
            };
        }
    }
}
