using System;
using System.Threading;

namespace SCZip.Core
{
    /// <summary>
    /// Detects accidental main-thread blocking work (Process, mdfind, heavy IO).
    /// </summary>
    public static class MainThreadGuard
    {
        private static int _mainThreadId = -1;

        public static void CaptureMainThread()
        {
            if (_mainThreadId < 0)
                _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        public static bool IsMainThread =>
            _mainThreadId >= 0 && Thread.CurrentThread.ManagedThreadId == _mainThreadId;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static void AssertBackgroundThread(string operation)
        {
            CaptureMainThread();
            if (!IsMainThread)
                return;

            throw new InvalidOperationException(
                $"{operation} must not run on the Unity main thread — it freezes UI input (AppBar buttons stop responding). " +
                "Load on a background thread via Task.Run and marshal results back with Notify().");
        }
#endif
    }
}
