namespace Bloxstrap.Utility
{
    public class InterProcessLock : IDisposable
    {
        public Mutex Mutex { get; private set; }

        public bool IsAcquired { get; private set; }

        private bool _disposed;

        public InterProcessLock(string name) : this(name, TimeSpan.Zero) { }

        public InterProcessLock(string name, TimeSpan timeout)
        {
            if (String.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A mutex name must be provided.", nameof(name));

            Mutex = new Mutex(false, $"{Bloxstrap.App.ProjectName}-{name}");

            try
            {
                IsAcquired = Mutex.WaitOne(timeout);
            }
            catch (AbandonedMutexException)
            {
                // The previous owner exited without releasing the mutex. Ownership is
                // transferred to this thread, so it is safe to continue.
                IsAcquired = true;
            }
            catch
            {
                Mutex.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                if (IsAcquired)
                {
                    Mutex.ReleaseMutex();
                    IsAcquired = false;
                }
            }
            finally
            {
                // A timed-out lock still owns a native handle; always close it.
                Mutex.Dispose();
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }
    }
}
