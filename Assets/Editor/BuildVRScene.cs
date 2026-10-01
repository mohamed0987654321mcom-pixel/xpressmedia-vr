using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace XpressMediaVR.EditorTools
{
    /// Builds the XpressMedia VR scene from nothing every time it runs —
    /// written after the previous approach quietly failed: it edited
    /// whatever scene happened to be open in the Editor but never saved
    /// that scene to disk, so every time the Editor reopened it reverted to
    /// Unity's stock sample scene. This version always creates a brand new
    /// scene and explicitly saves it to Assets/Scenes/Main.unity, and
    /// registers it in Build Settings, so what you build here is what you
    /// actually see on reopen and in a real build.
    public static class BuildVRScene
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string RigPrefabPath = "Assets/VRTemplateAssets/Prefabs/Setup/Complete XR Origin Set Up Variant.prefab";
        private const string GeneratedFolder = "Assets/Generated";

        [MenuItem("XpressMedia/Build VR Scene (From Scratch)")]
        public static void BuildScene()
        {
            EnsureFolder("Assets/Scenes");
            EnsureFolder(GeneratedFolder);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildLighting();
            var rig = BuildRig();
            var uiCamera = rig != null ? FindCameraIn(rig) : null;
            if (uiCamera == null) uiCamera = BuildFallbackCamera();
            EnsureEventSystem();

            BuildSessionRoot();

            var mainPanels = new GameObject("MainPanels");
            var home = BuildHomePanel(mainPanels.transform, uiCamera);
            var discover = BuildDiscoverPanel(mainPanels.transform, uiCamera);
            var inbox = BuildInboxPanel(mainPanels.transform, uiCamera);
            var profile = BuildProfilePanel(mainPanels.transform, uiCamera);

            var tabBar = BuildTabBar(uiCamera, home, discover, inbox, profile);

            var rail = home.GetComponentInChildren<FeedActionRail>(true);
            var tabController = tabBar.GetComponent<MainTabController>();
            if (rail != null) rail.OnAvatarTapped = tabController.ShowProfileOf;

            // Home is the default tab.
            discover.SetActive(false);
            inbox.SetActive(false);
            profile.SetActive(false);
            home.SetActive(true);

            EditorSceneManager.MarkSceneDirty(scene);
            var saved = EditorSceneManager.SaveScene(scene, ScenePath);
            if (!saved)
            {
                Debug.LogError("[BuildVRScene] Failed to save the scene to " + ScenePath + ". Check the Console for the underlying error.");
                return;
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();

            Debug.Log("[BuildVRScene] Built and saved " + ScenePath + " (now the only scene in Build Settings). " +
                       "Before testing sign-in, fill in Config.MparadiseClientID and Config.BackendBaseURL " +
                       "(Assets/Scripts/App/Config.cs).");
        }

        // MARK: Rig / lighting / infrastructure

        private static void BuildLighting()
        {
            var lightGO = new GameObject("Directional Light");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.0f;
            light.shadows = LightShadows.Soft;
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        }

        private static GameObject BuildRig()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[BuildVRScene] Couldn't find the XR rig prefab at " + RigPrefabPath +
                                  " — falling back to a plain camera. VR tracking/controllers won't work until this is fixed.");
                return null;
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "XR Origin";
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            return instance;
        }

        private static Camera FindCameraIn(GameObject rig) => rig.GetComponentInChildren<Camera>(true);

        private static Camera BuildFallbackCamera()
        {
            var go = new GameObject("Main Camera");
            var cam = go.AddComponent<Camera>();
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 1.6f, 0f);
            return cam;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        private static void BuildSessionRoot()
        {
            var go = new GameObject("Session");
            go.AddComponent<SessionStore>();
            go.AddComponent<AuthManager>();
        }

        // MARK: Home (Feed) — video wall + action rail + comments overlay

        private static GameObject BuildHomePanel(Transform parent, Camera cam)
        {
            var home = new GameObject("HomePanel");
            home.transform.SetParent(parent, false);

            var wallGO = new GameObject("VideoWall");
            wallGO.transform.SetParent(home.transform, false);
            wallGO.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            var meshFilter = wallGO.AddComponent<MeshFilter>();
            var meshRenderer = wallGO.AddComponent<MeshRenderer>();
            var curve = wallGO.AddComponent<CurvedMeshGenerator>();
            var renderTexture = EnsureRenderTexture();
            var material = EnsureWallMaterial(renderTexture);
            meshRenderer.sharedMaterial = material;
            curve.Generate();
            var videoPlayer = wallGO.AddComponent<VideoPlayer>();
            var wallController = wallGO.AddComponent<VideoWallController>();
            wallController.wallRenderer = meshRenderer;
            wallController.renderTexture = renderTexture;

            var actionRailGO = BuildActionRail(home.transform, cam, wallController);
            var commentsGO = BuildCommentsPanel(home.transform, cam);
            actionRailGO.GetComponent<FeedActionRail>().commentsPanel = commentsGO.GetComponent<CommentsPanelController>();

            var feedController = home.AddComponent<FeedController>();
            feedController.videoWall = wallController;
            feedController.actionRail = actionRailGO.GetComponent<FeedActionRail>();
            var feedInput = home.AddComponent<FeedInput>();
            feedInput.feed = feedController;

            return home;
        }

        private static GameObject BuildActionRail(Transform parent, Camera cam, VideoWallController wallController)
        {
            var canvasGO = CreateWorldCanvas("ActionRail", parent, new Vector3(1.15f, 1.3f, 2.2f),
                Quaternion.Euler(0f, 30f, 0f), new Vector2(220, 620), new Color(0f, 0f, 0f, 0f), cam);

            var avatar = CreateAvatarPlaceholder(canvasGO.transform, new Vector2(0f, 270f), 70f);
            var avatarButton = avatar.AddComponent<Button>();

            var likeBtn = CreateIconButton(canvasGO.transform, "Like", new Vector2(0f, 150f));
            var likeCount = CreateText(canvasGO.transform, new Vector2(0f, 105f), new Vector2(200f, 30f), "0", 20, Color.white, false, false);

            var commentBtn = CreateIconButton(canvasGO.transform, "Msg", new Vector2(0f, 40f));
            var commentCount = CreateText(canvasGO.transform, new Vector2(0f, -5f), new Vector2(200f, 30f), "0", 20, Color.white, false, false);

            var saveBtn = CreateIconButton(canvasGO.transform, "Save", new Vector2(0f, -70f));
            var shareBtn = CreateIconButton(canvasGO.transform, "Share", new Vector2(0f, -160f));
            var muteBtn = CreateIconButton(canvasGO.transform, "Mute", new Vector2(0f, -250f));
            var deleteBtn = CreateIconButton(canvasGO.transform, "Delete", new Vector2(0f, -340f));

            var rail = canvasGO.AddComponent<FeedActionRail>();
            rail.avatarButton = avatarButton;
            rail.avatarImage = avatar.GetComponent<RawImage>();
            rail.likeButton = likeBtn.GetComponent<Button>();
            rail.likeIcon = likeBtn.GetComponentInChildren<Image>();
            rail.likeCountText = likeCount.GetComponent<TMP_Text>();
            rail.commentButton = commentBtn.GetComponent<Button>();
            rail.commentCountText = commentCount.GetComponent<TMP_Text>();
            rail.saveButton = saveBtn.GetComponent<Button>();
            rail.saveIcon = saveBtn.GetComponentInChildren<Image>();
            rail.shareButton = shareBtn.GetComponent<Button>();
            rail.muteButton = muteBtn.GetComponent<Button>();
            rail.muteButtonLabel = muteBtn.GetComponentInChildren<TMP_Text>();
            rail.deleteButton = deleteBtn.GetComponent<Button>();
            rail.videoWall = wallController;

            return canvasGO;
        }

        private static GameObject BuildCommentsPanel(Transform parent, Camera cam)
        {
            var canvasGO = CreateWorldCanvas("CommentsPanel", parent, new Vector3(0f, 1.4f, 1.6f),
                Quaternion.identity, new Vector2(700, 900), XM.HexColor("#FBF9F7", 0.98f), cam);
            canvasGO.SetActive(false);

            CreateText(canvasGO.transform, new Vector2(0f, 420f), new Vector2(500f, 50f), "Comments", 30, XM.Ink, true, false);
            var closeBtn = CreateButton(canvasGO.transform, new Vector2(310f, 420f), new Vector2(60f, 50f), "X", XM.Ground, XM.Ink, 24);

            var listContent = CreateListContent(canvasGO.transform, new Vector2(0f, 20f), new Vector2(640f, 660f));
            var rowPrefab = CreateCommentRowTemplate();

            var composerField = CreateInputField(canvasGO.transform, new Vector2(-70f, -410f), new Vector2(500f, 70f), "Add a comment...");
            var sendBtn = CreateButton(canvasGO.transform, new Vector2(300f, -410f), new Vector2(80f, 70f), ">", XM.Action, Color.white, 28);

            var controller = canvasGO.AddComponent<CommentsPanelController>();
            controller.commentListContent = listContent;
            controller.commentRowPrefab = rowPrefab;
            controller.composerField = composerField.GetComponent<TMP_InputField>();
            controller.panelRoot = canvasGO;

            closeBtn.GetComponent<Button>().onClick.AddListener(controller.Hide);
            sendBtn.GetComponent<Button>().onClick.AddListener(controller.PostComposedComment);

            return canvasGO;
        }

        // MARK: Discover

        private static GameObject BuildDiscoverPanel(Transform parent, Camera cam)
        {
            var canvasGO = CreateWorldCanvas("DiscoverPanel", parent, new Vector3(0f, 1.4f, 2.2f),
                Quaternion.identity, new Vector2(900, 1100), XM.Ground, cam);

            CreateText(canvasGO.transform, new Vector2(0f, 500f), new Vector2(700f, 50f), "Discover", 32, XM.Ink, true, false);
            var searchField = CreateInputField(canvasGO.transform, new Vector2(0f, 420f), new Vector2(780f, 70f), "Search people...");
            var resultsContent = CreateListContent(canvasGO.transform, new Vector2(0f, -40f), new Vector2(780f, 780f));
            var rowPrefab = CreateUserRowTemplate();

            var controller = canvasGO.AddComponent<DiscoverPanelController>();
            controller.searchField = searchField.GetComponent<TMP_InputField>();
            controller.resultsContent = resultsContent;
            controller.userRowPrefab = rowPrefab;

            return canvasGO;
        }

        // MARK: Inbox

        private static GameObject BuildInboxPanel(Transform parent, Camera cam)
        {
            var canvasGO = CreateWorldCanvas("InboxPanel", parent, new Vector3(0f, 1.4f, 2.2f),
                Quaternion.identity, new Vector2(900, 1100), XM.Ground, cam);

            CreateText(canvasGO.transform, new Vector2(0f, 500f), new Vector2(700f, 50f), "Inbox", 32, XM.Ink, true, false);

            var conversationList = CreateListContent(canvasGO.transform, new Vector2(-220f, -40f), new Vector2(400f, 900f));
            var conversationRowPrefab = CreateConversationRowTemplate();

            var messageList = CreateListContent(canvasGO.transform, new Vector2(220f, 30f), new Vector2(400f, 700f));
            var messageBubblePrefab = CreateMessageBubbleTemplate();

            var composerField = CreateInputField(canvasGO.transform, new Vector2(130f, -430f), new Vector2(300f, 70f), "Message...");
            var sendBtn = CreateButton(canvasGO.transform, new Vector2(370f, -430f), new Vector2(80f, 70f), ">", XM.Action, Color.white, 28);

            var controller = canvasGO.AddComponent<InboxPanelController>();
            controller.conversationListContent = conversationList;
            controller.conversationRowPrefab = conversationRowPrefab;
            controller.messageListContent = messageList;
            controller.messageBubblePrefab = messageBubblePrefab;
            controller.composerField = composerField.GetComponent<TMP_InputField>();

            sendBtn.GetComponent<Button>().onClick.AddListener(controller.SendComposedMessage);

            return canvasGO;
        }

        // MARK: Profile

        private static GameObject BuildProfilePanel(Transform parent, Camera cam)
        {
            var canvasGO = CreateWorldCanvas("ProfilePanel", parent, new Vector3(0f, 1.4f, 2.2f),
                Quaternion.identity, new Vector2(900, 1200), XM.Ground, cam);

            var banner = CreateImage(canvasGO.transform, new Vector2(0f, 520f), new Vector2(900f, 220f), XM.StripeBase);
            var avatar = CreateAvatarPlaceholder(canvasGO.transform, new Vector2(0f, 400f), 120f);
            var displayName = CreateText(canvasGO.transform, new Vector2(0f, 290f), new Vector2(700f, 50f), "Display Name", 30, XM.Ink, true, false);
            var handle = CreateText(canvasGO.transform, new Vector2(0f, 245f), new Vector2(700f, 40f), "@handle", 22, XM.Muted, false, false);
            var bio = CreateText(canvasGO.transform, new Vector2(0f, 195f), new Vector2(700f, 60f), "", 20, XM.Ink, false, false);

            var followers = CreateText(canvasGO.transform, new Vector2(-220f, 120f), new Vector2(200f, 60f), "0\nFollowers", 20, XM.Ink, false, false);
            var following = CreateText(canvasGO.transform, new Vector2(0f, 120f), new Vector2(200f, 60f), "0\nFollowing", 20, XM.Ink, false, false);
            var likes = CreateText(canvasGO.transform, new Vector2(220f, 120f), new Vector2(200f, 60f), "0\nLikes", 20, XM.Ink, false, false);

            var editBtn = CreateButton(canvasGO.transform, new Vector2(0f, 50f), new Vector2(300f, 70f), "Edit profile", XM.Action, Color.white, 24);

            var gridContent = CreateGridContent(canvasGO.transform, new Vector2(0f, -280f), new Vector2(860f, 600f));
            var thumbPrefab = CreateThumbnailTemplate();

            var controller = canvasGO.AddComponent<ProfilePanelController>();
            controller.displayNameText = displayName.GetComponent<TMP_Text>();
            controller.handleText = handle.GetComponent<TMP_Text>();
            controller.bioText = bio.GetComponent<TMP_Text>();
            controller.followerCountText = followers.GetComponent<TMP_Text>();
            controller.followingCountText = following.GetComponent<TMP_Text>();
            controller.likesCountText = likes.GetComponent<TMP_Text>();
            controller.avatarImage = avatar.GetComponent<RawImage>();
            controller.editProfileButton = editBtn.GetComponent<Button>();
            controller.videoGridContent = gridContent;
            controller.videoThumbnailPrefab = thumbPrefab;

            return canvasGO;
        }

        // MARK: Tab bar

        private static GameObject BuildTabBar(Camera cam, GameObject home, GameObject discover, GameObject inbox, GameObject profile)
        {
            var canvasGO = CreateWorldCanvas("TabBar", null, new Vector3(0f, 0.9f, 1.6f),
                Quaternion.identity, new Vector2(900, 140), XM.Ground, cam);

            var tabController = canvasGO.AddComponent<MainTabController>();
            tabController.homePanel = home;
            tabController.discoverPanel = discover;
            tabController.inboxPanel = inbox;
            tabController.profilePanel = profile;
            tabController.profileController = profile.GetComponent<ProfilePanelController>();

            var homeBtn = CreateButton(canvasGO.transform, new Vector2(-360f, 0f), new Vector2(140f, 100f), "Home", XM.Ground, XM.Ink, 22);
            var discoverBtn = CreateButton(canvasGO.transform, new Vector2(-160f, 0f), new Vector2(140f, 100f), "Search", XM.Ground, XM.Ink, 22);
            var recordBtn = CreateButton(canvasGO.transform, new Vector2(0f, 0f), new Vector2(110f, 110f), "+", XM.Action, Color.white, 40);
            var inboxBtn = CreateButton(canvasGO.transform, new Vector2(160f, 0f), new Vector2(140f, 100f), "Inbox", XM.Ground, XM.Ink, 22);
            var meBtn = CreateButton(canvasGO.transform, new Vector2(360f, 0f), new Vector2(140f, 100f), "Me", XM.Ground, XM.Ink, 22);

            homeBtn.GetComponent<Button>().onClick.AddListener(tabController.ShowHome);
            discoverBtn.GetComponent<Button>().onClick.AddListener(tabController.ShowDiscover);
            recordBtn.GetComponent<Button>().onClick.AddListener(tabController.TapRecord);
            inboxBtn.GetComponent<Button>().onClick.AddListener(tabController.ShowInbox);
            meBtn.GetComponent<Button>().onClick.AddListener(tabController.ShowProfile);

            return canvasGO;
        }

        // MARK: Generic scene-building helpers

        private static GameObject CreateWorldCanvas(string name, Transform parent, Vector3 localPosition, Quaternion localRotation,
            Vector2 sizeDelta, Color bgColor, Camera cam)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.localPosition = localPosition;
            rect.localRotation = localRotation;
            rect.sizeDelta = sizeDelta;

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;
            go.AddComponent<GraphicRaycaster>();
            rect.localScale = Vector3.one * 0.0015f;

            if (bgColor.a > 0f)
            {
                var bgImage = go.AddComponent<Image>();
                bgImage.color = bgColor;
            }

            return go;
        }

        private static GameObject CreateButton(Transform parent, Vector2 anchoredPos, Vector2 size, string label,
            Color bgColor, Color labelColor, int fontSize)
        {
            var go = new GameObject("Button_" + label, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var image = go.AddComponent<Image>();
            image.color = bgColor;
            go.AddComponent<Button>();

            CreateText(go.transform, Vector2.zero, size, label, fontSize, labelColor, true, true);
            return go;
        }

        private static GameObject CreateIconButton(Transform parent, string label, Vector2 anchoredPos)
        {
            var go = new GameObject("Icon_" + label, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(100f, 80f);

            var image = go.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.28f);
            go.AddComponent<Button>();

            CreateText(go.transform, Vector2.zero, new Vector2(100f, 80f), label, 18, Color.white, false, true);
            return go;
        }

        private static GameObject CreateImage(Transform parent, Vector2 anchoredPos, Vector2 size, Color color)
        {
            var go = new GameObject("Image", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            var image = go.AddComponent<Image>();
            image.color = color;
            return go;
        }

        private static GameObject CreateText(Transform parent, Vector2 anchoredPos, Vector2 size, string text,
            int fontSize, Color color, bool bold, bool stretch)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            if (stretch)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            else
            {
                rect.anchoredPosition = anchoredPos;
                rect.sizeDelta = size;
            }

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            return go;
        }

        private static GameObject CreateAvatarPlaceholder(Transform parent, Vector2 anchoredPos, float diameter)
        {
            var go = new GameObject("Avatar", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(diameter, diameter);
            var raw = go.AddComponent<RawImage>();
            raw.texture = Texture2D.whiteTexture;
            raw.color = XM.HexColor("#141110", 0.25f);
            return go;
        }

        private static Transform CreateListContent(Transform parent, Vector2 anchoredPos, Vector2 viewportSize)
        {
            var scrollGO = new GameObject("ScrollView", typeof(RectTransform));
            scrollGO.transform.SetParent(parent, false);
            var scrollRect = scrollGO.GetComponent<RectTransform>();
            scrollRect.anchoredPosition = anchoredPos;
            scrollRect.sizeDelta = viewportSize;
            var scroll = scrollGO.AddComponent<ScrollRect>();
            scrollGO.AddComponent<RectMask2D>();
            scroll.horizontal = false;

            var viewportGO = new GameObject("Viewport", typeof(RectTransform));
            viewportGO.transform.SetParent(scrollGO.transform, false);
            var viewportRect = viewportGO.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewportGO.AddComponent<Image>().color = new Color(0, 0, 0, 0f);
            viewportGO.AddComponent<Mask>().showMaskGraphic = false;

            var contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(viewportGO.transform, false);
            var contentRect = contentGO.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, 0f);

            var layout = contentGO.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.padding = new RectOffset(10, 10, 10, 10);
            var fitter = contentGO.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewportRect;
            scroll.content = contentRect;

            return contentGO.transform;
        }

        private static Transform CreateGridContent(Transform parent, Vector2 anchoredPos, Vector2 viewportSize)
        {
            var scrollGO = new GameObject("GridScrollView", typeof(RectTransform));
            scrollGO.transform.SetParent(parent, false);
            var scrollRect = scrollGO.GetComponent<RectTransform>();
            scrollRect.anchoredPosition = anchoredPos;
            scrollRect.sizeDelta = viewportSize;
            var scroll = scrollGO.AddComponent<ScrollRect>();
            scrollGO.AddComponent<RectMask2D>();
            scroll.horizontal = false;

            var viewportGO = new GameObject("Viewport", typeof(RectTransform));
            viewportGO.transform.SetParent(scrollGO.transform, false);
            var viewportRect = viewportGO.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewportGO.AddComponent<Image>().color = new Color(0, 0, 0, 0f);
            viewportGO.AddComponent<Mask>().showMaskGraphic = false;

            var contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(viewportGO.transform, false);
            var contentRect = contentGO.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);

            var grid = contentGO.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(260f, 260f);
            grid.spacing = new Vector2(15f, 15f);
            var fitter = contentGO.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewportRect;
            scroll.content = contentRect;

            return contentGO.transform;
        }

        private static GameObject CreateInputField(Transform parent, Vector2 anchoredPos, Vector2 size, string placeholder)
        {
            var go = new GameObject("InputField", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            var bg = go.AddComponent<Image>();
            bg.color = XM.HexColor("#141110", 0.06f);

            var textArea = new GameObject("Text Area", typeof(RectTransform));
            textArea.transform.SetParent(go.transform, false);
            var textAreaRect = textArea.GetComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(20f, 5f);
            textAreaRect.offsetMax = new Vector2(-20f, -5f);
            textArea.AddComponent<RectMask2D>();

            var placeholderGO = CreateText(textArea.transform, Vector2.zero, size, placeholder, 22, XM.Muted, false, true);
            var placeholderTmp = placeholderGO.GetComponent<TextMeshProUGUI>();
            placeholderTmp.fontStyle = FontStyles.Italic;

            var textGO = CreateText(textArea.transform, Vector2.zero, size, "", 22, XM.Ink, false, true);
            var textTmp = textGO.GetComponent<TextMeshProUGUI>();

            var input = go.AddComponent<TMP_InputField>();
            input.textViewport = textAreaRect;
            input.textComponent = textTmp;
            input.placeholder = placeholderTmp;
            input.fontAsset = textTmp.font;

            return go;
        }

        // MARK: Row / cell templates (deactivated prefab-like objects, Instantiate()'d at runtime)

        private static GameObject CreateCommentRowTemplate()
        {
            var go = new GameObject("CommentRow", typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(600f, 90f);
            var layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = 90f;

            CreateText(go.transform, new Vector2(0f, 25f), new Vector2(600f, 30f), "Author", 20, XM.Ink, true, false);
            CreateText(go.transform, new Vector2(0f, -15f), new Vector2(600f, 40f), "Body", 20, XM.Ink, false, false);

            go.SetActive(false);
            return go;
        }

        private static GameObject CreateUserRowTemplate()
        {
            var go = new GameObject("UserRow", typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(760f, 100f);
            var layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = 100f;

            CreateAvatarPlaceholder(go.transform, new Vector2(-320f, 0f), 70f);
            CreateText(go.transform, new Vector2(50f, 15f), new Vector2(500f, 30f), "Display Name", 22, XM.Ink, true, false);
            CreateText(go.transform, new Vector2(50f, -15f), new Vector2(500f, 30f), "@handle", 18, XM.Muted, false, false);

            go.SetActive(false);
            return go;
        }

        private static GameObject CreateConversationRowTemplate()
        {
            var go = new GameObject("ConversationRow", typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(380f, 90f);
            var layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = 90f;
            go.AddComponent<Image>().color = XM.HexColor("#141110", 0.04f);
            go.AddComponent<Button>();

            CreateText(go.transform, new Vector2(0f, 20f), new Vector2(360f, 30f), "Name", 20, XM.Ink, true, false);
            CreateText(go.transform, new Vector2(0f, -15f), new Vector2(360f, 30f), "Last message", 16, XM.Muted, false, false);

            go.SetActive(false);
            return go;
        }

        private static GameObject CreateMessageBubbleTemplate()
        {
            var go = new GameObject("MessageBubble", typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(380f, 60f);
            var layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = 60f;
            go.AddComponent<Image>().color = XM.HexColor("#141110", 0.06f);

            CreateText(go.transform, Vector2.zero, new Vector2(360f, 50f), "Message", 20, XM.Ink, false, true);

            go.SetActive(false);
            return go;
        }

        private static GameObject CreateThumbnailTemplate()
        {
            var go = new GameObject("Thumbnail", typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(260f, 260f);
            go.AddComponent<RawImage>().color = XM.StripeBase;
            CreateText(go.transform, new Vector2(0f, -105f), new Vector2(240f, 30f), "0 views", 16, Color.white, false, false);

            go.SetActive(false);
            return go;
        }

        // MARK: Generated assets (material / render texture for the video wall)

        private static RenderTexture EnsureRenderTexture()
        {
            var path = GeneratedFolder + "/FeedRenderTexture.renderTexture";
            var existing = AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
            if (existing != null) return existing;
            var rt = new RenderTexture(1920, 1080, 16) { name = "FeedRenderTexture" };
            AssetDatabase.CreateAsset(rt, path);
            return AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
        }

        private static Material EnsureWallMaterial(RenderTexture texture)
        {
            var path = GeneratedFolder + "/VideoWallMaterial.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.mainTexture = texture;
                return existing;
            }
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
            var mat = new Material(shader) { name = "VideoWallMaterial" };
            mat.mainTexture = texture;
            AssetDatabase.CreateAsset(mat, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            var name = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
