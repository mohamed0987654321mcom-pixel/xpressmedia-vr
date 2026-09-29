using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace XpressMediaVR
{
    /// World-space port of CommentsSheetView.swift — opened from the Feed's
    /// action rail (FeedActionRail already calls Show(video.Id) for you).
    public class CommentsPanelController : MonoBehaviour
    {
        public Transform commentListContent;
        public GameObject commentRowPrefab; // two TMP_Text (author, body)
        public TMP_InputField composerField;
        public GameObject panelRoot;

        private int _videoId;

        public async void Show(int videoId)
        {
            _videoId = videoId;
            panelRoot.SetActive(true);
            await Refresh();
        }

        public void Hide() => panelRoot.SetActive(false);

        private async Task Refresh()
        {
            List<VideoComment> comments;
            try { comments = await APIClient.Shared.FetchComments(_videoId); }
            catch (System.Exception e) { Debug.LogError($"[Comments] load failed: {e.Message}"); return; }

            foreach (Transform child in commentListContent) Destroy(child.gameObject);
            foreach (var comment in comments)
            {
                var row = Instantiate(commentRowPrefab, commentListContent);
                var texts = row.GetComponentsInChildren<TMP_Text>();
                if (texts.Length > 0) texts[0].text = comment.Author?.DisplayName ?? "Someone";
                if (texts.Length > 1) texts[1].text = comment.Body;
            }
        }

        public async void PostComposedComment()
        {
            if (string.IsNullOrWhiteSpace(composerField.text)) return;
            try
            {
                await APIClient.Shared.PostComment(_videoId, composerField.text);
                composerField.text = "";
                await Refresh();
            }
            catch (System.Exception e) { Debug.LogError($"[Comments] post failed: {e.Message}"); }
        }
    }
}
