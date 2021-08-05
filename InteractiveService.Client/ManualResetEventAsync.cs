using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InteractiveService.Client
{
    sealed class ManualResetEventAsync : IDisposable
    {
        private volatile TaskCompletionSource<bool> m_tcs = new();

        public async Task WaitAsync(CancellationToken cancellationToken = default)
        {
            if (IsSet)
            {
                return;
            }

            using var registration = cancellationToken.Register(() => m_tcs.TrySetCanceled());
            await m_tcs.Task;
        }

        public bool IsSet => m_tcs.Task.IsCompletedSuccessfully;

        public void Set() { m_tcs.TrySetResult(true); }

        public void Reset()
        {
            while (true)
            {
                var tcs = m_tcs;
                if (!tcs.Task.IsCompleted ||
                    Interlocked.CompareExchange(ref m_tcs, new TaskCompletionSource<bool>(), tcs) == tcs)
                    return;
            }
        }

        public void Dispose()
        {
            m_tcs.TrySetCanceled();
        }
    }
}
