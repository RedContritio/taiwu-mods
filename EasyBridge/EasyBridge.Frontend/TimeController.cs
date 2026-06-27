using System.Collections.Generic;
using UnityEngine;

namespace EasyBridge.Frontend
{
    /// <summary>
    /// Deterministic game-time control for screenshot / animation testing.
    ///
    /// The problem it solves: sleeping the client (Start-Sleep) to land on a transient animation frame is
    /// unreliable — elapsed game time = wall-clock + command latency + the agent's *thinking* time, none of
    /// which is reproducible. Instead the game is PAUSED by default and advanced only by explicit, fixed
    /// steps of game time. "pause; step 0.35; screenshot" lands on the same frame every run, regardless of
    /// how long anything takes between commands. Stepping also runs ≥1 frame, so pending UI/mesh changes
    /// (e.g. an Outline edit) actually render before the capture.
    ///
    /// <see cref="Tick"/> must be called once per frame (wired from <see cref="MainThreadDispatcher.Update"/>).
    /// All mutating calls run on the Unity main thread; <see cref="IsStepping"/> is the only cross-thread read.
    /// </summary>
    internal static class TimeController
    {
        private enum Mode { Off, Paused, Running, Stepping }

        private static Mode _mode = Mode.Off;
        private static float _stepRemaining;
        private static volatile bool _stepping; // read by the pipe thread while it waits

        public static bool IsStepping => _stepping;

        public static object Pause()
        {
            Time.timeScale = 0f;
            _mode = Mode.Paused;
            _stepRemaining = 0f;
            _stepping = false;
            return State();
        }

        public static object Resume()
        {
            Time.timeScale = 1f;
            _mode = Mode.Running;
            _stepRemaining = 0f;
            _stepping = false;
            return State();
        }

        /// <summary>Begin advancing exactly <paramref name="seconds"/> of game time, then re-pause. Returns
        /// immediately; the caller polls <see cref="IsStepping"/> until it clears.</summary>
        public static object BeginStep(float seconds)
        {
            if (seconds <= 0f)
                return Pause();
            _stepRemaining = seconds;
            _stepping = true;
            _mode = Mode.Stepping;
            Time.timeScale = 1f;
            return State();
        }

        public static void Tick()
        {
            if (_mode != Mode.Stepping)
                return;
            // timeScale==1 here, so Time.deltaTime is the amount of game time this frame advanced.
            _stepRemaining -= Time.deltaTime;
            if (_stepRemaining <= 0f)
            {
                Time.timeScale = 0f;
                _mode = Mode.Paused;
                _stepRemaining = 0f;
                _stepping = false;
            }
        }

        public static object State()
        {
            return new Dictionary<string, object>
            {
                ["ok"] = true,
                ["mode"] = _mode.ToString(),
                ["timeScale"] = Time.timeScale,
                ["stepRemaining"] = _stepRemaining < 0f ? 0f : _stepRemaining,
            };
        }
    }
}
