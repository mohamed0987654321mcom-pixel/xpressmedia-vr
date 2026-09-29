using System.Collections.Generic;
using Newtonsoft.Json;

namespace XpressMediaVR
{
    /// Mirrors the JSON shape from `serializeUser` in the backend
    /// (backend/src/serializers.js) — same fields as CoreModels.swift's XMUser.
    public class XMUser
    {
        [JsonProperty("id")] public int Id;
        [JsonProperty("mid")] public string Mid;
        [JsonProperty("handle")] public string Handle;
        [JsonProperty("displayName")] public string DisplayName;
        [JsonProperty("bio")] public string Bio;
        [JsonProperty("avatarUrl")] public string AvatarUrl;
        [JsonProperty("appearance")] public XMAppearance Appearance;
        [JsonProperty("followerCount")] public int FollowerCount;
        [JsonProperty("followingCount")] public int FollowingCount;
        [JsonProperty("videoCount")] public int VideoCount;
        [JsonProperty("likesReceived")] public int LikesReceived;
        [JsonProperty("isFollowedByViewer")] public bool IsFollowedByViewer;
        [JsonProperty("isMe")] public bool IsMe;
        [JsonProperty("createdAt")] public string CreatedAt;
    }

    /// Mirrors MPARADISE's `profile.appearance` shape (AI.md, `/oauth/userinfo`).
    public class XMAppearance
    {
        [JsonProperty("background")] public XMBackground Background;
        [JsonProperty("ring")] public XMRing Ring;
    }

    public class XMBackground
    {
        [JsonProperty("type")] public string Type; // "color" | "gradient" | "image"
        [JsonProperty("value")] public string Value;
    }

    public class XMRing
    {
        [JsonProperty("color")] public string Color; // "#rrggbb"
        [JsonProperty("width")] public double Width;
        [JsonProperty("style")] public string Style; // "solid" | "dashed" | "double" | "none"
    }

    /// Mirrors `serializeVideo`.
    public class VideoPost
    {
        [JsonProperty("id")] public int Id;
        [JsonProperty("caption")] public string Caption;
        [JsonProperty("videoUrl")] public string VideoUrl;
        [JsonProperty("thumbnailUrl")] public string ThumbnailUrl;
        [JsonProperty("durationSeconds")] public double? DurationSeconds;
        [JsonProperty("duetOfVideoId")] public int? DuetOfVideoId;
        [JsonProperty("viewCount")] public int ViewCount;
        [JsonProperty("likeCount")] public int LikeCount;
        [JsonProperty("commentCount")] public int CommentCount;
        [JsonProperty("isLiked")] public bool IsLiked;
        [JsonProperty("owner")] public XMUser Owner;
        [JsonProperty("createdAt")] public string CreatedAt;
    }

    /// Mirrors `serializeComment`.
    public class VideoComment
    {
        [JsonProperty("id")] public int Id;
        [JsonProperty("videoId")] public int VideoId;
        [JsonProperty("body")] public string Body;
        [JsonProperty("author")] public XMUser Author;
        [JsonProperty("createdAt")] public string CreatedAt;
    }

    /// Mirrors `serializeNotification`.
    public class AppNotification
    {
        [JsonProperty("id")] public int Id;
        [JsonProperty("type")] public string Type; // "like" | "comment" | "follow"
        [JsonProperty("actor")] public XMUser Actor;
        [JsonProperty("videoId")] public int? VideoId;
        [JsonProperty("isRead")] public bool IsRead;
        [JsonProperty("createdAt")] public string CreatedAt;

        public string Summary
        {
            get
            {
                var name = Actor?.DisplayName ?? "Someone";
                switch (Type)
                {
                    case "like": return $"{name} liked your video";
                    case "comment": return $"{name} commented on your video";
                    case "follow": return $"{name} started following you";
                    default: return $"{name} interacted with your video";
                }
            }
        }
    }

    /// Mirrors `serializeMessage`.
    public class ChatMessage
    {
        [JsonProperty("id")] public int Id;
        [JsonProperty("conversationId")] public int ConversationId;
        [JsonProperty("senderId")] public int SenderId;
        [JsonProperty("body")] public string Body;
        [JsonProperty("createdAt")] public string CreatedAt;
    }

    /// One row of `GET /api/messages/conversations`.
    public class Conversation
    {
        [JsonProperty("id")] public int Id;
        [JsonProperty("otherUser")] public XMUser OtherUser;
        [JsonProperty("lastMessage")] public ChatMessage LastMessage;
    }

    // MARK: Response envelope shapes — match the backend's { "key": ... } wrappers exactly.
    public class UserWrapper { [JsonProperty("user")] public XMUser User; }
    public class UsersWrapper { [JsonProperty("users")] public List<XMUser> Users; }
    public class VideoWrapper { [JsonProperty("video")] public VideoPost Video; }
    public class VideosWrapper { [JsonProperty("videos")] public List<VideoPost> Videos; }
    public class FeedResponse { [JsonProperty("videos")] public List<VideoPost> Videos; [JsonProperty("nextCursor")] public int? NextCursor; }
    public class CommentWrapper { [JsonProperty("comment")] public VideoComment Comment; }
    public class CommentsWrapper { [JsonProperty("comments")] public List<VideoComment> Comments; }
    public class NotificationsWrapper { [JsonProperty("notifications")] public List<AppNotification> Notifications; }
    public class ConversationsWrapper { [JsonProperty("conversations")] public List<Conversation> Conversations; }
    public class MessagesWrapper { [JsonProperty("messages")] public List<ChatMessage> Messages; }
    public class MessageWrapper { [JsonProperty("message")] public ChatMessage Message; }
    public class SignInResponse { [JsonProperty("token")] public string Token; [JsonProperty("user")] public XMUser User; }
    public class ConversationStub { [JsonProperty("id")] public int Id; [JsonProperty("otherUser")] public XMUser OtherUser; }
    public class StartConversationResponse { [JsonProperty("conversation")] public ConversationStub Conversation; }
}
