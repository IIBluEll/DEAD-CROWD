using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DeadCrowd.Prototype;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace DeadCrowd.Session
{
    public enum CONNECTION_STATE { IDLE, CONNECTING, CONNECTED, DISCONNECTING, BROWSING }

    public readonly struct PublicRoom
    {
        public readonly string Code;
        public readonly int Count;
        public readonly int Capacity;

        public PublicRoom(string code, int count, int capacity) { Code = code; Count = count; Capacity = capacity; }
    }

    // One owner for the runner, cancellation token and scene cleanup. Input remains in Bootstrap until D3.
    public sealed class CrowdConnectionService : INetworkRunnerCallbacks, IDisposable
    {
        public const string MAIN_MENU_SCENE = "Assets/Main/01_Scene/MainMenu.unity";
        public const string IN_GAME_SCENE = "Assets/Main/01_Scene/InGame.unity";
        private const string ROOM_MODE = "mode";
        private const string CONTENT_VERSION = "cv";
        public const string INITIAL_STATUS = "Create a room or join with an invite code.";
        private readonly Func<CrowdInput> READ_INPUT;
        private NetworkRunner _runner;
        private CrowdMatch _match;
        private CancellationTokenSource _cancellation;
        private Task _connectTask = Task.CompletedTask;
        private Task _leaveTask = Task.CompletedTask;
        private int _attempt;
        private int _originSceneIndex;
        private int _gameSceneIndex;
        private string _connectionVersion;
        private bool _disposed;
        private bool _shutdownPending;
        private bool _sceneReady;
        private string _shutdownMessage;
        private float _nextLoadReport;
        private float _initialLoadDeadline;
        private bool _initialLoadComplete;
        private string _confirmedResult = "";
        private bool _resumeBrowser;
        private bool _restoreSceneOnLeave;
        private bool _autoStartPractice;

        public NetworkRunner Runner => _runner;
        public CrowdMatch Match => _match != null && _match.Valid ? _match : null;
        public CONNECTION_STATE State { get; private set; }
        public bool Busy => State == CONNECTION_STATE.CONNECTING || State == CONNECTION_STATE.DISCONNECTING || State == CONNECTION_STATE.CONNECTED && !_initialLoadComplete;
        public string Status { get; private set; } = INITIAL_STATUS;
        public string InviteCode { get; private set; } = "";
        public bool PublicSession => _runner != null && _runner.SessionInfo.IsValid && _runner.SessionInfo.IsVisible;
        public bool ShowRoomBrowser { get; private set; }
        public bool StoppingRoomBrowser => State == CONNECTION_STATE.DISCONNECTING && !_restoreSceneOnLeave;
        public bool ReceivedRoomList { get; private set; }
        public IReadOnlyList<PublicRoom> PublicRooms { get; private set; } = Array.Empty<PublicRoom>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool TestBlockLoading { get; set; }
        public float TestStartupCompletionDelaySeconds { get; set; }
        public bool TestSuccessfulStartReceived { get; private set; }
#endif

        public CrowdConnectionService(Func<CrowdInput> readInput) { READ_INPUT = readInput; }

        public Task StartPractice_async()
        {
            if (State != CONNECTION_STATE.IDLE) return Task.CompletedTask;
            _autoStartPractice = true;
            return Connect_async(GameMode.Single);
        }

        public void RequestPracticeStart() => _autoStartPractice = true;

        public void OpenRoomBrowser()
        {
            if (_disposed || State != CONNECTION_STATE.IDLE) return;
            ShowRoomBrowser = true;
            Status = "";
            StartBrowser();
        }

        public void CloseRoomBrowser()
        {
            ShowRoomBrowser = false;
            _resumeBrowser = false;
            if (State == CONNECTION_STATE.BROWSING) _ = Leave_async("");
            else if (State == CONNECTION_STATE.IDLE) Status = "";
        }

        private void StartBrowser()
        {
            ReceivedRoomList = false;
            PublicRooms = Array.Empty<PublicRoom>();
            if (!PrepareContent()) return;
            State = CONNECTION_STATE.BROWSING;
            _connectTask = BrowseCore_async(++_attempt);
        }

        private async Task BrowseCore_async(int attempt)
        {
            try
            {
                await Task.Yield();
                if (attempt != _attempt || _disposed) return;
                var runner = CreateRunner();
                var token = _cancellation.Token;
                StartGameResult result = await runner.JoinSessionLobby(SessionLobby.ClientServer, customAppSettings: AppSettings(), cancellationToken: token);
                if (!result.Ok || token.IsCancellationRequested) throw new InvalidOperationException(result.ShutdownReason.ToString());
                if (attempt != _attempt || _disposed) return;
                if (_shutdownPending) throw new InvalidOperationException("Room list unavailable.");
                ReceivedRoomList = true;
                _cancellation.CancelAfter(Timeout.Infinite);
            }
            catch (Exception exception)
            {
                if (attempt != _attempt || _disposed) return;
                Debug.LogWarning($"[Crowd] Room browser unavailable: {exception.Message}");
                BrowserFailed();
            }
        }

        private void BrowserFailed()
        {
            ReceivedRoomList = false;
            PublicRooms = Array.Empty<PublicRoom>();
            _ = Leave_async("Could not load public rooms. Try again.");
        }

        public Task Connect_async(GameMode mode, string code = null, int capacity = 2, bool publicRoom = false)
        {
            bool fromBrowser = ShowRoomBrowser && (mode == GameMode.Client || mode == GameMode.AutoHostOrClient);
            bool browsing = fromBrowser && State == CONNECTION_STATE.BROWSING;
            if (_disposed || State != CONNECTION_STATE.IDLE && !browsing) return Task.CompletedTask;
            string requestedCode = (code ?? "").Trim().ToUpperInvariant();
            if (mode == GameMode.Client && !ValidCode(requestedCode))
            {
                Status = "Enter 6 letters or digits. I, O, 0 and 1 are excluded.";
                return Task.CompletedTask;
            }
            if (mode != GameMode.Single && capacity != 2 && capacity != 4) { Status = "Choose a 2-player or 4-player room."; return Task.CompletedTask; }
            if (!PrepareContent()) return Task.CompletedTask;
            Task browserTask = browsing ? _connectTask : Task.CompletedTask;
            State = CONNECTION_STATE.CONNECTING;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            TestSuccessfulStartReceived = false;
#endif
            Status = mode == GameMode.Single ? "Preparing offline practice..." : mode == GameMode.AutoHostOrClient ? "Finding a match..." : "Connecting to the room...";
            _confirmedResult = "";
            _initialLoadComplete = false;
            _originSceneIndex = SceneManager.GetActiveScene().buildIndex;
            _gameSceneIndex = IsMainMenu ? SceneUtility.GetBuildIndexByScenePath(IN_GAME_SCENE) : _originSceneIndex;
            if (_gameSceneIndex < 0)
            {
                State = CONNECTION_STATE.IDLE;
                Status = "The game scene is missing from the build.";
                return Task.CompletedTask;
            }
            int attempt = ++_attempt;
            if (browsing) _cancellation?.Cancel();
            _connectTask = ConnectCore_async(mode, code, requestedCode, capacity, publicRoom, browsing, browserTask, attempt);
            return _connectTask;
        }

        private async Task ConnectCore_async(GameMode mode, string codeOverride, string requestedCode, int capacity, bool publicRoom, bool browsing, Task browserTask, int attempt)
        {
            bool connected = false;
            try
            {
                if (browsing)
                {
                    await browserTask;
                    await StopRunner_async();
                }
                bool quickMatch = mode == GameMode.AutoHostOrClient;
                for (int retry = 0; retry < (mode == GameMode.Host ? 5 : 1); retry++)
                {
                    if (_disposed || attempt != _attempt) return;
                    NetworkRunner runner = CreateRunner();
                    runner.ProvideInput = true;
                    var sceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
                    var provider = runner.gameObject.AddComponent<NetworkObjectProviderDefault>();
                    string code = quickMatch ? null : mode == GameMode.Single ? "PRACTICE" : mode == GameMode.Host && codeOverride == null ? GenerateCode() : requestedCode;
                    InviteCode = code;
                    var sceneInfo = new NetworkSceneInfo();
                    sceneInfo.AddSceneRef(SceneRef.FromIndex(_gameSceneIndex), LoadSceneMode.Single);
                    StartGameResult result = await runner.StartGame(new StartGameArgs
                    {
                        GameMode = mode, SessionName = code, PlayerCount = mode == GameMode.Single ? 1 : capacity,
                        SessionNameGenerator = quickMatch ? GenerateCode : null,
                        IsVisible = publicRoom || quickMatch, IsOpen = true, EnableClientSessionCreation = quickMatch,
                        SessionProperties = publicRoom || quickMatch ? new Dictionary<string, SessionProperty>
                        {
                            [ROOM_MODE] = capacity, [CONTENT_VERSION] = _connectionVersion
                        } : null,
                        ConnectionToken = Encoding.UTF8.GetBytes(_connectionVersion), CustomPhotonAppSettings = AppSettings(),
                        Scene = sceneInfo, SceneManager = sceneManager, ObjectProvider = provider, StartGameCancellationToken = _cancellation.Token
                    });
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    TestSuccessfulStartReceived = result.Ok;
                    if (result.Ok && TestStartupCompletionDelaySeconds > 0f)
                    {
                        Debug.Log("[SessionSmoke] Holding a successful StartGame response for cancellation testing.");
                        await Task.Delay(TimeSpan.FromSeconds(TestStartupCompletionDelaySeconds));
                    }
#endif
                    // A successful response is still stale when the user cancelled while it was in flight.
                    if (_disposed || attempt != _attempt) return;
                    if (_cancellation.IsCancellationRequested) { Status = "Connection timed out. Please try again."; return; }
                    if (result.Ok && runner != null && runner.IsRunning && !_shutdownPending)
                    {
                        // Fusion retains the token's shutdown callback after StartGame completes.
                        _cancellation.CancelAfter(Timeout.Infinite);
                        connected = true;
                        State = CONNECTION_STATE.CONNECTED;
                        if (quickMatch) InviteCode = runner.SessionInfo.Name;
                        _initialLoadDeadline = Time.realtimeSinceStartup + CrowdRules.LOAD_TIMEOUT_SECONDS;
                        Status = mode == GameMode.Single ? "Offline practice ready." : "The host can start when everyone is ready.";
                        Debug.Log($"[Crowd] Connected mode={mode}, room={code}, attempt={attempt}");
                        return;
                    }
                    string reason = result.ShutdownReason.ToString();
                    Status = result.Ok && _shutdownPending ? _shutdownMessage :
                        ShowRoomBrowser && reason == "GameNotFound" ? "That room is no longer available." : ConnectionMessage(reason);
                    Debug.LogWarning($"[Crowd] Connection failed: {reason}, attempt={attempt}");
                    await StopRunner_async();
                    if (mode != GameMode.Host || codeOverride != null || reason != "GameIdAlreadyExists") return;
                }
                Status = "Could not create a unique invite code. Please try again.";
            }
            catch (OperationCanceledException)
            {
                if (attempt == _attempt) Status = "Connection timed out. Please try again.";
            }
            catch (Exception exception)
            {
                if (attempt == _attempt) Status = "Could not connect. Check your connection and game version, then try again.";
                Debug.LogException(exception);
            }
            finally
            {
                if (!connected)
                {
                    await StopRunner_async();
                    // Explicit leave owns the reload when it has superseded this attempt.
                    if (State != CONNECTION_STATE.DISCONNECTING)
                    {
                        _autoStartPractice = false;
                        await RestoreScene_async();
                        State = CONNECTION_STATE.IDLE;
                        _resumeBrowser = ShowRoomBrowser;
                    }
                }
            }
        }

        private NetworkRunner CreateRunner()
        {
            _cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var runnerObj = new GameObject("Crowd Network Runner");
            UnityEngine.Object.DontDestroyOnLoad(runnerObj);
            _runner = runnerObj.AddComponent<NetworkRunner>();
            _sceneReady = false;
            _shutdownPending = false;
            _runner.AddCallbacks(this);
            return _runner;
        }

        private static FusionAppSettings AppSettings()
        {
            var settings = PhotonAppSettings.Global.AppSettings.GetCopy();
            settings.AppVersion = CrowdRules.BUILD_VERSION;
            settings.FixedRegion = "kr";
            return settings;
        }

        private static bool IsMainMenu => SceneManager.GetActiveScene().path == MAIN_MENU_SCENE;

        private bool PrepareContent()
        {
            try
            {
                string path = SceneManager.GetActiveScene().path;
                // The menu has no map geometry. The round-load handshake still compares the full map signature.
                if (path == MAIN_MENU_SCENE || path == IN_GAME_SCENE) _connectionVersion = CrowdRules.BUILD_VERSION;
                else
                {
                    DeadCrowd.Content.CrowdContent.Freeze();
                    _connectionVersion = DeadCrowd.Content.CrowdContent.SessionVersion;
                }
                return true;
            }
            catch (Exception exception) { Status = "Invalid game content. Check settings and map assets."; Debug.LogException(exception); return false; }
        }

        public void CancelConnection()
        {
            if (State == CONNECTION_STATE.CONNECTING || State == CONNECTION_STATE.CONNECTED && !_initialLoadComplete) _ = Leave_async("Connection cancelled.");
        }

        public Task Leave_async(string message = "You left the room.")
        {
            _autoStartPractice = false;
            if (_disposed) return Task.CompletedTask;
            if (State == CONNECTION_STATE.DISCONNECTING) return _leaveTask;
            if (State == CONNECTION_STATE.IDLE) { Status = message; return Task.CompletedTask; }
            _restoreSceneOnLeave = State != CONNECTION_STATE.BROWSING;
            State = CONNECTION_STATE.DISCONNECTING;
            Status = message;
            ++_attempt;
            _cancellation?.Cancel();
            _leaveTask = LeaveCore_async();
            return _leaveTask;
        }

        private async Task LeaveCore_async()
        {
            try
            {
                await _connectTask;
                await StopRunner_async();
                if (_restoreSceneOnLeave) await RestoreScene_async();
            }
            finally { State = CONNECTION_STATE.IDLE; _resumeBrowser = _restoreSceneOnLeave && ShowRoomBrowser; }
        }

        private async Task StopRunner_async()
        {
            NetworkRunner runner = _runner;
            _runner = null;
            _match = null;
            _sceneReady = false;
            _shutdownPending = false;
            try
            {
                if (runner != null)
                {
                    runner.RemoveCallbacks(this);
                    await runner.Shutdown();
                    if (runner != null) UnityEngine.Object.Destroy(runner.gameObject);
                }
            }
            catch (Exception exception) { Debug.LogException(exception); }
            finally { _cancellation?.Dispose(); _cancellation = null; }
        }

        private async Task RestoreScene_async()
        {
            if (_disposed) return;
            try
            {
                if (SceneManager.GetActiveScene().buildIndex == _originSceneIndex && IsMainMenu) return;
                AsyncOperation load = SceneManager.LoadSceneAsync(_originSceneIndex);
                while (!load.isDone && !_disposed) await Task.Yield();
            }
            catch (Exception exception) { Status = "Could not restore the game scene. Restart the game."; Debug.LogException(exception); }
        }

        public void Tick()
        {
            if (_disposed) return;
            if (State == CONNECTION_STATE.IDLE && _resumeBrowser)
            {
                _resumeBrowser = false;
                StartBrowser();
            }
            if (State == CONNECTION_STATE.BROWSING && _shutdownPending) BrowserFailed();
            if (State != CONNECTION_STATE.CONNECTED) return;
            if (_shutdownPending) { _ = Leave_async(_shutdownMessage); return; }
            if (_runner == null || !_runner.IsRunning) return;
            if (Match == null) _match = UnityEngine.Object.FindFirstObjectByType<CrowdMatch>();
            CrowdMatch match = Match;
            if (!_initialLoadComplete)
            {
                _initialLoadComplete = _sceneReady && match != null && match.Runner == _runner && match.LocalActor() != null;
                if (_initialLoadComplete) ShowRoomBrowser = false;
                if (!_initialLoadComplete && Time.realtimeSinceStartup >= _initialLoadDeadline)
                {
                    _ = Leave_async("The room scene could not load in time. Please try again.");
                    return;
                }
            }
            if (match == null || match.Runner != _runner) return;
            if (_autoStartPractice && _initialLoadComplete && match.Practice && match.CanStart())
            {
                _autoStartPractice = false;
                match.RPC_RequestStart(match.RoundId);
            }
            if (match.State == ROUND_STATE.RESULT)
                _confirmedResult = match.WinnerSlot > 0 ? $"Slot {match.WinnerSlot} Wins" : match.WinnerSlot == -1 ? "Draw - All Eliminated" : "Draw - Time Limit";
            else if (match.State == ROUND_STATE.LOBBY) _confirmedResult = "";
            if (match.HostLoadFailed) { _ = Leave_async("The host could not load the round. The room has closed."); return; }
            if (match.State != ROUND_STATE.LOADING) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (TestBlockLoading) return;
#endif
            CrowdActor local = match.LocalActor();
            if (!_sceneReady || local == null || local.LoadedRoundId == match.RoundId || Time.unscaledTime < _nextLoadReport) return;
            if (match.ConnectedCount() != match.RequiredPlayers || match.NpcCount() != match.ExpectedNpcCount) return;
            _nextLoadReport = Time.unscaledTime + 0.5f;
            bool mapReady = local.PresentationReady && NavMesh.SamplePosition(local.transform.position, out _, 1f, NavMesh.AllAreas);
            foreach (CrowdActor actor in match.Actors) if (!actor.PresentationReady) mapReady = false;
            local.RPC_ReportLoaded(match.RoundId, DeadCrowd.Content.CrowdContent.SessionVersion, mapReady);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ++_attempt;
            _cancellation?.Cancel();
            if (_runner != null) _runner.RemoveCallbacks(this);
            // Pending startup/leave keeps ownership until its continuation has released the runner.
            if (State == CONNECTION_STATE.CONNECTED || State == CONNECTION_STATE.BROWSING) _ = StopRunner_async();
        }

        private static bool ValidCode(string code)
        {
            if (code.Length != 6) return false;
            foreach (char character in code) if (CrowdRules.CODE_ALPHABET.IndexOf(character) < 0) return false;
            return true;
        }

        private static string GenerateCode()
        {
            var code = new char[6];
            for (int i = 0; i < code.Length; i++) code[i] = CrowdRules.CODE_ALPHABET[UnityEngine.Random.Range(0, CrowdRules.CODE_ALPHABET.Length)];
            return new string(code);
        }

        private static string ConnectionMessage(string reason)
        {
            switch (reason)
            {
                case "GameNotFound": return "Room not found. Check the invite code and game version.";
                case "GameIsFull": return "The room is full.";
                case "GameClosed": return "The match is in progress. Join when it returns to the lobby.";
                case "InvalidAuthentication": return "Could not authenticate with the game service.";
                case "IncompatibleConfiguration": return "The host and client must use the same game version.";
                case "OperationCanceled": return "Connection timed out. Please try again.";
                default: return "Could not connect. Check your connection and game version, then try again.";
            }
        }

        private void SessionEnded(NetworkRunner runner, string reason)
        {
            Debug.LogWarning("[Crowd] Session ended: " + reason);
            if (_disposed || runner != _runner) return;
            _shutdownPending = true;
            _shutdownMessage = Match != null && Match.State == ROUND_STATE.LOADING
                ? "The session ended during loading. Loading failed, timed out, or the host left."
                : "The session ended. Create a room or join again.";
            if (!string.IsNullOrEmpty(_confirmedResult)) _shutdownMessage += " Confirmed result: " + _confirmedResult + ".";
        }

        public void OnInput(NetworkRunner runner, NetworkInput input) { if (runner == _runner && State == CONNECTION_STATE.CONNECTED) input.Set(READ_INPUT()); }
        public void OnShutdown(NetworkRunner runner, ShutdownReason reason) { SessionEnded(runner, reason.ToString()); }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { SessionEnded(runner, reason.ToString()); }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
            if (runner == _runner && (Match == null || Match.State == ROUND_STATE.LOBBY) && Encoding.UTF8.GetString(token ?? Array.Empty<byte>()) == _connectionVersion) request.Accept();
            else request.Refuse();
        }
        public void OnSceneLoadStart(NetworkRunner runner) { if (runner == _runner) _sceneReady = false; }
        public void OnSceneLoadDone(NetworkRunner runner)
        {
            if (runner != _runner) return;
            _match = UnityEngine.Object.FindFirstObjectByType<CrowdMatch>(FindObjectsInactive.Include);
            try
            {
                if (_match != null && _match.gameObject.scene.path == IN_GAME_SCENE) DeadCrowd.Content.CrowdContent.Freeze(_match);
                _sceneReady = true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _shutdownPending = true;
                _shutdownMessage = "Invalid game content. Check settings and map assets.";
            }
        }
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { Debug.LogWarning($"[Crowd] Player left: {player}"); }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress address, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessions)
        {
            if (runner != _runner || State != CONNECTION_STATE.BROWSING) return;
            var rooms = new List<PublicRoom>();
            foreach (SessionInfo session in sessions)
            {
                if (!session.IsOpen || !session.IsVisible || session.PlayerCount >= session.MaxPlayers || !ValidCode(session.Name)) continue;
                if (session.Properties == null || !session.Properties.TryGetValue(CONTENT_VERSION, out var version) ||
                    !version.IsString || (string)version != _connectionVersion) continue;
                if (!session.Properties.TryGetValue(ROOM_MODE, out var mode) || !mode.IsInt ||
                    (int)mode != session.MaxPlayers || session.MaxPlayers != 2 && session.MaxPlayers != 4) continue;
                rooms.Add(new PublicRoom(session.Name, session.PlayerCount, session.MaxPlayers));
            }
            rooms.Sort((left, right) =>
            {
                int occupancy = right.Count.CompareTo(left.Count);
                return occupancy != 0 ? occupancy : string.CompareOrdinal(left.Code, right.Code);
            });
            PublicRooms = rooms;
            ReceivedRoomList = true;
        }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken token) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    }
}
