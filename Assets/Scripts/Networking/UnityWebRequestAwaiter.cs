using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace XpressMediaVR
{
    /// Makes `await someUnityWebRequest.SendWebRequest();` work — Unity's
    /// UnityWebRequestAsyncOperation isn't awaitable out of the box. Standard
    /// bridge pattern so APIClient reads like async/await, not a coroutine
    /// callback pyramid.
    public static class UnityWebRequestAwaiterExtensions
    {
        public static TaskAwaiter<UnityWebRequest> GetAwaiter(this UnityWebRequestAsyncOperation op)
        {
            var tcs = new TaskCompletionSource<UnityWebRequest>();
            op.completed += _ => tcs.TrySetResult(op.webRequest);
            if (op.isDone) tcs.TrySetResult(op.webRequest);
            return tcs.Task.GetAwaiter();
        }
    }
}
