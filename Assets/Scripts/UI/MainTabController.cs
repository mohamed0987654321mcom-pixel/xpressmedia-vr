using UnityEngine;

namespace XpressMediaVR
{
    /// Persistent bottom tab bar — ported from MainTabView.swift's 4-tab +
    /// center-button layout (Home / Discover / [+] / Inbox / Me). Unlike a
    /// wrist-raise menu, iOS's tab bar is always-on chrome, so this stays
    /// visible the whole time rather than gating on a gesture.
    public class MainTabController : MonoBehaviour
    {
        public GameObject homePanel;
        public GameObject discoverPanel;
        public GameObject inboxPanel;
        public GameObject profilePanel;
        public ProfilePanelController profileController;

        public void ShowHome() => Activate(homePanel);
        public void ShowDiscover() => Activate(discoverPanel);
        public void ShowInbox() => Activate(inboxPanel);

        public void ShowProfile()
        {
            Activate(profilePanel);
            profileController?.ShowMe();
        }

        public void ShowProfileOf(XMUser user)
        {
            if (user == null) { ShowProfile(); return; }
            Activate(profilePanel);
            profileController?.Show(user.Handle);
        }

        public void TapRecord() => Debug.Log("[MainTab] record tapped (not implemented yet)");

        private void Activate(GameObject target)
        {
            if (target == null) return;
            foreach (Transform sibling in target.transform.parent)
                sibling.gameObject.SetActive(sibling.gameObject == target);
        }
    }
}
