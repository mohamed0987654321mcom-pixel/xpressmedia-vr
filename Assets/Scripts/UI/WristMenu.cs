using UnityEngine;

namespace XpressMediaVR
{
    /// A wrist-anchored menu — the standard Quest-native navigation pattern.
    /// Parent this to the left hand/controller anchor from the Meta XR rig
    /// (see the VR README's "Scene setup"); it shows itself when the wrist
    /// turns up toward the headset, the same "raise to check" gesture most
    /// Quest apps use for their main menu instead of a screen-edge tab bar.
    public class WristMenu : MonoBehaviour
    {
        public Transform headTransform;   // CenterEyeAnchor from the Meta XR rig
        public Transform wristTransform;  // this object's own anchor (left hand/controller)
        public GameObject panelRoot;      // the menu Canvas, toggled on/off
        [Range(0f, 90f)] public float raiseAngleThreshold = 35f;

        private void Update()
        {
            if (headTransform == null || wristTransform == null || panelRoot == null) return;
            var toHead = (headTransform.position - wristTransform.position).normalized;
            var angle = Vector3.Angle(wristTransform.up, toHead);
            panelRoot.SetActive(angle < raiseAngleThreshold);
        }

        /// Wire each nav button's OnClick to this with the target panel —
        /// simple single-active-panel switch among siblings under the same parent.
        public void NavigateTo(GameObject targetPanel)
        {
            foreach (Transform sibling in targetPanel.transform.parent)
                sibling.gameObject.SetActive(sibling.gameObject == targetPanel);
        }
    }
}
