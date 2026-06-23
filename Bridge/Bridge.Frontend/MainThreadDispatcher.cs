using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;

namespace Bridge.Frontend
{
    /// <summary>
    /// 把后台 HTTP 线程的工作封送到 Unity 主线程执行。
    /// 读取/操作 UnityEngine 对象必须在主线程，否则会抛异常。
    /// </summary>
    public class MainThreadDispatcher : MonoBehaviour
    {
        private static MainThreadDispatcher _instance;
        private static readonly ConcurrentQueue<Job> _queue = new ConcurrentQueue<Job>();

        private sealed class Job
        {
            public Func<object> Fn;
            public object Result;
            public Exception Error;
            public readonly ManualResetEventSlim Done = new ManualResetEventSlim(false);
        }

        public static bool Ready => _instance != null;

        public static MainThreadDispatcher Create()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("Bridge.MainThreadDispatcher");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<MainThreadDispatcher>();
            return _instance;
        }

        public static void Destroy()
        {
            if (_instance != null)
            {
                UnityEngine.Object.Destroy(_instance.gameObject);
                _instance = null;
            }
            // 排空队列，唤醒所有等待者
            while (_queue.TryDequeue(out var job))
            {
                job.Error = new InvalidOperationException("dispatcher shutting down");
                job.Done.Set();
            }
        }

        /// <summary>在主线程同步执行 fn，并返回结果（带超时）。供后台线程调用。</summary>
        public static object Run(Func<object> fn, int timeoutMs = 5000)
        {
            if (_instance == null)
                throw new InvalidOperationException("dispatcher not running");

            var job = new Job { Fn = fn };
            _queue.Enqueue(job);
            if (!job.Done.Wait(timeoutMs))
                throw new TimeoutException("main-thread job timed out");
            if (job.Error != null)
                throw job.Error;
            return job.Result;
        }

        private void Update()
        {
            int guard = 64; // 每帧最多处理这么多，避免长卡顿
            while (guard-- > 0 && _queue.TryDequeue(out var job))
            {
                try { job.Result = job.Fn(); }
                catch (Exception ex) { job.Error = ex; }
                finally { job.Done.Set(); }
            }
        }
    }
}
