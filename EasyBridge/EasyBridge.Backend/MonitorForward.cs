using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace EasyBridge.Backend
{
    /// <summary>
    /// Forwards each handled backend request to the frontend in-game monitor over the easybridge-ui pipe
    /// (POST /monitor/push, marked mon=1 so it isn't itself logged), so one overlay shows BOTH pipes.
    /// Fire-and-forget; bounded (in-flight cap) and self-throttling (after a failure it backs off briefly),
    /// so an absent/unloaded frontend can never pile up thread-pool work. Never blocks the response or throws.
    /// </summary>
    internal static class MonitorForward
    {
        private const int MaxInFlight = 8;
        private static readonly TimeSpan BackoffAfterFail = TimeSpan.FromSeconds(3);

        private static int _inFlight;
        private static long _suppressUntilTicks;

        public static void Push(string method, string path, string query, string body, string note, int status, string resultJson, long ms)
        {
            if (Volatile.Read(ref _inFlight) >= MaxInFlight) return;
            if (DateTime.UtcNow.Ticks < Volatile.Read(ref _suppressUntilTicks)) return;

            Interlocked.Increment(ref _inFlight);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var entry = new Dictionary<string, object>
                    {
                        ["source"] = "backend",
                        ["method"] = method, ["path"] = path, ["query"] = query, ["body"] = body,
                        ["note"] = note, ["status"] = status, ["result"] = resultJson, ["ms"] = ms,
                    };
                    var req = new Dictionary<string, object>
                    {
                        ["path"] = "/monitor/push",
                        ["query"] = new Dictionary<string, object> { ["mon"] = "1" },
                        ["body"] = Json.Write(entry),
                    };
                    using (var pipe = new NamedPipeClientStream(".", "easybridge-ui", PipeDirection.InOut))
                    {
                        pipe.Connect(300);
                        using (var w = new StreamWriter(pipe, new UTF8Encoding(false), 4096, leaveOpen: true))
                        {
                            w.WriteLine(Json.Write(req));
                            w.Flush();
                        }
                        // The forward doesn't care about the reply; read one line so the server can drain.
                        using (var r = new StreamReader(pipe, Encoding.UTF8)) { r.ReadLine(); }
                    }
                }
                catch
                {
                    // Frontend absent/busy — back off so we don't keep blocking on Connect.
                    Volatile.Write(ref _suppressUntilTicks, (DateTime.UtcNow + BackoffAfterFail).Ticks);
                }
                finally
                {
                    Interlocked.Decrement(ref _inFlight);
                }
            });
        }
    }
}
