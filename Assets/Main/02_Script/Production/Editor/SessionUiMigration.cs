using System;
using System.Collections.Generic;
using DeadCrowd.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DeadCrowd.Editor
{
    public static class SessionUiMigration
    {
        private static readonly string[,] LEGACY_TEXT =
        {
            { "WASD 이동    Shift 질주    좌클릭 근접    우클릭 조준    R 재장전    Esc 메뉴", "WASD Move    Shift Sprint    LMB Melee    RMB Aim    R Reload    Esc Menu" },
            { "연결을 완료하지 못했습니다. 접속 상태와 빌드를 확인하고 다시 시도하세요.", "Could not connect. Check your connection and game version, then try again." },
            { "영문·숫자 6자리를 입력하세요. I, O, 0, 1은 사용하지 않습니다.", "Enter 6 letters or digits. I, O, 0 and 1 are excluded." },
            { "방 연결이 종료됐습니다. 방을 만들거나 다시 참가하세요.", "The session ended. Create a room or join again." },
            { "진행 중인 경기입니다. 대기실 복귀 후 참가하세요.", "The match is in progress. Join when it returns to the lobby." },
            { "군중 속에서 상대를 찾아 마지막까지 살아남으세요.", "Find your opponents in the crowd. Be the last survivor." },
            { "방을 찾을 수 없습니다. 초대 코드를 확인하세요.", "Room not found. Check the invite code." },
            { "방을 만든 뒤에는 모드를 변경할 수 없습니다.", "The room mode cannot be changed after creation." },
            { "영문·숫자 6자리 · I, O, 0, 1 제외", "6 letters or digits. No I, O, 0 or 1." },
            { "전원이 준비하면 호스트가 시작할 수 있습니다.", "The host can start when everyone is ready." },
            { "연결 시간이 초과됐습니다. 다시 시도하세요.", "Connection timed out. Please try again." },
            { "생존자 관전 중 · A / D로 대상 전환", "Spectating survivors - A / D to switch" },
            { "초대 방을 만들거나 코드로 참가하세요.", "Create a room or join with an invite code." },
            { "전원이 준비하면 시작할 수 있습니다.", "Everyone must be ready before starting." },
            { "감도와 음량은 이 PC에 저장됩니다.", "Sensitivity and volume are saved on this PC." },
            { "온라인 경기는 계속 진행됩니다.", "The online match keeps running." },
            { "호스트가 경기를 시작합니다.", "Waiting for the host to start." },
            { "방 정원이 가득 찼습니다.", "The room is full." },
            { "방에 연결하고 있습니다.", "Connecting to the room..." },
            { "연결을 취소했습니다.", "Connection cancelled." },
            { "경기 시작 · 호스트", "Start Match (Host)" },
            { "1 vs 1 · 2명", "1 vs 1 - 2 Players" },
            { "잠시 기다려 주세요.", "Please wait." },
            { "방에서 나왔습니다.", "You left the room." },
            { "대기실로 · 호스트", "Return to Lobby (Host)" },
            { "시간 초과 무승부", "Draw - Time Limit" },
            { "주변을 살펴보세요", "Look around" },
            { "개인전 · 4명", "Free For All - 4 Players" },
            { "초대 코드 입력", "Enter Invite Code" },
            { "저장하고 닫기", "Save and Close" },
            { "초대 방 생성", "Create Invite Room" },
            { "초대 코드  ", "Invite Code  " },
            { "방 전체 종료", "Close Room" },
            { "전멸 무승부", "Draw - All Eliminated" },
            { "코드로 참가", "Join With Code" },
            { "마우스 감도", "Mouse Sensitivity" },
            { "초대 코드", "Invite Code" },
            { "방 나가기", "Leave Room" },
            { "방 만들기", "Create Room" },
            { "준비 취소", "Cancel Ready" },
            { "준비 완료", "Ready" },
            { "참가 대기", "Waiting for Player" },
            { "게임 종료", "Quit Game" },
            { "연결 취소", "Cancel Connection" },
            { "연결 종료", "Disconnected" },
            { "후방 근접", "Backstab" },
            { "근접 공격", "Melee" },
            { "좀비 공격", "Zombie" },
            { "탄약 없음", "No Ammo" },
            { "  · 나", "  (You)" },
            { "명    ", " players    " },
            { "재장전  ", "Reloading  " },
            { "사망 · ", "Eliminated - " },
            { "경기 종료", "Match Over" },
            { "코드 복사", "Copy Code" },
            { "준비 중", "Not Ready" },
            { "정렬 중", "Aligning" },
            { "노출 중", "Exposed" },
            { "계속하기", "Resume" },
            { "체력  ", "Health  " },
            { "노출  ", "Exposure  " },
            { "권총  ", "Pistol  " },
            { "연결 중", "Connecting" },
            { "비노출", "Hidden" },
            { "슬롯 ", "Slot " },
            { "생존 ", "Alive " },
            { " 승리", " Wins" },
            { "대기실", "Lobby" },
            { "재장전", "Reloading" },
            { " · ", " - " },
            { "뒤로", "Back" },
            { "참가", "Join" },
            { "설정", "Settings" },
            { "음량", "Volume" },
            { "메뉴", "Menu" },
            { "체력", "Health" },
            { "노출", "Exposure" },
            { "권총", "Pistol" },
            { "사망", "Eliminated" },
            { "}초", "}s" },
        };

        public static TextAlignmentOptions Alignment(TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
                case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
                default: return TextAlignmentOptions.Center;
            }
        }

        private static string Translate(string value)
        {
            for (int i = 0; i < LEGACY_TEXT.GetLength(0); i++) value = value.Replace(LEGACY_TEXT[i, 0], LEGACY_TEXT[i, 1]);
            foreach (char character in value)
                if (character >= '\uac00' && character <= '\ud7a3') throw new InvalidOperationException("Untranslated UI text: " + value);
            return value;
        }

        [MenuItem("DEAD CROWD/Development/Convert UI to English TMP")]
        public static void Migrate()
        {
            var scene = EditorSceneManager.OpenScene(ProductionSceneEditor.SCENE_PATH);
            SessionScreen_view view = UnityEngine.Object.FindFirstObjectByType<SessionScreen_view>();
            if (view == null) throw new InvalidOperationException("Main Scene has no session view.");
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ProductionSceneEditor.FONT_PATH);
            if (font == null) throw new InvalidOperationException("TMP LiberationSans SDF font asset is required.");
            var savedView = new SerializedObject(view);
            var bindings = new List<SessionActionBinding>();
            SerializedProperty savedBindings = savedView.FindProperty("_bindings");
            for (int i = 0; i < savedBindings.arraySize; i++)
            {
                SerializedProperty binding = savedBindings.GetArrayElementAtIndex(i);
                bindings.Add(new SessionActionBinding { Action = (SESSION_ACTION)binding.FindPropertyRelative("Action").enumValueIndex, TargetBtn = (Button)binding.FindPropertyRelative("TargetBtn").objectReferenceValue });
            }
            GameObject[] screens = new GameObject[8];
            SerializedProperty savedScreens = savedView.FindProperty("_screens");
            for (int i = 0; i < screens.Length; i++) screens[i] = (GameObject)savedScreens.GetArrayElementAtIndex(i).objectReferenceValue;
            GameObject backdrop = (GameObject)savedView.FindProperty("_backdropObj").objectReferenceValue;
            GameObject countdown = (GameObject)savedView.FindProperty("_countdownObj").objectReferenceValue;
            GameObject menu = (GameObject)savedView.FindProperty("_menuObj").objectReferenceValue;
            GameObject settings = (GameObject)savedView.FindProperty("_settingsObj").objectReferenceValue;
            Image health = (Image)savedView.FindProperty("_healthImg").objectReferenceValue;
            Image exposure = (Image)savedView.FindProperty("_exposureImg").objectReferenceValue;
            Slider sensitivity = (Slider)savedView.FindProperty("_sensitivitySlider").objectReferenceValue;
            Slider volume = (Slider)savedView.FindProperty("_volumeSlider").objectReferenceValue;
            GameObject inputObj = null;
            string inputValue = "";
            int inputLimit = 16;
            foreach (InputField legacyInput in view.GetComponentsInChildren<InputField>(true))
            {
                if (legacyInput.onValueChanged.GetPersistentEventCount() != 0 || legacyInput.onEndEdit.GetPersistentEventCount() != 0) throw new InvalidOperationException("Custom InputField events require explicit migration.");
                inputObj = legacyInput.gameObject; inputValue = legacyInput.text; inputLimit = legacyInput.characterLimit;
                UnityEngine.Object.DestroyImmediate(legacyInput);
            }
            int converted = 0;
            foreach (Text legacy in view.GetComponentsInChildren<Text>(true))
            {
                GameObject obj = legacy.gameObject;
                string value = Translate(legacy.text);
                int size = legacy.fontSize;
                Color color = legacy.color;
                TextAlignmentOptions alignment = Alignment(legacy.alignment);
                bool raycast = legacy.raycastTarget;
                bool richText = legacy.supportRichText;
                FontStyle style = legacy.fontStyle;
                var rect = (RectTransform)obj.transform;
                Vector2 anchorMin = rect.anchorMin, anchorMax = rect.anchorMax, pivot = rect.pivot, rectSize = rect.sizeDelta;
                Vector3 position = rect.anchoredPosition3D, scale = rect.localScale;
                Quaternion rotation = rect.localRotation;
                UnityEngine.Object.DestroyImmediate(legacy);
                TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();
                rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.pivot = pivot; rect.sizeDelta = rectSize;
                rect.anchoredPosition3D = position; rect.localScale = scale; rect.localRotation = rotation;
                text.font = font; text.text = value; text.fontSize = size; text.color = color;
                text.alignment = alignment; text.raycastTarget = raycast; text.richText = richText;
                text.fontStyle = style == FontStyle.Bold ? FontStyles.Bold : style == FontStyle.Italic ? FontStyles.Italic : style == FontStyle.BoldAndItalic ? FontStyles.Bold | FontStyles.Italic : FontStyles.Normal;
                converted++;
            }
            foreach (TMP_Text text in view.GetComponentsInChildren<TMP_Text>(true)) { text.font = font; text.text = Translate(text.text); }
            TMP_InputField codeInput = view.GetComponentInChildren<TMP_InputField>(true);
            if (inputObj != null)
            {
                codeInput = inputObj.AddComponent<TMP_InputField>();
                Transform inputText = inputObj.transform.Find("Text");
                Transform placeholder = inputObj.transform.Find("Placeholder");
                var viewportObj = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
                var viewport = (RectTransform)viewportObj.transform; viewport.SetParent(inputObj.transform, false);
                viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one; viewport.offsetMin = new Vector2(25, 5); viewport.offsetMax = new Vector2(-25, -5);
                inputText.SetParent(viewport, false); placeholder.SetParent(viewport, false);
                foreach (Transform child in viewport)
                {
                    var rect = (RectTransform)child; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
                }
                codeInput.textViewport = viewport;
                codeInput.textComponent = inputText.GetComponent<TMP_Text>();
                codeInput.placeholder = placeholder.GetComponent<TMP_Text>();
                codeInput.fontAsset = font; codeInput.characterLimit = inputLimit; codeInput.lineType = TMP_InputField.LineType.SingleLine; codeInput.text = inputValue;
            }
            string[] textNames = { "Connection Status", "Invite Code", "Participant Slots", "Countdown", "Health", "Exposure", "Ammunition", "Round Time", "Crosshair", "Spectator", "Result", "Start Condition" };
            var fields = new TMP_Text[textNames.Length];
            foreach (TMP_Text text in view.GetComponentsInChildren<TMP_Text>(true))
                for (int i = 0; i < textNames.Length; i++) if (text.name == textNames[i]) fields[i] = text;
            view.Configure(screens, fields, bindings.ToArray(), codeInput, backdrop, countdown, menu, settings, health, exposure, sensitivity, volume);
            EditorUtility.SetDirty(view);
            VerifyText(view);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[TMP Migration] Converted {converted} legacy text components. Scene layout and button references preserved.");
        }

        public static void VerifyText(SessionScreen_view view)
        {
            if (view.GetComponentsInChildren<Text>(true).Length != 0 || view.GetComponentsInChildren<InputField>(true).Length != 0) throw new InvalidOperationException("Legacy UI text remains in the Canvas.");
            TMP_Text[] texts = view.GetComponentsInChildren<TMP_Text>(true);
            TMP_InputField input = view.GetComponentInChildren<TMP_InputField>(true);
            if (texts.Length == 0 || input == null || input.textViewport == null || input.textComponent == null || input.placeholder == null) throw new InvalidOperationException("TMP text or input wiring is incomplete.");
            foreach (TMP_Text text in texts)
            {
                if (text.font == null || text.font.atlasTexture == null || text.font.material == null) throw new InvalidOperationException("Missing embedded SDF font: " + text.name);
                foreach (char character in text.text)
                    // TMP_InputField uses a zero-width space as its empty-input caret sentinel.
                    if (character != '\u200B' && !char.IsWhiteSpace(character) && (!text.font.HasCharacter(character) || character > 127)) throw new InvalidOperationException("Unsupported UI character in " + text.name + ": " + character);
            }
            Debug.Log($"[TMP Migration] PASS: {texts.Length} English TMP texts, embedded font atlases and TMP input viewport.");
        }
    }
}
