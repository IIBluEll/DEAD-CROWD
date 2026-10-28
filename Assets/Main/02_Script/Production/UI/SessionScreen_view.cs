using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DeadCrowd.UI
{
    public enum SESSION_TEXT { STATUS, CODE, SLOTS, COUNTDOWN, HEALTH, EXPOSURE, AMMO, TIME, CROSSHAIR, SPECTATOR, RESULT, HOST_NOTICE }

    [Serializable]
    public struct SessionActionBinding
    {
        public SESSION_ACTION Action;
        public Button TargetBtn;
    }

    public sealed class SessionScreen_view : MonoBehaviour
    {
        [SerializeField] private GameObject[] _screens;
        [SerializeField] private TMP_Text[] _texts;
        [SerializeField] private SessionActionBinding[] _bindings;
        [SerializeField] private TMP_InputField _codeInput;
        [SerializeField] private GameObject _backdropObj;
        [SerializeField] private GameObject _countdownObj;
        [SerializeField] private GameObject _menuObj;
        [SerializeField] private GameObject _settingsObj;
        [SerializeField] private Image _healthImg;
        [SerializeField] private Image _exposureImg;
        [SerializeField] private Slider _sensitivitySlider;
        [SerializeField] private Slider _volumeSlider;

        public event Action<SESSION_ACTION> Requested;
        public string InviteCode => _codeInput.text;
        public float Sensitivity => _sensitivitySlider.value;
        public float Volume => _volumeSlider.value;
        public int BindingCount => _bindings != null ? _bindings.Length : 0;

        public void Configure(GameObject[] screens, TMP_Text[] texts, SessionActionBinding[] bindings, TMP_InputField codeInput, GameObject backdrop, GameObject countdown, GameObject menu, GameObject settings, Image health, Image exposure, Slider sensitivity, Slider volume)
        {
            _screens = screens; _texts = texts; _bindings = bindings; _codeInput = codeInput;
            _backdropObj = backdrop; _countdownObj = countdown; _menuObj = menu; _settingsObj = settings;
            _healthImg = health; _exposureImg = exposure; _sensitivitySlider = sensitivity; _volumeSlider = volume;
        }

        private void Awake()
        {
            foreach (SessionActionBinding binding in _bindings)
            {
                SESSION_ACTION action = binding.Action;
                binding.TargetBtn.onClick.AddListener(() => Requested?.Invoke(action));
            }
        }

        public void LoadSettings(float sensitivity, float volume)
        {
            _sensitivitySlider.SetValueWithoutNotify(sensitivity);
            _volumeSlider.SetValueWithoutNotify(volume);
        }

        private void SetText(SESSION_TEXT field, string value)
        {
            TMP_Text text = _texts[(int)field];
            if (text != null && text.text != value) text.text = value;
        }

        public void Show(SessionModel_model model)
        {
            bool gameplay = model.Screen == SESSION_SCREEN.COUNTDOWN || model.Screen == SESSION_SCREEN.PLAYING;
            int screenIndex = (int)(gameplay ? SESSION_SCREEN.PLAYING : model.Screen == SESSION_SCREEN.LOADING ? SESSION_SCREEN.LOBBY : model.Screen);
            for (int i = 0; i < _screens.Length; i++) if (_screens[i] != null && _screens[i].activeSelf != (i == screenIndex)) _screens[i].SetActive(i == screenIndex);
            _backdropObj.SetActive(!gameplay);
            _countdownObj.SetActive(model.Screen == SESSION_SCREEN.COUNTDOWN);
            _menuObj.SetActive(model.MenuOpen && gameplay && !model.SettingsOpen);
            _settingsObj.SetActive(model.SettingsOpen);
            _texts[(int)SESSION_TEXT.STATUS].gameObject.SetActive(!gameplay && model.Screen != SESSION_SCREEN.RESULT);
            SetText(SESSION_TEXT.STATUS, model.Status);
            SetText(SESSION_TEXT.CODE, "Invite Code  " + model.Code);
            SetText(SESSION_TEXT.SLOTS, model.Slots);
            SetText(SESSION_TEXT.COUNTDOWN, model.Countdown + "\nLook around");
            SetText(SESSION_TEXT.HEALTH, model.Health);
            SetText(SESSION_TEXT.EXPOSURE, model.Exposure);
            SetText(SESSION_TEXT.AMMO, model.Ammo);
            SetText(SESSION_TEXT.TIME, model.Time);
            SetText(SESSION_TEXT.CROSSHAIR, model.Crosshair);
            SetText(SESSION_TEXT.SPECTATOR, model.Spectator);
            SetText(SESSION_TEXT.RESULT, model.Result);
            SetText(SESSION_TEXT.HOST_NOTICE, model.Screen == SESSION_SCREEN.LOADING ? "Loading round. Waiting for all players..." : model.Host ? "Everyone must be ready before starting." : "Waiting for the host to start.");
            _texts[(int)SESSION_TEXT.CROSSHAIR].gameObject.SetActive(model.ShowCrosshair);
            _healthImg.fillAmount = model.HealthRatio;
            _exposureImg.fillAmount = model.ExposureRatio;
            _healthImg.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(model.HealthRatio), 1f);
            _exposureImg.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(model.ExposureRatio), 1f);
            _exposureImg.color = model.Exposed ? new Color(1f, 0.32f, 0.21f) : new Color(0.83f, 0.71f, 0.35f);
            foreach (SessionActionBinding binding in _bindings)
            {
                bool interactable = !model.Busy;
                if (binding.Action == SESSION_ACTION.CANCEL) interactable = model.Busy;
                if (binding.Action == SESSION_ACTION.READY) interactable = model.CanReady;
                if (binding.Action == SESSION_ACTION.START) interactable = model.CanStart;
                if (binding.Action == SESSION_ACTION.RETURN_LOBBY) interactable = model.Host;
                binding.TargetBtn.interactable = interactable;
                if (binding.Action == SESSION_ACTION.DUEL || binding.Action == SESSION_ACTION.FFA)
                    binding.TargetBtn.GetComponent<Image>().color = model.Capacity == (binding.Action == SESSION_ACTION.DUEL ? 2 : 4) ? new Color(0.13f, 0.39f, 0.37f) : new Color(0.12f, 0.16f, 0.19f);
                if (binding.Action == SESSION_ACTION.READY) binding.TargetBtn.GetComponentInChildren<TMP_Text>().text = model.Ready ? "Cancel Ready" : "Ready";
                if (binding.Action == SESSION_ACTION.LEAVE) binding.TargetBtn.GetComponentInChildren<TMP_Text>().text = model.Host ? "Close Room" : "Leave Room";
            }
        }

        public bool ReferencesValid()
        {
            if (_screens == null || _screens.Length != 8 || _texts == null || _texts.Length != 12 || _bindings == null || _bindings.Length == 0) return false;
            foreach (TMP_Text text in _texts) if (text == null) return false;
            foreach (SessionActionBinding binding in _bindings) if (binding.TargetBtn == null) return false;
            return _codeInput != null && _healthImg != null && _exposureImg != null && _sensitivitySlider != null && _volumeSlider != null;
        }
    }
}
