using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace EasyBridge.Frontend
{
    /// <summary>
    /// 命名管道服务器。管道名固定为 "easybridge-ui"，即连接地址。
    /// 协议：客户端写一行 JSON 请求，服务端写一行 JSON 响应，然后关闭连接。
    /// 请求格式：{"path":"/ui","query":{"detail":"full"},"body":"..."}
    /// method 由 path 隐含：有 body 则 POST，否则 GET。
    /// </summary>
    internal sealed class PipeServer
    {
        public const string PipeName = "easybridge-ui";
        private const int MaxServerInstances = 8;
        private const int MaxConcurrentClients = 4;
        private const int MaxRequestChars = 256 * 1024;

        private volatile bool _running;
        private Thread _acceptThread;
        private static readonly SemaphoreSlim ClientSlots = new SemaphoreSlim(MaxConcurrentClients, MaxConcurrentClients);

        public void Start()
        {
            _running = true;
            _acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "EasyBridge.PipeServer" };
            _acceptThread.Start();
        }

        public void Stop()
        {
            _running = false;
            var thread = _acceptThread;
            _acceptThread = null;
            if (thread != null && thread.IsAlive)
                thread.Join(1000);
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                NamedPipeServerStream pipe = null;
                try
                {
                    pipe = new NamedPipeServerStream(PipeName, PipeDirection.InOut,
                        MaxServerInstances,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                    var ar = pipe.BeginWaitForConnection(null, null);
                    while (!ar.AsyncWaitHandle.WaitOne(500))
                    {
                        if (!_running) { pipe.Dispose(); return; }
                    }
                    pipe.EndWaitForConnection(ar);

                    var captured = pipe;
                    if (!ClientSlots.Wait(0))
                    {
                        ThreadPool.QueueUserWorkItem(_ => RejectBusy(captured));
                        pipe = null;
                        continue;
                    }
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        try { HandleClient(captured); }
                        finally { ClientSlots.Release(); }
                    });
                    pipe = null;
                }
                catch
                {
                    pipe?.Dispose();
                    if (!_running) break;
                    Thread.Sleep(100);
                }
            }
        }

        private static void HandleClient(NamedPipeServerStream pipe)
        {
            try
            {
                using (pipe)
                {
                    TrySetTimeouts(pipe);
                    string requestLine;
                    using (var reader = new StreamReader(pipe, Encoding.UTF8, false, 4096, leaveOpen: true))
                    {
                        requestLine = reader.ReadLine();
                    }

                    if (string.IsNullOrEmpty(requestLine))
                    {
                        WriteResponse(pipe, new Dictionary<string, object> { ["error"] = "empty request" });
                        return;
                    }
                    if (requestLine.Length > MaxRequestChars)
                    {
                        WriteResponse(pipe, new Dictionary<string, object> { ["ok"] = false, ["error"] = "request too large" });
                        return;
                    }

                    var parsed = Json.Parse(requestLine);
                    var path = Json.GetString(parsed, "path") ?? "/";
                    var body = Json.GetString(parsed, "body") ?? "";
                    var query = ExtractQuery(parsed);
                    var method = string.IsNullOrEmpty(body) ? "GET" : "POST";
                    var note = Json.GetString(parsed, "note") ?? "";

                    int status = 200;
                    object responseObj;
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    try
                    {
                        var (st, b) = Router.Handle(method, path, query, body);
                        status = st;
                        responseObj = b;
                    }
                    catch (Exception ex)
                    {
                        status = 500;
                        responseObj = new Dictionary<string, object> { ["error"] = ex.Message };
                    }
                    sw.Stop();

                    string json = Json.Write(responseObj);
                    // Skip infrastructure noise: monitor polls (mon=1), the cross-pipe push, and bare
                    // connectivity health-checks (/ping, /) — none are game operations worth showing.
                    bool skip = (query != null && query.TryGetValue("mon", out var mv) && mv == "1")
                        || path == "/monitor/push" || path == "/ping" || path == "/";
                    if (!skip && Router.EnableMonitor)
                        RequestLog.Add("frontend", method, path, QueryString(query), body, note, status, json, sw.ElapsedMilliseconds);
                    WriteRaw(pipe, json);
                }
            }
            catch { }
        }

        private static void RejectBusy(NamedPipeServerStream pipe)
        {
            try
            {
                using (pipe)
                {
                    TrySetTimeouts(pipe);
                    WriteResponse(pipe, new Dictionary<string, object>
                    {
                        ["ok"] = false,
                        ["error"] = "busy",
                        ["maxConcurrentClients"] = MaxConcurrentClients,
                    });
                }
            }
            catch { }
        }

        private static void TrySetTimeouts(NamedPipeServerStream pipe)
        {
            try { pipe.ReadTimeout = 3000; } catch { }
            try { pipe.WriteTimeout = 3000; } catch { }
        }

        private static Dictionary<string, string> ExtractQuery(object parsed)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (parsed is IDictionary<string, object> root && root.TryGetValue("query", out var qv)
                && qv is IDictionary<string, object> qmap)
            {
                foreach (var kv in qmap)
                {
                    if (kv.Value != null)
                        result[kv.Key] = kv.Value is string s ? s : kv.Value.ToString();
                }
            }
            return result;
        }

        private static void WriteResponse(NamedPipeServerStream pipe, object responseObj)
            => WriteRaw(pipe, Json.Write(responseObj));

        private static void WriteRaw(NamedPipeServerStream pipe, string json)
        {
            var bytes = Encoding.UTF8.GetBytes(json + "\n");
            pipe.Write(bytes, 0, bytes.Length);
            pipe.Flush();
        }

        private static string QueryString(Dictionary<string, string> q)
        {
            if (q == null || q.Count == 0) return "";
            var parts = new List<string>();
            foreach (var kv in q) { if (kv.Key == "mon") continue; parts.Add(kv.Key + "=" + kv.Value); }
            return string.Join("&", parts);
        }
    }
}
