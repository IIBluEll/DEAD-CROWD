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
        private bool _restoreCancelledScreen;
        public SessionModel_model Model => MODEL;

        public void Configure(CrowdSessionController session, SessionScreen_view view) { _session = session; _view = view; }
        private void OnEnable() { _view.Requested += OnSessionActioned; }
        private void OnDisable() { _view.Requested -= OnSessionActioned; }
        private void Start() { _view.LoadSettings(_session.Runtime.MouseSensitivity, AudioListener.volume); Refresh(); }
        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.1f;
            Refresh();
        }

        private void Refresh()
        {
            _session.ReadModel(MODEL);
            if (_restoreCancelledScreen && !MODEL.Busy && _session.Runtime.Runner == null)
            {
                MODEL.Screen = _connectionReturnScreen;
                _restoreCancelledScreen = false;
            }
            if (!MODEL.MenuOpen) MODEL.SettingsOpen = false;
            _view.Show(MODEL);
        }

        private void OnSessionActioned(SESSION_ACTION action)
        {
            if (_session.Runtime.Busy && action != SESSION_ACTION.CANCEL) return;
            switch (action)
            {
                case SESSION_ACTION.SHOW_CREATE: MODEL.Screen = SESSION_SCREEN.CREATE; break;
                case SESSION_ACTION.SHOW_JOIN: MODEL.Screen = SESSION_SCREEN.JOIN; break;
                case SESSION_ACTION.BACK: MODEL.Screen = SESSION_SCREEN.TITLE; break;
                case SESSION_ACTION.DUEL: MODEL.Capacity = 2; break;
                case SESSION_ACTION.FFA: MODEL.Capacity = 4; break;
                case SESSION_ACTION.CREATE:
                    _connectionReturnScreen = SESSION_SCREEN.CREATE;
                    _ = _session.CreateRoom_async(MODEL.Capacity);
                    break;
                case SESSION_ACTION.JOIN:
                    _connectionReturnScreen = SESSION_SCREEN.JOIN;
                    _ = _session.JoinRoom_async(_view.InviteCode);
                    break;
                case SESSION_ACTION.CANCEL:
                    _restoreCancelledScreen = true;
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
