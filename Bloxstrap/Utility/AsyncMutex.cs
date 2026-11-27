namespace Bloxstrap.Utility
{
    // https://gist.github.com/dfederm/35c729f6218834b764fa04c219181e4e

    public sealed class AsyncMutex : IAsyncDisposable
    {
        private readonly bool _initiallyOwned;
        private readonly string _name;
        private Task? _mutexTask;
        private ManualResetEventSlim? _releaseEvent;
        private CancellationTokenSource? _cancellationTokenSource;
        private int _acquireStarted;

        public AsyncMutex(bool initiallyOwned, string name)
        {
            if (String.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A mutex name must be provided.", nameof(name));

            _initiallyOwned = initiallyOwned;
            _name = name;
        }

        public Task AcquireAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Interlocked.Exchange(ref _acquireStarted, 1) != 0)
                throw new InvalidOperationException("An AsyncMutex instance can only be acquired once.");

            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseEvent = new ManualResetEventSlim();
            var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _releaseEvent = releaseEvent;
            _cancellationTokenSource = cancellationSource;

            // The worker must run even if cancellation arrives before scheduling,
            // so it can complete the TCS and dispose its synchronization resources.
            _mutexTask = Task.Factory.StartNew(
                () =>
                {
                    try
                    {
                        using var mutex = new Mutex(_initiallyOwned, _name);
                        bool ownsMutex = _initiallyOwned;

                        try
                        {
                            if (!ownsMutex)
                            {
                                try
                                {
                                    int waitResult = WaitHandle.WaitAny(
                                        new[] { mutex, cancellationSource.Token.WaitHandle }
                                    );

                                    if (waitResult != 0)
                                    {
                                        completion.TrySetCanceled(cancellationSource.Token);
                                        return;
                                    }

                                    ownsMutex = true;
                                }
                                catch (AbandonedMutexException)
                                {
                                    // The wait acquired the abandoned mutex for this thread.
                                    ownsMutex = true;
                                }
                            }

                            completion.TrySetResult();
                            releaseEvent.Wait();
                        }
                        finally
                        {
                            if (ownsMutex)
                                mutex.ReleaseMutex();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        completion.TrySetCanceled(cancellationSource.Token);
                    }
                    catch (Exception ex)
                    {
                        completion.TrySetException(ex);
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

            return completion.Task;
        }

        public async Task ReleaseAsync()
        {
            _releaseEvent?.Set();

            if (_mutexTask != null)
                await _mutexTask.ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            _cancellationTokenSource?.Cancel();
            await ReleaseAsync().ConfigureAwait(false);

            _releaseEvent?.Dispose();
            _cancellationTokenSource?.Dispose();
        }
    }
}