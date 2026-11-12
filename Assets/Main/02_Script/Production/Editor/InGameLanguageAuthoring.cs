
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DeadCrowd.Prototype;
using DeadCrowd.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

internal static class InGameLanguageAuthoring
{
    private const string SCENE = "Assets/Main/01_Scene/InGame.unity";
    private const string FONT = "Assets/Main/99_ETC/KCC-Ganpan SDF.asset";
    private const string SOURCE = "Assets/Main/99_ETC/KCC-Ganpan.ttf";
    private const string FALLBACK = "Assets/Main/99_ETC/KCC-Ganpan Supplement SDF.asset";

    public static void Apply()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT);
        var required = Enumerable.Range(0xAC00, 0xD7A4 - 0xAC00).Concat(Enumerable.Range(32, 95)).Concat(new[] { 0xB7 }).ToArray();
        TMP_FontAsset supplement = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FALLBACK);
        if (supplement == null)
        {
            Font source = AssetDatabase.LoadAssetAtPath<Font>(SOURCE);
            int[] missing = required.Where(value => !font.HasCharacter(value)).ToArray();
            supplement = TMP_FontAsset.CreateFontAsset(source, (int)font.faceInfo.pointSize, font.atlasPadding, font.atlasRenderMode, 2048, 2048, AtlasPopulationMode.Dynamic, false);
            supplement.name = "KCC-Ganpan Supplement SDF";
            if (!supplement.TryAddCharacters(missing.Select(value => (uint)value).ToArray(), out uint[] unfilled))
                throw new InvalidOperationException("Cannot generate these glyphs: " + string.Join(", ", unfilled.Select(value => "U+" + value.ToString("X4"))));
            supplement.atlasPopulationMode = AtlasPopulationMode.Static;
            supplement.atlasTexture.name = "KCC-Ganpan Supplement Atlas";
            supplement.material.name = "KCC-Ganpan Supplement Material";
            supplement.atlasTexture.Apply(false, true);
            AssetDatabase.CreateAsset(supplement, FALLBACK);
            AssetDatabase.AddObjectToAsset(supplement.atlasTexture, supplement);
            AssetDatabase.AddObjectToAsset(supplement.material, supplement);
            EditorUtility.SetDirty(supplement);
        }

        font.fallbackFontAssetTable ??= new System.Collections.Generic.List<TMP_FontAsset>();
        if (!font.fallbackFontAssetTable.Contains(supplement))
        {
            font.fallbackFontAssetTable.Add(supplement);
            EditorUtility.SetDirty(font);
        }
        AssetDatabase.SaveAssets();
        if (required.Any(value => !font.HasCharacter((char)value, true)))
            throw new InvalidOperationException("Missing font glyphs after installing supplement.");

        var scene = EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Single);
        var view = UnityEngine.Object.FindObjectsByType<SessionScreen_view>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
        var settings = new SerializedObject(view);
        var settingsObj = (GameObject)settings.FindProperty("_settingsObj").objectReferenceValue;
        var saveBtn = settingsObj.GetComponentsInChildren<Button>(true).Single(button => button.name == "Save Settings");
        Button languageBtn = (Button)settings.FindProperty("_languageBtn").objectReferenceValue;
        if (languageBtn == null)
        {
            var languageObj = UnityEngine.Object.Instantiate(saveBtn.gameObject, saveBtn.transform.parent, false);
            languageObj.name = "Language";
            languageObj.transform.SetSiblingIndex(saveBtn.transform.GetSiblingIndex());
            languageBtn = languageObj.GetComponent<Button>();
            languageBtn.onClick = new Button.ButtonClickedEvent();
            var rect = (RectTransform)languageObj.transform;
            rect.anchoredPosition = new Vector2(0, -115);
            rect.sizeDelta = new Vector2(650, 52);
            languageBtn.GetComponentInChildren<TMP_Text>(true).text = "Language   English";
        }
        settings.FindProperty("_languageBtn").objectReferenceValue = languageBtn;
        settings.FindProperty("_bilingualFont").objectReferenceValue = font;
        settings.ApplyModifiedPropertiesWithoutUndo();

        var input = (TMP_InputField)settings.FindProperty("_codeInput").objectReferenceValue;
        input.fontAsset = font;
        EditorUtility.SetDirty(input);
        var labels = view.GetComponentsInChildren<TMP_Text>(true);
        foreach (var label in labels)
        {
            label.font = font;
            label.fontSharedMaterial = font.material;
            EditorUtility.SetDirty(label);
        }

        var match = UnityEngine.Object.FindObjectsByType<CrowdMatch>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
        var matchSettings = new SerializedObject(match);
        matchSettings.FindProperty("_searchMarkerFont").objectReferenceValue = font;
        matchSettings.ApplyModifiedPropertiesWithoutUndo();

        if (!view.ReferencesValid()) throw new InvalidOperationException("Session UI references are incomplete.");
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save InGame.");

        VerifyPreview(view, font);
        File.WriteAllText("Temp/InGameLanguage.result", $"PASS: {labels.Length} TMP objects, {required.Length} glyphs, both UI locales.");
    }

    private static void VerifyPreview(SessionScreen_view sourceView, TMP_FontAsset font)
    {
        const BindingFlags INSTANCE_PRIVATE = BindingFlags.Instance | BindingFlags.NonPublic;
        var preview = UnityEngine.Object.Instantiate(sourceView.gameObject);
        try
        {
            var view = preview.GetComponent<SessionScreen_view>();
            var settings = new SerializedObject(view);
            var button = (Button)settings.FindProperty("_languageBtn").objectReferenceValue;
            var language = typeof(SessionScreen_view).Assembly.GetType("DeadCrowd.UI.SessionLanguage", true);
            var localeProperty = language.GetProperty("Korean", BindingFlags.Static | BindingFlags.Public);
            typeof(SessionScreen_view).GetMethod("Awake", INSTANCE_PRIVATE).Invoke(view, null);
            foreach (bool korean in new[] { true, false })
            {
                localeProperty.SetValue(null, korean);
                typeof(SessionScreen_view).GetMethod("ApplyLanguage", INSTANCE_PRIVATE).Invoke(view, null);
                var model = new SessionModel_model
                {
                    Korean = korean, Screen = SESSION_SCREEN.TITLE, SettingsOpen = true,
                    HasLocalPlayer = true, LocalAlive = true, SearchEnabled = true, HealthValue = 100, MaxHealth = 100,
                    HealthRatio = 1, AliveCount = 3, RoundSeconds = 252, SearchScore = 4, LeadSearchScore = 6,
                    Reloading = true, ReloadSeconds = 1.5f, ExposurePercent = 42, ExposureRatio = .42f,
                    Spectator = korean ? "E 길게 눌러 수색    2회 남음" : "Hold E to search    2 left"
                };
                view.Show(model);
                string expected = korean ? "언어   한국어" : "Language   English";
                if (button.GetComponentInChildren<TMP_Text>(true).text != expected) throw new InvalidOperationException("Locale selection label mismatch.");
                VerifyText(view, font);
                model.Screen = SESSION_SCREEN.PLAYING;
                model.SettingsOpen = false;
                view.Show(model);
                VerifyText(view, font);
                string time = korean ? "남은 시간" : "TIME";
                if (!view.GetComponentsInChildren<TMP_Text>().Any(text => text.text == time))
                    throw new InvalidOperationException("HUD locale label mismatch.");
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(preview); }
    }

    private static void VerifyText(SessionScreen_view view, TMP_FontAsset font)
    {
        Canvas.ForceUpdateCanvases();
        foreach (var text in view.GetComponentsInChildren<TextMeshProUGUI>())
        {
            if (text.font != font) throw new InvalidOperationException("Font is not assigned: " + text.name);
            foreach (char character in text.text)
                if (!char.IsWhiteSpace(character) && character != '\u200B' && !font.HasCharacter(character, true))
                    throw new InvalidOperationException("Unsupported glyph in " + text.text);
            text.ForceMeshUpdate();
            if (text.isTextOverflowing)
                Debug.LogWarning($"[Language preview] Overflow in {text.transform.parent.name}/{text.name}: {text.text}");
        }
    }
}
