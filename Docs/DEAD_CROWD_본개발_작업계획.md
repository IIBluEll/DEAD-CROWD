# DEAD CROWD 본 개발 작업계획

2026-10-28 기준입니다. 게임 규칙은 상세 기획서 v1.0을 따릅니다. 사용자가 프로토타입 동작을 확인했으며, 이 문서는 본 개발의 작업 순서와 현재 구현 범위를 기록합니다. 프로토타입 확인을 전체 인수 기준 통과로 간주하지 않습니다.

## 현재 상태와 판단

프로토타입은 초대 접속과 한 판의 이동·근접·권총·노출·NPC·결과·재경기를 연결했습니다. 접속 성공 후 10초 타이머가 세션을 종료하던 문제는 수정됐고 사용자도 정상 동작을 확인했습니다.

D1에서 저장된 Canvas·영어 TMP UI를 연결했고, D2에서는 `CrowdConnectionService`가 Runner·접속 시도·취소·종료·Scene 복구를 소유하도록 분리했습니다. `CrowdBootstrap`에는 입력·카메라와 프로토타입 IMGUI가 남아 있습니다. `CrowdMatch`에는 참가자별 로드 응답을 기다리는 LOADING 단계를 추가했습니다. 이동 예측과 전투·AI 책임 분리는 후속 단계이며 캐릭터는 임시 외형입니다.

## 작업 순서

| 단계 | 작업 | 주요 관련 규칙 | 완료 기준 |
| --- | --- | --- | --- |
| D1 화면·세션 진입 | 본 개발 Scene, 저장된 Canvas, UI MVP, 세션 명령·표시 데이터 연결 | LOB-01~03, UI-01~04 | 생성·입장·준비·시작·HUD·결과·복귀를 새 화면으로 연결 |
| D2 접속·로딩 수명 | 접속 서비스를 기존 Bootstrap에서 분리, 시도 ID·취소·예외 정리, 참가자별 로딩 확인 | MAT-01~02, ERR-01~02 | 지연 성공 응답·로드 실패·호스트 종료를 명확히 처리 |
| D3 조작·권한 이동 | 입력·카메라 분리, 본인 이동 예측·재시뮬레이션, 권한 위치·공통 충돌 | CTL-01~04, CAM-01~02, NET-02~05 | 두 PC와 지연 환경에서 이동·몸 회전·공격 잠금 확인 |
| D4 전투·AI·데이터 | 라운드 결산 순서를 유지한 전투·AI 분리, 설정 자산과 버전·검증, 경계·중복 판정 보강 | MEL, GUN, EXP, AI, NET-03~04 | 기능별 인수 기준으로 동작 확인, 초기값 단일 관리 |
| D5 표현·맵 | 공통 캐릭터·리그·애니메이션·음향·효과, 승인된 시작 조합, 실제 밀도 조정 | AST, SND, LVL | 플레이어 전용 식별 흔적 없이 타격 시각·몸 방향·시야 차단을 표현 |
| D6 통합 검증 | 4인·30 NPC, 관전 호스트·이탈·재경기, 지연·손실·빌드 성능과 플레이 기록 | QA-001~071, NFR, TST | 결과표와 재현 자료, 결함·미확인 범위를 명시 |

각 단계에서 새 코드의 컴파일과 해당 흐름을 확인합니다. 날짜·인력·아트 제작량이 산정되지 않은 상태에서 완료 일정을 확약하지 않습니다. 임의로 신규 모드·운반·탈출·호스트 이전·과거 히트박스 보정을 추가하지 않습니다.

## D1 구현 기록

### 구현 범위

- `Assets/Main/01_Scene/DeadCrowd.unity`: 본 개발 진입 Scene. 확인된 시가지·공용 캐릭터·권한 Match를 재사용합니다.
- `Game Application/Game Canvas`: 저장된 타이틀·방 생성·코드 입장·연결 중·대기실·HUD·카운트다운·결과·메뉴·설정 패널입니다. UI 오브젝트를 실행 중 생성하지 않으며 Inspector에서 편집할 수 있습니다.
- `SessionScreen_view`: 화면 표시와 UI 입력 전달만 담당합니다.
- `SessionModel_model`: 본인 상태와 공개 가능한 정보만 담는 화면 데이터입니다. 대상 정체·상대 체력·상대 위치를 표시 데이터에 넣지 않습니다.
- `SessionPresenter_presenter`: UI에서 요청한 동작을 세션 명령으로 연결하고 화면 전환을 반영합니다. MVP는 UI에만 사용합니다.
- `CrowdSessionController`: UI가 사용하는 세션 명령과 상태 읽기의 경계입니다. D1에서는 검증된 `CrowdBootstrap`을 연결하는 이행 계층으로 시작했습니다. D2 이후 접속 수명은 `CrowdConnectionService`가 소유하며 Bootstrap의 호환 API를 통해 연결합니다.
- 기존 Bootstrap에는 Canvas 사용 여부와 필요한 메뉴·설정·세션 읽기 API만 추가합니다. 기존 프로토타입 Scene은 계속 IMGUI로 실행할 수 있습니다.
- 현재 UI 문구는 영어이며 TextMeshPro와 프로젝트에 포함된 Liberation Sans SDF를 사용합니다. 코드 입력란도 TMP_InputField입니다. 초기 시스템 글꼴 사용 방식은 폰트 표시 문제로 교체했습니다.

### 실행과 편집

1. Unity에서 `DEAD CROWD > Development > Open Main Scene`을 실행하거나 `DeadCrowd.unity`를 엽니다.
2. Play 후 방을 만들거나 코드로 참가합니다. 정원 전원이 준비한 뒤 호스트가 시작합니다.
3. Canvas의 각 화면 그룹과 버튼·텍스트·바·입력란을 Hierarchy와 Inspector에서 편집합니다. 메뉴를 다시 실행해도 기존 Scene을 덮어쓰지 않습니다.
4. `DEAD CROWD > Development > Verify Main Scene`으로 저장된 참조를 확인합니다.
5. `DEAD CROWD > Development > Build Windows`로 `Builds/Development/DEAD-CROWD.exe`를 빌드합니다.

이번 UI는 기능을 연결한 본 개발의 첫 화면 구성입니다. 최종 미술·레이아웃 확정이나 마우스 조작·모든 해상도의 시각 검증이 끝났다는 뜻은 아닙니다. 이동 예측, 전투·AI 책임 분리, 사운드·애니메이션, 실제 두 PC·4인·성능 검증은 남아 있습니다. 참가자별 로딩 동기화와 배포 폰트의 후속 작업 기록은 아래에 정리합니다.

### 검증 기록

| 항목 | 결과 |
| --- | --- |
| Unity 컴파일·Windows 개발 빌드 | 성공. 사용자 Unity가 열린 상태이므로 검증 사본에서 실행 |
| Scene 저장 검사 | UI 참조, 21개 액션 연결, EventSystem 하나, Presenter·세션 연결 계층, Fusion Match 등록 키 확인 |
| 원본과 검증 소스 | 본 개발 C# 파일 6개 해시 일치 확인 |
| Canvas 이벤트 온라인 점검 | 동일 PC Host·Client 모두 통과. 타이틀·모드 선택·코드 입력·생성/입장·준비·시작·HUD·동일 결과·대기실 초기화 |
| 종료 조건 점검 방식 | UI 자동 점검에서는 경기 시작 3초 후 호스트의 종료 Tick을 앞당겨 시간 초과 무승부를 확인. 일반 게임의 제한 시간은 변경하지 않음 |
| 미확인 | OS 마우스 클릭·시각 배치·해상도별 UI, 실제 두 PC·4인, 지연·손실·성능 |

버튼 이벤트 자동 점검은 실제 OS 마우스 클릭과 화면 시각 검증을 대신하지 않습니다. 이 결과는 D1의 첫 흐름을 확인한 것이며 D2~D6 완료를 뜻하지 않습니다. 아래 D2 기록에서 접속 수명·오류·로딩 준비 확인의 분리 결과를 이어서 설명합니다.

로그: `Logs/DevelopmentBuildFinal.log`, `Logs/DevelopmentUiHost.log`, `Logs/DevelopmentUiClient.log`. 빌드: `Builds/Development/DEAD-CROWD.exe`. 본 개발 생성·검증 메뉴는 기존 Scene을 덮어쓰지 않으며, 이후 화면 편집은 저장된 Scene에서 진행합니다.

### 영어 UI와 TextMeshPro 전환

사용자 요청으로 본 개발 Scene의 Text 53개를 TextMeshProUGUI로, 코드 입력란 1개를 TMP_InputField로 전환했습니다. 버튼·HUD·안내·오류·관전·결과 문구는 영어로 변경했습니다. 폰트는 프로젝트에 포함된 Liberation Sans SDF와 아틀라스를 참조하며 시스템 글꼴에 의존하지 않습니다.

기존 Scene의 RectTransform과 버튼·Slider·화면 그룹 참조를 유지한 채 컴포넌트를 교체했습니다. `DEAD CROWD > Development > Convert UI to English TMP`는 같은 변환을 다시 적용할 수 있는 편집 메뉴이며, 새 Scene 생성 코드도 TMP를 사용합니다.

Unity 컴파일·Windows 개발 빌드, 레거시 Text/InputField 0개, TMP 텍스트 53개와 입력란 1개, 폰트·글자 지원·입력 viewport 참조를 확인했습니다. 동일 PC Host·Client가 TMP 입력란·Canvas 버튼 이벤트로 생성/입장·준비·시작·HUD·같은 결과·대기실 초기화까지 모두 통과했습니다. 실제 마우스 입력과 시각 배치 확인을 대신하는 결과는 아닙니다.

수정 빌드: `Builds/Development/DEAD-CROWD.exe`. 로그: `Logs/TmpEnglishBuildVerified.log`, `Logs/TmpUiHost.log`, `Logs/TmpUiClient.log`.


## D2 접속·로딩 수명

### 구현

- `CrowdConnectionService`가 Runner 생성·등록·종료·제거, 접속 시도 ID와 취소 토큰, 연결 상태와 오류 안내, 연결 종료 후 Scene 복구를 담당합니다. 중복 나가기는 같은 정리 작업을 기다리고, 정리가 끝나기 전 새 접속은 시작하지 않습니다.
- 접속 성공 직후에는 기존 10초 접속 제한 타이머를 해제합니다. 취소된 시도의 늦은 성공 응답은 연결 성공으로 반영하지 않으며, 정리 후 원래의 생성/코드 입력 화면으로 돌아갑니다. 초기 Scene·Match·본인 캐릭터가 준비되지 않으면 별도의 30초 제한으로 연결을 정리합니다.
- 라운드는 `LOBBY → LOADING → COUNTDOWN → PLAYING → RESULT`로 진행합니다. 호스트는 시작 요청 시 입장을 닫고 배치 Seed·플레이어·NPC를 준비합니다. 각 참가자는 Scene 로드 완료, 참가자·NPC 복제 수, 본인 위치의 NavMesh를 확인한 뒤 해당 RoundId와 프로토콜 버전으로 완료 응답을 보냅니다.
- 호스트는 전원 완료 후에만 3초 카운트다운을 시작합니다. 이전 라운드 응답과 중복 응답은 현재 상태를 바꾸지 않습니다. 로딩 중에는 이동·전투·경기 시간이 진행되지 않습니다.
- 30초 안에 응답하지 않거나 실패를 알린 일반 참가자는 연결을 종료하고, 남은 참가자는 준비를 해제한 대기실로 돌아갑니다. 호스트의 로드 실패는 방 전체 종료로 처리합니다. 로딩·카운트다운 이탈은 시작 취소로 처리합니다.
- 영어 TMP 대기실 화면을 로딩 표시에도 사용합니다. 슬롯별 Loading/Loaded, 완료 인원과 남은 시간을 표시하며 Ready/Start는 비활성화하고 나가기는 유지합니다. 기존 Scene 배치·저장 참조·패키지 설정은 변경하지 않습니다.
- 호스트 종료 시 클라이언트는 Runner와 Match를 정리하고 타이틀로 돌아갑니다. 이미 수신한 확정 결과가 있으면 종료 안내에 함께 보존합니다. 새 승자를 추측하지 않습니다.
- 네트워크 데이터 구조가 바뀌었으므로 프로토콜 식별자는 `dead-crowd-development-2`로 변경했습니다. 참가자 모두 같은 새 빌드를 사용해야 합니다. 예전 AppVersion의 방은 별도 매칭 범위이므로 옛 코드 입력 시 방을 찾지 못할 수 있습니다.

현재는 이미 열린 단일 맵의 준비 상태를 확인하는 로딩 단계입니다. 여러 맵·Addressables 다운로드·설정 자산 해시를 검증하는 구조는 아직 없으며 설정 자산/버전 통합은 D4 작업입니다. 네트워크 종료 원인을 확정할 수 없는 경우 원시 오류 대신 세션 종료 안내를 사용합니다.

### 검증 방법

`SessionLifecycleSmokeTest`는 Editor/Development Build에서 명시적인 `--session-smoke` 인수가 있을 때만 설치됩니다. 저장 Scene에는 추가하지 않습니다. 정상 실행에서는 성공 응답 지연·로딩 응답 보류·타이머 조작이 적용되지 않습니다. 접속 취소 검사는 실제 성공 응답을 받은 후 테스트에서 전달을 3초 늦추며, 시간 초과 검사는 기획서의 실제 30초 제한을 기다립니다.

| 항목 | 결과 / 근거 |
| --- | --- |
| Unity 컴파일·Windows 개발 빌드 | 검증 사본에서 성공, TMP 53개·입력란·UI 참조 검사 통과 |
| 잘못된 코드·없는 방 | 서버 요청 전 형식 거절, 실제 GameNotFound 이후 Runner 정리 |
| 취소 후 성공 응답·재접속 | 실제 StartGame 성공 응답 전달을 지연시킨 상태에서 취소, Runner 제거 후 재생성 성공 |
| 접속 제한 회귀·중복 나가기 | 접속 후 12초 유지, 나가기 중복 호출이 동일 Task를 기다린 뒤 정리 |
| UI 회귀 | 같은 PC Host/Client의 생성·입장·준비·시작·HUD·결과·대기실 초기화 통과 |
| 지연 로딩·이전 RoundId | 응답 보류 동안 로딩 유지, 오래된 응답 거절, 전원 완료 후 경기 시작 |
| 참가자 로드 실패·30초 초과 | 실패 참가자 타이틀 복귀, 남은 호스트 연결 유지·대기실 초기화 |
| 호스트 로딩 시간 초과 | 호스트와 클라이언트 모두 연결 정리 |
| 로딩·카운트다운 이탈 | 전투 미시작, 남은 참가자 준비·NPC·시간 초기화 |
| 경기 중 호스트 종료 | 클라이언트 Runner/Match 정리·타이틀 복귀 |
| 결과 후 호스트 종료 | 클라이언트가 이미 받은 시간 초과 무승부 결과를 종료 안내에 보존 |

로그: `Logs/SessionD2BuildFinal.log`, `Logs/D2Final-*.log`. 10개 시나리오의 19개 실행 프로세스가 PASS와 종료 코드 0을 반환했습니다. 없는 방을 요청하는 테스트의 GameNotFound 로그는 의도한 실패 입력입니다. 취소 화면 복귀 보강 후 최종 빌드와 lifecycle/UI도 재검증했습니다. `Logs/SessionD2BuildRelease.log`, `Logs/D2Release-Lifecycle.log`, `Logs/D2Release-ui-Host.log`, `Logs/D2Release-ui-Client.log`에서 성공 응답 취소 후 생성 화면 복귀와 기존 UI 흐름의 PASS를 확인했습니다. 원본과 검증 사본의 스크립트·meta 37개 해시가 일치합니다.

테스트 재실행: Windows 개발 빌드 후 PowerShell에서 `./Tools/Verify-SessionLifecycle.ps1`을 실행합니다. 실행 파일이 다른 위치에 있으면 `-GamePath`를 지정하고, 일부만 실행하려면 `-Cases @('lifecycle', 'ui')`처럼 선택합니다. 실행마다 별도의 로그 이름을 사용하며 테스트가 띄운 프로세스만 관리합니다.

확인하지 않은 항목: 실제 두 PC·4인·패킷 지연/손실·OS 마우스·화면 시각 배치, 초기 Scene 로더 자체의 실패 주입, 별도 설정 자산 버전 불일치. 같은 PC의 로딩 응답 지연은 실제 네트워크 지연 환경 검증을 대신하지 않습니다.

구현 참고: 설치된 Fusion 2.1.3 SDK의 StartGame/Shutdown/Disconnect/Scene 콜백 시그니처와 [Photon NetworkRunner 문서](https://doc-api.photonengine.com/en/fusion/current/class_fusion_1_1_network_runner.html)를 확인했습니다. 종료된 Runner를 재사용하지 않고 새 인스턴스로 접속합니다.

다음 구현 단계는 D3의 입력·카메라 분리와 본인 이동 예측입니다.
