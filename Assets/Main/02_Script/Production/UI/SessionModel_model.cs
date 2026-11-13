using System;
using System.Collections.Generic;
using DeadCrowd.Session;

namespace DeadCrowd.UI
{
    public enum SESSION_SCREEN { TITLE, CREATE, JOIN, CONNECTING, LOBBY, COUNTDOWN, PLAYING, RESULT, LOADING, PUBLIC_ROOMS }
    public enum SESSION_ACTION { SHOW_CREATE, SHOW_JOIN, BACK, DUEL, FFA, CREATE, JOIN, CANCEL, READY, START, COPY_CODE, LEAVE, RETURN_LOBBY, RESUME, SETTINGS, SAVE_SETTINGS, QUIT, SOLO, SHOW_PUBLIC_ROOMS, CREATE_PUBLIC, QUICK_DUEL, QUICK_FFA, ROOM_PREVIOUS, ROOM_NEXT, ROOM_RETRY, SELECT_PUBLIC, SELECT_INVITE }

    public readonly struct LobbyParticipant
    {
        public readonly bool Connected;
        public readonly bool Ready;
        public readonly bool Loaded;
        public readonly bool Local;
        public readonly bool Host;

        public LobbyParticipant(bool connected, bool ready, bool loaded, bool local, bool host)
        {
            Connected = connected; Ready = ready; Loaded = loaded; Local = local; Host = host;
        }
    }

    // Presentation data only. World identity and other players' health never enter this model.
    public sealed class SessionModel_model
    {
        public SESSION_SCREEN Screen;
        public bool Korean;
        public bool Busy;
        public bool Host;
        public bool Practice;
        public bool Ready;
        public bool CanReady;
        public bool CanStart;
        public bool MenuOpen;
        public bool SettingsOpen;
        public bool ShowCrosshair;
        public float HealthRatio;
        public float ExposureRatio;
        public bool Exposed;
        public bool HasLocalPlayer;
        public bool LocalAlive;
        public bool SearchEnabled;
        public bool Reloading;
        public int RoundSeconds;
        public int AliveCount;
        public int SearchScore;
        public int LeadSearchScore;
        public int HealthValue;
        public int MaxHealth;
        public int ExposurePercent;
        public int Magazine;
        public int Reserve;
        public float ReloadSeconds;
        public float SearchProgress = -1f;
        public int Capacity = 2;
        public const int ROOMS_PER_PAGE = 5;
        public IReadOnlyList<PublicRoom> Rooms = Array.Empty<PublicRoom>();
        public int RoomPage;
        public bool RoomListReady;
        public bool RoomListFailed;
        public bool CreatePublic = true;
        public bool PublicSession;
        public int ConnectedPlayers;
        public readonly LobbyParticipant[] Participants = new LobbyParticipant[4];
        public int RoomPageCount => Math.Max(1, (Rooms.Count + ROOMS_PER_PAGE - 1) / ROOMS_PER_PAGE);
        public string Status = "Create a room or join with an invite code.";
        public string Code = "";
        public string Slots = "";
        public string Countdown = "";
        public string Health = "";
        public string Exposure = "";
        public string Ammo = "";
        public string Time = "";
        public string Crosshair = "";
        public string Spectator = "";
        public string Result = "";

        public string Text(string english, string korean) => Korean ? korean : english;
    }
}
