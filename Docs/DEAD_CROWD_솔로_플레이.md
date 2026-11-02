# Solo Practice

본 개발 Scene `Assets/Main/01_Scene/DeadCrowd.unity` 또는 개발 빌드의 타이틀에서 **Solo Practice**를 선택합니다.

- Photon Fusion의 `GameMode.Single`을 사용합니다. 온라인 방이나 다른 참가자가 필요하지 않습니다.
- 로컬 플레이어와 NPC 군중이 생성되고, 로딩 확인과 카운트다운을 거쳐 자동으로 시작합니다. NPC는 기존 군중·좀비 AI를 사용하며 상대 플레이어를 흉내 내는 봇은 추가하지 않습니다.
- 현재 기본 설정은 NPC 20명, 제한 시간 6분입니다. 기존 공격·노출·추격·재위장 규칙과 맵을 그대로 사용합니다.
- 사망하면 `Practice Over - Eliminated`, 시간까지 생존하면 `Practice Complete - Time Limit`으로 종료합니다. 혼자 남았다는 이유로 즉시 승리하지 않습니다.
- 결과 화면의 **Play Again**은 NPC와 라운드 상태를 초기화하고 다시 시작합니다. **Back to Title**은 로컬 세션을 종료합니다.
- Esc 메뉴에서도 **Back to Title**로 나올 수 있습니다. 메뉴를 열어도 시뮬레이션은 계속 진행되며 일시 정지는 지원하지 않습니다.
- 온라인 2인/4인 모드는 기존처럼 참가자 전원의 Ready와 호스트 시작을 사용합니다.

## 수정 위치

- `CrowdSessionController`: 오프라인 시작·자동 시작·재시작 및 솔로 결과 표시.
- `CrowdMatch.CanStart`: 솔로에서만 Ready 조건 생략. 참가자 수·로딩·라운드 초기화는 기존 경로 사용.
- `SessionPresenter_presenter` / `SessionScreen_view`: Solo 명령과 TMP 표시, 솔로에서 초대 코드 복사·Ready 숨김.
- `ProductionSceneEditor.InstallSoloPractice`: Scene에 저장된 TMP 버튼과 명령 연결을 추가합니다. 기존 명령 번호는 유지하며 반복 실행해도 버튼을 중복 생성하지 않습니다.
- 수치 변경은 기존 `GameSettings.asset`의 `DUEL_NPCS`, `ROUND_SECONDS`를 사용합니다. 온라인 2인전에도 공통 적용됩니다.

## 검증 범위

컴파일·Scene 참조 및 Windows 빌드 확인은 작업 완료 보고에 따릅니다. 사용자 요청에 따라 실제 플레이와 온라인 회귀 QA는 이번 작업에 포함하지 않습니다.
