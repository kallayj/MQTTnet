﻿using System;
using System.Threading;
using System.Threading.Tasks;

namespace MQTTnet.Internal
{
    // From Stephen Toub (https://blogs.msdn.microsoft.com/pfxteam/2012/02/12/building-async-coordination-primitives-part-6-asynclock/)
    public sealed class AsyncLock : IDisposable
    {
        readonly object _syncRoot = new object();
        readonly Task<IDisposable> _releaser;

        SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public AsyncLock()
        {
            _releaser = Task.FromResult((IDisposable)new Releaser(this));
        }

        public Task<IDisposable> WaitAsync()
        {
            return WaitAsync(CancellationToken.None);
        }

        public Task<IDisposable> WaitAsync(CancellationToken cancellationToken)
        {
            var task = _semaphore.WaitAsync(cancellationToken);
            if (task.Status == TaskStatus.RanToCompletion)
            {
                return _releaser;
            }

            return task.ContinueWith(
                (_, state) => (IDisposable)state, 
                _releaser.Result, 
                cancellationToken, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                _semaphore?.Dispose();
                _semaphore = null;
            }
        }

        internal void Release()
        {
            lock (_syncRoot)
            {
                _semaphore?.Release();
            }
        }

        sealed class Releaser : IDisposable
        {
            readonly AsyncLock _lock;

            internal Releaser(AsyncLock @lock)
            {
                _lock = @lock;
            }

            public void Dispose()
            {
                _lock.Release();
            }
        }
    }
}
