using Common.Logging;
using System.Runtime.InteropServices;
using System.Text;

namespace Common.Helper.Kernel32API
{
    public class ConsolePoller : IConsolePoller
    {
        [DllImport("kernel32.dll")]
        static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool AttachConsole(uint dwProcessId);

        [DllImport("kernel32.dll")]
        static extern bool FreeConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetConsoleScreenBufferInfo(IntPtr hConsoleOutput, out CONSOLE_SCREEN_BUFFER_INFO lpConsoleScreenBufferInfo);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetConsoleScreenBufferSize(IntPtr hConsoleOutput, COORD dwSize);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool ReadConsoleOutputCharacter(IntPtr hConsoleOutput, StringBuilder lpCharacter, uint nLength, COORD dwReadCoord, out uint lpNumberOfCharsRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReleaseMutex(IntPtr hMutex);

        private readonly Dictionary<uint, bool> _firstPolls = new Dictionary<uint, bool>();

        public async Task<string> CaptureCurrentConsoleAsync(
           ChildProcess child,
           IntPtr mutex,
           bool expandBuffer = true)
        {
            if (child == null || child.hProcess == IntPtr.Zero)
            {
                throw new ArgumentException("Invalid child process handle");
            }

            LogManager.Instance.LogDebug($"📸 Capturing console snapshot for process PID: {child.processId}");

            // Wait for mutex
            WaitForSingleObject(mutex, Constants.INFINITE);

            try
            {
                // Try to attach to console
                bool attached = false;
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    if (AttachConsole(child.processId))
                    {
                        attached = true;
                        break;
                    }
                    await Task.Delay(50);
                }

                if (!attached)
                {
                    LogManager.Instance.LogWarning($"⚠️ Could not attach to console for process {child.processId}");
                    return string.Empty;
                }

                IntPtr hOut = GetStdHandle(Constants.STD_OUTPUT_HANDLE);
                if (hOut == (IntPtr)(-1))
                {
                    FreeConsole();
                    return string.Empty;
                }

                CONSOLE_SCREEN_BUFFER_INFO info;
                if (!GetConsoleScreenBufferInfo(hOut, out info))
                {
                    FreeConsole();
                    return string.Empty;
                }

                // ✅ Expand buffer size if requested (first time only)
                if (expandBuffer && !_firstPolls.ContainsKey(child.processId))
                {
                    COORD newSize = info.dwSize;
                    newSize.Y = 9999;
                    SetConsoleScreenBufferSize(hOut, newSize);
                    _firstPolls[child.processId] = true;

                    // Re-read buffer info after expansion
                    GetConsoleScreenBufferInfo(hOut, out info);

                    LogManager.Instance.LogDebug($"✅ Buffer expanded for process {child.processId}");
                }

                // ✅ Read entire console buffer
                uint length = (uint)(info.dwSize.X * info.dwSize.Y);
                StringBuilder sb = new StringBuilder((int)length);
                uint read;
                COORD coord = new COORD { X = 0, Y = 0 };

                if (!ReadConsoleOutputCharacter(hOut, sb, length, coord, out read))
                {
                    FreeConsole();
                    LogManager.Instance.LogWarning($"⚠️ Failed to read console buffer for process {child.processId}");
                    return string.Empty;
                }

                // ✅ Extract visible lines up to cursor position
                string currentBuffer = sb.ToString(0, (int)read).TrimEnd('\0');
                List<string> lines = new List<string>();

                for (short y = 0; y <= info.dwCursorPosition.Y; y++)
                {
                    int start = y * info.dwSize.X;
                    if (start >= currentBuffer.Length) break;

                    int lineLength = Math.Min(info.dwSize.X, currentBuffer.Length - start);
                    string line = currentBuffer.Substring(start, lineLength).TrimEnd();

                    //if (!string.IsNullOrEmpty(line))
                    //{
                    //    lines.Add(line);
                    //}
                    lines.Add(line);
                }

                string result = string.Join(Environment.NewLine, lines);

                FreeConsole();

                LogManager.Instance.LogInfomation($"✅ Captured {lines.Count} lines, {result.Length} characters from process {child.processId}");

                return result;
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError($"❌ Error capturing console for process {child.processId}: {ex.Message}");
                try { FreeConsole(); } catch { }
                return string.Empty;
            }
            finally
            {
                ReleaseMutex(mutex);
            }
        }

        /// <summary>
        /// ✅ SIMPLER VERSION: Capture với retry logic
        /// Tự động retry nếu capture thất bại
        /// </summary>
        public async Task<string> CaptureCurrentConsoleWithRetryAsync(
            ChildProcess child,
            IntPtr mutex,
            int maxRetries = 3,
            int retryDelayMs = 200,
            bool expandBuffer = true)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    string result = await CaptureCurrentConsoleAsync(child, mutex, expandBuffer);

                    if (!string.IsNullOrEmpty(result))
                    {
                        return result;
                    }

                    if (attempt < maxRetries)
                    {
                        LogManager.Instance.LogDebug($"⏳ Retry {attempt}/{maxRetries} for process {child.processId}");
                        await Task.Delay(retryDelayMs);
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Instance.LogError($"❌ Attempt {attempt} failed: {ex.Message}");

                    if (attempt >= maxRetries)
                    {
                        throw;
                    }

                    await Task.Delay(retryDelayMs);
                }
            }

            LogManager.Instance.LogWarning($"⚠️ All {maxRetries} attempts failed for process {child.processId}");
            return string.Empty;
        }
    }
}

