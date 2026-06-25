using System.Collections.Generic;
using GameData.Common;
using GameData.Domains;
using GameData.Domains.Character;
using GameData.Domains.Character.Relation;
using GameData.Domains.Combat;
using GameData.Domains.Item;
using GameData.Domains.Map;
using GameData.Domains.Organization;
using GameData.Domains.TaiwuEvent.EventHelper;

namespace EasyBridge.Backend
{
    /// <summary>
    /// 后端游戏状态操作：读取/构造角色与关系，专门用于满足 ForceEncounter 各分支的前置条件。
    /// 所有方法都假定运行在后端主线程上（由 MainThreadPump 在 GlobalDomain.OnUpdate tick 中调用），
    /// 并使用传入的主线程 context 做域写操作。
    /// </summary>
    public static class GameOps
    {
        // ForceEncounter 相关常量（与 ForceEncounterConstants 保持一致，避免跨 mod 依赖）。
        public const ushort RelSpouse = 1024;
        public const ushort RelAdored = 16384;
        public const short FeatureDeepValleyCloseFriend = 685;
        public const sbyte TaiwuVillageOrgTemplateId = 16;

        // ---------- 基础读 ----------

        public static int TaiwuId() => DomainManager.Taiwu.GetTaiwuCharId();

        public static Dictionary<string, object> Taiwu()
        {
            int taiwuId = DomainManager.Taiwu.GetTaiwuCharId();
            var r = new Dictionary<string, object>
            {
                ["ok"] = true,
                ["taiwuId"] = taiwuId,
                ["closeFriendId"] = DomainManager.Taiwu.GetTaiwuCharIdForCloseFriend(),
            };
            if (DomainManager.Character.TryGetElement_Objects(taiwuId, out var taiwu))
            {
                var loc = taiwu.GetLocation();
                r["name"] = SafeName(taiwuId);
                r["areaId"] = (int)loc.AreaId;
                r["blockId"] = (int)loc.BlockId;
                r["behaviorType"] = (int)taiwu.GetBehaviorType();
                r["orgTemplateId"] = (int)taiwu.GetOrganizationInfo().OrgTemplateId;
                r["settlementId"] = (int)taiwu.GetOrganizationInfo().SettlementId;
            }
            r["villageSettlementId"] = (int)DomainManager.Taiwu.GetTaiwuVillageSettlementId();
            return r;
        }

        public static Dictionary<string, object> Combat(DataContext ctx)
        {
            bool inCombat = DomainManager.Combat.IsInCombat();
            var result = new Dictionary<string, object>
            {
                ["ok"] = true,
                ["status"] = SafeCombatStatus(),
                ["inCombat"] = inCombat,
                ["autoCombat"] = SafeAutoCombat(),
                ["autoMove"] = SafeAutoMove(),
            };

            if (!inCombat)
            {
                result["currentDistance"] = null;
                result["lastTargetDistance"] = null;
                result["self"] = null;
                result["enemy"] = null;
                return result;
            }

            result["currentDistance"] = SafeCurrentDistance();
            result["lastTargetDistance"] = SafeLastTargetDistance();
            result["self"] = CombatCharInfo(DomainManager.Combat.GetCombatCharacter(isAlly: true));
            result["enemy"] = CombatCharInfo(DomainManager.Combat.GetCombatCharacter(isAlly: false));
            return result;
        }

        private static int? SafeCombatStatus()
        {
            try { return (int)DomainManager.Combat.GetCombatStatus(); }
            catch { return null; }
        }

        private static bool? SafeAutoCombat()
        {
            try { return DomainManager.Combat.GetAutoCombat(); }
            catch { return null; }
        }

        private static bool? SafeAutoMove()
        {
            try { return DomainManager.Combat.AiOptions != null ? DomainManager.Combat.AiOptions.AutoMove : (bool?)null; }
            catch { return null; }
        }

        private static int? SafeCurrentDistance()
        {
            try { return DomainManager.Combat.GetCurrentDistance(); }
            catch { return null; }
        }

        private static int? SafeLastTargetDistance()
        {
            try { return DomainManager.Combat.GetLastTargetDistance(); }
            catch { return null; }
        }

        private static Dictionary<string, object> CombatCharInfo(GameData.Domains.Combat.CombatCharacter ch)
        {
            if (ch == null) return null;
            return new Dictionary<string, object>
            {
                ["id"] = ch.GetId(),
                ["targetDistance"] = ch.GetTargetDistance(),
            };
        }

        public static Dictionary<string, object> Snapshot(int id)
        {
            if (!DomainManager.Character.TryGetElement_Objects(id, out var ch))
                return Err("character not found: " + id);

            int taiwuId = DomainManager.Taiwu.GetTaiwuCharId();
            var loc = ch.GetLocation();
            var org = ch.GetOrganizationInfo();
            var r = new Dictionary<string, object>
            {
                ["ok"] = true,
                ["id"] = id,
                ["name"] = SafeName(id),
                ["alive"] = DomainManager.Character.IsCharacterAlive(id),
                ["gender"] = (int)ch.GetGender(),
                ["ageGroup"] = (int)ch.GetAgeGroup(),
                ["behaviorType"] = (int)ch.GetBehaviorType(),
                ["orgTemplateId"] = (int)org.OrgTemplateId,
                ["settlementId"] = (int)org.SettlementId,
                ["isTaiwuVillager"] = org.OrgTemplateId == TaiwuVillageOrgTemplateId,
                ["areaId"] = (int)loc.AreaId,
                ["blockId"] = (int)loc.BlockId,
                ["kidnapperId"] = ch.GetKidnapperId(),
                ["hasFeature685"] = ch.GetFeatureIds().Contains(FeatureDeepValleyCloseFriend),
                ["hasGuard"] = DomainManager.Character.HasGuard(id, ch),
            };

            if (DomainManager.Character.TryGetElement_Objects(taiwuId, out var taiwu))
            {
                if (DomainManager.Character.TryGetRelation(taiwuId, id, out RelatedCharacter a2t))
                {
                    r["taiwuToTarget"] = RelationInfo(a2t);
                }
                if (DomainManager.Character.TryGetRelation(id, taiwuId, out RelatedCharacter t2a))
                {
                    r["targetToTaiwu"] = RelationInfo(t2a);
                }
                r["isDeepValleyCloseFriendToTaiwu"] =
                    ch.GetFeatureIds().Contains(FeatureDeepValleyCloseFriend) &&
                    taiwuId == DomainManager.Taiwu.GetTaiwuCharIdForCloseFriend();
                r["aliveSpouseOfTarget"] = DomainManager.Character.GetAliveSpouse(id);
            }
            return r;
        }

        private static Dictionary<string, object> RelationInfo(RelatedCharacter rc)
        {
            return new Dictionary<string, object>
            {
                ["relationType"] = (int)rc.RelationType,
                ["favorability"] = (int)rc.Favorability,
                ["favorabilityType"] = (int)rc.GetFavorabilityType(),
                ["hasSpouse"] = RelationType.HasRelation(rc.RelationType, RelSpouse),
                ["hasAdored"] = RelationType.HasRelation(rc.RelationType, RelAdored),
            };
        }

        // ---------- 构造（生成 NPC） ----------

        /// <summary>在太吾所在格生成一个普通智能 NPC。</summary>
        public static Dictionary<string, object> Spawn(DataContext ctx, sbyte gender, short age, short settlementId, sbyte grade, short baseAttraction, bool villager)
        {
            int taiwuId = DomainManager.Taiwu.GetTaiwuCharId();
            if (!DomainManager.Character.TryGetElement_Objects(taiwuId, out var taiwu))
                return Err("taiwu not found");
            var loc = taiwu.GetLocation();

            if (settlementId < 0)
            {
                settlementId = taiwu.GetOrganizationInfo().SettlementId;
                if (settlementId < 0)
                    settlementId = DomainManager.Taiwu.GetTaiwuVillageSettlementId();
            }

            var ch = EventHelper.CreateIntelligentCharacter(loc, gender, age, baseAttraction, settlementId, grade);
            int id = ch.GetId();
            // 落位到太吾所在格，确保出现在同一张地图角色列表里供 UI 交互。
            ch.SetLocation(loc, ctx);

            // 默认转为非村民（org 0 散人）。太吾村村民缺少村民角色固定行为数据，过月时
            // TaiwuDomain.UpdateVillagerFixedActions 会空引用崩溃、卡死整个过月流程（验证过月类 mod
            // 如 DreamLover 必须用非村民）。需要村民身份（如 ForceEncounter 村民文案分支）时显式传
            // villager=true，但不要在这种 NPC 上过月。
            if (!villager)
            {
                var outsider = new OrganizationInfo(0, 0, true, DomainManager.Organization.GetSettlementIdByOrgTemplateId(0));
                DomainManager.Organization.ChangeOrganization(ctx, ch, outsider);
                ch.SetLocation(loc, ctx);
            }

            var res = Snapshot(id);
            res["spawned"] = true;
            res["villager"] = villager;
            return res;
        }

        /// <summary>
        /// 生成一个谷中密友（带原生 685 特性，并登记为太吾密友）。直接用 CharacterDomain.CreateCloseFriend，
        /// 不调用静态包装里的 JoinGroup，使其留在地图格里、可被敌对菜单交互。
        /// </summary>
        public static Dictionary<string, object> SpawnCloseFriend(DataContext ctx, sbyte gender)
        {
            int taiwuId = DomainManager.Taiwu.GetTaiwuCharId();
            if (!DomainManager.Character.TryGetElement_Objects(taiwuId, out var taiwu))
                return Err("taiwu not found");
            var loc = taiwu.GetLocation();

            sbyte sectOrg = OrganizationDomain.GetRandomSectOrgTemplateId(ctx.Random, gender);
            sbyte stateTmpl = DomainManager.Map.GetStateTemplateIdByAreaId(loc.AreaId);
            short charTemplateId = OrganizationDomain.GetCharacterTemplateId(sectOrg, stateTmpl, gender);
            short morality = taiwu.GetMorality();

            // CreateCloseFriend 内部：建在太吾所在格、加 685 特性、登记密友 id、JoinGroup、并已 Complete。
            var ch = DomainManager.Character.CreateCloseFriend(ctx, charTemplateId, morality, taiwu);
            if (ch == null)
                return Err("CreateCloseFriend returned null");
            int id = ch.GetId();
            // 它会被加入太吾队伍而从地图角色列表消失；这里退队（不随机移格），并锁定在太吾所在格，便于敌对菜单交互。
            DomainManager.Taiwu.LeaveGroup(ctx, id, true, false, false);
            ch.SetLocation(loc, ctx);
            var res = Snapshot(id);
            res["spawned"] = true;
            res["closeFriend"] = true;
            res["note"] = "close friend is age 13 (native 谷中密友), minor text expected";
            return res;
        }

        // ---------- 移动 / 地图 ----------

        private static readonly string[] BlockTypeNames =
            { "City", "Sect", "Town", "Station", "Developed", "Normal", "Wild", "Bad", "scenery" };

        private static string BlockTypeName(int t) => (t >= 0 && t < BlockTypeNames.Length) ? BlockTypeNames[t] : ("#" + t);

        /// <summary>太吾当前所在格信息（含 BlockType，护卫判定依赖它）。</summary>
        public static Dictionary<string, object> WhereAmI()
        {
            int taiwuId = DomainManager.Taiwu.GetTaiwuCharId();
            if (!DomainManager.Character.TryGetElement_Objects(taiwuId, out var taiwu))
                return Err("taiwu not found");
            var loc = taiwu.GetLocation();
            var block = DomainManager.Map.GetBlock(loc);
            int bt = (int)block.BlockType;
            return new Dictionary<string, object>
            {
                ["ok"] = true,
                ["areaId"] = (int)loc.AreaId,
                ["blockId"] = (int)loc.BlockId,
                ["blockType"] = bt,
                ["blockTypeName"] = BlockTypeName(bt),
            };
        }

        /// <summary>把太吾瞬移到指定格（QuickTravel 跨区 + Move + 脏标记同步前端）。</summary>
        public static Dictionary<string, object> MoveTaiwu(DataContext ctx, int areaId, int blockId)
        {
            if (areaId < 0 || areaId >= 135)
                return Err("areaId out of range [0,135): " + areaId);
            EventHelper.TeleportMoveTaiwuToLocation(new Location((short)areaId, (short)blockId));
            var r = WhereAmI();
            r["moved"] = true;
            return r;
        }

        /// <summary>列出所有据点（含城镇 CivilianSettlement / 门派 Sect），用于找城镇格——城镇平民(如镇长)战力低、
        /// 其守卫反而更强，可触发「护卫出面」。civilianOnly=true 只返回城镇据点。</summary>
        public static Dictionary<string, object> Settlements(DataContext ctx, bool civilianOnly, int max)
        {
            if (max <= 0) max = 40;
            var all = new List<Settlement>();
            DomainManager.Organization.GetAllSettlements(all);
            var list = new List<object>();
            foreach (var s in all)
            {
                bool civ = s is CivilianSettlement;
                if (civilianOnly && !civ) continue;
                var loc = s.GetLocation();
                if (loc.AreaId < 0 || loc.AreaId >= 45) continue;
                int bt = (int)DomainManager.Map.GetBlockData(loc.AreaId, loc.BlockId).BlockType;
                list.Add(new Dictionary<string, object>
                {
                    ["settlementId"] = (int)s.GetId(),
                    ["orgTemplate"] = (int)s.GetOrgTemplateId(),
                    ["isCivilian"] = civ,
                    ["areaId"] = (int)loc.AreaId,
                    ["blockId"] = (int)loc.BlockId,
                    ["blockType"] = bt,
                    ["blockTypeName"] = BlockTypeName(bt),
                });
                if (list.Count >= max) break;
            }
            return new Dictionary<string, object> { ["ok"] = true, ["count"] = list.Count, ["settlements"] = list };
        }

        /// <summary>列出若干门派据点的格位（用于瞬移到门派格，那里的原生门派 NPC 通常带护卫）。</summary>
        public static Dictionary<string, object> FindSects(DataContext ctx, int count)
        {
            if (count <= 0) count = 6;
            var list = new List<object>();
            var seen = new HashSet<int>();
            for (int i = 0; i < count * 6 && list.Count < count; i++)
            {
                sbyte org = OrganizationDomain.GetRandomSectOrgTemplateId(ctx.Random, -1);
                if (!seen.Add(org)) continue;
                var settlement = DomainManager.Organization.GetSettlementByOrgTemplateId(org);
                if (settlement == null) continue;
                var loc = settlement.GetLocation();
                int bt = (int)DomainManager.Map.GetBlock(loc).BlockType;
                list.Add(new Dictionary<string, object>
                {
                    ["orgTemplate"] = (int)org,
                    ["settlementId"] = (int)settlement.GetId(),
                    ["areaId"] = (int)loc.AreaId,
                    ["blockId"] = (int)loc.BlockId,
                    ["blockType"] = bt,
                    ["blockTypeName"] = BlockTypeName(bt),
                });
            }
            return new Dictionary<string, object> { ["ok"] = true, ["count"] = list.Count, ["sects"] = list };
        }

        // ---------- 关系 / 好感 ----------

        public static Dictionary<string, object> SetRelation(DataContext ctx, int a, int b, ushort type, bool both, bool clear)
        {
            if (!DomainManager.Character.TryGetElement_Objects(a, out _) ||
                !DomainManager.Character.TryGetElement_Objects(b, out _))
                return Err("a or b not found");

            if (clear)
            {
                DomainManager.Character.RemoveRelation(ctx, a, b);
                if (both) DomainManager.Character.RemoveRelation(ctx, b, a);
            }
            else
            {
                DomainManager.Character.AddRelation(ctx, a, b, type);
                if (both) DomainManager.Character.AddRelation(ctx, b, a, type);
            }
            return new Dictionary<string, object> { ["ok"] = true, ["a"] = a, ["b"] = b, ["type"] = (int)type, ["both"] = both, ["clear"] = clear };
        }

        /// <summary>把 from 对 to 的好感设到指定 favorability 档位 type（1..6），返回实际结果。</summary>
        public static Dictionary<string, object> SetFavor(DataContext ctx, int from, int to, int targetType, int explicitValue)
        {
            if (!DomainManager.Character.TryGetElement_Objects(from, out var fromCh) ||
                !DomainManager.Character.TryGetElement_Objects(to, out var toCh))
                return Err("from or to not found");

            int desired;
            if (explicitValue != int.MinValue)
                desired = explicitValue;
            else if (targetType <= 0)
                desired = 0;
            else
                desired = 6000 + (targetType - 1) * 4000 + 2000; // 落在该档区间中部
            if (desired > 30000) desired = 30000;
            if (desired < -30000) desired = -30000;

            short before = DomainManager.Character.GetFavorability(from, to);
            // ChangeFavorability 会按百分比缩放并对“面向太吾”的好感做上限钳制，单次往往达不到目标；
            // 这里迭代逼近：每次施加 (desired-当前) 的增量，收敛到 desired（或上限）。
            int iters = 0;
            for (; iters < 12; iters++)
            {
                short cur = DomainManager.Character.GetFavorability(from, to);
                int delta = desired - cur;
                if (System.Math.Abs(delta) <= 80) break;
                DomainManager.Character.ChangeFavorability(ctx, fromCh, toCh, delta);
                short next = DomainManager.Character.GetFavorability(from, to);
                if (System.Math.Abs(next - cur) < 20) break; // 已到上限，无法再逼近
            }

            short after = DomainManager.Character.GetFavorability(from, to);
            int afterType = 0;
            if (DomainManager.Character.TryGetRelation(from, to, out RelatedCharacter rc))
                afterType = rc.GetFavorabilityType();
            return new Dictionary<string, object>
            {
                ["ok"] = true, ["from"] = from, ["to"] = to, ["targetType"] = targetType,
                ["desired"] = desired, ["before"] = (int)before, ["after"] = (int)after, ["afterType"] = afterType, ["iters"] = iters,
            };
        }

        /// <summary>把角色转入太吾村（org 模板 16），用于 Taiwu villager 分支。</summary>
        public static Dictionary<string, object> MakeVillager(DataContext ctx, int id)
        {
            if (!DomainManager.Character.TryGetElement_Objects(id, out var ch))
                return Err("character not found: " + id);
            short villageSettlement = DomainManager.Taiwu.GetTaiwuVillageSettlementId();
            var org = new OrganizationInfo(TaiwuVillageOrgTemplateId, 0, true, villageSettlement);
            DomainManager.Organization.ChangeOrganization(ctx, ch, org);
            var res = Snapshot(id);
            res["madeVillager"] = true;
            return res;
        }

        /// <summary>把角色转出太吾村，变为非村民（org 模板 0 散人），用于强制路线结仇验证。</summary>
        public static Dictionary<string, object> MakeNonVillager(DataContext ctx, int id)
        {
            if (!DomainManager.Character.TryGetElement_Objects(id, out var ch))
                return Err("character not found: " + id);
            short settlement = DomainManager.Organization.GetSettlementIdByOrgTemplateId(0);
            var org = new OrganizationInfo(0, 0, true, settlement);
            DomainManager.Organization.ChangeOrganization(ctx, ch, org);
            // 保持在太吾所在格便于交互
            if (DomainManager.Character.TryGetElement_Objects(DomainManager.Taiwu.GetTaiwuCharId(), out var taiwu))
                ch.SetLocation(taiwu.GetLocation(), ctx);
            var res = Snapshot(id);
            res["madeNonVillager"] = true;
            return res;
        }

        /// <summary>把角色设为太吾的俘虏（GetKidnapperId()==太吾），用于 already-prisoner 分支。</summary>
        public static Dictionary<string, object> MakePrisoner(DataContext ctx, int id)
        {
            if (!DomainManager.Character.TryGetElement_Objects(id, out var ch))
                return Err("character not found: " + id);
            int taiwuId = DomainManager.Taiwu.GetTaiwuCharId();
            // 直接设置绑架者 id，满足 ForceEncounter 的 GetKidnapperId()==太吾 判定，
            // 不必构造绳索物品走 AddKidnappedCharacter（那条路径需要库存里的真实绳索）。
            ch.SetKidnapperId(taiwuId, ctx);
            var res = Snapshot(id);
            res["madePrisoner"] = true;
            return res;
        }

        // ---------- 战斗前置状态（无力应战 / 削弱太吾） ----------

        /// <summary>给角色叠满伤势（每部位内外各 level，默认 6），使其 GetDefeatMarksCount>=36 → 无力应战（直接成功路径）。</summary>
        public static Dictionary<string, object> Injure(DataContext ctx, int id, int level)
        {
            if (!DomainManager.Character.TryGetElement_Objects(id, out var ch))
                return Err("character not found: " + id);
            if (level <= 0) level = 6;
            sbyte delta = (sbyte)System.Math.Clamp(level, -6, 6);
            for (sbyte part = 0; part < 7; part++)
            {
                ch.ChangeInjury(ctx, part, false, delta);
                ch.ChangeInjury(ctx, part, true, delta);
            }
            int marks = EventHelper.GetDefeatMarksCountOutOfCombat(id);
            bool fallen = EventHelper.IsCharacterDirectFallenInCombat(id, (CombatType)2 /* DieNormal */);
            return new Dictionary<string, object>
            {
                ["ok"] = true, ["id"] = id, ["level"] = (int)delta,
                ["defeatMarks"] = marks, ["threshold"] = 36, ["directFallen"] = fallen,
            };
        }

        /// <summary>清空角色伤势（测试后复原，例如治好被削弱的太吾）。</summary>
        public static Dictionary<string, object> Heal(DataContext ctx, int id)
        {
            if (!DomainManager.Character.TryGetElement_Objects(id, out var ch))
                return Err("character not found: " + id);
            for (sbyte part = 0; part < 7; part++)
            {
                ch.ChangeInjury(ctx, part, false, -6);
                ch.ChangeInjury(ctx, part, true, -6);
            }
            int marks = EventHelper.GetDefeatMarksCountOutOfCombat(id);
            return new Dictionary<string, object> { ["ok"] = true, ["id"] = id, ["defeatMarks"] = marks };
        }

        /// <summary>
        /// 削弱目标战力（撤销全部战斗技能 + 内力清零），使其 GetCombatPower 低于其护卫，
        /// 让 ForceEncounter 的护卫排序 enemyTeam[0]=护卫 → 触发「护卫出面」事件。
        /// </summary>
        public static Dictionary<string, object> Cripple(DataContext ctx, int id)
        {
            if (!DomainManager.Character.TryGetElement_Objects(id, out var ch))
                return Err("character not found: " + id);
            int before = ch.GetCombatPower();
            var skills = DomainManager.CombatSkill.GetCharCombatSkills(id);
            var ids = new List<short>(skills.Keys);
            if (ids.Count > 0)
                DomainManager.Character.GmCmd_RevokeCombatSkill(ctx, id, ids);
            DomainManager.Character.GmCmd_SetCurrNeili(ctx, id, 0);
            int after = ch.GetCombatPower();
            return new Dictionary<string, object>
            {
                ["ok"] = true, ["id"] = id,
                ["skillsRevoked"] = ids.Count,
                ["combatPowerBefore"] = before, ["combatPowerAfter"] = after,
            };
        }

        // ---------- 精确好感（绕过缩放/上限，用于谷友 BranchA） ----------

        /// <summary>把 from→to 好感精确设为 value（不缩放、不钳制到太吾上限），保留 to→from 不变。</summary>
        public static Dictionary<string, object> SetFavorExact(DataContext ctx, int from, int to, int value)
        {
            if (!DomainManager.Character.TryGetElement_Objects(from, out _) ||
                !DomainManager.Character.TryGetElement_Objects(to, out _))
                return Err("from or to not found");
            short v = (short)System.Math.Clamp(value, -30000, 30000);
            short keepReverse = DomainManager.Character.GetFavorability(to, from);
            DomainManager.Character.DirectlySetFavorabilities(ctx, from, to, v, keepReverse);
            int afterType = 0;
            if (DomainManager.Character.TryGetRelation(from, to, out RelatedCharacter rc))
                afterType = rc.GetFavorabilityType();
            return new Dictionary<string, object>
            {
                ["ok"] = true, ["from"] = from, ["to"] = to,
                ["value"] = (int)v, ["afterType"] = afterType,
            };
        }

        // ---------- 战斗内捕绳（擒获处置路径：hack 战斗过程，扔绳子捕获目标） ----------

        private static ItemKey _lastRopeKey;
        private static bool _hasRope;

        /// <summary>给太吾一条捕绳（itemType 12，template 默认 90 高级），加入背包并记住 key，供 /throwrope 使用。</summary>
        public static Dictionary<string, object> GiveRope(DataContext ctx, int template)
        {
            int taiwuId = DomainManager.Taiwu.GetTaiwuCharId();
            if (!DomainManager.Character.TryGetElement_Objects(taiwuId, out var taiwu))
                return Err("taiwu not found");
            if (template <= 0) template = 90;
            ItemKey rope = DomainManager.Item.CreateItem(ctx, (sbyte)12, (short)template);
            EventHelper.AddInventoryItem(taiwu, rope, 1);
            _lastRopeKey = rope;
            _hasRope = true;
            return new Dictionary<string, object>
            {
                ["ok"] = true,
                ["itemType"] = (int)rope.ItemType,
                ["templateId"] = (int)rope.TemplateId,
                ["modState"] = (int)rope.ModificationState,
                ["id"] = rope.Id,
            };
        }

        /// <summary>在进行中的战斗里向敌人扔出之前给的捕绳（CombatDomain.UseItem）。命中受标记/RNG 影响，需多次重试。</summary>
        public static Dictionary<string, object> ThrowRope(DataContext ctx)
        {
            if (!_hasRope)
                return Err("no rope given; call /giverope first");
            int status = (int)DomainManager.Combat.GetCombatStatus();
            DomainManager.Combat.UseItem(ctx, _lastRopeKey, (sbyte)-1, true, null);
            return new Dictionary<string, object>
            {
                ["ok"] = true,
                ["thrown"] = true,
                ["combatStatus"] = status,
                ["ropeTemplate"] = (int)_lastRopeKey.TemplateId,
            };
        }

        // ---------- 当前格角色（含护卫，用于护卫拦截测试目标选取） ----------

        public static Dictionary<string, object> BlockChars()
        {
            int taiwuId = DomainManager.Taiwu.GetTaiwuCharId();
            if (!DomainManager.Character.TryGetElement_Objects(taiwuId, out var taiwu))
                return Err("taiwu not found");
            var loc = taiwu.GetLocation();
            var block = DomainManager.Map.GetBlockData(loc.AreaId, loc.BlockId);
            var ids = new HashSet<int>();
            if (block.CharacterSet != null) foreach (var i in block.CharacterSet) ids.Add(i);
            if (block.FixedCharacterSet != null) foreach (var i in block.FixedCharacterSet) ids.Add(i);
            var list = new List<object>();
            foreach (int id in ids)
            {
                if (id == taiwuId) continue;
                if (!DomainManager.Character.TryGetElement_Objects(id, out var ch)) continue;
                list.Add(new Dictionary<string, object>
                {
                    ["id"] = id,
                    ["name"] = SafeName(id),
                    ["orgTemplateId"] = (int)ch.GetOrganizationInfo().OrgTemplateId,
                    ["hasGuard"] = DomainManager.Character.HasGuard(id, ch),
                    ["kidnapperId"] = ch.GetKidnapperId(),
                });
            }
            return new Dictionary<string, object>
            {
                ["ok"] = true,
                ["areaId"] = (int)loc.AreaId,
                ["blockId"] = (int)loc.BlockId,
                ["count"] = list.Count,
                ["chars"] = list,
            };
        }

        // ---------- 工具 ----------

        private static string SafeName(int id)
        {
            try { return DomainManager.Character.GetName(id); }
            catch { return "#" + id; }
        }

        public static Dictionary<string, object> Err(string msg)
            => new Dictionary<string, object> { ["ok"] = false, ["error"] = msg };
    }
}
