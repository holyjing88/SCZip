using System;
using System.Collections.Generic;
using UnityEngine;

namespace SCZip.Core
{
    public sealed class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static UnityMainThreadDispatcher _instance;
        private readonly Queue<Action> _queue = new();
        private readonly object _lock = new();

        public static UnityMainThreadDispatcher Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var go = new GameObject(nameof(UnityMainThreadDispatcher));
                _instance = go.AddComponent<UnityMainThreadDispatcher>();
                DontDestroyOnLoad(go);
                return _instance;
            }
        }

        private void Awake()
        {
            MainThreadGuard.CaptureMainThread();

            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void Enqueue(Action action)
        {
            lock (_lock) { _queue.Enqueue(action); }
        }

        public static void FlushPending()
        {
            _instance?.DrainQueue();
        }

        private void Update() => DrainQueue();

        private void DrainQueue()
        {
            while (true)
            {
                Action action;
                lock (_lock)
                {
                    if (_queue.Count == 0) break;
                    action = _queue.Dequeue();
                }

                try { action?.Invoke(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }
    }
}
