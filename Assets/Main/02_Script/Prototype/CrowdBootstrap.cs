using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using DeadCrowd.Control;
using DeadCrowd.Session;

namespace DeadCrowd.Prototype
{
    public sealed class CrowdBootstrap : MonoBehaviour
    {
        [SerializeField] private Camera _viewCamera;
        [SerializeField] private bool _showPrototypeGui = true;
        private CrowdConnectionService _connection;
        private readonly CrowdLocalInput _controls = new CrowdLocalInput();
        private readonly CrowdCameraController CAMERA = new CrowdCameraController();
        private NetworkRunner _observedRunner;
        private string _code = "";
        private int _capacity = 2;
        private int _roundId = -1;
        private bool _primary;
        private bool _captureMode;
        private GUIStyle _titleStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _smallStyle;

        public CrowdConnectionService Connection => _connection;
        public NetworkRunner Runner => _connection?.Runner;
        public CrowdMatch Match => _connection?.Match;
        public string Status => _connection?.Status ?? "";
        public bool Busy => _connection != null && _connection.Busy;
        public string InviteCode => _connection?.InviteCode ?? "";
        public bool MenuOpen => _controls.MenuOpen;
        public bool LocalAim => _controls.Aiming;
        public float MouseSensitivity => _controls.Sensitivity;
        public float AimYaw => _controls.Yaw;

        public void UseCanvasUI() { _showPrototypeGui = false; }
        public void SetMenuOpen(bool open) { _controls.MenuOpen = open; }
        public void CancelConnection() { _connection.CancelConnection(); }
        public void SaveSettings(float sensitivity, float volume)
        {
            _controls.Sensitivity = Mathf.Clamp(sensitivity, 0.03f, 0.4f);
            AudioListener.volume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat("Crowd.MouseSensitivity", _controls.Sensitivity);
            PlayerPrefs.SetFloat("Crowd.Volume", AudioListener.volume);
            PlayerPrefs.Save();
        }

        public void Configure(Camera viewCamera) { _viewCamera = viewCamera; }
        public void SetCaptureMode(bool capture) { _captureMode = capture; }

        private void Awake()
        {
            foreach (CrowdBootstrap other in FindObjectsByType<CrowdBootstrap>(FindObjectsSortMode.None))
                if (other != this && other._primary) { Destroy(gameObject); return; }
            _primary = true;
            _connection = new CrowdConnectionService(ReadInput);
            DontDestroyOnLoad(gameObject);
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            _controls.Sensitivity = PlayerPrefs.GetFloat("Crowd.MouseSensitivity", 0.12f);
            AudioListener.volume = PlayerPrefs.GetFloat("Crowd.Volume", 0.8f);
        }

        private void Update()
        {
            if (!_primary) return;
            _connection.Tick();
            if (!ReferenceEquals(_observedRunner, Runner))
            {
                _observedRunner = Runner;
                _controls.Reset();
                _roundId = -1;
                _controls.MenuOpen = false;
            }
            CrowdMatch match = Match;
            CrowdActor local = match != null ? match.LocalActor() : null;
            if (match != null && local != null && match.RoundId != _roundId)
            {
                _roundId = match.RoundId;
                _controls.Reset(local);
            }
            _controls.Poll(match, local);
        }

        private void LateUpdate() { if (_primary) CAMERA.Follow(_viewCamera, Match, _controls); }

        public Task Connect_async(GameMode mode, string codeOverride = null, int capacity = 2) => _connection.Connect_async(mode, codeOverride ?? (mode == GameMode.Client ? _code : null), capacity);
        public Task Leave_async(string message = "You left the room.") => _connection.Leave_async(message);
        public void OverrideTestInput(CrowdInput input) { _controls.Override(input); }
        private CrowdInput ReadInput() => _controls.Read(Match != null ? Match.RoundId : -1);

        private void OnGUI()
        {
            if (!_showPrototypeGui || !_primary || Application.isBatchMode && !_captureMode) return;
            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 36, fontStyle = FontStyle.Bold };
                _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = true };
                _smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            }
            GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / 1280f, Screen.height / 720f, 1f));
            GUI.skin.button.fontSize = 18;
            GUI.skin.button.fixedHeight = 38;
            GUI.skin.textField.fontSize = 24;
            CrowdMatch match = Match;
            if (match == null)
            {
                DrawPanel(new Rect(410, 105, 460, 510));
                GUILayout.Label("DEAD CROWD", _titleStyle);
                GUILayout.Label("Crowd stealth / online combat prototype", _smallStyle);
                GUILayout.Space(20);
                GUILayout.Label(Status, _labelStyle);
                GUILayout.Space(15);
                GUI.enabled = !Busy;
                _capacity = GUILayout.Toolbar(_capacity == 2 ? 0 : 1, new[] { "1 vs 1", "4 PLAYER FFA" }) == 0 ? 2 : 4;
                if (GUILayout.Button("CREATE INVITE ROOM")) _ = Connect_async(GameMode.Host, capacity: _capacity);
                GUILayout.Space(12);
                _code = GUILayout.TextField(_code, 8).ToUpperInvariant();
                if (GUILayout.Button("JOIN WITH CODE")) _ = Connect_async(GameMode.Client, capacity: _capacity);
                GUILayout.Space(12);
                if (GUILayout.Button("SOLO PRACTICE (OFFLINE)")) _ = Connect_async(GameMode.Single);
                GUI.enabled = true;
                if (Busy && GUILayout.Button("CANCEL")) CancelConnection();
                GUILayout.Label("WASD move  |  Shift sprint  |  Mouse look\nLMB melee  |  RMB + LMB pistol  |  R reload\nEsc menu  |  Spectator A / D", _smallStyle);
                GUILayout.EndArea();
                return;
            }
            CrowdActor local = match.LocalActor();
            if (match.State == ROUND_STATE.LOADING)
            {
                DrawPanel(new Rect(410, 190, 460, 300));
                GUILayout.Label("LOADING ROUND", _titleStyle);
                GUILayout.Label($"{match.LoadedCount()} / {match.RequiredPlayers} loaded", _labelStyle);
                if (GUILayout.Button("LEAVE ROOM")) _ = Leave_async();
                GUILayout.EndArea();
                return;
            }
            if (match.State == ROUND_STATE.LOBBY)
            {
                DrawPanel(new Rect(410, 90, 460, 545));
                GUILayout.Label("LOBBY", _titleStyle);
                GUILayout.Label(match.Practice ? "OFFLINE PRACTICE" : "INVITE CODE  " + InviteCode, _labelStyle);
                if (!match.Practice && GUILayout.Button("COPY CODE")) GUIUtility.systemCopyBuffer = InviteCode;
                GUILayout.Label($"{match.ConnectedCount()} / {match.RequiredPlayers} connected", _labelStyle);
                foreach (CrowdActor actor in match.Actors)
                    if (!actor.IsNpc && actor.Connected) GUILayout.Label($"SLOT {actor.Slot}    {(actor.Ready ? "READY" : "NOT READY")}{(actor == local ? "  (YOU)" : "")}", _labelStyle);
                GUILayout.Space(12);
                GUI.enabled = local != null;
                if (GUILayout.Button(local != null && local.Ready ? "CANCEL READY" : "READY")) local.RPC_SetReady(!local.Ready, match.RoundId);
                GUI.enabled = Runner.IsServer && match.CanStart();
                if (GUILayout.Button("HOST: START ROUND")) match.RPC_RequestStart(match.RoundId);
                GUI.enabled = true;
                if (GUILayout.Button(Runner.IsServer ? "CLOSE ROOM" : "LEAVE ROOM")) _ = Leave_async();
                GUILayout.Label("Everyone must be ready. The host starts the round.\nPlayer and NPC appearances are deliberately identical.", _smallStyle);
                GUILayout.EndArea();
                return;
            }
            DrawPanel(new Rect(20, 18, 320, 185));
            GUILayout.Label(match.Practice ? "OFFLINE PRACTICE" : $"ALIVE {match.AlivePlayers}    {Mathf.CeilToInt(match.RemainingSeconds)}s", _labelStyle);
            if (local != null)
            {
                GUILayout.Label($"HEALTH  {local.Health} / 100", _labelStyle);
                GUI.contentColor = local.Exposed ? new Color(1f, 0.4f, 0.25f) : local.Exposure >= 75f ? Color.yellow : Color.white;
                GUILayout.Label($"EXPOSURE  {CrowdRules.ExposureDisplay(local.Exposure)}%  {(local.Exposed ? "EXPOSED" : "HIDDEN")}", _labelStyle);
                GUI.contentColor = Color.white;
                GUILayout.Label($"PISTOL  {local.Magazine} / {local.Reserve}", _labelStyle);
                if (local.Action == ACTION_STATE.RELOAD) GUILayout.Label($"Reloading  {Mathf.Max(0f, (local.ActionEndTick - match.CurrentTick) * Runner.DeltaTime):0.0}s", _smallStyle);
            }
            GUILayout.EndArea();
            GUI.Label(new Rect(20, 660, 900, 50), "WASD move   SHIFT sprint   LMB melee   RMB aim + LMB fire   R reload   ESC menu", _smallStyle);
            if (local != null && !local.Alive) GUI.Label(new Rect(400, 580, 600, 60), $"SPECTATING  |  Cause: {local.DeathCause}\nA / D switch survivor", _labelStyle);
            if (local != null && local.Alive && _controls.Aiming)
            {
                float angle = Vector3.Angle(local.transform.forward, Quaternion.Euler(0f, _controls.Yaw, 0f) * Vector3.forward);
                string crosshair = local.Action == ACTION_STATE.RELOAD ? "RELOADING" : local.Magazine == 0 ? "EMPTY" : angle > CrowdRules.AIM_TOLERANCE ? "ALIGNING" : "+";
                GUI.Label(new Rect(590, 350, 200, 45), crosshair, _labelStyle);
            }
            if (match.State == ROUND_STATE.COUNTDOWN) GUI.Label(new Rect(560, 270, 500, 100), $"{Mathf.Max(1, Mathf.CeilToInt((match.CountdownEndTick - match.CurrentTick) * Runner.DeltaTime))}\nCamera only", _titleStyle);
            if (match.State == ROUND_STATE.RESULT)
            {
                DrawPanel(new Rect(410, 240, 460, 230));
                GUILayout.Label("ROUND OVER", _titleStyle);
                GUILayout.Label(match.WinnerSlot > 0 ? $"SLOT {match.WinnerSlot} WINS" : match.WinnerSlot == -1 ? "DRAW - ALL ELIMINATED" : "DRAW - TIME LIMIT", _labelStyle);
                if (Runner.IsServer && GUILayout.Button("RETURN TO LOBBY")) match.RPC_ReturnToLobby(match.RoundId);
                if (GUILayout.Button(Runner.IsServer ? "CLOSE ROOM" : "LEAVE ROOM")) _ = Leave_async();
                GUILayout.EndArea();
            }
            if (_controls.MenuOpen && match.State != ROUND_STATE.RESULT)
            {
                DrawPanel(new Rect(430, 230, 420, 280));
                GUILayout.Label("MENU", _titleStyle);
                GUILayout.Label("The online match keeps running.", _smallStyle);
                GUILayout.Label($"Mouse sensitivity {_controls.Sensitivity:0.00}", _smallStyle);
                _controls.Sensitivity = GUILayout.HorizontalSlider(_controls.Sensitivity, 0.03f, 0.4f);
                AudioListener.volume = GUILayout.HorizontalSlider(AudioListener.volume, 0f, 1f);
                if (GUILayout.Button("RESUME")) { _controls.MenuOpen = false; PlayerPrefs.SetFloat("Crowd.MouseSensitivity", _controls.Sensitivity); PlayerPrefs.SetFloat("Crowd.Volume", AudioListener.volume); }
                if (GUILayout.Button(Runner.IsServer ? "CLOSE ROOM FOR EVERYONE" : "LEAVE AND FORFEIT")) _ = Leave_async();
                GUILayout.EndArea();
            }
        }

        private static void DrawPanel(Rect rect) { GUI.Box(rect, GUIContent.none); GUILayout.BeginArea(new Rect(rect.x + 20, rect.y + 16, rect.width - 40, rect.height - 32)); }

        private void OnApplicationQuit() { _connection?.Dispose(); }
        private void OnDestroy() { if (_primary) { _connection?.Dispose(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true; } }
    }
}
