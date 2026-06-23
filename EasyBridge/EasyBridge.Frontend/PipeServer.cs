using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace EasyBridge.Frontend
{
    /// <summary>
    /// 命名管道服务器。管道名固定为 "taiwu-uibridge"，即连接地址。
    /// 协议：客户端写一行 JSON 请求，服务端写一行 JSON 响应，然后关闭连接。
    /// 请求格式：{"path":"/ui","query":{"detail":"full"},"body":"..."}
    /// method 由 path 隐含：有 body 则 POST，否则 GET。
    /// </summary>
    internal sealed class PipeServer
    {
        public const string PipeName = "taiwu-uibridge";

        private volatile bool _running;
        private Thread _acceptThread;

        public void Start()
        {
            _running = true;
            _acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "EasyBridge.PipeServer" };
            _acceptThread.Start();
        }

        public void Stop()
        {
            _running = false;
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                NamedPipeServerStream pipe = null;
                try
                {
                    pipe = new NamedPipeServerStream(PipeName, PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                    var ar = pipe.BeginWaitForConnection(null, null);
                    while (!ar.AsyncWaitHandle.WaitOne(500))
                    {
                        if (!_running) { pipe.Dispose(); return; }
                    }
                    pipe.EndWaitForConnection(ar);

                    var captured = pipe;
                    ThreadPool.QueueUserWorkItem(_ => HandleClient(captured));
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

                    var parsed = Json.Parse(requestLine);
                    var path = Json.GetString(parsed, "path") ?? "/";
                    var body = Json.GetString(parsed, "body") ?? "";
                    var query = ExtractQuery(parsed);
                    var method = string.IsNullOrEmpty(body) ? "GET" : "POST";

                    object responseObj;
                    try
                    {
                        var (_, b) = Router.Handle(method, path, query, body);
                        responseObj = b;
                    }
                    catch (Exception ex)
                    {
                        responseObj = new Dictionary<string, object> { ["error"] = ex.Message };
                    }

                    WriteResponse(pipe, responseObj);
                }
            }
            catch { }
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
        {
            var json = Json.Write(responseObj);
            var bytes = Encoding.UTF8.GetBytes(json + "\n");
            pipe.Write(bytes, 0, bytes.Length);
            pipe.Flush();
            pipe.WaitForPipeDrain();
        }
    }
}
