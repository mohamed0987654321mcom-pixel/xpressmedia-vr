using UnityEngine;
#if !OVR_SDK_PRESENT && !META_XR_SDK && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace XpressMediaVR
{
    /// Thumbstick-flick navigation for the Feed. OVRInput comes from the
    /// Meta XR Core SDK; once installed and OVR_SDK_PRESENT (or META_XR_SDK)
    /// is defined under Project Settings > Player > Scripting Define
    /// Symbols, this compiles against real thumbstick input. Until then it
    /// falls back to arrow keys in the Editor, using whichever input
    /// backend this project is actually configured for (Project Settings >
    /// Player > Active Input Handling) so it never throws reading the
    /// wrong one.
    public class FeedInput : MonoBehaviour
    {
        public FeedController feed;
        [Range(0.3f, 1f)] public float flickThreshold = 0.7f;
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
#elif ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.downArrowKey.wasPressedThisFrame) { feed.Next(); _cooldownTimer = flickCooldown; }
            else if (keyboard.upArrowKey.wasPressedThisFrame) { feed.Previous(); _cooldownTimer = flickCooldown; }
#else
            if (Input.GetKeyDown(KeyCode.DownArrow)) { feed.Next(); _cooldownTimer = flickCooldown; }
            if (Input.GetKeyDown(KeyCode.UpArrow)) { feed.Previous(); _cooldownTimer = flickCooldown; }
#endif
        }
    }
}
