﻿using System;
using System.Threading.Tasks;

namespace WpfOpenGLBitmap.Helpers
{
    using System.Collections.Concurrent;
    using System.Threading;

    internal sealed class MessagingTask : IDisposable
    {
        private ConcurrentQueue<AsyncResult> _queue = new ConcurrentQueue<AsyncResult>();
        private CancellationTokenSource _cancelToken = new CancellationTokenSource();
        private Task _messageLoopTask = null;

        public MessagingTask()
        {
        }

        public void StartMessageLoop(Action prepare, Action finalize)
        {
            _messageLoopTask = Task.Factory.StartNew(
                () =>
                    {
                        if (prepare != null)
                        {
                            prepare.Invoke();
                        }
                        MessageLoop();
                        if (finalize != null)
                        {
                            finalize.Invoke();
                        }
                    },
                TaskCreationOptions.LongRunning);
        }


        private void MessageLoop()
        {
            AsyncResult result;
            while (!_cancelToken.IsCancellationRequested)
            {
                if (_queue.TryDequeue(out result))
                {
                    result.Action();
                    result._handle.Set();
                }
                else
                {
                    Thread.Sleep(1);
                }
            }
        }

        public void SyncAction(Action action)
        {
            this.EndAysncAction(this.BeginAsyncAction(action));
        }

        public IAsyncResult BeginAsyncAction(Action action)
        {
            var asyncresult = new AsyncResult()
                                 {
                                     Action =  action
                                 };
            _queue.Enqueue(asyncresult);
            return asyncresult;
        }

        public bool EndAysncAction(IAsyncResult result)
        {
            if (result is AsyncResult)
            {
                var asyncResult = result as AsyncResult;
                var complete = asyncResult.CompletedSynchronously;
                asyncResult.Dispose();
                return true;
            }
            else
            {
                return false;
            }

        }

        public void Dispose()
        {
            if (_messageLoopTask != null)
            {
                _cancelToken.Cancel();
                _messageLoopTask.Wait();
                AsyncResult result;
                while (_queue.TryDequeue(out result))
                {
                    result.Dispose();
                }
                _messageLoopTask = null;
            }

        }

        public bool IsRunning
        {
            get { return _messageLoopTask != null && !_messageLoopTask.IsCompleted; }
        }
    }
    
}
