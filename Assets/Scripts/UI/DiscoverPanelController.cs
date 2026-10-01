using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace XpressMediaVR
{
    /// World-space port of DiscoverView.swift's user-search half.
    public class DiscoverPanelController : MonoBehaviour
    {
        public TMP_InputField searchField;
        public Transform resultsContent;
        public GameObject userRowPrefab;

        private void Awake() => searchField.onEndEdit.AddListener(_ => Search());

        public async void Search()
        {
            var query = searchField.text;
            if (string.IsNullOrWhiteSpace(query)) return;

            List<XMUser> users;
            try { users = await APIClient.Shared.SearchUsers(query); }
            catch (System.Exception e) { Debug.LogError($"[Discover] search failed: {e.Message}"); return; }

            foreach (Transform child in resultsContent) Destroy(child.gameObject);
            foreach (var user in users)
            {
                var row = Instantiate(userRowPrefab, resultsContent);
                var texts = row.GetComponentsInChildren<TMP_Text>();
                if (texts.Length > 0) texts[0].text = user.DisplayName;
                if (texts.Length > 1) texts[1].text = "@" + user.Handle;
                var image = row.GetComponentInChildren<RawImage>();
                if (image != null && !string.IsNullOrEmpty(user.AvatarUrl))
                    StartCoroutine(ImageLoader.LoadInto(user.AvatarUrl, image));
            }
        }
    }
}
