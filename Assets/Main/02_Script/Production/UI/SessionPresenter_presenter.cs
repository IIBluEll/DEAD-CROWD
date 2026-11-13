using DeadCrowd.Session;
using UnityEngine;

namespace DeadCrowd.UI
{
    [DefaultExecutionOrder(200)]
    public sealed class SessionPresenter_presenter : MonoBehaviour
    {
        [SerializeField] private CrowdSessionController _session;
        [SerializeField] private SessionScreen_view _view;
        private readonly SessionModel_model MODEL = new SessionModel_model();
        private float _nextRefresh;
        private SESSION_SCREEN _connectionReturnScreen = SESSION_SCREEN.TITLE;
        private bool _restoreConnectionScreen;
        [SerializeField] private bool _frontEnd;
        public SessionModel_model Model => MODEL;

        public void Configure(CrowdSessionController session, SessionScreen_view view) { _session = session; _view = view; }
        private void OnEnable() { _view.Requested += OnSessionActioned; _view.PublicRoomRequested += OnPublicRoomActioned; _view.LanguageChanged += Refresh; }
        private void OnDisable() { _view.Requested -= OnSessionActioned; _view.PublicRoomRequested -= OnPublicRoomActioned; _view.LanguageChanged -= Refresh; }
        private void Start() { _view.LoadSettings(_session.Runtime.MouseSensitivity, AudioListener.volume); Refresh(); }
        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.1f;
            Refresh();
        }

        private void LateUpdate() { if (!_frontEnd) _view.ShowPistolImpact(_session.Runtime.PistolImpactViewport); }

        private void Refresh()
        {
            MODEL.Korean = _view.Korean;
            _session.ReadModel(MODEL);
            if (_frontEnd && MODEL.Screen == SESSION_SCREEN.TITLE && _session.Runtime.Status == CrowdConnectionService.INITIAL_STATUS) MODEL.Status = "";
            MODEL.RoomPage = Mathf.Clamp(MODEL.RoomPage, 0, MODEL.RoomPageCount - 1);
            if (_restoreConnectionScreen && !MODEL.Busy && _session.Runtime.Runner == null)
            {
                MODEL.Screen = _connectionReturnScreen;
                _restoreConnectionScreen = false;
            }
            if (!MODEL.MenuOpen) MODEL.SettingsOpen = false;
            _view.Show(MODEL);
        }

        private void OnPublicRoomActioned(string code)
        {
            if (_session.Runtime.Busy || MODEL.Screen != SESSION_SCREEN.PUBLIC_ROOMS) return;
            _ = _session.JoinRoom_async(code);
            Refresh();
        }

        private void OnSessionActioned(SESSION_ACTION action)
        {
            if (_view.StartupGuideOpen) return;
            if (_session.Runtime.Busy && action != SESSION_ACTION.CANCEL) return;
            switch (action)
            {
                case SESSION_ACTION.SHOW_CREATE:
                case SESSION_ACTION.SHOW_JOIN:
                    if (MODEL.Screen == SESSION_SCREEN.PUBLIC_ROOMS) _session.CloseRoomBrowser();
                    MODEL.Screen = action == SESSION_ACTION.SHOW_CREATE ? SESSION_SCREEN.CREATE : SESSION_SCREEN.JOIN;
                    break;
                case SESSION_ACTION.BACK:
                    if (MODEL.Screen == SESSION_SCREEN.PUBLIC_ROOMS) _session.CloseRoomBrowser();
                    MODEL.Screen = SESSION_SCREEN.TITLE;
                    break;
                case SESSION_ACTION.SHOW_PUBLIC_ROOMS:
                    MODEL.RoomPage = 0;
                    _session.OpenRoomBrowser();
                    break;
                case SESSION_ACTION.ROOM_PREVIOUS: MODEL.RoomPage--; break;
                case SESSION_ACTION.ROOM_NEXT: MODEL.RoomPage++; break;
                case SESSION_ACTION.ROOM_RETRY: _session.OpenRoomBrowser(); break;
                case SESSION_ACTION.DUEL: MODEL.Capacity = 2; break;
                case SESSION_ACTION.FFA: MODEL.Capacity = 4; break;
                case SESSION_ACTION.SELECT_PUBLIC: MODEL.CreatePublic = true; break;
                case SESSION_ACTION.SELECT_INVITE: MODEL.CreatePublic = false; break;
                case SESSION_ACTION.SOLO:
                    _connectionReturnScreen = SESSION_SCREEN.TITLE;
                    _ = _session.StartPractice_async();
                    break;
                case SESSION_ACTION.CREATE:
                case SESSION_ACTION.CREATE_PUBLIC:
                    _connectionReturnScreen = SESSION_SCREEN.CREATE;
                    _restoreConnectionScreen = _frontEnd;
                    _ = _session.CreateRoom_async(MODEL.Capacity, action == SESSION_ACTION.CREATE_PUBLIC || _frontEnd && MODEL.CreatePublic);
                    break;
                case SESSION_ACTION.QUICK_DUEL:
                case SESSION_ACTION.QUICK_FFA:
                    _connectionReturnScreen = SESSION_SCREEN.PUBLIC_ROOMS;
                    _ = _session.QuickMatch_async(action == SESSION_ACTION.QUICK_DUEL ? 2 : 4);
                    break;
                case SESSION_ACTION.JOIN:
                    _connectionReturnScreen = SESSION_SCREEN.JOIN;
                    _restoreConnectionScreen = _frontEnd;
                    _ = _session.JoinRoom_async(_view.InviteCode);
                    break;
                case SESSION_ACTION.CANCEL:
                    _restoreConnectionScreen = !_session.Runtime.Connection.ShowRoomBrowser;
                    _session.CancelConnection();
                    break;
                case SESSION_ACTION.READY: _session.Ready(); break;
                case SESSION_ACTION.START: _session.StartRound(); break;
                case SESSION_ACTION.COPY_CODE: GUIUtility.systemCopyBuffer = MODEL.Code; break;
                case SESSION_ACTION.LEAVE: MODEL.SettingsOpen = false; _ = _session.LeaveRoom_async(); break;
                case SESSION_ACTION.RETURN_LOBBY: _session.ReturnToLobby(); break;
                case SESSION_ACTION.RESUME: _session.SetMenuOpen(false); break;
                case SESSION_ACTION.SETTINGS:
                    _view.LoadSettings(_session.Runtime.MouseSensitivity, AudioListener.volume);
                    MODEL.SettingsOpen = true;
                    _session.SetMenuOpen(true);
                    break;
                case SESSION_ACTION.SAVE_SETTINGS:
                    _session.Runtime.SaveSettings(_view.Sensitivity, _view.Volume);
                    MODEL.SettingsOpen = false;
                    _session.SetMenuOpen(false);
                    break;
                case SESSION_ACTION.QUIT: Application.Quit(); break;
            }
            Refresh();
        }
    }
}
