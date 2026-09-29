using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace XpressMediaVR
{
    /// World-space port of InboxView.swift/ChatView.swift's conversation
    /// list and a thread view. Functional, not yet given the Feed's full
    /// spatial treatment — see the VR README's "What's built vs stubbed".
    public class InboxPanelController : MonoBehaviour
    {
        public Transform conversationListContent;
        public GameObject conversationRowPrefab; // a Button + two TMP_Text (name, last message)
        public Transform messageListContent;
        public GameObject messageBubblePrefab;    // one TMP_Text
        public TMP_InputField composerField;

        private int? _openConversationId;

        private async void Start()
        {
            List<Conversation> conversations;
            try { conversations = await APIClient.Shared.FetchConversations(); }
            catch (System.Exception e) { Debug.LogError($"[Inbox] load failed: {e.Message}"); return; }

            foreach (Transform child in conversationListContent) Destroy(child.gameObject);
            foreach (var convo in conversations)
            {
                var row = Instantiate(conversationRowPrefab, conversationListContent);
                var texts = row.GetComponentsInChildren<TMP_Text>();
                if (texts.Length > 0) texts[0].text = convo.OtherUser.DisplayName;
                if (texts.Length > 1) texts[1].text = convo.LastMessage?.Body ?? "";
                var id = convo.Id;
                var button = row.GetComponent<Button>();
                if (button != null) button.onClick.AddListener(() => OpenConversation(id));
            }
        }

        public async void OpenConversation(int conversationId)
        {
            _openConversationId = conversationId;
            List<ChatMessage> messages;
            try { messages = await APIClient.Shared.FetchMessages(conversationId); }
            catch (System.Exception e) { Debug.LogError($"[Inbox] messages failed: {e.Message}"); return; }

            foreach (Transform child in messageListContent) Destroy(child.gameObject);
            foreach (var message in messages)
            {
                var bubble = Instantiate(messageBubblePrefab, messageListContent);
                var text = bubble.GetComponentInChildren<TMP_Text>();
                if (text != null) text.text = message.Body;
            }
        }

        public async void SendComposedMessage()
        {
            if (_openConversationId == null || string.IsNullOrWhiteSpace(composerField.text)) return;
            try
            {
                await APIClient.Shared.SendMessage(_openConversationId.Value, composerField.text);
                composerField.text = "";
                OpenConversation(_openConversationId.Value); // simplest refresh: re-fetch the thread
            }
            catch (System.Exception e) { Debug.LogError($"[Inbox] send failed: {e.Message}"); }
        }
    }
}
