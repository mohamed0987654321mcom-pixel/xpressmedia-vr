using UnityEngine;

namespace XpressMediaVR
{
    /// Thumbstick-flick navigation for the Feed — flick up/down on either
    /// controller's thumbstick to advance/go back, the simplest input to
    /// wire up first. `OVRInput` comes from the Meta XR Core SDK (see the VR
    /// README's setup steps); once that package is installed this compiles
    /// and works as-is. Swap/extend this for a hand-tracking grab-swipe via
    /// the Meta XR Interaction SDK's grab interactables later — Next()/
    /// Previous() on FeedController are the two calls anything needs to make.
    public class FeedInput : MonoBehaviour
    {
        public FeedController feed;
        [Tooltip("How far the thumbstick has to travel before it counts as a flick.")]
        [Range(0.3f, 1f)] public float flickThreshold = 0.7f;
        [Tooltip("Seconds to wait before another flick can register, so one flick doesn't fire twice.")]
        public float flickCooldown = 0.35f;

        private float _cooldownTimer;

        private void Update()
        {
            if (_cooldownTimer > 0f)
            {
                _cooldownTimer -= Time.deltaTime;
                return;
            }

#if OVR_SDK_PRESENT || META_XR_SDK
            var stick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
            if (Mathf.Abs(stick.y) < flickThreshold) return;

            if (stick.y > 0f) feed.Previous();
            else feed.Next();

            _cooldownTimer = flickCooldown;
#else
            // Meta XR Core SDK not installed yet — fall back to arrow keys /
            // gamepad D-pad so this still runs in the Unity Editor for quick
            // iteration without a headset attached. Define OVR_SDK_PRESENT
            // (Project Settings > Player > Scripting Define Symbols) once the
            // Meta XR Core SDK is installed to switch to real thumbstick input.
            if (Input.GetKeyDown(KeyCode.DownArrow)) { feed.Next(); _cooldownTimer = flickCooldown; }
            if (Input.GetKeyDown(KeyCode.UpArrow)) { feed.Previous(); _cooldownTimer = flickCooldown; }
#endif
        }
    }
}
