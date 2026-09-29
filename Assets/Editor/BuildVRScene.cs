using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;
using XpressMediaVR;

namespace XpressMediaVR.EditorTools
{
    /// One-click scene assembler for the Feed's "real spatial redesign"
    /// milestone (see README-VR.md "Scene setup" / "What's built vs
    /// stubbed"): the curved VideoWall, the floating like/comment/follow
    /// ActionRail, the FeedManager that drives them, and the Comments panel
    /// the rail opens. Run this from Unity's menu bar instead of building
    /// the hierarchy by hand — it's idempotent, so running it again rebuilds
    /// cleanly rather than duplicating objects.
    ///
    /// Deliberately NOT covered here yet (next pass, once this core loop is
    /// verified working): Profile/Discover/Inbox panels and the WristMenu
    /// nav, since their anchor points depend on the exact XR rig hierarchy
    /// and are safer to wire by hand the first time. SessionStore/AuthManager
    /// ARE included since they're just two self-contained components.
    public static class BuildVRScene
    {
        private const string GeneratedFolder = "Assets/VR/Generated";

        [MenuItem("XpressMedia/Build VR Scene (Feed core)")]
        public static void BuildScene()
        {
            EnsureFolder(GeneratedFolder);

            var origin = FindXROrigin();
            var basePos = origin != null ? origin.position : Vector3.zero;
            var baseRot = origin != null ? origin.rotation : Quaternion.identity;
            var forward = baseRot * Vector3.forward;
            var right = baseRot * Vector3.right;

            var renderTexture = EnsureRenderTexture();
            var wallMaterial = EnsureWallMaterial();

            var videoWall = BuildVideoWall(basePos, baseRot, forward, renderTexture, wallMaterial);
            var commentsPanel = BuildCommentsPanel(basePos, baseRot, forward, right);
            var actionRail = BuildActionRail(basePos, baseRot, forward, right, commentsPanel);
            BuildFeedManager(videoWall, actionRail);
            BuildSessionAuth();

            EditorSceneManager_MarkDirty();

            Debug.Log("[BuildVRScene] Done. VideoWall + ActionRail + FeedManager + CommentsPanel + " +
                      "SessionStore/AuthManager are built and wired. Still TODO by hand: fill in real " +
                      "MparadiseClientID / BackendBaseURL in Assets/Scripts/App/Config.cs, then press Play " +
                      "(or run in the Meta XR Simulator) to test. Profile/Discover/Inbox panels and the " +
                      "WristMenu nav are a deliberate next pass, not built by this script yet.");
        }

        // MARK: - XR Origin lookup

        private static Transform FindXROrigin()
        {
            var scene = SceneManager.GetActiveScene();
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name.IndexOf("XR Origin", StringComparison.OrdinalIgnoreCase) >= 0)
                    return go.transform;
            }
            return null;
        }

        // MARK: - Assets

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            var leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static RenderTexture EnsureRenderTexture()
        {
            var path = GeneratedFolder + "/FeedRenderTexture.renderTexture";
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
            if (rt != null) return rt;
            rt = new RenderTexture(1920, 1080, 16) { name = "FeedRenderTexture" };
            AssetDatabase.CreateAsset(rt, path);
            return rt;
        }

        private static Material EnsureWallMaterial()
        {
            var path = GeneratedFolder + "/VideoWallMaterial.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Texture")
                         ?? Shader.Find("Standard");
            mat = new Material(shader) { name = "VideoWallMaterial" };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // MARK: - VideoWall

        private static VideoWallController BuildVideoWall(Vector3 basePos, Quaternion baseRot, Vector3 forward,
            RenderTexture renderTexture, Material wallMaterial)
        {
            var go = FreshRoot("VideoWall");
            go.transform.position = basePos + forward * 2.2f + Vector3.up * 1.5f;
            go.transform.rotation = baseRot;

            go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = wallMaterial;

            var curved = go.AddComponent<CurvedMeshGenerator>();
            curved.Generate(); // populate the mesh immediately so it's visible in edit mode too

            go.AddComponent<VideoPlayer>();

            var controller = go.AddComponent<VideoWallController>();
            controller.wallRenderer = renderer;
            controller.renderTexture = renderTexture;

            return controller;
        }

        // MARK: - Comments panel (built first so ActionRail can reference it)

        private static CommentsPanelController BuildCommentsPanel(Vector3 basePos, Quaternion baseRot,
            Vector3 forward, Vector3 right)
        {
            var pos = basePos + forward * 1.6f + right * 1.6f + Vector3.up * 1.5f;
            var canvasGO = CreateWorldCanvas("CommentsPanel", pos, baseRot, new Vector2(700, 900), 0.0016f);

            var listContent = CreateListContent(canvasGO.transform, "CommentList",
                new Vector2(0f, 120f), new Vector2(640, 620));

            var rowTemplate = CreateRowTemplate(canvasGO.transform, "CommentRowTemplate", twoLines: true);

            var composer = CreateInputField(canvasGO.transform, "ComposerField",
                new Vector2(0f, -380f), new Vector2(500, 60));

            var postButton = CreateButton(canvasGO.transform, "PostButton", "Post",
                new Vector2(280f, -380f), new Vector2(120, 60));

            var controller = canvasGO.AddComponent<CommentsPanelController>();
            controller.commentListContent = listContent;
            controller.commentRowPrefab = rowTemplate;
            controller.composerField = composer;
            controller.panelRoot = canvasGO;

            postButton.onClick.AddListener(controller.PostComposedComment);

            canvasGO.SetActive(false); // opened on demand via FeedActionRail's comment button
            return controller;
        }

        // MARK: - Action rail

        private static FeedActionRail BuildActionRail(Vector3 basePos, Quaternion baseRot, Vector3 forward,
            Vector3 right, CommentsPanelController commentsPanel)
        {
            var pos = basePos + forward * 1.9f + right * 1.1f + Vector3.up * 1.5f;
            var canvasGO = CreateWorldCanvas("ActionRail", pos, baseRot, new Vector2(220, 620), 0.002f);

            var likeButton = CreateButton(canvasGO.transform, "LikeButton", "♥",
                new Vector2(0f, 220f), new Vector2(140, 140));
            var likeCount = CreateText(canvasGO.transform, "LikeCount", "0",
                new Vector2(0f, 130f), new Vector2(200, 50));

            var commentButton = CreateButton(canvasGO.transform, "CommentButton", "✇",
                new Vector2(0f, 20f), new Vector2(140, 140));
            var commentCount = CreateText(canvasGO.transform, "CommentCount", "0",
                new Vector2(0f, -70f), new Vector2(200, 50));

            var followButton = CreateButton(canvasGO.transform, "FollowButton", "Follow",
                new Vector2(0f, -180f), new Vector2(180, 100));
            var followLabel = followButton.GetComponentInChildren<TextMeshProUGUI>();

            var rail = canvasGO.AddComponent<FeedActionRail>();
            rail.likeButton = likeButton;
            rail.likeIcon = likeButton.GetComponent<Image>();
            rail.likeCountText = likeCount;
            rail.commentButton = commentButton;
            rail.commentCountText = commentCount;
            rail.followButton = followButton;
            rail.followButtonLabel = followLabel;
            rail.commentsPanel = commentsPanel;

            return rail;
        }

        // MARK: - Feed manager

        private static void BuildFeedManager(VideoWallController videoWall, FeedActionRail actionRail)
        {
            var go = FreshRoot("FeedManager");
            var controller = go.AddComponent<FeedController>();
            controller.videoWall = videoWall;
            controller.actionRail = actionRail;

            var input = go.AddComponent<FeedInput>();
            input.feed = controller;
        }

        // MARK: - Session / Auth

        private static void BuildSessionAuth()
        {
            var go = FreshRoot("Session");
            go.AddComponent<AuthManager>();
            go.AddComponent<SessionStore>();
        }

        // MARK: - Generic scene helpers

        private static GameObject FreshRoot(string name)
        {
            var existing = GameObject.Find("/" + name);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
            return new GameObject(name);
        }

        private static void EditorSceneManager_MarkDirty()
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        // MARK: - UI helpers

        private static GameObject CreateWorldCanvas(string name, Vector3 worldPos, Quaternion rot, Vector2 size,
            float scale)
        {
            var existing = GameObject.Find("/" + name);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing);

            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            go.transform.position = worldPos;
            go.transform.rotation = rot;
            go.transform.localScale = Vector3.one * scale;

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.07f, 0.07f, 0.85f); // XM.Ink, translucent panel backing

            EnsureEventSystem();
            return go;
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
            var go = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPos,
            Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;

            var img = go.GetComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.12f);

            CreateText(go.transform, "Label", label, Vector2.zero, size, 28, stretch: true);

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            return btn;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string content,
            Vector2 anchoredPos, Vector2 size, int fontSize = 24, bool stretch = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            if (stretch)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            else
            {
                rt.sizeDelta = size;
                rt.anchoredPosition = anchoredPos;
            }

            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            return text;
        }

        private static RectTransform CreateListContent(Transform parent, string name, Vector2 anchoredPos,
            Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;

            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return rt;
        }

        private static GameObject CreateRowTemplate(Transform parent, string name, bool twoLines)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = twoLines ? 90 : 60;

            CreateText(go.transform, "Primary", "", new Vector2(0f, twoLines ? 15f : 0f),
                new Vector2(600, 40), 24, stretch: false);
            if (twoLines)
                CreateText(go.transform, "Secondary", "", new Vector2(0f, -20f), new Vector2(600, 40), 20);

            go.SetActive(false); // acts as an Instantiate() template, never itself rendered
            return go;
        }

        private static TMP_InputField CreateInputField(Transform parent, string name, Vector2 anchoredPos,
            Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);

            var textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(go.transform, false);
            var textAreaRt = textArea.GetComponent<RectTransform>();
            textAreaRt.anchorMin = Vector2.zero;
            textAreaRt.anchorMax = Vector2.one;
            textAreaRt.offsetMin = new Vector2(12, 6);
            textAreaRt.offsetMax = new Vector2(-12, -6);

            var textGO = new GameObject("Text", typeof(RectTransform));
            textGO.transform.SetParent(textArea.transform, false);
            var textRt = textGO.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var tmpText = textGO.AddComponent<TextMeshProUGUI>();
            tmpText.fontSize = 24;
            tmpText.color = Color.white;
            tmpText.textWrappingMode = TextWrappingModes.NoWrap;
            tmpText.alignment = TextAlignmentOptions.MidlineLeft;

            var field = go.AddComponent<TMP_InputField>();
            field.textViewport = textAreaRt;
            field.textComponent = tmpText;
            return field;
        }
    }
}
