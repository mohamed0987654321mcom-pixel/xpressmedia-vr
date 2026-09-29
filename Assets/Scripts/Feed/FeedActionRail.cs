using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace XpressMediaVR
{
    /// The Feed's like/comment/follow rail, as a small world-space panel
    /// anchored beside the video wall (see the VR README's "Scene setup" for
    /// the exact hierarchy) rather than FeedVideoCell.swift's screen-edge
    /// overlay — VR has no screen edge to pin to, so it floats in space next
    /// to the wall instead.
    public class FeedActionRail : MonoBehaviour
    {
        public Button likeButton;
        public Image likeIcon;
        public TMP_Text likeCountText;
        public Button commentButton;
        public TMP_Text commentCountText;
        public Button followButton;
        public TMP_Text followButtonLabel;
        public CommentsPanelController commentsPanel;

        private VideoPost _video;

        private void Awake()
        {
            likeButton.onClick.AddListener(ToggleLike);
            followButton.onClick.AddListener(ToggleFollow);
            commentButton.onClick.AddListener(OpenComments);
        }

        public void Bind(VideoPost video)
        {
            _video = video;
            Refresh();
        }

        private void Refresh()
        {
            if (_video == null) return;
            likeCountText.text = FormatCount(_video.LikeCount);
            commentCountText.text = FormatCount(_video.CommentCount);
            if (likeIcon != null) likeIcon.color = _video.IsLiked ? XM.Action : Color.white;
            if (_video.Owner != null && followButtonLabel != null)
                followButtonLabel.text = _video.Owner.IsFollowedByViewer ? "Following" : "Follow";
        }

        private async void ToggleLike()
        {
            if (_video == null) return;
            try
            {
                _video = _video.IsLiked
                    ? await APIClient.Shared.UnlikeVideo(_video.Id)
                    : await APIClient.Shared.LikeVideo(_video.Id);
                Refresh();
            }
            catch (System.Exception e) { Debug.LogError($"[FeedActionRail] like failed: {e.Message}"); }
        }

        private async void ToggleFollow()
        {
            if (_video?.Owner == null) return;
            try
            {
                var updated = _video.Owner.IsFollowedByViewer
                    ? await APIClient.Shared.Unfollow(_video.Owner.Handle)
                    : await APIClient.Shared.Follow(_video.Owner.Handle);
                _video.Owner = updated;
                Refresh();
            }
            catch (System.Exception e) { Debug.LogError($"[FeedActionRail] follow failed: {e.Message}"); }
        }

        private void OpenComments()
        {
            if (_video != null) commentsPanel?.Show(_video.Id);
        }

        private static string FormatCount(int n)
        {
            if (n >= 1_000_000) return (n / 1_000_000f).ToString("0.#") + "M";
            if (n >= 1_000) return (n / 1_000f).ToString("0.#") + "K";
            return n.ToString();
        }
    }
}
