using UnityEngine;
using UnityEngine.Video;

namespace XpressMediaVR
{
    /// Streams the current feed video onto the curved wall mesh via Unity's
    /// VideoPlayer -> RenderTexture pipeline. The backend already serves
    /// uploaded .mp4/.mov files as plain HTTP (see backend/src/storage.js),
    /// so this is just pointing a normal Unity VideoPlayer at a URL — no
    /// VR-specific server change needed.
    [RequireComponent(typeof(VideoPlayer))]
    public class VideoWallController : MonoBehaviour
    {
        public MeshRenderer wallRenderer;
        [Tooltip("Assign a RenderTexture asset (e.g. 1920x1080) in the Inspector.")]
        public RenderTexture renderTexture;

        private VideoPlayer _player;

        private void Awake()
        {
            _player = GetComponent<VideoPlayer>();
            _player.playOnAwake = false;
            _player.waitForFirstFrame = true;
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.targetTexture = renderTexture;
            _player.isLooping = true;
            wallRenderer.material.mainTexture = renderTexture;
        }

        public void Play(VideoPost video)
        {
            _player.url = video.VideoUrl;
            _player.prepareCompleted -= OnPrepared;
            _player.prepareCompleted += OnPrepared;
            _player.Prepare();
        }

        private void OnPrepared(VideoPlayer source) => source.Play();

        public void Pause() => _player.Pause();
        public void Resume() => _player.Play();
        public void Stop() => _player.Stop();
    }
}
