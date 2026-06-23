using System;
using System.Collections.Concurrent;
using System.Threading;
using GameData.Common;

namespace EasyBridge.Backend
{
    /// <summary>
    /// 把管道线程的工作封送到后端主线程执行。后端的域写操作（DomainManager.*）
    /// 必须在主循环线程、用主线程 DataContext 进行，否则与主循环并发会破坏数据。
    /// 队列由 Harmony 对 GlobalDomain.OnUpdate 的补丁每帧排空（见 HarmonyTick）。
    /// </summary>
    internal static class MainThreadPump
    {
        private sealed class Job
        {
            public Func<DataContext, object> Fn;
            public object Result;
            public Exception Error;
            public readonly ManualResetEventSlim Done = new ManualResetEventSlim(false);
        }

        private static readonly ConcurrentQueue<Job> Queue = new ConcurrentQueue<Job>();

        public static volatile bool TickAlive;

        /// <summary>在主线程同步执行 fn（带主线程 DataContext），返回结果。供管道线程调用。</summary>
        public static object Run(Func<DataContext, object> fn, int timeoutMs = 8000)
        {
            var job = new Job { Fn = fn };
            Queue.Enqueue(job);
            if (!job.Done.Wait(timeoutMs))
                throw new TimeoutException("main-thread job timed out (is a save loaded so GlobalDomain ticks?)");
            if (job.Error != null)
                throw job.Error;
            return job.Result;
        }

        /// <summary>由主线程 tick 调用，排空队列。每帧最多处理 guard 个，避免长卡顿。</summary>
        public static void Drain(DataContext context)
        {
            TickAlive = true;
            int guard = 32;
            while (guard-- > 0 && Queue.TryDequeue(out var job))
            {
                try { job.Result = job.Fn(context); }
                catch (Exception ex) { job.Error = ex; }
                finally { job.Done.Set(); }
            }
        }

        /// <summary>关停：唤醒所有等待者并报错。</summary>
        public static void Shutdown()
        {
            while (Queue.TryDequeue(out var job))
            {
                job.Error = new InvalidOperationException("pump shutting down");
                job.Done.Set();
            }
        }
    }
}
