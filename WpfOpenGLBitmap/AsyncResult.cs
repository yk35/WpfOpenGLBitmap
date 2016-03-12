using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfOpenGLBitmap
{
    using System.Threading;

    internal class AsyncResult : IAsyncResult, IDisposable
    {
        internal AsyncResult()
        {
            _handle = new EventWaitHandle(false, EventResetMode.ManualReset);
        }

        public bool IsCompleted { get { return this.AsyncWaitHandle.WaitOne(TimeSpan.Zero); } }

        public WaitHandle AsyncWaitHandle { get { return _handle; } }

        public bool CompletedSynchronously { get { return this.AsyncWaitHandle.WaitOne(); } }

        public object AsyncState { get; set; }


        internal Action Action;

        internal EventWaitHandle _handle;

        public void Dispose()
        {
            if (AsyncWaitHandle != null)
            {
                AsyncWaitHandle.Dispose();
                _handle = null;
            }
        }
    }
}
