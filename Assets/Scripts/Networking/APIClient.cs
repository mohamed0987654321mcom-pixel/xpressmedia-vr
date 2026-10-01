using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine.Networking;

namespace XpressMediaVR
{
    public class ApiException : Exception
    {
        public readonly int StatusCode;
        public ApiException(int statusCode, string message) : base(message) { StatusCode = statusCode; }
    }

    /// Thin async wrapper around the XpressMedia backend REST API — the same
    /// endpoints XpressMediaApp/Networking/APIClient.swift calls. The Quest
    /// client is just another consumer of the same backend.
    public class APIClient
    {
        public static readonly APIClient Shared = new APIClient();
        private APIClient() { }

        /// Set by SessionStore after a successful sign-in.
        public string AuthToken;

        private static string Url(string path) => Config.BackendBaseURL.TrimEnd('/') + path;

        private UnityWebRequest MakeRequest(string path, string method, Dictionary<string, string> query = null)
        {
            var url = Url(path);
            if (query != null && query.Count > 0)
            {
                var pairs = new List<string>();
                foreach (var kv in query)
                    pairs.Add($"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}");
                url += "?" + string.Join("&", pairs);
            }
            var request = new UnityWebRequest(url, method) { downloadHandler = new DownloadHandlerBuffer() };
            if (!string.IsNullOrEmpty(AuthToken))
                request.SetRequestHeader("Authorization", "Bearer " + AuthToken);
            return request;
        }

        private async Task<UnityWebRequest> Send(UnityWebRequest request)
        {
            await request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success || request.responseCode >= 400)
            {
                var message = $"Request failed ({request.responseCode})";
                try
                {
                    var errorBody = JsonConvert.DeserializeObject<Dictionary<string, string>>(request.downloadHandler.text);
                    if (errorBody != null && errorBody.TryGetValue("error", out var e)) message = e;
                }
                catch { /* body wasn't JSON — keep the generic message */ }
                throw new ApiException((int)request.responseCode, message);
            }
            return request;
        }

        private async Task<TResponse> Json<TResponse>(string path, string method = "GET", Dictionary<string, string> query = null)
        {
            using var request = MakeRequest(path, method, query);
            var done = await Send(request);
            return JsonConvert.DeserializeObject<TResponse>(done.downloadHandler.text);
        }

        private async Task<TResponse> Json<TBody, TResponse>(string path, string method, TBody body)
        {
            using var request = MakeRequest(path, method);
            var json = JsonConvert.SerializeObject(body);
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.SetRequestHeader("Content-Type", "application/json");
            var done = await Send(request);
            return JsonConvert.DeserializeObject<TResponse>(done.downloadHandler.text);
        }

        private async Task NoContent(string path, string method)
        {
            using var request = MakeRequest(path, method);
            await Send(request);
        }

        // MARK: Auth

        public Task<SignInResponse> SignIn(string mparadiseAccessToken) =>
            Json<Dictionary<string, string>, SignInResponse>("/api/auth/mparadise", "POST",
                new Dictionary<string, string> { ["access_token"] = mparadiseAccessToken });

        public async Task<XMUser> FetchMe() => (await Json<UserWrapper>("/api/auth/me")).User;

        // MARK: Users

        public async Task<XMUser> FetchUser(string idOrHandle) => (await Json<UserWrapper>($"/api/users/{idOrHandle}")).User;

        public async Task<List<VideoPost>> FetchUserVideos(string idOrHandle) =>
            (await Json<VideosWrapper>($"/api/users/{idOrHandle}/videos")).Videos;

        public async Task<List<XMUser>> SearchUsers(string query) =>
            (await Json<UsersWrapper>("/api/users/search", "GET", new Dictionary<string, string> { ["q"] = query })).Users;

        public async Task<XMUser> UpdateProfile(string displayName, string bio, string handle)
        {
            var body = new Dictionary<string, string>();
            if (displayName != null) body["displayName"] = displayName;
            if (bio != null) body["bio"] = bio;
            if (handle != null) body["handle"] = handle;
            return (await Json<Dictionary<string, string>, UserWrapper>("/api/users/me", "PATCH", body)).User;
        }

        public async Task<XMUser> Follow(string idOrHandle) =>
            (await Json<object, UserWrapper>($"/api/users/{idOrHandle}/follow", "POST", new object())).User;

        public async Task<XMUser> Unfollow(string idOrHandle) =>
            (await Json<UserWrapper>($"/api/users/{idOrHandle}/follow", "DELETE")).User;

        public async Task<List<XMUser>> Followers(string idOrHandle) =>
            (await Json<UsersWrapper>($"/api/users/{idOrHandle}/followers")).Users;

        public async Task<List<XMUser>> Following(string idOrHandle) =>
            (await Json<UsersWrapper>($"/api/users/{idOrHandle}/following")).Users;

        // MARK: Feed / videos

        public Task<FeedResponse> FetchFeed(int? cursor = null)
        {
            var query = new Dictionary<string, string>();
            if (cursor.HasValue) query["cursor"] = cursor.Value.ToString();
            return Json<FeedResponse>("/api/videos/feed", "GET", query);
        }

        public async Task<VideoPost> FetchVideo(int id) => (await Json<VideoWrapper>($"/api/videos/{id}")).Video;

        public Task RegisterView(int id) => NoContent($"/api/videos/{id}/view", "POST");

        public Task DeleteVideo(int id) => NoContent($"/api/videos/{id}", "DELETE");

        public async Task<VideoPost> LikeVideo(int id) =>
            (await Json<object, VideoWrapper>($"/api/videos/{id}/like", "POST", new object())).Video;

        public async Task<VideoPost> UnlikeVideo(int id) =>
            (await Json<VideoWrapper>($"/api/videos/{id}/like", "DELETE")).Video;

        /// Uploads a video file (multipart/form-data). `filePath` is a local
        /// path on the headset's filesystem.
        public async Task<VideoPost> UploadVideo(string filePath, string caption, int? duetOfVideoId = null)
        {
            if (string.IsNullOrEmpty(AuthToken)) throw new ApiException(401, "You're not signed in.");

            var form = new List<IMultipartFormSection> { new MultipartFormDataSection("caption", caption) };
            if (duetOfVideoId.HasValue)
                form.Add(new MultipartFormDataSection("duetOfVideoId", duetOfVideoId.Value.ToString()));

            var fileBytes = File.ReadAllBytes(filePath);
            form.Add(new MultipartFormFileSection("file", fileBytes, Path.GetFileName(filePath), "video/mp4"));

            using var request = UnityWebRequest.Post(Url("/api/videos"), form);
            request.SetRequestHeader("Authorization", "Bearer " + AuthToken);
            var done = await Send(request);
            return JsonConvert.DeserializeObject<VideoWrapper>(done.downloadHandler.text).Video;
        }

        // MARK: Comments

        public async Task<List<VideoComment>> FetchComments(int videoId) =>
            (await Json<CommentsWrapper>($"/api/videos/{videoId}/comments")).Comments;

        public async Task<VideoComment> PostComment(int videoId, string body) =>
            (await Json<Dictionary<string, string>, CommentWrapper>($"/api/videos/{videoId}/comments", "POST",
                new Dictionary<string, string> { ["body"] = body })).Comment;

        // MARK: Notifications

        public async Task<List<AppNotification>> FetchNotifications() =>
            (await Json<NotificationsWrapper>("/api/notifications")).Notifications;

        public Task MarkNotificationsRead() => NoContent("/api/notifications/read-all", "POST");

        // MARK: Messages

        public async Task<List<Conversation>> FetchConversations() =>
            (await Json<ConversationsWrapper>("/api/messages/conversations")).Conversations;

        public async Task<ConversationStub> StartConversation(int userId) =>
            (await Json<Dictionary<string, int>, StartConversationResponse>("/api/messages/conversations", "POST",
                new Dictionary<string, int> { ["userId"] = userId })).Conversation;

        public async Task<List<ChatMessage>> FetchMessages(int conversationId) =>
            (await Json<MessagesWrapper>($"/api/messages/conversations/{conversationId}/messages")).Messages;

        public async Task<ChatMessage> SendMessage(int conversationId, string body) =>
            (await Json<Dictionary<string, string>, MessageWrapper>($"/api/messages/conversations/{conversationId}/messages", "POST",
                new Dictionary<string, string> { ["body"] = body })).Message;
    }
}
