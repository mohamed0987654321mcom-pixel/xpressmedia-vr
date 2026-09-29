using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace XpressMediaVR
{
    /// Makes `await someUnityWebRequest.SendWebRequest();` work. Unity's
    /// UnityWebRequestAsyncOperation isn't awaitable out of the box — this is
    /// the standard, widely-used pattern for bridging it to C# async/await so
    /// APIClient can read like XpressMediaApp/Networking/APIClient.swift's
    /// async/await calls instead of a callback/coroutine pyramid.
    public static class UnityWebRequestAwaiterExtensions
    {
        public static TaskAwaiter<UnityWebRequest> GetAwaiter(this UnityWebRequestAsyncOperation op)
        {
            var tcs = new TaskCompletionSource<UnityWebRequest>();
            op.completed += _ => tcs.SetResult(op.webRequest);
            if (op.isDone) tcs.TrySetResult(op.webRequest); // already finished before we attached
            return tcs.Task.GetAwaiter();
        }
    }
}
