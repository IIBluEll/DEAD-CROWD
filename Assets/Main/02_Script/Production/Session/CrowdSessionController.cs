using System.Text;
using System.Threading.Tasks;
using DeadCrowd.Prototype;
using DeadCrowd.UI;
using Fusion;
using UnityEngine;

namespace DeadCrowd.Session
{
    // UI commands and presentation data. Connection ownership lives in CrowdConnectionService.
    public sealed class CrowdSessionController : MonoBehaviour
    {
        [SerializeField] private CrowdBootstrap _runtime;
        private readonly StringBuilder SLOT_TEXT = new StringBuilder();
        private static readonly string[] SEARCH_DIRECTIONS = { "Ahead", "Front right", "Right", "Back right", "Behind", "Back left", "Left", "Front left" };
        private static readonly string[] SEARCH_DIRECTIONS_KOREAN = { "정면", "오른쪽 앞", "오른쪽", "오른쪽 뒤", "후방", "왼쪽 뒤", "왼쪽", "왼쪽 앞" };

        public CrowdBootstrap Runtime => _runtime;
        private void Awake() { if (CrowdBootstrap.Instance != null) _runtime = CrowdBootstrap.Instance; }
        public void Configure(CrowdBootstrap runtime) { _runtime = runtime; }
        public Task CreateRoom_async(int capacity, bool publicRoom) => _runtime.Connection.Connect_async(GameMode.Host, capacity: capacity, publicRoom: publicRoom);
        public Task JoinRoom_async(string code) => _runtime.Connect_async(GameMode.Client, code);
        public Task QuickMatch_async(int capacity) => _runtime.Connection.Connect_async(GameMode.AutoHostOrClient, capacity: capacity);
        public void OpenRoomBrowser() => _runtime.Connection.OpenRoomBrowser();
        public void CloseRoomBrowser() => _runtime.Connection.CloseRoomBrowser();
        public Task StartPractice_async() => _runtime.Connection.StartPractice_async();
        public Task LeaveRoom_async() => _runtime.Leave_async();
        public void CancelConnection() => _runtime.CancelConnection();
        public void SetMenuOpen(bool open) { _runtime.SetMenuOpen(open); }
        public void Ready()
        {
            CrowdMatch match = _runtime.Match;
            CrowdActor local = match != null ? match.LocalActor() : null;
            if (local != null) local.RPC_SetReady(!local.Ready, match.RoundId);
        }
        public void StartRound() { CrowdMatch match = _runtime.Match; if (match != null && _runtime.Runner.IsServer && match.CanStart()) match.RPC_RequestStart(match.RoundId); }
        public void ReturnToLobby()
        {
            CrowdMatch match = _runtime.Match;
            if (match == null || !_runtime.Runner.IsServer || match.State != ROUND_STATE.RESULT) return;
            if (match.Practice) _runtime.Connection.RequestPracticeStart();
            match.RPC_ReturnToLobby(match.RoundId);
        }

        public void ReadModel(SessionModel_model model)
        {
            model.Busy = _runtime.Busy;
            model.MenuOpen = _runtime.MenuOpen;
            model.Status = model.Korean ? SessionLanguage.TranslateStatus(_runtime.Status) : _runtime.Status;
            var connection = _runtime.Connection;
            model.Rooms = connection.PublicRooms;
            model.RoomListReady = connection.ReceivedRoomList;
            model.RoomListFailed = connection.ShowRoomBrowser && connection.State == CONNECTION_STATE.IDLE && !connection.ReceivedRoomList;
            CrowdMatch match = _runtime.Match;
            model.Practice = match != null && match.Practice;
            if (match == null || model.Busy)
            {
                if (_runtime.Runner == null && !model.Busy && model.Capacity == 1) model.Capacity = 2;
                if (model.Busy && !connection.StoppingRoomBrowser) model.Screen = SESSION_SCREEN.CONNECTING;
                else if (connection.ShowRoomBrowser) model.Screen = SESSION_SCREEN.PUBLIC_ROOMS;
                else if (_runtime.Runner != null && !connection.StoppingRoomBrowser) model.Screen = SESSION_SCREEN.CONNECTING;
                else if (model.Screen != SESSION_SCREEN.TITLE && model.Screen != SESSION_SCREEN.CREATE && model.Screen != SESSION_SCREEN.JOIN) model.Screen = SESSION_SCREEN.TITLE;
                return;
            }
            model.Screen = match.State == ROUND_STATE.LOADING ? SESSION_SCREEN.LOADING : match.State == ROUND_STATE.LOBBY ? SESSION_SCREEN.LOBBY : match.State == ROUND_STATE.COUNTDOWN ? SESSION_SCREEN.COUNTDOWN : match.State == ROUND_STATE.RESULT ? SESSION_SCREEN.RESULT : SESSION_SCREEN.PLAYING;
            model.Host = _runtime.Runner.IsServer;
            model.PublicSession = connection.PublicSession;
            model.Code = _runtime.InviteCode;
            model.Capacity = match.RequiredPlayers;
            model.CanStart = model.Host && match.CanStart();
            CrowdActor local = match.LocalActor();
            model.CanReady = !model.Practice && local != null && !model.Busy && match.State == ROUND_STATE.LOBBY;
            model.Ready = local != null && local.Ready;
            SLOT_TEXT.Clear();
            model.ConnectedPlayers = 0;
            for (int slot = 1; slot <= match.RequiredPlayers; slot++)
            {
                CrowdActor participant = null;
                foreach (CrowdActor actor in match.Actors) if (!actor.IsNpc && actor.Connected && actor.Slot == slot) { participant = actor; break; }
                model.Participants[slot - 1] = participant == null ? default :
                    new LobbyParticipant(true, participant.Ready, participant.LoadedRoundId == match.RoundId,
                        participant == local, !match.Practice && slot == match.HostSlot);
                if (participant != null) model.ConnectedPlayers++;
                SLOT_TEXT.Append(model.Text("Slot ", "플레이어 ")).Append(slot).Append("    ");
                if (participant == null) SLOT_TEXT.Append(model.Text("Waiting for Player", "참가 대기 중"));
                else SLOT_TEXT.Append(match.State == ROUND_STATE.LOADING ? (participant.LoadedRoundId == match.RoundId ? model.Text("Loaded", "불러오기 완료") : model.Text("Loading...", "불러오는 중")) : participant.Ready ? model.Text("Ready", "준비 완료") : model.Text("Not Ready", "준비 전")).Append(participant == local ? model.Text("  (You)", "  (나)") : "");
                SLOT_TEXT.AppendLine();
            }
            model.Slots = SLOT_TEXT.ToString();
            if (model.Practice) model.Slots = model.Text("Single Player\nPractice with the crowd and zombies.", "싱글 플레이\n군중과 좀비 사이에서 연습하세요.");
            if (match.State == ROUND_STATE.LOADING)
            {
                int loaded = match.LoadedCount();
                int remainingSeconds = Mathf.Max(0, Mathf.CeilToInt((match.LoadDeadlineTick - match.CurrentTick) * _runtime.Runner.DeltaTime));
                model.Status = model.Practice ? model.Text("Preparing offline practice...", "오프라인 연습을 준비하고 있습니다.") : model.Korean
                    ? $"경기 불러오는 중  {loaded} / {match.RequiredPlayers}명 완료  |  {remainingSeconds}초 남음"
                    : $"Loading round: {loaded} / {match.RequiredPlayers} players ready. {remainingSeconds}s remaining.";
            }
            else if (match.State == ROUND_STATE.LOBBY && match.LastStartFailure != START_FAILURE.NONE)
                model.Status = match.LastStartFailure == START_FAILURE.PARTICIPANT_LEFT
                    ? model.Text("Start cancelled: a player left. Everyone must ready up again.", "플레이어가 나가 경기가 취소됐습니다. 다시 준비해 주세요.")
                    : model.Text("Start cancelled: a player failed to load or timed out. Everyone must ready up again.", "경기를 불러오지 못했습니다. 다시 준비해 주세요.");
            model.Countdown = Mathf.Max(1, Mathf.CeilToInt((match.CountdownEndTick - match.CurrentTick) * _runtime.Runner.DeltaTime)).ToString();
            int seconds = Mathf.CeilToInt(match.RemainingSeconds);
            model.RoundSeconds = match.State == ROUND_STATE.COUNTDOWN ? CrowdRules.ROUND_SECONDS : seconds;
            model.AliveCount = match.AlivePlayers;
            string clock = $"{seconds / 60:00}:{seconds % 60:00}";
            model.Time = model.Korean ? $"생존 {match.AlivePlayers}명    {clock}" : $"Alive {match.AlivePlayers} players    {clock}";
            model.Result = match.WinnerSlot > 0 ? (model.Korean ? $"{match.WinnerSlot}번 플레이어 승리" : $"Slot {match.WinnerSlot} Wins")
                : match.WinnerSlot == -1 ? model.Text("Draw - All Eliminated", "전멸로 무승부") : model.Text("Draw - Time Limit", "시간 종료로 무승부");
            if (model.Practice)
            {
                model.Time = model.Text("Solo Practice    ", "혼자 연습    ") + clock;
                model.Result = match.WinnerSlot == -1 ? model.Text("Practice Over - Eliminated", "연습 종료 - 탈락") : model.Text("Practice Complete - Time Limit", "연습 종료 - 시간 종료");
            }
            model.ShowCrosshair = false;
            model.Spectator = "";
            model.HasLocalPlayer = local != null;
            model.LocalAlive = local != null && local.Alive;
            model.SearchEnabled = match.SearchEnabled && local != null;
            model.SearchProgress = -1f;
            if (local == null) return;
            if (match.SearchEnabled)
            {
                int lead = 0;
                foreach (CrowdActor actor in match.Actors)
                    if (!actor.IsNpc && actor.Connected && actor.Alive) lead = Mathf.Max(lead, actor.SearchScore);
                model.SearchScore = local.SearchScore;
                model.LeadSearchScore = lead;
                model.Time += model.Text("    Search ", "    수색 ") + local.SearchScore;
                if (!model.Practice) model.Time += model.Text(" / Lead ", " / 선두 ") + lead;
                if (match.WinnerSlot > 0 && match.AlivePlayers > 1) model.Result += model.Text(" - Search Score", " - 수색 점수");
                model.Result += model.Text("\nYour search score: ", "\n내 수색 점수 ") + local.SearchScore;
                if (local.Alive && model.Screen == SESSION_SCREEN.PLAYING && !model.MenuOpen)
                {
                    if (local.SearchSiteId != 0)
                    {
                        float remaining = (local.SearchEndTick - match.CurrentTick) * _runtime.Runner.DeltaTime;
                        model.SearchProgress = Mathf.Clamp01(1f - remaining / CrowdMatch.SEARCH_SECONDS);
                        model.Spectator = $"{model.Text("Searching", "수색 중")}  {model.SearchProgress * CrowdMatch.SEARCH_SECONDS:0.0} / {CrowdMatch.SEARCH_SECONDS:0.#}{model.Text(" s", "초")}";
                    }
                    else if (match.TryGetSearchTarget(local, out int charges)) model.Spectator = model.Korean ? $"E 길게 눌러 수색    {charges}회 남음" : $"Hold E to search    {charges} left";
                    else if (match.TryGetNearestSearchOffset(local.transform.position, out Vector3 offset))
                    {
                        float angle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg - _runtime.ViewYaw;
                        int direction = Mathf.RoundToInt(Mathf.Repeat(angle, 360f) / 45f) % SEARCH_DIRECTIONS.Length;
                        model.Spectator = offset.sqrMagnitude < 9f
                            ? model.Text("Approach the front of the bin", "수색 지점 앞으로 이동하세요")
                            : $"{Mathf.CeilToInt(offset.magnitude)} m  |  {(model.Korean ? SEARCH_DIRECTIONS_KOREAN[direction] : SEARCH_DIRECTIONS[direction])}";
                    }
                    else model.Spectator = model.Text("Waiting for supplies to reappear", "수색 지점이 다시 활성화되기를 기다리는 중");
                }
            }
            model.HealthValue = local.Health;
            model.MaxHealth = CrowdRules.MAX_HP;
            model.Health = $"{model.Text("Health", "체력")}  {model.HealthValue} / {model.MaxHealth}";
            model.HealthRatio = (float)model.HealthValue / model.MaxHealth;
            model.ExposurePercent = CrowdRules.ExposureDisplay(local.Exposure);
            model.Exposure = $"{model.Text("Exposure", "노출")}  {model.ExposurePercent}% - {(local.Exposed ? model.Text("Exposed", "노출") : model.Text("Hidden", "비노출"))}";
            model.ExposureRatio = local.Exposure / 100f;
            model.Exposed = local.Exposed;
            model.Reloading = local.ConfirmedAction == ACTION_STATE.RELOAD;
            model.ReloadSeconds = Mathf.Max(0f, (local.ConfirmedActionEndTick - match.CurrentTick) * _runtime.Runner.DeltaTime);
            model.Magazine = local.ConfirmedMagazine;
            model.Reserve = local.ConfirmedReserve;
            model.Ammo = model.Reloading ? $"{model.Text("Reloading", "재장전 중")}  {model.ReloadSeconds:0.0}{model.Text("s", "초")}" : $"{model.Text("Pistol", "권총")}  {model.Magazine} / {model.Reserve}";
            if (!local.Alive)
            {
                model.Spectator = $"{model.Text("Eliminated", "탈락")} - {CauseText(local.DeathCause, model)}";
                if (_runtime.SpectatedSlot > 0) model.Spectator += model.Korean ? $"\n{_runtime.SpectatedSlot}번 관전 중 - A / D로 전환" : $"\nSpectating Slot {_runtime.SpectatedSlot} - A / D to switch";
            }
            model.ShowCrosshair = local.Alive && _runtime.LocalAim && !model.MenuOpen;
            model.Crosshair = local.ConfirmedAction == ACTION_STATE.RELOAD ? model.Text("Reloading", "재장전 중") : local.ConfirmedMagazine == 0 ? model.Text("No Ammo", "탄약 없음") : "+";
        }

        private static string CauseText(DEATH_CAUSE cause, SessionModel_model model)
        {
            switch (cause)
            {
                case DEATH_CAUSE.BACKSTAB: return model.Text("Backstab", "기습");
                case DEATH_CAUSE.MELEE: return model.Text("Melee", "근접 공격");
                case DEATH_CAUSE.PISTOL: return model.Text("Pistol", "권총");
                case DEATH_CAUSE.ZOMBIE: return model.Text("Zombie", "좀비");
                case DEATH_CAUSE.DISCONNECTED: return model.Text("Disconnected", "연결 종료");
                default: return model.Text("Eliminated", "탈락");
            }
        }

    }
}
