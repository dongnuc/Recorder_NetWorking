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

        const int STD_OUTPUT_HANDLE = -11;
        const uint INFINITE = 0xFFFFFFFF;
        const uint WAIT_TIMEOUT = 0x102;

        private readonly Dictionary<uint, string> _previousBuffers = new Dictionary<uint, string>();
        private readonly Dictionary<uint, bool> _firstPolls = new Dictionary<uint, bool>();

        public async Task<string> PollAsync(ChildProcess child, IntPtr mutex)
        {
            StringBuilder logBuilder = new StringBuilder();
            string previousBuffer = "";
            bool first = true;
            while (WaitForSingleObject(child.hProcess, 0) != 0x80)
            {
                WaitForSingleObject(mutex, INFINITE);
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
                    ReleaseMutex(mutex);
                    await Task.Delay(100);
                    continue;
                }
                IntPtr hOut = GetStdHandle(STD_OUTPUT_HANDLE);
                if (hOut != (IntPtr)(-1))
                {
                    CONSOLE_SCREEN_BUFFER_INFO info;
                    if (GetConsoleScreenBufferInfo(hOut, out info))
                    {
                        if (first)
                        {
                            COORD newSize = info.dwSize;
                            newSize.Y = 9999;
                            SetConsoleScreenBufferSize(hOut, newSize);
                            first = false;
                            GetConsoleScreenBufferInfo(hOut, out info);
                        }
                        uint length = (uint)(info.dwSize.X * info.dwSize.Y);
                        StringBuilder sb = new StringBuilder((int)length);
                        uint read;
                        COORD coord = new COORD { X = 0, Y = 0 };
                        if (ReadConsoleOutputCharacter(hOut, sb, length, coord, out read))
                        {
                            string currentBuffer = sb.ToString(0, (int)read).TrimEnd('\0');
                            List<string> currentLines = new List<string>();
                            for (short y = 0; y <= info.dwCursorPosition.Y; y++)
                            {
                                int start = y * info.dwSize.X;
                                if (start + info.dwSize.X > currentBuffer.Length) break;
                                string line = currentBuffer.Substring(start, Math.Min(info.dwSize.X, currentBuffer.Length - start)).TrimEnd();
                                if (!string.IsNullOrEmpty(line))
                                    currentLines.Add(line);
                            }
                            string processedCurrent = string.Join(Environment.NewLine, currentLines);
                            if (processedCurrent.Length > previousBuffer.Length && processedCurrent.StartsWith(previousBuffer))
                            {
                                string newText = processedCurrent.Substring(previousBuffer.Length);
                                if (!string.IsNullOrEmpty(newText))
                                {
                                    logBuilder.Append(newText);
                                }
                            }
                            else if (processedCurrent != previousBuffer)
                            {
                                logBuilder.Append(processedCurrent);
                            }
                            previousBuffer = processedCurrent;
                        }
                    }
                }
                FreeConsole();
                ReleaseMutex(mutex);
                await Task.Delay(100);
            }
            return logBuilder.ToString();
        }

        /// <summary>
        /// ✅ NEW: Poll console buffer ONCE and return new output immediately (non-blocking)
        /// This is for continuous real-time monitoring
        /// </summary>
        public async Task<string> PollOnceAsync(ChildProcess child, IntPtr mutex)
        {
            // Check if process is still alive
            uint waitResult = WaitForSingleObject(child.hProcess, 0);
            if (waitResult == 0x80) // Process terminated
            {
                return string.Empty;
            }

            // Wait for mutex
            WaitForSingleObject(mutex, INFINITE);

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
                    return string.Empty;
                }

                IntPtr hOut = GetStdHandle(STD_OUTPUT_HANDLE);
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

                // Initialize buffer tracking for this process
                if (!_firstPolls.ContainsKey(child.processId))
                {
                    _firstPolls[child.processId] = true;
                    _previousBuffers[child.processId] = string.Empty;
                }

                // On first poll, expand buffer size
                if (_firstPolls[child.processId])
                {
                    COORD newSize = info.dwSize;
                    newSize.Y = 9999;
                    SetConsoleScreenBufferSize(hOut, newSize);
                    _firstPolls[child.processId] = false;
                    GetConsoleScreenBufferInfo(hOut, out info);
                }

                // Read console buffer
                uint length = (uint)(info.dwSize.X * info.dwSize.Y);
                StringBuilder sb = new StringBuilder((int)length);
                uint read;
                COORD coord = new COORD { X = 0, Y = 0 };

                if (!ReadConsoleOutputCharacter(hOut, sb, length, coord, out read))
                {
                    FreeConsole();
                    return string.Empty;
                }

                // Extract visible lines up to cursor position
                string currentBuffer = sb.ToString(0, (int)read).TrimEnd('\0');
                List<string> currentLines = new List<string>();

                for (short y = 0; y <= info.dwCursorPosition.Y; y++)
                {
                    int start = y * info.dwSize.X;
                    if (start + info.dwSize.X > currentBuffer.Length) break;
                    string line = currentBuffer.Substring(start, Math.Min(info.dwSize.X, currentBuffer.Length - start)).TrimEnd();
                    if (!string.IsNullOrEmpty(line))
                        currentLines.Add(line);
                }

                string processedCurrent = string.Join(Environment.NewLine, currentLines);

                // Get previous buffer for this process
                string previousBuffer = _previousBuffers[child.processId];
                string newOutput = string.Empty;

                // Detect new output
                if (processedCurrent.Length > previousBuffer.Length && processedCurrent.StartsWith(previousBuffer))
                {
                    newOutput = processedCurrent.Substring(previousBuffer.Length);
                }
                else if (processedCurrent != previousBuffer)
                {
                    newOutput = processedCurrent; // Full buffer changed
                }

                // Update previous buffer
                _previousBuffers[child.processId] = processedCurrent;

                FreeConsole();

                return newOutput;
            }
            catch (Exception)
            {
                try { FreeConsole(); } catch { }
                return string.Empty;
            }
            finally
            {
                ReleaseMutex(mutex);
            }
        }
    }
}

