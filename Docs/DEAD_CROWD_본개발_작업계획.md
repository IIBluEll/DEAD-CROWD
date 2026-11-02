# DEAD CROWD 본 개발 작업계획

2026-10-28 기준입니다. 게임 규칙은 상세 기획서 v1.0을 따릅니다. 사용자가 프로토타입 동작을 확인했으며, 이 문서는 본 개발의 작업 순서와 현재 구현 범위를 기록합니다. 프로토타입 확인을 전체 인수 기준 통과로 간주하지 않습니다.

## 현재 상태와 판단

프로토타입은 초대 접속과 한 판의 이동·근접·권총·노출·NPC·결과·재경기를 연결했습니다. 접속 성공 후 10초 타이머가 세션을 종료하던 문제는 수정됐고 사용자도 정상 동작을 확인했습니다.

D1에서 저장된 Canvas·영어 TMP UI를 연결했고, D2에서는 `CrowdConnectionService`가 Runner·접속 시도·취소·종료·Scene 복구를 소유하도록 분리했습니다. `CrowdBootstrap`은 분리된 접속·입력·카메라 모듈을 호출하며 프로토타입 IMGUI의 호환 진입점도 유지합니다. `CrowdMatch`에는 참가자별 로드 응답을 기다리는 LOADING 단계를 추가했습니다. D3에서 입력·카메라를 분리하고 입력 권한 클라이언트의 이동 예측·재시뮬레이션 경로를 추가했습니다. D4에서 전투·AI 코드와 설정을 구분하고, D5에서 교체 가능한 기본 사람형 모델·모션·음향과 맵 편집 도구를 연결했습니다. 실제 두 PC 및 전체 통합 검증은 남아 있습니다.

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

D3 구현 기록은 아래와 같습니다. 실제 두 PC에서의 인수 검증은 별도로 남아 있습니다.


## D3 입력·카메라 분리와 본인 이동 예측 (2026-11-02)

### 구조와 동작

- `CrowdLocalInput`: Input System의 프레임 입력 수집, 시점 각도, 공격/재장전 Sequence, 메뉴의 중립 입력, 라운드 초기화와 관전 선택을 담당합니다. `CrowdBootstrap`은 이 클래스를 호출하고 Fusion 입력 콜백에 Tick용 입력을 제공합니다.
- `CrowdCameraController`: 3인칭 추적·조준 거리/FOV·벽 SphereCast·생존자 관전을 담당합니다. 관전 대상 목록은 재사용하여 매 프레임 List 생성을 줄였습니다. 기존 Scene의 카메라 참조를 사용하며 새 Scene/Prefab 컴포넌트 연결은 필요하지 않습니다.
- `CrowdMatch`: 호스트 전용이던 player step을 입력 권한 클라이언트의 본인 캐릭터에도 적용합니다. 클라이언트에서 Match를 명시적으로 시뮬레이션에 등록하고 `FixedUpdateNetwork`에서 본인만 실행합니다. NPC·다른 플레이어는 클라이언트에서 독립 시뮬레이션하지 않습니다.
- 서버와 클라이언트가 같은 `SimulatePlayer`를 사용하므로 대각선 크기 제한, 회전 상한, 걷기/질주/조준/근접 속도, 재장전 우선, 후딜, 입력 Sequence 소비가 같은 경로를 따릅니다. 클라이언트는 이동 제약에 필요한 행동·탄약을 예측하고, Fusion이 받은 호스트 상태로 Networked 값과 NetworkTransform을 복원한 뒤 미확정 Tick을 다시 실행합니다.
- CharacterController는 Move 전에 비활성/활성하여 rollback으로 복원된 Transform에 네이티브 컨트롤러 위치를 다시 맞춥니다. 호스트/NPC/예측 플레이어가 같은 캡슐·월드 충돌 코드를 사용합니다. 움직이는 상대의 클라이언트 위치와 호스트 위치가 다르면 보정은 발생할 수 있습니다.
- 클라이언트 예측은 공격 명중·피해·노출·소음·AI·결과를 실행하지 않습니다. 호스트의 공격 수집 후 동시 피해 결산 순서를 유지합니다. 로딩·카운트다운·결과·사망 상태에서는 본인 예측 이동을 실행하지 않습니다.
- `ConfirmedMagazine/Reserve/Action/ActionEndTick`은 호스트만 갱신합니다. 본 개발 HUD는 이 값을 읽으므로 예측 탄약을 확정 비용처럼 표시하지 않습니다. 행동과 정렬에 필요한 본인 자세는 예측할 수 있습니다.
- 데이터 구조가 바뀌어 프로토콜을 `dead-crowd-development-3`로 갱신했습니다. 참가자 모두 새 개발 빌드가 필요합니다.

예측/재시뮬레이션의 기준은 설치된 SDK API와 [Fusion Host Mode Prediction](https://doc.photonengine.com/fusion/v2/tutorials/host-mode-basics/3-prediction) 문서를 참고했습니다. 과거 위치를 되감는 명중 보정은 추가하지 않았습니다.

### 검증

`MovementSmokeTest`는 Development Build의 명시적인 `--motion-smoke host|client` 인수로만 설치됩니다. 기본 실행에서는 테스트 입력이나 지연을 적용하지 않습니다. 같은 PC 두 프로세스에서 카운트다운 이동/비용 차단, 실제 이동, 클라이언트 예측과 재시뮬레이션 호출, 발사 Sequence 유지 시 탄약 중복 소비 없음, 재장전의 확정 탄약을 검사합니다.

첫 실행에서 Host/Client 모두 PASS했습니다. 클라이언트에서 forward 예측 614회·재시뮬레이션 131회, 발사 1회, 재장전 후 확정 탄약 6/11을 확인했습니다. 이 횟수는 해당 실행의 측정치이며 성능 기준값이 아닙니다. 로그는 `Logs/D3Motion-motion-Host.log`, `Logs/D3Motion-motion-Client.log`입니다.

실행 도구: `Tools/Verify-Movement.ps1`. `-SimulatedDelay`는 해당 테스트 프로세스의 SDK NetworkConditions에 0.08초 고정 패킷 지연을 설정하며 프로젝트 설정 자산을 저장하지 않습니다. 입력 장치·카메라 시각 품질, 벽 모서리의 부드러움, 실제 두 PC·4인 환경 및 패킷 손실은 별도의 수동/통합 검증이 필요합니다. 두 PC 검증이 남아 있으므로 D3의 모든 인수 조건을 완료했다고 보지 않습니다.


최종 검증: `Logs/D3BuildVerified.log`의 Unity 컴파일·Windows 빌드와 저장 UI 검사에 성공했습니다. 원본과 검증 사본의 스크립트·meta 44개 해시가 일치합니다.

- 기본 네트워크의 첫 이동 검사: Host/Client PASS, 클라이언트 예측 614회·재시뮬레이션 131회.
- 80ms 고정 패킷 지연 검사: Host/Client PASS, 클라이언트 예측 617회·재시뮬레이션 2,631회. 발사 1회, 재장전 후 확정 탄약 6/11 (`Logs/D3Delay-motion-*.log`). RTT 실측치가 아닌 SDK에 설정한 지연값입니다.
- 최종 지연 검사: 두 번째 라운드까지 진행해 공격/재장전 Sequence가 재경기 시작에 새 입력으로 오인되지 않음을 검사했습니다. Host/Client PASS, 재경기 후 노출 0·탄약 6/12 (`Logs/D3Verified-motion-*.log`).
- 기존 접속·UI 회귀: 취소 후 늦은 성공 응답·생성 화면 복귀·재접속, 전체 Canvas 진행과 대기실 복귀, 카운트다운 이탈의 Host/Client 검사 PASS (`Logs/D3Regression-*.log`).

재경기 입력 초기화는 호스트가 마지막으로 처리한 공격/재장전 Sequence를 기준으로 합니다. 이전 라운드의 Sequence를 0으로 되돌려 의도하지 않은 공격이 발생하는 것을 방지합니다. 테스트의 라운드 종료 Tick 조정은 명시적인 개발 검증 실행에서만 적용됩니다.

빌드: `Builds/Development/DEAD-CROWD.exe`. 입력/카메라와 예측 코드 구현은 완료했으며, 실제 두 PC·수동 조작감·벽 접촉 보정·전체 행동 경계값 검증을 거쳐 D3 인수 완료를 판단해야 합니다. D4·D5 구현 기록은 아래에 이어집니다.


## D4·D5 커스텀 가능한 설정과 공통 표현 (2026-11-02)

### D4 구조·규칙

- CrowdGameSettings: 체력·이동·근접·권총·노출·NPC·카메라·모드별 개체 수를 Inspector에서 편집합니다. CrowdRules는 현재 세션 설정과 순수 계산의 진입점입니다. 기본값은 v1.0의 기존 값입니다.
- CrowdContentCatalog: 활성 GameSettings/Map/Presentation 참조를 한 곳에서 선택합니다. 설정을 복제한 뒤 Catalog 참조만 바꿔 변형을 구성합니다.
- CrowdContent: 접속 시 설정/맵을 복제해 고정하고 유효값을 검사합니다. 규칙·맵·Scene 충돌 구성의 SHA-256 서명을 연결 토큰과 라운드 로드 응답에 사용합니다. 읽을 수 없는 MeshCollider의 내부 형상이나 네트워크 본체 충돌 변경은 MapVersion 증가와 동일 빌드 배포도 필요합니다.
- CrowdMatch를 Player/Combat/Npc partial 파일로 구분했습니다. 공유 상태·권한 Tick·동시 피해 수집 후 결산은 원래 Match가 소유합니다. 클라이언트 예측은 Player 경로만 실행하며 AI·명중·피해·노출·소음은 호스트가 확정합니다.
- AttackId/SettledAttackId로 타격 중복 결산을 막고, ActionSequence/ActionEvent로 모션·소리 사건을 전달합니다. Animator 이벤트가 피해를 발생시키지 않습니다.

### D5 기본 콘텐츠·교체 지점

- Assets/Main/06_Content: 공통 사람형 Transform 리그 Prefab, Animator Controller, AnimationClip 11개, 합성 WAV 7개, 게임·맵·표현 설정과 Catalog 자산을 저장했습니다.
- 대기·보행·질주·조준 이동·근접·발사·재장전·사망과 별도 피격 레이어를 제공합니다. 권총과 근접 무기 오브젝트는 별도로 제어합니다. 플레이어와 NPC는 동일 외형·충돌·평상시 소리를 사용합니다.
- CrowdActorPresentation은 Animator·Renderer·무기 참조를 연결하는 교체용 어댑터입니다. 파라미터/State 이름, 근접 클립의 타격 Phase, 소리·거리·발소리 간격·색·피격 플래시를 프로필에서 설정합니다. 행동 모션은 호스트 타이머에 맞춰 샘플링하며 기본 근접 접촉 프레임을 선딜에 맞춥니다.
- 기본 맵은 기존 40m 시가지 블록아웃을 재사용합니다. MapProfile에 모드별 승인 조합과 배치 범위를 저장하고, 조합 내 참가자 배정을 섞습니다. 시작점 벽 겹침·직접 시야·완전한 NavMesh 경로를 실행 시 검사합니다. 기본은 2인 조합 2개와 4인 조합 1개입니다.
- 본 개발 Scene에 Map Editing Tools를 저장했습니다. Gizmos로 범위·시작점·NPC 시작 반경을 확인합니다. Content 메뉴에서 본 개발 전용 NavMesh를 재생성할 수 있으며 프로토타입 NavMesh를 덮어쓰지 않습니다.
- 모델과 음향은 교체 가능한 기본 콘텐츠입니다. 최종 미술·음향 품질과 1~2분 후 NPC 밀도/밸런스 판정은 별도 플레이 검증이 필요합니다.

커스텀 방법: [커스텀 가이드](DEAD_CROWD_커스텀_가이드.md). 기존 사용자 설정을 초기화하는 자동 재생성은 하지 않으며 새 기본 자산/도구를 추가했습니다. 프로토콜은 dead-crowd-development-5이므로 참가자 모두 새 빌드를 사용합니다.

### 검증 기록

- Unity 컴파일·Windows 개발 빌드·UI 저장 참조 검사 성공: Logs/D45FinalVerified.log.
- 기본 자산·AnimationClip·AudioClip 참조, 잘못된 설정 거절, 실행 중 스냅샷 불변, 실제 방에 다른 수치로 입장한 클라이언트의 ConnectionRefused를 확인했습니다. 호스트는 35초 동안 참가자 1명인 대기실을 유지했습니다: D45MismatchClient.log, D45CompatibleHost.log.
- 같은 PC 4인 프로세스가 승인 시작 조합·30 NPC 배치·전원 로드 완료 후 시작까지 통과했습니다: D45FfaHost.log, D45FfaClient1~3.log. 최종 Catalog 연결 전의 중간 빌드에서 확인한 기본 배치 검사이며 장시간 밀도/성능 검증은 아닙니다.
- 최종 단독 실행에서 이동·노출·발사·재장전·피해·결과·초기화 PASS: D45FinalSolo.log.
- 최종 SDK 80ms 패킷 지연에서 Host/Client 이동 예측·재시뮬레이션·발사 1회·재장전·재경기 초기화 PASS. 클라이언트 예측 677회, 재시뮬레이션 3,094회: D45FinalMotion-motion-*.log.
- 최종 Canvas의 생성/입장·준비·시작·HUD·결과·대기실 복귀와 로딩 실패 정리 PASS: D45FinalUi-*.log.
- URP SingleCameraRequest로 실제 렌더한 Camera 프레임에서 사람형 캐릭터·벽·피격 플래시를 확인했습니다: Logs/D45FinalGameplay.png. Overlay UI는 포함하지 않는 진단용 화면입니다. 캡처 방법은 [Unity URP 렌더 요청 문서](https://docs.unity.com/en-us/engine/6000.6/manual/cameras/urp/multiple/user-render-requests)와 설치 SDK를 참고했습니다.

미확인: 실제 두 PC·장시간 4인·패킷 손실·성능, 모든 커스텀 모델/셰이더 조합, 전체 행동 경계값과 기획서 QA 71개, 실제 음향 청취와 애니메이션 모든 전환의 시각 검토. 기본 콘텐츠 연결과 교체 구조의 구현 결과이며 D6 통합 검증 완료를 뜻하지 않습니다.


최종 커스텀 방어 검사: 시각용 Prefab의 Animator/Renderer 연결 누락과 Collider/NetworkObject 혼입을 접속 전에 안내합니다. 컴파일·빌드의 최종 로그는 Logs/D45CustomVerified.log입니다. 앞의 게임 실행 검사는 같은 기본 프로필에서 수행했으며 이 마지막 변경은 교체 Prefab의 구성 검사만 강화했습니다. 원본/검증 소스·meta 69개와 배포 빌드 파일의 해시를 대조했습니다.


## QA 제외 구현 보완 (2026-11-02, protocol v6)

사용자가 QA를 제외한 나머지 작업을 가능한 범위까지 진행하도록 요청했습니다. 이번에는 게임 실행·멀티플레이 시나리오·성능/밸런스 QA를 수행하지 않았습니다. 이전 v5의 PASS 기록을 v6의 실행 검증 결과로 간주하지 않습니다.

### 반영한 코드

- NPC 추격의 목표 갱신과 진행 기록 초기화를 구분했습니다. 추격 중 실제 이동이 없으면 제한 시간 후 현 위치 수색/짧은 재시도 대기로 전환합니다. 의도적인 공격 정지 동안에는 막힘 시간을 누적하지 않습니다.
- 움직이는 추격 목표는 실제 변위, 고정 목표는 경로 길이 감소로 진행을 측정합니다. 죽은 목표를 정리할 때 복구 대기도 초기화합니다.
- 근접/AI 주기 > 선딜, 양수 발사 간격·재장전·경로 갱신·실패 제한 등을 검증합니다. Tick 양자화로 타격/종료가 겹치면 타격 결산 전에 행동을 종료하지 않습니다.
- 시작점 중복과 NaN/Infinity 값을 거절하고, NPC 배치/배회 후보의 완전한 NavMesh 경로를 확인합니다.
- Animator 파라미터 이름/타입·기본 레이어 State를 Editor와 실제 인스턴스 초기화에서 검사합니다. 모든 캐릭터의 표현 초기화가 성공해야 라운드 로드 완료를 보고합니다.
- 새 라운드에는 모션·피격 플래시·발소리 누적 거리를 초기화합니다. 기존 기본 자산을 사용자가 편집한 뒤 빌드하더라도 생성기가 무기를 다시 채우지 않도록 변경했습니다.

### 반영한 콘텐츠

`Assets/Main/07_Polish`에 새 사람형 CommonCitizen, 피부/의상/신발/금속 머티리얼, 보행 팔 교차·상체 호흡과 근접 포즈를 보정한 AnimatorOverrideController/클립 11개, 타격/권총/휘두르기/발소리/재장전/사망의 합성 효과음 6개를 생성했습니다. 원래 기본 자산은 남아 있습니다. 활성 Catalog가 원래 기본 프로필을 사용하고 있을 때만 새 RefinedPresentation/CityLayout 변형을 선택하도록 했습니다.

본 개발 Scene의 City Details에 가로등과 영어 TMP 간판을 추가하고 도로/건물/광장/지붕 재질을 구분했습니다. 본 개발 전용 NavMesh를 다시 생성했습니다. 실제 플레이로 위장 밀도·동선·음향 크기를 판단하는 작업은 QA에 포함되어 이번에는 수치를 임의로 조정하거나 완료 판정하지 않았습니다.

### 확인 범위

검증 사본에서 Unity 컴파일·Windows 개발 빌드와 정적 설정/Animator 연결 검사에 성공했습니다. 로그: `Logs/ImplementationBuild.log`, `Logs/ImplementationBuildFinal.log`, 최종 `Logs/ImplementationCompileOnly.log`. Player/SmokeTest는 실행하지 않았습니다. 새 표현의 실행 시각 품질, AI 복구와 근접 경계의 실제 동작, 네트워크·성능·밸런스는 아직 미검증입니다.

배포 빌드: `Builds/Development/DEAD-CROWD.exe`. 편집 안내는 커스텀 가이드의 v6 항목을 따릅니다. 이번 결과를 '모든 구현과 검증이 끝난 출시본'으로 보지 않습니다.


## 플레이 피드백 조정 — v7 (2026-11-02)

선공 후 추격을 끊기 어렵다는 피드백에 따라 활성 GameSettings의 질주 속도를 5→6m/s로 바꾸고, EXPOSED_SPRINT_ALLOWS_RECOVERY를 추가했습니다. 이미 노출된 동안에는 실제 질주가 노출 증가·회복 대기 갱신을 하지 않습니다. 비노출 질주와 근접·발사 비용, 보행/NPC 추격 속도는 유지합니다.

맵 외곽 네 방향에 두 출구를 둔 L자 시야 차단물을 추가하고 NavMesh를 다시 생성했습니다. 기존 건물·시작점·NPC 개수는 유지합니다. 맵 버전에 +escape-1을 추가하고 프로토콜을 dead-crowd-development-7로 올렸습니다. 상세 규칙은 DEAD_CROWD_밸런스_변경_v7.md를 따릅니다.

확인은 컴파일·정적 구성 검사·NavMesh 생성·Windows 빌드까지이며, 이전 요청을 유지해 자동 플레이 QA는 실행하지 않았습니다. 선공 이익과 추격 탈출의 실제 체감은 새 빌드로 확인해야 합니다. 로그: Logs/BalanceEscapeBuild.log.
