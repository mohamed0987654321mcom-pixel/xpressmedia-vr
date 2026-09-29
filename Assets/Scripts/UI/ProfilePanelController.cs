using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace XpressMediaVR
{
    /// World-space panel port of ProfileView.swift — stats, bio, and a
    /// scrolling grid of the user's videos. Functional, but hasn't had the
    /// Feed's full spatial (curved-wall) treatment yet — see the VR README's
    /// "What's built vs what's stubbed".
    public class ProfilePanelController : MonoBehaviour
    {
        public TMP_Text displayNameText;
        public TMP_Text handleText;
        public TMP_Text bioText;
        public TMP_Text followerCountText;
        public TMP_Text followingCountText;
        public TMP_Text likesCountText;
        public RawImage avatarImage;
        public Transform videoGridContent;      // a GridLayoutGroup container
        public GameObject videoThumbnailPrefab;  // a prefab with a RawImage + TMP_Text

        public async void Show(string idOrHandle)
        {
            XMUser user;
            List<VideoPost> videos;
            try
            {
                user = await APIClient.Shared.FetchUser(idOrHandle);
                videos = await APIClient.Shared.FetchUserVideos(idOrHandle);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Profile] load failed: {e.Message}");
                return;
            }

            displayNameText.text = user.DisplayName;
            handleText.text = "@" + user.Handle;
            bioText.text = user.Bio;
            followerCountText.text = FormatCount(user.FollowerCount);
            followingCountText.text = FormatCount(user.FollowingCount);
            likesCountText.text = FormatCount(user.LikesReceived);
            if (!string.IsNullOrEmpty(user.AvatarUrl))
                StartCoroutine(ImageLoader.LoadInto(user.AvatarUrl, avatarImage));

            foreach (Transform child in videoGridContent) Destroy(child.gameObject);
            foreach (var video in videos)
            {
                var row = Instantiate(videoThumbnailPrefab, videoGridContent);
                var label = row.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = FormatCount(video.ViewCount) + " views";
                var image = row.GetComponentInChildren<RawImage>();
                if (image != null && !string.IsNullOrEmpty(video.ThumbnailUrl))
                    StartCoroutine(ImageLoader.LoadInto(video.ThumbnailUrl, image));
            }
        }

        private static string FormatCount(int n)
        {
            if (n >= 1_000_000) return (n / 1_000_000f).ToString("0.#") + "M";
            if (n >= 1_000) return (n / 1_000f).ToString("0.#") + "K";
            return n.ToString();
        }
    }
}
