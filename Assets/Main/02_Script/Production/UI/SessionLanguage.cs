using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadCrowd.UI
{
    internal static class SessionLanguage
    {
        private const string LANGUAGE_KEY = "Crowd.UiLanguage";
        public static bool Korean { get; private set; }

        private static readonly Dictionary<string, string> KOREAN_TEXT = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Find your opponents in the crowd. Be the last survivor."] = "군중 속에서 상대를 찾아 마지막까지 살아남으세요.",
            ["Create a room or join with an invite code."] = "방을 만들거나 초대 코드로 참가하세요.",
            ["Solo Practice"] = "혼자 연습",
            ["Create Room"] = "방 만들기",
            ["Join With Code"] = "코드로 참가",
            ["Settings"] = "설정",
            ["Quit Game"] = "게임 종료",
            ["The room mode cannot be changed after creation."] = "방을 만든 뒤에는 모드를 바꿀 수 없습니다.",
            ["1 vs 1 - 2 Players"] = "1 대 1 / 2명",
            ["Free For All - 4 Players"] = "개인전 / 4명",
            ["Create Invite Room"] = "초대 방 만들기",
            ["Back"] = "뒤로",
            ["6 letters or digits. No I, O, 0 or 1."] = "영문·숫자 6자리 / I, O, 0, 1 제외",
            ["Enter Invite Code"] = "초대 코드 입력",
            ["Join"] = "참가",
            ["Connecting"] = "연결 중",
            ["Please wait."] = "잠시 기다려 주세요.",
            ["Cancel Connection"] = "연결 취소",
            ["Lobby"] = "대기실",
            ["Copy Code"] = "코드 복사",
            ["Waiting for the host to start."] = "호스트가 경기를 시작할 때까지 기다려 주세요.",
            ["Ready"] = "준비 완료",
            ["Start Match (Host)"] = "경기 시작 (호스트)",
            ["Leave Room"] = "방 나가기",
            ["Match Over"] = "경기 종료",
            ["Return to Lobby (Host)"] = "대기실로 (호스트)",
            ["Menu"] = "메뉴",
            ["The match keeps running."] = "경기는 계속 진행됩니다.",
            ["Resume"] = "계속하기",
            ["Sensitivity and volume are saved on this PC."] = "감도와 음량 설정은 이 PC에 저장됩니다.",
            ["Mouse Sensitivity"] = "마우스 감도",
            ["Volume"] = "음량",
            ["Save and Close"] = "저장하고 닫기",
            ["TIME"] = "남은 시간",
            ["ALIVE"] = "생존",
            ["YOUR SCORE"] = "내 점수",
            ["LEAD"] = "선두",
            ["EXPOSURE"] = "노출",
            ["HEALTH"] = "체력",
            ["PISTOL"] = "권총",
            ["HIDDEN"] = "비노출",
            ["SUPPLIES"] = "수색 지점",
            ["Enter 6 letters or digits. I, O, 0 and 1 are excluded."] = "영문·숫자 6자리를 입력하세요. I, O, 0, 1은 사용할 수 없습니다.",
            ["Choose a 2-player or 4-player room."] = "2인 또는 4인 방을 선택하세요.",
            ["Invalid game content. Check settings and map assets."] = "게임 설정 또는 맵 데이터를 확인해 주세요.",
            ["Preparing offline practice..."] = "오프라인 연습을 준비하고 있습니다.",
            ["Connecting to the room..."] = "방에 연결하고 있습니다.",
            ["Connection timed out. Please try again."] = "연결 시간이 초과됐습니다. 다시 시도해 주세요.",
            ["Offline practice ready."] = "오프라인 연습이 준비됐습니다.",
            ["The host can start when everyone is ready."] = "전원이 준비하면 호스트가 시작할 수 있습니다.",
            ["Could not create a unique invite code. Please try again."] = "초대 코드를 만들 수 없습니다. 다시 시도해 주세요.",
            ["Could not connect. Check your connection and game version, then try again."] = "연결할 수 없습니다. 접속 상태와 게임 버전을 확인해 주세요.",
            ["Connection cancelled."] = "연결을 취소했습니다.",
            ["You left the room."] = "방에서 나왔습니다.",
            ["Could not restore the game scene. Restart the game."] = "게임 화면을 복구할 수 없습니다. 게임을 다시 실행해 주세요.",
            ["The room scene could not load in time. Please try again."] = "방을 불러오는 시간이 초과됐습니다. 다시 시도해 주세요.",
            ["The host could not load the round. The room has closed."] = "호스트가 경기를 불러오지 못해 방이 닫혔습니다.",
            ["Room not found. Check the invite code and game version."] = "방을 찾을 수 없습니다. 초대 코드와 게임 버전을 확인해 주세요.",
            ["The room is full."] = "방 정원이 가득 찼습니다.",
            ["The match is in progress. Join when it returns to the lobby."] = "경기 중입니다. 대기실로 돌아온 뒤 참가해 주세요.",
            ["Could not authenticate with the game service."] = "게임 서비스 인증에 실패했습니다.",
            ["The host and client must use the same game version."] = "호스트와 참가자의 게임 버전이 같아야 합니다.",
            ["The session ended during loading. Loading failed, timed out, or the host left."] = "경기를 불러오는 중 연결이 종료됐습니다.",
            ["The session ended. Create a room or join again."] = "연결이 종료됐습니다. 방을 만들거나 다시 참가해 주세요.",
            ["Draw - All Eliminated"] = "전멸로 무승부",
            ["Draw - Time Limit"] = "시간 종료로 무승부"
        };

        public static void Load()
        {
            Korean = PlayerPrefs.GetInt(LANGUAGE_KEY, Application.systemLanguage == SystemLanguage.Korean ? 1 : 0) == 1;
        }

        public static void Toggle()
        {
            Korean = !Korean;
            PlayerPrefs.SetInt(LANGUAGE_KEY, Korean ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static bool TryTranslate(string english, out string korean) => KOREAN_TEXT.TryGetValue(english, out korean);

        public static string TranslateStatus(string english)
        {
            if (string.IsNullOrEmpty(english)) return english;
            const string resultSeparator = " Confirmed result: ";
            int split = english.IndexOf(resultSeparator, StringComparison.Ordinal);
            string message = split < 0 ? english : english.Substring(0, split);
            if (TryTranslate(message, out string translated)) message = translated;
            if (split < 0) return message;

            string result = english.Substring(split + resultSeparator.Length).TrimEnd('.');
            if (TryTranslate(result, out translated)) result = translated;
            else if (result.StartsWith("Slot ", StringComparison.Ordinal) && result.EndsWith(" Wins", StringComparison.Ordinal) &&
                int.TryParse(result.Substring(5, result.Length - 10), out int slot)) result = $"{slot}번 플레이어 승리";
            return $"{message} 경기 결과: {result}";
        }
    }
}
