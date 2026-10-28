using System;
using System.Collections.Generic;
using System.IO;
using DeadCrowd.Prototype;
using DeadCrowd.Session;
using DeadCrowd.UI;
using Fusion;
using Fusion.Editor;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace DeadCrowd.Editor
{
    public static class ProductionSceneEditor
    {
        public const string SCENE_PATH = "Assets/Main/01_Scene/DeadCrowd.unity";
        private const string PROTOTYPE_PATH = "Assets/Main/01_Scene/DeadCrowdPrototype.unity";
        public const string FONT_PATH = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        private static readonly Color BACKGROUND = new Color(0.035f, 0.055f, 0.075f, 0.98f);
        private static readonly Color CARD = new Color(0.075f, 0.11f, 0.14f, 0.98f);
        private static readonly Color ACCENT = new Color(0.18f, 0.65f, 0.56f);
        private static readonly Color INK = new Color(0.9f, 0.94f, 0.93f);

        [MenuItem("DEAD CROWD/Development/Open Main Scene")]
        public static void OpenOrCreate()
        {
            if (File.Exists(SCENE_PATH)) { EditorSceneManager.OpenScene(SCENE_PATH); return; }
            if (!File.Exists(PROTOTYPE_PATH)) throw new InvalidOperationException("Create the prototype scene before migrating its verified world.");
            if (!AssetDatabase.CopyAsset(PROTOTYPE_PATH, SCENE_PATH)) throw new IOException("Could not create the main scene.");
            Scene scene = EditorSceneManager.OpenScene(SCENE_PATH);
            CrowdBootstrap runtime = UnityEngine.Object.FindFirstObjectByType<CrowdBootstrap>();
            runtime.name = "Game Application";
            runtime.UseCanvasUI();
            CrowdSessionController session = runtime.gameObject.AddComponent<CrowdSessionController>();
            session.Configure(runtime);
            Directory.CreateDirectory("Assets/Main/05_UI");
            AssetDatabase.Refresh();
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH);
            if (font == null) throw new InvalidOperationException("Import TMP Essential Resources before creating the main UI.");
            var canvasObj = new GameObject("Game Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObj.transform.SetParent(runtime.transform, false);
            Canvas canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var eventObj = new GameObject("UI Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventObj.transform.SetParent(runtime.transform, false);
            eventObj.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

            SessionScreen_view view = canvasObj.AddComponent<SessionScreen_view>();
            var presenter = runtime.gameObject.AddComponent<SessionPresenter_presenter>();
            presenter.Configure(session, view);
            runtime.gameObject.AddComponent<DeadCrowd.Tests.SessionUiSmokeTest>();
            var bindings = new List<SessionActionBinding>();
            var screens = new GameObject[8];
            var texts = new TMP_Text[12];
            GameObject backdrop = FullImage("Menu Background", canvasObj.transform, BACKGROUND);

            for (int i = 0; i < screens.Length; i++)
                if (i != (int)SESSION_SCREEN.COUNTDOWN) screens[i] = FullRect(((SESSION_SCREEN)i).ToString(), canvasObj.transform).gameObject;
            TMP_Text status = Label("Connection Status", canvasObj.transform, font, "Create a room or join with an invite code.", new Vector2(0, -450), new Vector2(1300, 70), 23);
            texts[(int)SESSION_TEXT.STATUS] = status;
            Transform title = Card(screens[0].transform);
            Label("Brand", title, font, "DEAD CROWD", new Vector2(0, 235), new Vector2(700, 85), 62);
            Label("Subtitle", title, font, "Find your opponents in the crowd. Be the last survivor.", new Vector2(0, 145), new Vector2(690, 65), 25);
            Button("Create Room", "Create Room", title, font, new Vector2(0, 35), SESSION_ACTION.SHOW_CREATE, bindings);
            Button("Join Room", "Join With Code", title, font, new Vector2(0, -45), SESSION_ACTION.SHOW_JOIN, bindings);
            Button("Settings", "Settings", title, font, new Vector2(0, -125), SESSION_ACTION.SETTINGS, bindings);
            Button("Quit", "Quit Game", title, font, new Vector2(0, -205), SESSION_ACTION.QUIT, bindings);

            Transform create = Card(screens[(int)SESSION_SCREEN.CREATE].transform);
            Header(create, font, "Create Room", "The room mode cannot be changed after creation.");
            Button("Duel", "1 vs 1 - 2 Players", create, font, new Vector2(0, 70), SESSION_ACTION.DUEL, bindings);
            Button("FFA", "Free For All - 4 Players", create, font, new Vector2(0, -10), SESSION_ACTION.FFA, bindings);
            Button("Confirm Create", "Create Invite Room", create, font, new Vector2(0, -145), SESSION_ACTION.CREATE, bindings);
            Button("Back", "Back", create, font, new Vector2(0, -225), SESSION_ACTION.BACK, bindings);

            Transform join = Card(screens[(int)SESSION_SCREEN.JOIN].transform);
            Header(join, font, "Join With Code", "6 letters or digits. No I, O, 0 or 1.");
            TMP_InputField codeInput = Input(join, font);
            Button("Confirm Join", "Join", join, font, new Vector2(0, -120), SESSION_ACTION.JOIN, bindings);
            Button("Back", "Back", join, font, new Vector2(0, -200), SESSION_ACTION.BACK, bindings);
            Transform connecting = Card(screens[(int)SESSION_SCREEN.CONNECTING].transform);
            Header(connecting, font, "Connecting", "Please wait.");
            Button("Cancel", "Cancel Connection", connecting, font, new Vector2(0, -100), SESSION_ACTION.CANCEL, bindings);

            Transform lobby = Card(screens[(int)SESSION_SCREEN.LOBBY].transform);
            Label("Lobby Title", lobby, font, "Lobby", new Vector2(0, 295), new Vector2(680, 55), 38);
            texts[(int)SESSION_TEXT.CODE] = Label("Invite Code", lobby, font, "Invite Code", new Vector2(0, 235), new Vector2(680, 55), 30);
            Button("Copy Code", "Copy Code", lobby, font, new Vector2(0, 165), SESSION_ACTION.COPY_CODE, bindings);
            texts[(int)SESSION_TEXT.SLOTS] = Label("Participant Slots", lobby, font, "Waiting for Player", new Vector2(0, 35), new Vector2(650, 180), 26);
            texts[(int)SESSION_TEXT.HOST_NOTICE] = Label("Start Condition", lobby, font, "Everyone must be ready before starting.", new Vector2(0, -90), new Vector2(690, 50), 21);
            Button("Ready", "Ready", lobby, font, new Vector2(0, -150), SESSION_ACTION.READY, bindings);
            Button("Host Start", "Start Match (Host)", lobby, font, new Vector2(0, -225), SESSION_ACTION.START, bindings);
            Button("Leave", "Leave Room", lobby, font, new Vector2(0, -300), SESSION_ACTION.LEAVE, bindings);

            Transform hud = screens[(int)SESSION_SCREEN.PLAYING].transform;
            RectTransform vitals = Rect("Vitals", hud, new Vector2(270, -130), new Vector2(460, 200), new Vector2(0, 1));
            Image vitalsBg = vitals.gameObject.AddComponent<Image>(); vitalsBg.color = CARD; vitalsBg.raycastTarget = false;
            texts[(int)SESSION_TEXT.HEALTH] = Label("Health", vitals, font, "Health", new Vector2(0, 65), new Vector2(410, 35), 24, TextAnchor.MiddleLeft);
            Image health = Bar("Health Bar", vitals, new Vector2(0, 30), ACCENT);
            texts[(int)SESSION_TEXT.EXPOSURE] = Label("Exposure", vitals, font, "Exposure", new Vector2(0, -10), new Vector2(410, 35), 24, TextAnchor.MiddleLeft);
            Image exposure = Bar("Exposure Bar", vitals, new Vector2(0, -45), Color.yellow);
            texts[(int)SESSION_TEXT.AMMO] = Label("Ammunition", hud, font, "Pistol", new Vector2(-230, -80), new Vector2(400, 80), 30, TextAnchor.MiddleRight, new Vector2(1, 1));
            texts[(int)SESSION_TEXT.TIME] = Label("Round Time", hud, font, "", new Vector2(0, -55), new Vector2(700, 65), 30, TextAnchor.MiddleCenter, new Vector2(0.5f, 1));
            texts[(int)SESSION_TEXT.CROSSHAIR] = Label("Crosshair", hud, font, "+", Vector2.zero, new Vector2(300, 60), 26);
            texts[(int)SESSION_TEXT.SPECTATOR] = Label("Spectator", hud, font, "", new Vector2(0, 150), new Vector2(1000, 100), 26, TextAnchor.MiddleCenter, new Vector2(0.5f, 0));
            Label("Controls", hud, font, "WASD Move    Shift Sprint    LMB Melee    RMB Aim    R Reload    Esc Menu", new Vector2(0, 50), new Vector2(1500, 65), 21, TextAnchor.MiddleCenter, new Vector2(0.5f, 0));
            GameObject countdown = FullRect("Countdown Overlay", canvasObj.transform).gameObject;
            texts[(int)SESSION_TEXT.COUNTDOWN] = Label("Countdown", countdown.transform, font, "3\nLook around", Vector2.zero, new Vector2(700, 200), 42);

            Transform result = Card(screens[(int)SESSION_SCREEN.RESULT].transform);
            Header(result, font, "Match Over", "");
            texts[(int)SESSION_TEXT.RESULT] = Label("Result", result, font, "", new Vector2(0, 70), new Vector2(690, 100), 38);
            Button("Return Lobby", "Return to Lobby (Host)", result, font, new Vector2(0, -90), SESSION_ACTION.RETURN_LOBBY, bindings);
            Button("Leave", "Leave Room", result, font, new Vector2(0, -175), SESSION_ACTION.LEAVE, bindings);
            GameObject menu = FullImage("Menu Overlay", canvasObj.transform, new Color(0, 0, 0, 0.65f));
            Transform menuCard = Card(menu.transform);
            Header(menuCard, font, "Menu", "The online match keeps running.");
            Button("Resume", "Resume", menuCard, font, new Vector2(0, 20), SESSION_ACTION.RESUME, bindings);
            Button("Settings", "Settings", menuCard, font, new Vector2(0, -65), SESSION_ACTION.SETTINGS, bindings);
            Button("Leave", "Leave Room", menuCard, font, new Vector2(0, -150), SESSION_ACTION.LEAVE, bindings);
            GameObject settings = FullImage("Settings Overlay", canvasObj.transform, new Color(0, 0, 0, 0.75f));
            Transform settingsCard = Card(settings.transform);
            Header(settingsCard, font, "Settings", "Sensitivity and volume are saved on this PC.");
            Slider sensitivity = Slider("Mouse Sensitivity", "Mouse Sensitivity", settingsCard, font, 70, 0.03f, 0.4f, 0.12f);
            Slider volume = Slider("Volume", "Volume", settingsCard, font, -40, 0f, 1f, 0.8f);
            Button("Save Settings", "Save and Close", settingsCard, font, new Vector2(0, -190), SESSION_ACTION.SAVE_SETTINGS, bindings);

            view.Configure(screens, texts, bindings.ToArray(), codeInput, backdrop, countdown, menu, settings, health, exposure, sensitivity, volume);
            view.Show(new SessionModel_model());
            NetworkObjectPostprocessor.BakeScene(scene);
            EditorSceneManager.SaveScene(scene);
            var buildScenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(SCENE_PATH, true) };
            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes) if (existing.path != SCENE_PATH) buildScenes.Add(existing);
            EditorBuildSettings.scenes = buildScenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[Development] Main Scene and editable Canvas UI saved.");
        }

        private static RectTransform FullRect(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
        private static GameObject FullImage(string name, Transform parent, Color color)
        {
            RectTransform rect = FullRect(name, parent); rect.gameObject.AddComponent<Image>().color = color; return rect.gameObject;
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size, Vector2? anchor = null)
        {
            var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform; rect.anchorMin = rect.anchorMax = anchor ?? new Vector2(0.5f, 0.5f); rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
        }
        private static Transform Card(Transform parent)
        {
            RectTransform rect = Rect("Panel", parent, Vector2.zero, new Vector2(820, 780)); rect.gameObject.AddComponent<Image>().color = CARD; return rect;
        }
        private static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, string content, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment = TextAnchor.MiddleCenter, Vector2? anchor = null)
        {
            TMP_Text text = Rect(name, parent, position, size, anchor).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font; text.fontSize = fontSize; text.text = content; text.color = INK;
            text.alignment = SessionUiMigration.Alignment(alignment); text.raycastTarget = false; text.richText = false;
            return text;
        }
        private static void Header(Transform parent, TMP_FontAsset font, string title, string subtitle)
        {
            Label("Heading", parent, font, title, new Vector2(0, 280), new Vector2(700, 65), 40);
            Label("Description", parent, font, subtitle, new Vector2(0, 195), new Vector2(710, 80), 23);
        }
        private static Button Button(string name, string label, Transform parent, TMP_FontAsset font, Vector2 position, SESSION_ACTION action, List<SessionActionBinding> bindings)
        {
            RectTransform rect = Rect(name, parent, position, new Vector2(650, 62));
            Image image = rect.gameObject.AddComponent<Image>(); image.color = new Color(0.12f, 0.22f, 0.25f);
            Button button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            Label("Label", rect, font, label, Vector2.zero, new Vector2(620, 58), 25);
            bindings.Add(new SessionActionBinding { Action = action, TargetBtn = button }); return button;
        }
        private static TMP_InputField Input(Transform parent, TMP_FontAsset font)
        {
            RectTransform rect = Rect("Invite Code Input", parent, new Vector2(0, 45), new Vector2(650, 90));
            rect.gameObject.AddComponent<Image>().color = new Color(0.035f, 0.055f, 0.075f);
            TMP_InputField input = rect.gameObject.AddComponent<TMP_InputField>(); input.characterLimit = 16;
            RectTransform viewport = Rect("Text Area", rect, Vector2.zero, new Vector2(600, 80));
            viewport.gameObject.AddComponent<RectMask2D>();
            TMP_Text text = Label("Text", viewport, font, "", Vector2.zero, new Vector2(600, 80), 36);
            TMP_Text placeholder = Label("Placeholder", viewport, font, "Enter Invite Code", Vector2.zero, new Vector2(600, 80), 27); placeholder.color = new Color(0.5f, 0.6f, 0.6f);
            input.textViewport = viewport; input.textComponent = text; input.placeholder = placeholder;
            input.fontAsset = font; input.lineType = TMP_InputField.LineType.SingleLine; return input;
        }
        private static Image Bar(string name, Transform parent, Vector2 position, Color color)
        {
            RectTransform rect = Rect(name, parent, position, new Vector2(410, 12)); rect.gameObject.AddComponent<Image>().color = new Color(0.18f, 0.21f, 0.24f);
            Image fill = FullRect("Fill", rect).gameObject.AddComponent<Image>(); fill.color = color; fill.raycastTarget = false; return fill;
        }
        private static Slider Slider(string name, string label, Transform parent, TMP_FontAsset font, float y, float min, float max, float value)
        {
            Label(name + " Label", parent, font, label, new Vector2(0, y + 35), new Vector2(650, 40), 25, TextAnchor.MiddleLeft);
            RectTransform root = Rect(name, parent, new Vector2(0, y - 10), new Vector2(650, 35));
            Slider slider = root.gameObject.AddComponent<Slider>();
            Image track = FullRect("Track", root).gameObject.AddComponent<Image>(); track.color = new Color(0.18f, 0.23f, 0.26f);
            Image fill = FullRect("Fill", root).gameObject.AddComponent<Image>(); fill.color = ACCENT;
            RectTransform handle = Rect("Handle", root, Vector2.zero, new Vector2(18, 42)); Image handleImage = handle.gameObject.AddComponent<Image>(); handleImage.color = INK;
            slider.fillRect = fill.rectTransform; slider.handleRect = handle; slider.targetGraphic = handleImage;
            slider.minValue = min; slider.maxValue = max; slider.value = value; return slider;
        }

        [MenuItem("DEAD CROWD/Development/Verify Main Scene")]
        public static void Verify()
        {
            EditorSceneManager.OpenScene(SCENE_PATH);
            SessionScreen_view view = UnityEngine.Object.FindFirstObjectByType<SessionScreen_view>();
            if (view == null || !view.ReferencesValid()) throw new InvalidOperationException("Canvas UI references are incomplete.");
            SessionUiMigration.VerifyText(view);
            if (UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length != 1) throw new InvalidOperationException("Expected one UI event system.");
            CrowdMatch match = UnityEngine.Object.FindFirstObjectByType<CrowdMatch>();
            if (match == null || match.GetComponent<NetworkObject>().SortKey == 0) throw new InvalidOperationException("Main Scene must retain a baked authoritative match.");
            if (UnityEngine.Object.FindFirstObjectByType<SessionPresenter_presenter>() == null || UnityEngine.Object.FindFirstObjectByType<CrowdSessionController>() == null) throw new InvalidOperationException("UI and session command boundary missing.");
            Debug.Log($"[Development] PASS: saved UI references, {view.BindingCount} actions, EventSystem, presenter, session boundary and baked match.");
        }

        [MenuItem("DEAD CROWD/Development/Build Windows")]
        public static void VerifyAndBuild()
        {
            OpenOrCreate(); SessionUiMigration.Migrate(); Verify();
            Directory.CreateDirectory("Builds/Development");
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { SCENE_PATH }, locationPathName = "Builds/Development/DEAD-CROWD.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Main Scene build failed: " + report.summary.result);
            Debug.Log("[Development] Windows build succeeded.");
        }
    }
}
