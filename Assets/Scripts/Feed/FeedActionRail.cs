using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace XpressMediaVR
{
    /// The Feed's transparent, icon-only action rail — ported from
    /// FeedVideoCell.swift's actionColumn (avatar, like, comment, save,
    /// share, mute, delete-if-owner). Deliberately has NO Follow button —
    /// the iOS action column doesn't have one either; following happens
    /// from the Profile screen.
    public class FeedActionRail : MonoBehaviour
    {
        public Button avatarButton;
        public RawImage avatarImage;
        public Button likeButton;
        public Image likeIcon;
        public TMP_Text likeCountText;
        public Button commentButton;
        public TMP_Text commentCountText;
        public Button saveButton;
        public Image saveIcon;
        public Button shareButton;
        public Button muteButton;
        public TMP_Text muteButtonLabel;
        public Button deleteButton;
        public CommentsPanelController commentsPanel;
        public VideoWallController videoWall;

        public System.Action<XMUser> OnAvatarTapped;

        private VideoPost _video;
        private bool _saved;

        private void Awake()
        {
            likeButton.onClick.AddListener(ToggleLike);
            commentButton.onClick.AddListener(OpenComments);
            if (saveButton != null) saveButton.onClick.AddListener(ToggleSave);
            if (shareButton != null) shareButton.onClick.AddListener(Share);
            if (muteButton != null) muteButton.onClick.AddListener(ToggleMute);
            if (deleteButton != null) deleteButton.onClick.AddListener(DeleteVideo);
            if (avatarButton != null) avatarButton.onClick.AddListener(() => OnAvatarTapped?.Invoke(_video?.Owner));
        }

        public void Bind(VideoPost video)
        {
            _video = video;
            _saved = false;
            Refresh();
            if (avatarImage != null && !string.IsNullOrEmpty(video.Owner?.AvatarUrl))
                StartCoroutine(ImageLoader.LoadInto(video.Owner.AvatarUrl, avatarImage));
        }

        private void Refresh()
        {
            if (_video == null) return;
            likeCountText.text = FormatCount(_video.LikeCount);
            commentCountText.text = FormatCount(_video.CommentCount);
            if (likeIcon != null) likeIcon.color = _video.IsLiked ? XM.Action : Color.white;
            if (saveIcon != null) saveIcon.color = _saved ? XM.Action : Color.white;
            if (deleteButton != null) deleteButton.gameObject.SetActive(_video.Owner != null && _video.Owner.IsMe);
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

        private void ToggleSave()
        {
            _saved = !_saved;
            Refresh();
        }

        private void Share()
        {
            Debug.Log($"[FeedActionRail] share tapped for video {_video?.Id} (not implemented yet)");
        }

        private void ToggleMute()
        {
            if (videoWall == null) return;
            var muted = videoWall.ToggleMute();
            if (muteButtonLabel != null) muteButtonLabel.text = muted ? "Unmute" : "Mute";
        }

        private async void DeleteVideo()
        {
            if (_video == null) return;
            try
            {
                await APIClient.Shared.DeleteVideo(_video.Id);
                var feed = GetComponentInParent<FeedController>();
                feed?.RemoveCurrent();
            }
            catch (System.Exception e) { Debug.LogError($"[FeedActionRail] delete failed: {e.Message}"); }
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
