using System;
using System.Collections.Generic;
using DeadCrowd.Prototype;
using UnityEngine;
using UnityEngine.EventSystems;
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
        [SerializeField] private InGameHud_view _inGameHud;
        [SerializeField] private TMP_FontAsset _bilingualFont;
        [SerializeField] private Button _languageBtn;
        [SerializeField] private GameObject _startupGuideObj;
        [SerializeField] private Button _startupGuideConfirmBtn;
        [SerializeField] private TMP_Text _startupGuideBodyTxt;
        [SerializeField] private GameObject _publicRoomsObj;
        [SerializeField] private Button[] _roomBtns;
        [SerializeField] private TMP_Text _roomListTxt;
        [SerializeField] private TMP_Text[] _roomModeTexts;
        [SerializeField] private TMP_Text[] _roomCountTexts;
        [SerializeField] private bool _frontEnd;
        [SerializeField] private TMP_Text _lobbyModeTxt;
        [SerializeField] private TMP_Text _lobbyVisibilityTxt;
        [SerializeField] private TMP_Text _lobbyCountTxt;
        [SerializeField] private TMP_Text[] _participantNames;
        [SerializeField] private TMP_Text[] _participantStates;
        private TMP_Text[] _roomLabels;
        private string[] _roomCodes;
        private readonly List<(TMP_Text Target, string English, string Korean)> LOCALIZED_TEXTS = new List<(TMP_Text, string, string)>();
        private TMP_Text _languageTxt;
        private TextMeshProUGUI _pistolImpactTxt;
        private static bool s_startupGuideDismissed;

        public event Action<SESSION_ACTION> Requested;
        public event Action<string> PublicRoomRequested;
        public event Action LanguageChanged;
        public bool Korean => _languageBtn != null && SessionLanguage.Korean;
        public bool StartupGuideOpen => _startupGuideObj != null && _startupGuideObj.activeInHierarchy;
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
            if (_startupGuideObj != null)
            {
                _startupGuideObj.SetActive(!s_startupGuideDismissed);
                _startupGuideConfirmBtn.onClick.AddListener(OnStartupGuideConfirmActioned);
            }
            foreach (SessionActionBinding binding in _bindings)
            {
                SESSION_ACTION action = binding.Action;
                binding.TargetBtn.onClick.AddListener(() => Requested?.Invoke(action));
            }
            if (_roomBtns != null && _roomBtns.Length > 0)
            {
                _roomLabels = new TMP_Text[_roomBtns.Length];
                _roomCodes = new string[_roomBtns.Length];
                for (int i = 0; i < _roomBtns.Length; i++)
                {
                    int index = i;
                    if (!_frontEnd) _roomLabels[i] = _roomBtns[i].GetComponentInChildren<TMP_Text>(true);
                    _roomBtns[i].onClick.AddListener(() => PublicRoomRequested?.Invoke(_roomCodes[index]));
                }
            }
            if (_languageBtn == null || _bilingualFont == null) return;
            SessionLanguage.Load();
            _languageTxt = _languageBtn.GetComponentInChildren<TMP_Text>(true);
            foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
            {
                text.font = _bilingualFont;
                text.fontSharedMaterial = _bilingualFont.material;
                if (SessionLanguage.TryTranslate(text.text, out string korean)) LOCALIZED_TEXTS.Add((text, text.text, korean));
            }
            _languageBtn.onClick.AddListener(OnLanguageActioned);
            ApplyLanguage();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStartupGuide() => s_startupGuideDismissed = false;

        private void Start()
        {
            if (StartupGuideOpen && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_startupGuideConfirmBtn.gameObject);
        }

        private void OnStartupGuideConfirmActioned()
        {
            s_startupGuideDismissed = true;
            _startupGuideObj.SetActive(false);
        }

        private void OnLanguageActioned()
        {
            SessionLanguage.Toggle();
            ApplyLanguage();
            LanguageChanged?.Invoke();
        }

        private void ApplyLanguage()
        {
            foreach (var text in LOCALIZED_TEXTS) text.Target.text = Korean ? text.Korean : text.English;
            _languageTxt.text = Korean ? "언어   한국어" : "Language   English";
            if (_startupGuideBodyTxt != null)
            {
                _startupGuideBodyTxt.text = Korean
                    ? $"<color=#F9C14F>수색</color>\n수색 지점 앞에서 E를 {CrowdMatch.SEARCH_SECONDS:0.#}초 누르면 1점을 얻습니다.\n\n" +
                      "<color=#F9C14F>노출</color>\n공격하거나 질주하면 노출이 오릅니다. 100%가 되면 좀비에게 추격당할 수 있습니다.\n\n" +
                      "<color=#F9C14F>승리</color>\n마지막 생존자가 승리합니다. 시간 종료 시 여러 명이 살아 있으면, 생존자 중 수색 점수 단독 1위가 승리합니다."
                    : $"<color=#F9C14F>SEARCH</color>\nHold E in front of a search site for {CrowdMatch.SEARCH_SECONDS:0.#} seconds to earn 1 point.\n\n" +
                      "<color=#F9C14F>EXPOSURE</color>\nAttacking or sprinting raises exposure. At 100%, zombies can chase you.\n\n" +
                      "<color=#F9C14F>WIN</color>\nBe the last survivor. If several players survive until time runs out, the sole top scorer among them wins.";
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

        public void ShowPistolImpact(Vector2? viewport)
        {
            if (!viewport.HasValue)
            {
                if (_pistolImpactTxt != null && _pistolImpactTxt.gameObject.activeSelf) _pistolImpactTxt.gameObject.SetActive(false);
                return;
            }

            if (_pistolImpactTxt == null)
            {
                TMP_Text crosshairTxt = _texts[(int)SESSION_TEXT.CROSSHAIR];
                _pistolImpactTxt = new GameObject("Pistol Impact Reticle", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                _pistolImpactTxt.transform.SetParent(crosshairTxt.transform.parent, false);
                _pistolImpactTxt.rectTransform.sizeDelta = new Vector2(40f, 40f);
                _pistolImpactTxt.font = crosshairTxt.font;
                _pistolImpactTxt.fontSize = 28f;
                _pistolImpactTxt.alignment = TextAlignmentOptions.Midline;
                _pistolImpactTxt.text = "o";
                _pistolImpactTxt.raycastTarget = false;
            }

            RectTransform impactRectTrans = _pistolImpactTxt.rectTransform;
            impactRectTrans.anchorMin = viewport.Value;
            impactRectTrans.anchorMax = viewport.Value;
            impactRectTrans.anchoredPosition = Vector2.zero;
            if (!_pistolImpactTxt.gameObject.activeSelf) _pistolImpactTxt.gameObject.SetActive(true);
        }

        public void Show(SessionModel_model model)
        {
            bool gameplay = model.Screen == SESSION_SCREEN.COUNTDOWN || model.Screen == SESSION_SCREEN.PLAYING;
            int screenIndex = (int)(gameplay ? SESSION_SCREEN.PLAYING : model.Screen == SESSION_SCREEN.LOADING ? SESSION_SCREEN.LOBBY : model.Screen);
            for (int i = 0; i < _screens.Length; i++) if (_screens[i] != null && _screens[i].activeSelf != (i == screenIndex)) _screens[i].SetActive(i == screenIndex);
            if (_publicRoomsObj != null)
            {
                bool browsing = model.Screen == SESSION_SCREEN.PUBLIC_ROOMS;
                _publicRoomsObj.SetActive(browsing);
                if (browsing && _roomLabels != null) ShowPublicRooms(model);
            }
            _backdropObj.SetActive(!gameplay);
            if (_countdownObj != null) _countdownObj.SetActive(model.Screen == SESSION_SCREEN.COUNTDOWN);
            if (_menuObj != null) _menuObj.SetActive(model.MenuOpen && gameplay && !model.SettingsOpen);
            _settingsObj.SetActive(model.SettingsOpen);
            _texts[(int)SESSION_TEXT.STATUS].gameObject.SetActive(!gameplay && model.Screen != SESSION_SCREEN.RESULT);
            SetText(SESSION_TEXT.STATUS, model.Status);
            SetText(SESSION_TEXT.CODE, model.Practice ? model.Text("Solo Practice - Offline", "혼자 연습 (오프라인)") : model.Text("Invite Code  ", "초대 코드  ") + model.Code);
            SetText(SESSION_TEXT.SLOTS, model.Slots);
            SetText(SESSION_TEXT.COUNTDOWN, model.Countdown + model.Text("\nLook around", "\n주변을 살펴보세요"));
            SetText(SESSION_TEXT.HEALTH, model.Health);
            SetText(SESSION_TEXT.EXPOSURE, model.Exposure);
            SetText(SESSION_TEXT.AMMO, model.Ammo);
            SetText(SESSION_TEXT.TIME, model.Time);
            SetText(SESSION_TEXT.CROSSHAIR, model.Crosshair);
            SetText(SESSION_TEXT.SPECTATOR, model.Spectator);
            SetText(SESSION_TEXT.RESULT, model.Result);
            SetText(SESSION_TEXT.HOST_NOTICE, model.Practice ? model.Text("Preparing practice. No other players required.", "혼자 연습을 준비하고 있습니다.") :
                model.Screen == SESSION_SCREEN.LOADING ? model.Text("Loading round. Waiting for all players...", "모든 플레이어가 경기를 불러올 때까지 기다려 주세요.") :
                model.Host ? model.Text("Everyone must be ready before starting.", "전원이 준비하면 시작할 수 있습니다.") :
                model.Text("Waiting for the host to start.", "호스트가 경기를 시작할 때까지 기다려 주세요."));
            if (_lobbyModeTxt != null && screenIndex == (int)SESSION_SCREEN.LOBBY) ShowLobby(model);
            if (!_frontEnd)
            {
                _texts[(int)SESSION_TEXT.CROSSHAIR].gameObject.SetActive(model.ShowCrosshair);
                if (_inGameHud == null)
                {
                    _healthImg.fillAmount = model.HealthRatio;
                    _exposureImg.fillAmount = model.ExposureRatio;
                    _healthImg.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(model.HealthRatio), 1f);
                    _exposureImg.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(model.ExposureRatio), 1f);
                    _exposureImg.color = model.Exposed ? new Color(1f, 0.32f, 0.21f) : new Color(0.83f, 0.71f, 0.35f);
                }
            }
            if (gameplay && _inGameHud != null) _inGameHud.Show(model);
            foreach (SessionActionBinding binding in _bindings)
            {
                if (binding.Action == SESSION_ACTION.READY || binding.Action == SESSION_ACTION.COPY_CODE) binding.TargetBtn.gameObject.SetActive(!model.Practice);
                bool interactable = !model.Busy;
                if (binding.Action == SESSION_ACTION.CANCEL) interactable = model.Busy;
                if (binding.Action == SESSION_ACTION.READY) interactable = model.CanReady;
                if (binding.Action == SESSION_ACTION.START) interactable = model.CanStart;
                if (_lobbyModeTxt != null && binding.Action == SESSION_ACTION.START) binding.TargetBtn.gameObject.SetActive(model.Host);
                if (binding.Action == SESSION_ACTION.RETURN_LOBBY) interactable = model.Host;
                if (binding.Action == SESSION_ACTION.ROOM_PREVIOUS) interactable &= model.RoomPage > 0;
                if (binding.Action == SESSION_ACTION.ROOM_NEXT) interactable &= model.RoomPage + 1 < model.RoomPageCount;
                if (binding.Action == SESSION_ACTION.ROOM_RETRY) binding.TargetBtn.gameObject.SetActive(model.RoomListFailed);
                binding.TargetBtn.interactable = interactable;
                if (_frontEnd && binding.Action is SESSION_ACTION.DUEL or SESSION_ACTION.FFA or SESSION_ACTION.SELECT_PUBLIC or SESSION_ACTION.SELECT_INVITE)
                {
                    bool selected = binding.Action switch
                    {
                        SESSION_ACTION.DUEL => model.Capacity == 2,
                        SESSION_ACTION.FFA => model.Capacity == 4,
                        SESSION_ACTION.SELECT_PUBLIC => model.CreatePublic,
                        _ => !model.CreatePublic
                    };
                    binding.TargetBtn.image.color = selected ? new Color32(112, 34, 29, 255) : new Color32(31, 32, 35, 255);
                    if (binding.TargetBtn.TryGetComponent(out Outline outline)) outline.enabled = selected;
                }
                else if (binding.Action == SESSION_ACTION.DUEL || binding.Action == SESSION_ACTION.FFA)
                    binding.TargetBtn.GetComponent<Image>().color = model.Capacity == (binding.Action == SESSION_ACTION.DUEL ? 2 : 4) ? new Color(0.13f, 0.39f, 0.37f) : new Color(0.12f, 0.16f, 0.19f);
                if (binding.Action == SESSION_ACTION.READY) binding.TargetBtn.GetComponentInChildren<TMP_Text>(true).text = model.Ready ? model.Text("Cancel Ready", "준비 취소") : model.Text("Ready", _lobbyModeTxt != null ? "준비" : "준비 완료");
                if (binding.Action == SESSION_ACTION.START) binding.TargetBtn.GetComponentInChildren<TMP_Text>(true).text = model.Practice ? model.Text("Start Practice", "연습 시작") : _lobbyModeTxt != null ? model.Text("Start Match", "게임 시작") : model.Text("Start Match (Host)", "경기 시작 (호스트)");
                if (binding.Action == SESSION_ACTION.LEAVE) binding.TargetBtn.GetComponentInChildren<TMP_Text>(true).text = model.Practice ? model.Text("Back to Title", "타이틀로") :
                    model.Host ? model.Text("Close Room", "방 닫기") : model.Text("Leave Room", "방 나가기");
                if (binding.Action == SESSION_ACTION.RETURN_LOBBY) binding.TargetBtn.GetComponentInChildren<TMP_Text>(true).text = model.Practice ?
                    model.Text("Play Again", "다시 연습") : model.Text("Return to Lobby (Host)", "대기실로 (호스트)");
            }
        }

        private void ShowLobby(SessionModel_model model)
        {
            bool loading = model.Screen == SESSION_SCREEN.LOADING;
            _lobbyModeTxt.text = model.Practice ? model.Text("Solo Practice", "혼자 연습")
                : model.Capacity == 2 ? model.Text("1 vs 1", "1 대 1") : model.Text("4-player Free For All", "4인 개인전");
            _lobbyVisibilityTxt.text = model.Practice ? model.Text("Offline", "오프라인")
                : model.PublicSession ? model.Text("Public Room", "공개 방") : model.Text("Invite Only", "초대 전용");
            _lobbyCountTxt.text = model.Text("Players", "참가자") + $"  {model.ConnectedPlayers} / {model.Capacity}";
            SetText(SESSION_TEXT.CODE, model.Code);
            _texts[(int)SESSION_TEXT.CODE].transform.parent.gameObject.SetActive(!model.Practice);
            for (int i = 0; i < _participantNames.Length; i++)
            {
                TMP_Text nameTxt = _participantNames[i];
                nameTxt.transform.parent.gameObject.SetActive(i < model.Capacity);
                LobbyParticipant participant = model.Participants[i];
                nameTxt.text = participant.Connected ? $"Player{i + 1:00}" + (participant.Host ? model.Text("  Host", "  호스트") : "") +
                    (participant.Local ? model.Text("  (You)", "  (나)") : "") : model.Text("Open Slot", "빈 자리");
                TMP_Text stateTxt = _participantStates[i];
                bool complete = loading ? participant.Loaded : participant.Ready;
                stateTxt.text = !participant.Connected ? "" : loading ? (complete ? model.Text("Loaded", "불러오기 완료") : model.Text("Loading", "불러오는 중"))
                    : model.Practice ? "" : complete ? model.Text("Ready", "준비 완료") : model.Text("Not Ready", "준비 전");
                stateTxt.color = complete ? new Color32(140, 214, 157, 255) : new Color32(183, 178, 168, 255);
            }
        }

        private void ShowPublicRooms(SessionModel_model model)
        {
            _roomListTxt.text = model.RoomListFailed ? model.Text("Room list unavailable", "방 목록을 불러올 수 없습니다")
                : !model.RoomListReady ? model.Text("Loading rooms...", "방 목록 불러오는 중")
                : model.Rooms.Count == 0 ? model.Text("No open rooms", "참가 가능한 공개 방이 없습니다")
                : model.Korean ? $"공개 방 {model.Rooms.Count}개   |   {model.RoomPage + 1} / {model.RoomPageCount}"
                : $"{model.Rooms.Count} open rooms   |   {model.RoomPage + 1} / {model.RoomPageCount}";
            int start = model.RoomPage * SessionModel_model.ROOMS_PER_PAGE;
            for (int i = 0; i < _roomBtns.Length; i++)
            {
                int roomIndex = start + i;
                bool available = model.RoomListReady && roomIndex < model.Rooms.Count;
                _roomBtns[i].gameObject.SetActive(available);
                _roomBtns[i].interactable = !model.Busy;
                if (!available) { _roomCodes[i] = null; continue; }
                var room = model.Rooms[roomIndex];
                _roomCodes[i] = room.Code;
                string mode = room.Capacity == 2 ? model.Text("1 vs 1", "1 대 1") : model.Text("Free For All", "개인전");
                if (_frontEnd)
                {
                    _roomModeTexts[i].text = room.Capacity == 2 ? mode : model.Text("4-player Free For All", "4인 개인전");
                    _roomCountTexts[i].text = $"{room.Count} / {room.Capacity}";
                }
                else _roomLabels[i].text = model.Korean ? $"{mode}    {room.Count}/{room.Capacity}명    #{room.Code}" : $"{mode}    {room.Count}/{room.Capacity} players    #{room.Code}";
            }
        }

        public bool ReferencesValid()
        {
            if (_startupGuideObj != null && (_startupGuideConfirmBtn == null || _startupGuideBodyTxt == null)) return false;
            if (_screens == null || _screens.Length != 8 || _texts == null || _texts.Length != 12 || _bindings == null || _bindings.Length == 0) return false;
            for (int i = 0; i < _texts.Length; i++)
            {
                var field = (SESSION_TEXT)i;
                if (_frontEnd && field != SESSION_TEXT.STATUS) continue;
                if (_lobbyModeTxt != null && field == SESSION_TEXT.SLOTS) continue;
                if (_inGameHud != null && field is SESSION_TEXT.HEALTH or SESSION_TEXT.EXPOSURE or SESSION_TEXT.AMMO or SESSION_TEXT.TIME or SESSION_TEXT.SPECTATOR) continue;
                if (_texts[i] == null) return false;
            }
            foreach (SessionActionBinding binding in _bindings) if (binding.TargetBtn == null) return false;
            if (_publicRoomsObj != null && (_roomListTxt == null || _roomBtns == null || _roomBtns.Length != SessionModel_model.ROOMS_PER_PAGE || Array.Exists(_roomBtns, button => button == null))) return false;
            if (_frontEnd && (_roomModeTexts == null || _roomCountTexts == null || _roomModeTexts.Length != _roomBtns.Length ||
                _roomCountTexts.Length != _roomBtns.Length || Array.Exists(_roomModeTexts, text => text == null) || Array.Exists(_roomCountTexts, text => text == null))) return false;
            return (_frontEnd || _inGameHud != null || _healthImg != null && _exposureImg != null) &&
                _codeInput != null && _sensitivitySlider != null && _volumeSlider != null;
        }
    }
}
