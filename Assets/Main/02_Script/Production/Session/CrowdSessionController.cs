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

        public CrowdBootstrap Runtime => _runtime;
        public void Configure(CrowdBootstrap runtime) { _runtime = runtime; }
        public Task CreateRoom_async(int capacity) => _runtime.Connect_async(GameMode.Host, capacity: capacity);
        public Task JoinRoom_async(string code) => _runtime.Connect_async(GameMode.Client, code);
        public Task LeaveRoom_async() => _runtime.Leave_async();
        public void CancelConnection() { _runtime.CancelConnection(); }
        public void SetMenuOpen(bool open) { _runtime.SetMenuOpen(open); }
        public void Ready()
        {
            CrowdMatch match = _runtime.Match;
            CrowdActor local = match != null ? match.LocalActor() : null;
            if (local != null) local.RPC_SetReady(!local.Ready, match.RoundId);
        }
        public void StartRound() { CrowdMatch match = _runtime.Match; if (match != null && _runtime.Runner.IsServer && match.CanStart()) match.RPC_RequestStart(match.RoundId); }
        public void ReturnToLobby() { CrowdMatch match = _runtime.Match; if (match != null && _runtime.Runner.IsServer) match.RPC_ReturnToLobby(match.RoundId); }

        public void ReadModel(SessionModel_model model)
        {
            model.Busy = _runtime.Busy;
            model.MenuOpen = _runtime.MenuOpen;
            model.Status = _runtime.Status;
            CrowdMatch match = _runtime.Match;
            if (match == null || model.Busy)
            {
                if (model.Busy || _runtime.Runner != null) model.Screen = SESSION_SCREEN.CONNECTING;
                else if (model.Screen != SESSION_SCREEN.TITLE && model.Screen != SESSION_SCREEN.CREATE && model.Screen != SESSION_SCREEN.JOIN) model.Screen = SESSION_SCREEN.TITLE;
                return;
            }
            model.Screen = match.State == ROUND_STATE.LOADING ? SESSION_SCREEN.LOADING : match.State == ROUND_STATE.LOBBY ? SESSION_SCREEN.LOBBY : match.State == ROUND_STATE.COUNTDOWN ? SESSION_SCREEN.COUNTDOWN : match.State == ROUND_STATE.RESULT ? SESSION_SCREEN.RESULT : SESSION_SCREEN.PLAYING;
            model.Host = _runtime.Runner.IsServer;
            model.Code = _runtime.InviteCode;
            model.Capacity = match.RequiredPlayers;
            model.CanStart = model.Host && match.CanStart();
            CrowdActor local = match.LocalActor();
            model.CanReady = local != null && !model.Busy && match.State == ROUND_STATE.LOBBY;
            model.Ready = local != null && local.Ready;
            SLOT_TEXT.Clear();
            for (int slot = 1; slot <= match.RequiredPlayers; slot++)
            {
                CrowdActor participant = null;
                foreach (CrowdActor actor in match.Actors) if (!actor.IsNpc && actor.Connected && actor.Slot == slot) { participant = actor; break; }
                SLOT_TEXT.Append("Slot ").Append(slot).Append("    ");
                if (participant == null) SLOT_TEXT.Append("Waiting for Player");
                else SLOT_TEXT.Append(match.State == ROUND_STATE.LOADING ? (participant.LoadedRoundId == match.RoundId ? "Loaded" : "Loading...") : participant.Ready ? "Ready" : "Not Ready").Append(participant == local ? "  (You)" : "");
                SLOT_TEXT.AppendLine();
            }
            model.Slots = SLOT_TEXT.ToString();
            if (match.State == ROUND_STATE.LOADING)
                model.Status = $"Loading round: {match.LoadedCount()} / {match.RequiredPlayers} players ready. {Mathf.Max(0, Mathf.CeilToInt((match.LoadDeadlineTick - match.CurrentTick) * _runtime.Runner.DeltaTime))}s remaining.";
            else if (match.State == ROUND_STATE.LOBBY && match.LastStartFailure != START_FAILURE.NONE)
                model.Status = match.LastStartFailure == START_FAILURE.PARTICIPANT_LEFT ? "Start cancelled: a player left. Everyone must ready up again." : "Start cancelled: a player failed to load or timed out. Everyone must ready up again.";
            model.Countdown = Mathf.Max(1, Mathf.CeilToInt((match.CountdownEndTick - match.CurrentTick) * _runtime.Runner.DeltaTime)).ToString();
            int seconds = Mathf.CeilToInt(match.RemainingSeconds);
            model.Time = $"Alive {match.AlivePlayers} players    {seconds / 60:00}:{seconds % 60:00}";
            model.Result = match.WinnerSlot > 0 ? $"Slot {match.WinnerSlot} Wins" : match.WinnerSlot == -1 ? "Draw - All Eliminated" : "Draw - Time Limit";
            model.ShowCrosshair = false;
            model.Spectator = "";
            if (local == null) return;
            model.Health = $"Health  {local.Health} / {CrowdRules.MAX_HP}";
            model.HealthRatio = (float)local.Health / CrowdRules.MAX_HP;
            model.Exposure = $"Exposure  {CrowdRules.ExposureDisplay(local.Exposure)}% - {(local.Exposed ? "Exposed" : "Hidden")}";
            model.ExposureRatio = local.Exposure / 100f;
            model.Exposed = local.Exposed;
            model.Ammo = local.Action == ACTION_STATE.RELOAD ? $"Reloading  {Mathf.Max(0f, (local.ActionEndTick - match.CurrentTick) * _runtime.Runner.DeltaTime):0.0}s" : $"Pistol  {local.Magazine} / {local.Reserve}";
            if (!local.Alive) model.Spectator = $"Eliminated - {CauseText(local.DeathCause)}\nSpectating survivors - A / D to switch";
            model.ShowCrosshair = local.Alive && _runtime.LocalAim && !model.MenuOpen;
            float angle = Vector3.Angle(local.transform.forward, Quaternion.Euler(0f, _runtime.AimYaw, 0f) * Vector3.forward);
            model.Crosshair = local.Action == ACTION_STATE.RELOAD ? "Reloading" : local.Magazine == 0 ? "No Ammo" : angle > CrowdRules.AIM_TOLERANCE ? "Aligning" : "+";
        }

        private static string CauseText(DEATH_CAUSE cause)
        {
            switch (cause)
            {
                case DEATH_CAUSE.BACKSTAB: return "Backstab";
                case DEATH_CAUSE.MELEE: return "Melee";
                case DEATH_CAUSE.PISTOL: return "Pistol";
                case DEATH_CAUSE.ZOMBIE: return "Zombie";
                case DEATH_CAUSE.DISCONNECTED: return "Disconnected";
                default: return "Eliminated";
            }
        }

    }
}
