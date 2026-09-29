using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace XpressMediaVR
{
    /// Drives the Feed scene: paginates /api/videos/feed, hands the current
    /// clip to VideoWallController, and exposes Next()/Previous() for
    /// whatever input you wire up (thumbstick flick, a grab-and-swipe
    /// gesture via the Meta XR Interaction SDK, a wrist-menu button — see
    /// the VR README's "Scene setup").
    public class FeedController : MonoBehaviour
    {
        public VideoWallController videoWall;
        public FeedActionRail actionRail;

        private readonly List<VideoPost> _videos = new List<VideoPost>();
        private int? _nextCursor;
        private int _index = -1;
        private bool _isLoadingMore;

        private async void Start()
        {
            await LoadMore();
            if (_videos.Count > 0) ShowIndex(0);
        }

        public async Task LoadMore()
        {
            if (_isLoadingMore) return;
            _isLoadingMore = true;
            try
            {
                var page = await APIClient.Shared.FetchFeed(_nextCursor);
                _videos.AddRange(page.Videos);
                _nextCursor = page.NextCursor;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Feed] couldn't load more: {e.Message}");
            }
            finally
            {
                _isLoadingMore = false;
            }
        }

        public async void Next()
        {
            // Prefetch a bit before actually running out, same "load more
            // near the end" pattern FeedView.swift uses.
            if (_index + 1 >= _videos.Count - 2 && _nextCursor.HasValue)
                await LoadMore();
            if (_index + 1 < _videos.Count)
                ShowIndex(_index + 1);
        }

        public void Previous()
        {
            if (_index - 1 >= 0)
                ShowIndex(_index - 1);
        }

        private async void ShowIndex(int i)
        {
            _index = i;
            var video = _videos[i];
            videoWall.Play(video);
            actionRail?.Bind(video);
            try { await APIClient.Shared.RegisterView(video.Id); }
            catch { /* view counting is best-effort, never blocks playback */ }
        }
    }
}
