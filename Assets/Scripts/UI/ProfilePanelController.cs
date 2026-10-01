using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace XpressMediaVR
{
    /// World-space panel port of ProfileView.swift — banner, avatar, stats,
    /// bio, Edit-profile button, and a scrolling grid of the user's videos.
    public class ProfilePanelController : MonoBehaviour
    {
        public TMP_Text displayNameText;
        public TMP_Text handleText;
        public TMP_Text bioText;
        public TMP_Text followerCountText;
        public TMP_Text followingCountText;
        public TMP_Text likesCountText;
        public RawImage avatarImage;
        public Button editProfileButton;
        public Transform videoGridContent;
        public GameObject videoThumbnailPrefab;

        private void Awake()
        {
            if (editProfileButton != null)
                editProfileButton.onClick.AddListener(() =>
                    Debug.Log("[Profile] edit profile tapped (not implemented yet)"));
        }

        public void ShowMe()
        {
            var me = SessionStore.Instance != null ? SessionStore.Instance.CurrentUser : null;
            if (me != null) Show(me.Handle);
        }

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
            if (editProfileButton != null) editProfileButton.gameObject.SetActive(user.IsMe);
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
