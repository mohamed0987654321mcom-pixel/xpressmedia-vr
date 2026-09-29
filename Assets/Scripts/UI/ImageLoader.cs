using System.Collections;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace XpressMediaVR
{
    /// Tiny shared helper for pulling an avatar/thumbnail URL onto a
    /// RawImage — every panel that shows a remote image (Profile, Discover,
    /// Inbox) uses this instead of duplicating a download coroutine.
    public static class ImageLoader
    {
        public static IEnumerator LoadInto(string url, RawImage target)
        {
            using var request = UnityWebRequestTexture.GetTexture(url);
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success && target != null)
                target.texture = ((DownloadHandlerTexture)request.downloadHandler).texture;
        }
    }
}
