using UnityEngine;
using UnityEngine.Video;

namespace XpressMediaVR
{
    /// Streams the current feed video onto the curved wall mesh via Unity's
    /// VideoPlayer -> RenderTexture pipeline.
    [RequireComponent(typeof(VideoPlayer))]
    public class VideoWallController : MonoBehaviour
    {
        public MeshRenderer wallRenderer;
        [Tooltip("Assign a RenderTexture asset (e.g. 1920x1080) in the Inspector.")]
        public RenderTexture renderTexture;

        private VideoPlayer _player;
        private bool _muted;

        private void Awake()
        {
            _player = GetComponent<VideoPlayer>();
            _player.playOnAwake = false;
            _player.waitForFirstFrame = true;
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.targetTexture = renderTexture;
            _player.isLooping = true;
            _player.audioOutputMode = VideoAudioOutputMode.Direct;
            wallRenderer.material.mainTexture = renderTexture;
        }

        public void Play(VideoPost video)
        {
            _player.url = video.VideoUrl;
            _player.prepareCompleted -= OnPrepared;
            _player.prepareCompleted += OnPrepared;
            _player.SetDirectAudioMute(0, _muted);
            _player.Prepare();
        }

        private void OnPrepared(VideoPlayer source) => source.Play();

        public void Pause() => _player.Pause();
        public void Resume() => _player.Play();
        public void Stop() => _player.Stop();

        public bool ToggleMute()
        {
            _muted = !_muted;
            _player.SetDirectAudioMute(0, _muted);
            return _muted;
        }
    }
}
