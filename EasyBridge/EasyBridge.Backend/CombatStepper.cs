using System;
using System.Collections.Generic;
using GameData.Common;
using GameData.Domains;
using GameData.Domains.Combat;
using GameData.Domains.Item;

namespace EasyBridge.Backend
{
    /// <summary>
    /// In-process combat stepping for tests. Pipe requests arm/resume/cancel the watch;
    /// the actual breakpoint check runs on the backend main-thread tick.
    /// </summary>
    internal static class CombatStepper
    {
        private sealed class Watch
        {
            public int Id;
            public string Mode;
            public string Side;
            public string FreezeBy;
            public int Threshold;
            public int Frames;
            public int MaxFrames;
            public int MaxMs;
            public bool WaitForCombat;
            public bool ResumeOnArm;
            public float RunTimeScale;
            public bool? SetAutoCombat;
            public bool? SetAutoMove;
            public bool RestoreOnCancel;

            public bool Started;
            public bool Done;
            public string Reason;
            public string Error;
            public ulong? StartFrame;
            public long StartTicks;
            public long EndTicks;
            public string SuppressedSignature;
            public bool SuppressionCleared;
            public string BypassBeforeCommitSignature;

            public float? SavedTimeScale;
            public bool? SavedAutoCombat;
            public bool? SavedAutoMove;
            public Dictionary<string, object> Hit;
            public Dictionary<string, object> Snapshot;
            public int BreakCount;
        }

        private const string ModeAnyReady = "anyReady";
        private const string ModeFrames = "frames";
        private const string ModeBeforeCommit = "beforeCommit";
        private const string FreezeTimeScale0 = "timeScale0";

        private static readonly object Sync = new object();
        private static int _nextId = 1;
        private static Watch _watch;

        public static void Tick(DataContext context)
        {
            Watch watch;
            lock (Sync) watch = _watch;
            if (watch == null || watch.Done)
                return;

            try
            {
                TickWatch(context, watch);
            }
            catch (Exception ex)
            {
                Finish(context, watch, "error", null, ex.GetType().Name + ": " + ex.Message, freeze: true);
            }
        }

        public static Dictionary<string, object> Arm(DataContext context, object body)
        {
            var watch = new Watch
            {
                Id = _nextId++,
                Mode = GetString(body, "mode", ModeAnyReady),
                Side = GetString(body, "side", "any"),
                FreezeBy = GetString(body, "freezeBy", FreezeTimeScale0),
                Threshold = Clamp(Json.GetInt(body, "threshold", 100), 0, 100),
                Frames = Math.Max(1, Json.GetInt(body, "frames", 1)),
                MaxFrames = Math.Max(0, Json.GetInt(body, "maxFrames", 600)),
                MaxMs = Math.Max(0, Json.GetInt(body, "maxMs", 0)),
                WaitForCombat = Json.GetBool(body, "waitForCombat", true),
                ResumeOnArm = Json.GetBool(body, "resume", true),
                RestoreOnCancel = Json.GetBool(body, "restoreOnCancel", true),
                StartTicks = StopwatchTicks(),
            };

            watch.SavedTimeScale = SafeTimeScale();
            watch.SavedAutoCombat = SafeAutoCombat();
            watch.SavedAutoMove = SafeAutoMove();
            watch.RunTimeScale = GetFloat(body, "runTimeScale",
                watch.SavedTimeScale.HasValue && watch.SavedTimeScale.Value > 0f ? watch.SavedTimeScale.Value : 1f);
            if (Json.TryGetBool(body, "autoCombat", out bool autoCombat))
                watch.SetAutoCombat = autoCombat;
            if (Json.TryGetBool(body, "autoMove", out bool autoMove))
                watch.SetAutoMove = autoMove;

            lock (Sync) _watch = watch;
            ApplyStartControls(context, watch);
            watch.Snapshot = GameOps.Combat(context);
            return Status(context);
        }

        public static Dictionary<string, object> Status(DataContext context)
        {
            Watch watch;
            lock (Sync) watch = _watch;
            if (watch == null)
                return new Dictionary<string, object> { ["ok"] = true, ["active"] = false, ["watch"] = null };

            return new Dictionary<string, object>
            {
                ["ok"] = true,
                ["active"] = true,
                ["watch"] = WatchInfo(watch),
                ["combat"] = GameOps.Combat(context),
            };
        }

        public static Dictionary<string, object> Resume(DataContext context, object body)
        {
            Watch watch;
            lock (Sync) watch = _watch;
            if (watch == null)
                return new Dictionary<string, object> { ["ok"] = false, ["error"] = "no active combat watch" };

            bool rearm = Json.GetBool(body, "rearm", true);
            if (!rearm)
            {
                ApplyStartControls(context, watch);
                return Status(context);
            }

            string resumeSignature = GetString(watch.Hit, "signature", null);
            watch.Done = false;
            watch.Reason = null;
            watch.Error = null;
            watch.Hit = null;
            watch.Snapshot = null;
            watch.StartFrame = SafeCombatFrame();
            watch.StartTicks = StopwatchTicks();
            watch.EndTicks = 0;
            watch.Started = DomainManager.Combat.IsInCombat();
            watch.SuppressionCleared = true;
            watch.BypassBeforeCommitSignature = watch.Mode == ModeBeforeCommit ? resumeSignature : null;
            ApplyStartControls(context, watch);
            return Status(context);
        }

        public static bool TryBreakBeforeCommit(DataContext context, CombatCharacter ch,
            string kind, int value, int percent)
        {
            if (context == null || ch == null)
                return true;

            Watch watch;
            lock (Sync) watch = _watch;
            if (watch == null || watch.Done || watch.Mode != ModeBeforeCommit)
                return true;
            if (!SideMatches(ch.IsAlly, watch.Side))
                return true;
            if (!DomainManager.Combat.IsInCombat())
                return true;

            if (!watch.Started)
            {
                watch.Started = true;
                watch.StartFrame = SafeCombatFrame();
                ApplyStartControls(context, watch);
            }

            string state = StateName(ch);
            var hit = Hit(ch.GetId(), ch.IsAlly, kind, state, value, percent);
            string signature = GetString(hit, "signature", null);
            if (!string.IsNullOrEmpty(signature) && signature == watch.BypassBeforeCommitSignature)
            {
                watch.BypassBeforeCommitSignature = null;
                return true;
            }
            Finish(context, watch, "hit", hit, null, freeze: true);
            return false;
        }

        public static Dictionary<string, object> Cancel(DataContext context, object body)
        {
            Watch watch;
            lock (Sync)
            {
                watch = _watch;
                _watch = null;
            }
            if (watch == null)
                return new Dictionary<string, object> { ["ok"] = true, ["active"] = false, ["canceled"] = false };

            bool restore = Json.GetBool(body, "restore", watch.RestoreOnCancel);
            if (restore)
                Restore(context, watch);
            return new Dictionary<string, object>
            {
                ["ok"] = true,
                ["active"] = false,
                ["canceled"] = true,
                ["restored"] = restore,
                ["watch"] = WatchInfo(watch),
                ["combat"] = GameOps.Combat(context),
            };
        }

        private static void TickWatch(DataContext context, Watch watch)
        {
            bool inCombat = DomainManager.Combat.IsInCombat();
            if (!inCombat)
            {
                if (!watch.WaitForCombat || TimedOut(watch))
                    Finish(context, watch, watch.WaitForCombat ? "timeoutWaitingCombat" : "notInCombat", null, null, freeze: false);
                return;
            }

            if (!watch.Started)
            {
                watch.Started = true;
                watch.StartFrame = SafeCombatFrame();
                ApplyStartControls(context, watch);
            }
            else
            {
                ApplyStartControls(context, watch);
            }

            ulong? frame = SafeCombatFrame();
            if (!watch.StartFrame.HasValue)
                watch.StartFrame = frame;

            Dictionary<string, object> hit = Evaluate(watch);
            if (hit != null)
            {
                string signature = GetString(hit, "signature", null);
                if (!watch.SuppressionCleared)
                {
                    if (signature == watch.SuppressedSignature)
                        hit = null;
                    else
                    {
                        watch.SuppressionCleared = true;
                        watch.SuppressedSignature = null;
                    }
                }
            }
            else if (!watch.SuppressionCleared)
            {
                watch.SuppressionCleared = true;
                watch.SuppressedSignature = null;
            }

            if (hit != null)
            {
                Finish(context, watch, "hit", hit, null, freeze: true);
                return;
            }

            if (watch.Mode == ModeFrames && frame.HasValue && watch.StartFrame.HasValue &&
                frame.Value >= watch.StartFrame.Value + (ulong)watch.Frames)
            {
                Finish(context, watch, "frames", new Dictionary<string, object>
                {
                    ["kind"] = "frames",
                    ["frame"] = frame.Value,
                    ["signature"] = "frames:" + frame.Value,
                }, null, freeze: true);
                return;
            }

            if (watch.MaxFrames > 0 && frame.HasValue && watch.StartFrame.HasValue &&
                frame.Value >= watch.StartFrame.Value + (ulong)watch.MaxFrames)
            {
                Finish(context, watch, "timeoutFrames", null, null, freeze: true);
                return;
            }

            if (TimedOut(watch))
                Finish(context, watch, "timeoutMs", null, null, freeze: true);
        }

        private static Dictionary<string, object> Evaluate(Watch watch)
        {
            if (watch.Mode == ModeFrames || watch.Mode == ModeBeforeCommit)
                return null;

            if (SideMatches(true, watch.Side))
            {
                var hit = Probe(DomainManager.Combat.GetCombatCharacter(isAlly: true), watch.Threshold);
                if (hit != null) return hit;
            }
            if (SideMatches(false, watch.Side))
            {
                var hit = Probe(DomainManager.Combat.GetCombatCharacter(isAlly: false), watch.Threshold);
                if (hit != null) return hit;
            }
            return null;
        }

        private static Dictionary<string, object> Probe(CombatCharacter ch, int threshold)
        {
            if (ch == null || ch.StateMachine == null)
                return null;

            string state = StateName(ch);
            int id = ch.GetId();
            bool isAlly = ch.IsAlly;
            short skill = SafePreparingSkill(ch);
            int skillPercent = SafeSkillPreparePercent(ch);
            if (skill >= 0 && skillPercent >= threshold)
                return Hit(id, isAlly, "skillPrepare", state, skill, skillPercent);

            sbyte other = SafePreparingOtherAction(ch);
            int otherPercent = SafeOtherActionPreparePercent(ch);
            if (other >= 0 && otherPercent >= threshold)
                return Hit(id, isAlly, "otherActionPrepare", state, other, otherPercent);

            ItemKey item = SafePreparingItem(ch);
            int itemPercent = SafeUseItemPreparePercent(ch);
            if (item.IsValid() && itemPercent >= threshold)
                return Hit(id, isAlly, "useItemPrepare", state, item.TemplateId, itemPercent);

            switch (state)
            {
                case "Attack":
                case "CastSkill":
                case "UseItem":
                case "UnlockAttack":
                case "AnimalAttack":
                    return Hit(id, isAlly, "state", state, 0, 100);
                default:
                    return null;
            }
        }

        private static Dictionary<string, object> Hit(int id, bool isAlly, string kind, string state, int value, int percent)
        {
            string side = isAlly ? "self" : "enemy";
            string signature = side + ":" + id + ":" + kind + ":" + state + ":" + value;
            return new Dictionary<string, object>
            {
                ["charId"] = id,
                ["side"] = side,
                ["kind"] = kind,
                ["state"] = state,
                ["value"] = value,
                ["percent"] = percent,
                ["frame"] = SafeCombatFrame(),
                ["signature"] = signature,
            };
        }

        private static void Finish(DataContext context, Watch watch, string reason,
            Dictionary<string, object> hit, string error, bool freeze)
        {
            watch.Done = true;
            watch.Reason = reason;
            watch.Error = error;
            watch.Hit = hit;
            watch.EndTicks = StopwatchTicks();
            if (hit != null)
            {
                watch.BreakCount++;
                watch.SuppressedSignature = GetString(hit, "signature", null);
                watch.SuppressionCleared = string.IsNullOrEmpty(watch.SuppressedSignature);
            }
            if (freeze)
                Freeze(context, watch);
            watch.Snapshot = GameOps.Combat(context);
        }

        private static void ApplyStartControls(DataContext context, Watch watch)
        {
            if (!DomainManager.Combat.IsInCombat())
                return;
            if (watch.SetAutoCombat.HasValue)
                DomainManager.Combat.SetPlayerAutoCombat(context, watch.SetAutoCombat.Value);
            if (watch.SetAutoMove.HasValue && DomainManager.Combat.AiOptions != null)
                DomainManager.Combat.AiOptions.AutoMove = watch.SetAutoMove.Value;
            if (watch.ResumeOnArm && watch.RunTimeScale > 0f)
                DomainManager.Combat.SetTimeScale(context, watch.RunTimeScale);
        }

        private static void Freeze(DataContext context, Watch watch)
        {
            if (!DomainManager.Combat.IsInCombat())
                return;
            if (watch.FreezeBy == FreezeTimeScale0)
                DomainManager.Combat.SetTimeScale(context, 0f);
        }

        private static void Restore(DataContext context, Watch watch)
        {
            if (!DomainManager.Combat.IsInCombat())
                return;
            if (watch.SavedAutoCombat.HasValue)
                DomainManager.Combat.SetPlayerAutoCombat(context, watch.SavedAutoCombat.Value);
            if (watch.SavedAutoMove.HasValue && DomainManager.Combat.AiOptions != null)
                DomainManager.Combat.AiOptions.AutoMove = watch.SavedAutoMove.Value;
            if (watch.SavedTimeScale.HasValue)
                DomainManager.Combat.SetTimeScale(context, watch.SavedTimeScale.Value);
        }

        private static Dictionary<string, object> WatchInfo(Watch watch)
        {
            return new Dictionary<string, object>
            {
                ["id"] = watch.Id,
                ["mode"] = watch.Mode,
                ["side"] = watch.Side,
                ["freezeBy"] = watch.FreezeBy,
                ["threshold"] = watch.Threshold,
                ["frames"] = watch.Frames,
                ["maxFrames"] = watch.MaxFrames,
                ["maxMs"] = watch.MaxMs,
                ["waitForCombat"] = watch.WaitForCombat,
                ["started"] = watch.Started,
                ["done"] = watch.Done,
                ["reason"] = watch.Reason,
                ["error"] = watch.Error,
                ["startFrame"] = watch.StartFrame,
                ["elapsedMs"] = ElapsedMs(watch),
                ["breakCount"] = watch.BreakCount,
                ["hit"] = watch.Hit,
                ["bypassBeforeCommit"] = watch.BypassBeforeCommitSignature,
                ["snapshot"] = watch.Snapshot,
                ["saved"] = new Dictionary<string, object>
                {
                    ["timeScale"] = watch.SavedTimeScale,
                    ["autoCombat"] = watch.SavedAutoCombat,
                    ["autoMove"] = watch.SavedAutoMove,
                },
            };
        }

        private static bool TimedOut(Watch watch)
            => watch.MaxMs > 0 && ElapsedMs(watch) >= watch.MaxMs;

        private static long ElapsedMs(Watch watch)
        {
            long end = watch.EndTicks != 0 ? watch.EndTicks : StopwatchTicks();
            return (long)((end - watch.StartTicks) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
        }

        private static bool SideMatches(bool isAlly, string side)
        {
            switch ((side ?? "any").Trim().ToLowerInvariant())
            {
                case "any":
                case "both":
                    return true;
                case "self":
                case "ally":
                case "left":
                    return isAlly;
                case "enemy":
                case "opponent":
                case "right":
                    return !isAlly;
                default:
                    return true;
            }
        }

        private static string StateName(CombatCharacter ch)
        {
            try { return ch.StateMachine.GetCurrentStateType().ToString(); }
            catch { return null; }
        }

        private static short SafePreparingSkill(CombatCharacter ch)
        {
            try { return ch.GetPreparingSkillId(); }
            catch { return -1; }
        }

        private static int SafeSkillPreparePercent(CombatCharacter ch)
        {
            try { return ch.GetSkillPreparePercent(); }
            catch { return 0; }
        }

        private static sbyte SafePreparingOtherAction(CombatCharacter ch)
        {
            try { return ch.GetPreparingOtherAction(); }
            catch { return -1; }
        }

        private static int SafeOtherActionPreparePercent(CombatCharacter ch)
        {
            try { return ch.GetOtherActionPreparePercent(); }
            catch { return 0; }
        }

        private static ItemKey SafePreparingItem(CombatCharacter ch)
        {
            try { return ch.GetPreparingItem(); }
            catch { return ItemKey.Invalid; }
        }

        private static int SafeUseItemPreparePercent(CombatCharacter ch)
        {
            try { return ch.GetUseItemPreparePercent(); }
            catch { return 0; }
        }

        private static ulong? SafeCombatFrame()
        {
            try { return DomainManager.Combat.GetCombatFrame(); }
            catch { return null; }
        }

        private static float? SafeTimeScale()
        {
            try { return DomainManager.Combat.GetTimeScale(); }
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

        private static string GetString(object obj, string key, string fallback)
        {
            string value = Json.GetString(obj, key);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        private static float GetFloat(object obj, string key, float fallback)
        {
            return Json.TryGetDouble(obj, key, out double value) ? (float)value : fallback;
        }

        private static int Clamp(int value, int min, int max)
            => Math.Max(min, Math.Min(max, value));

        private static long StopwatchTicks() => System.Diagnostics.Stopwatch.GetTimestamp();
    }
}
