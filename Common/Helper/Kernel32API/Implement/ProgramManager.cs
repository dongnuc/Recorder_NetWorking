using Common.Helper.Kernel32API.Implement;
using Common.Helper.Kernel32API.Interface;

namespace Common.Helper.Kernel32API
{
    public class ProgramManager
    {
        private readonly IProcessStarter _processStarter;
        private readonly IConsolePoller _consolePoller;
        private readonly IKeyListener _keyListener;
        private readonly IMutexManager _mutexManager;
        private readonly IConsoleManager _consoleManager;
        private readonly IProcessWaiter _processWaiter;

        public ProgramManager()
        {
            _processStarter = new ProcessStarter();
            _consolePoller = new ConsolePoller();
            _keyListener = new KeyListener();
            _mutexManager = new MutexManager();
            _consoleManager = new ConsoleManager();
            _processWaiter = new ProcessWaiter();
        }

        public (string log1, string log2) Run(string exe1, string exe2, bool showConsoleMessages = true)
        {
            if (!File.Exists(exe1) || !File.Exists(exe2))
            {
                if (showConsoleMessages)
                {
                    _consoleManager.Alloc();
                    Console.WriteLine("Một hoặc cả hai đường dẫn EXE không hợp lệ.");
                    _consoleManager.Free();
                }
                return (string.Empty, string.Empty);
            }

            IntPtr mutex = _mutexManager.Create("Global\\ConsoleAttachMutex");

            ChildProcess child1 = _processStarter.Start(exe1, "Proc1");
            ChildProcess child2 = _processStarter.Start(exe2, "Proc2");

            Thread.Sleep(2000); // Chờ console khởi tạo

            if (showConsoleMessages)
            {
                _consoleManager.Alloc();
                Console.WriteLine("Đã khởi động cả hai ứng dụng console với cửa sổ riêng.");
                Console.WriteLine("Tương tác trực tiếp với chúng trong các cửa sổ console tương ứng.");
                Console.WriteLine("Nội dung buffer console (bao gồm đầu vào và đầu ra) đang được ghi.");
                Console.WriteLine("Nhấn phím F12 để gửi Ctrl+C đến cả hai tiến trình và thoát. Cửa sổ console chính sẽ đóng sau thông báo này.");
            }

            _consoleManager.Free();

            Task<string> pollTask1 = Task.Run(() => _consolePoller.PollAsync(child1, mutex));
            Task<string> pollTask2 = Task.Run(() => _consolePoller.PollAsync(child2, mutex));

            while (true)
            {
                if (_keyListener.IsKeyPressed(0x7B)) // VK_F12
                {
                    break;
                }
                Thread.Sleep(100);
            }

            _consoleManager.SendCtrlC(child1.processId, mutex, _mutexManager);
            _consoleManager.SendCtrlC(child2.processId, mutex, _mutexManager);

            _processWaiter.WaitForProcess(child1.hProcess);
            _processWaiter.WaitForProcess(child2.hProcess);

            string log1 = pollTask1.Result;
            string log2 = pollTask2.Result;

            if (showConsoleMessages)
            {
                _consoleManager.Alloc();
                Console.WriteLine("Cả hai tiến trình đã thoát.");
                Console.WriteLine("Nhấn Enter để đóng.");
                Console.ReadLine();
            }

            _consoleManager.CloseHandles(child1);
            _consoleManager.CloseHandles(child2);
            _mutexManager.Close(mutex);

            return (log1, log2);
        }
        public (ChildProcess child, IntPtr mutex) StartSingle(string exePath, string name, bool showConsoleMessages = true)
        {
            if (!File.Exists(exePath))
            {
                if (showConsoleMessages)
                {
                    _consoleManager.Alloc();
                    Console.WriteLine("Đường dẫn EXE không hợp lệ.");
                    _consoleManager.Free();
                }
                throw new Exception("Đường dẫn EXE không hợp lệ.");
            }

            IntPtr mutex = _mutexManager.Create($"Global\\ConsoleAttachMutex_{name}");

            ChildProcess child = _processStarter.Start(exePath, name);

            Thread.Sleep(2000); // Chờ console khởi tạo

            if (showConsoleMessages)
            {
                _consoleManager.Alloc();
                Console.WriteLine($"Đã khởi động ứng dụng console {name} với cửa sổ riêng.");
                Console.WriteLine("Tương tác trực tiếp với nó trong cửa sổ console tương ứng.");
                _consoleManager.Free();
            }

            return (child, mutex);
        }

        // Method mới: Bắt đầu polling độc lập cho một child process, trả về Task<string> log
        public Task<string> PollSingleAsync(ChildProcess child, IntPtr mutex)
        {
            return Task.Run(() => _consolePoller.PollAsync(child, mutex));
        }

        // Method mới: Close độc lập một child process
        public void CloseSingle(ChildProcess child, IntPtr mutex, bool showConsoleMessages = true)
        {
            _consoleManager.SendCtrlC(child.processId, mutex, _mutexManager);

            _processWaiter.WaitForProcess(child.hProcess);

            _consoleManager.CloseHandles(child);
            _mutexManager.Close(mutex);

            if (showConsoleMessages)
            {
                _consoleManager.Alloc();
                Console.WriteLine($"Tiến trình {child.name} đã thoát.");
                _consoleManager.Free();
            }
        }
        public async Task<string> RunSingle(string exePath, string name, bool showConsoleMessages = true)
        {
            var (child, mutex) = StartSingle(exePath, name, showConsoleMessages);

            _consoleManager.Free();

            Task<string> pollTask = PollSingleAsync(child, mutex);

            while (true)
            {
                if (_keyListener.IsKeyPressed(0x7B)) // VK_F12
                {
                    break;
                }
                Thread.Sleep(100);
            }

            CloseSingle(child, mutex, showConsoleMessages);

            string log = await pollTask;

            if (showConsoleMessages)
            {
                _consoleManager.Alloc();
                Console.WriteLine("Nhấn Enter để đóng.");
                Console.ReadLine();
            }

            return log;
        }
    }
}
