namespace Bloxstrap
{
    // https://stackoverflow.com/a/53873141/11852173

    public class Logger
    {
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private FileStream? _filestream;

        public readonly List<string> History = new();
        public bool Initialized = false;
        public bool NoWriteMode = false;
        public string? FileLocation;

        public string AsDocument => String.Join('\n', History);

        public void Initialize(bool useTempDir = false)
        {
            const string LOG_IDENT = "Logger::Initialize";

            string directory = useTempDir ? Path.Combine(Paths.TempLogs) : Path.Combine(Paths.Base, "Logs");
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'");
            // Multiple ClaudeTrap processes are expected (settings, watcher and desktop pet).
            // Include the PID so a second process started in the same second does not fail
            // logger initialization because it found the first process' log file.
            string filename = $"{App.ProjectName}_{timestamp}_{Environment.ProcessId}.log";
            string location = Path.Combine(directory, filename);

            WriteLine(LOG_IDENT, $"Initializing at {location}");

            if (Initialized)
            {
                WriteLine(LOG_IDENT, "Failed to initialize because logger is already initialized");
                return;
            }

            Directory.CreateDirectory(directory);

            if (File.Exists(location))
            {
                WriteLine(LOG_IDENT, "Failed to initialize because log file already exists");
                return;
            }

            try
            {
                _filestream = File.Open(location, FileMode.Create, FileAccess.Write, FileShare.Read);
            }
            catch (IOException)
            {
                WriteLine(LOG_IDENT, "Failed to initialize because log file already exists");
                return;
            }
            catch (UnauthorizedAccessException)
            {
                if (NoWriteMode)
                    return;

                WriteLine(LOG_IDENT, $"Failed to initialize because Bloxstrap cannot write to {directory}");

                Frontend.ShowMessageBox(
                    String.Format(Strings.Logger_NoWriteMode, directory), 
                    System.Windows.MessageBoxImage.Warning, 
                    System.Windows.MessageBoxButton.OK
                );

                NoWriteMode = true;

                return;
            }
            

            Initialized = true;

            if (History.Count > 0)
                WriteToLog(string.Join("\r\n", History));

            WriteLine(LOG_IDENT, "Finished initializing!");

            FileLocation = location;

            // delete older logs if there are more than 15
            if (Paths.Initialized && Directory.Exists(Paths.Logs))
            {
                const int maxLogs = 15;
                FileInfo[] logs = new DirectoryInfo(Paths.Logs).GetFiles();

                if (logs.Length <= maxLogs)
                    return;

                foreach (FileInfo log in logs.OrderByDescending(log => log.LastWriteTimeUtc).Skip(maxLogs))
                {
                    WriteLine(LOG_IDENT, $"Cleaning up old log file '{log.Name}'");

                    try
                    {
                       log.Delete();
                    }
                    catch (Exception ex)
                    {
                        WriteLine(LOG_IDENT, "Failed to delete log!");
                        WriteException(LOG_IDENT, ex);
                    }
                }
            }
        }

        private void WriteLine(string message)
        {
            string timestamp = DateTime.UtcNow.ToString("s") + "Z";
            string outcon = $"{timestamp} {message}";
            string outlog = outcon.Replace(Paths.UserProfile, "%UserProfile%", StringComparison.InvariantCultureIgnoreCase);

            Debug.WriteLine(outcon);
            WriteToLog(outlog);

            History.Add(outlog);
        }

        public void WriteLine(string identifier, string message) => WriteLine($"[{identifier}] {message}");

        public void WriteException(string identifier, Exception ex)
        {
            CultureInfo originalCulture = Thread.CurrentThread.CurrentUICulture;

            try
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

                string hresult = "0x" + ex.HResult.ToString("X8");
                WriteLine($"[{identifier}] ({hresult}) {ex}");
            }
            finally
            {
                Thread.CurrentThread.CurrentUICulture = originalCulture;
            }
        }

        private void WriteToLog(string message)
        {
            // Do not use async void here: an I/O failure must not become an unhandled
            // exception on a thread-pool continuation.
            _ = WriteToLogAsync(message);
        }

        private async Task WriteToLogAsync(string message)
        {
            if (!Initialized)
                return;

            bool lockAcquired = false;

            try
            {
                await _semaphore.WaitAsync().ConfigureAwait(false);
                lockAcquired = true;

                FileStream? stream = _filestream;
                if (stream is null)
                    return;

                byte[] bytes = Encoding.UTF8.GetBytes($"{message}\r\n");
                await stream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Logging is best-effort and must never crash the launcher.
                Debug.WriteLine($"Logger::WriteToLogAsync failed: {ex}");
            }
            finally
            {
                if (lockAcquired)
                    _semaphore.Release();
            }
        }
    }
}
