using System;
using UnityEngine;

namespace LocalLLM
{
    /// <summary>
    /// Invokes user callbacks so that an exception in game code is logged instead of
    /// corrupting the backend's state or being reported as a generation failure.
    /// </summary>
    static class SafeCallback
    {
        public static void Invoke<T>(Action<T> callback, T value)
        {
            if (callback == null) return;
            try
            {
                callback(value);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public static void Invoke(Action callback)
        {
            if (callback == null) return;
            try
            {
                callback();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
