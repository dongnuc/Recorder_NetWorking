using System.Runtime.InteropServices;

namespace Common.Helper.Kernel32API
{
    public class KeyListener : IKeyListener
    {
        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);

        public bool IsKeyPressed(int vKey)
        {
            return GetAsyncKeyState(vKey) < 0;
        }
    }
}
