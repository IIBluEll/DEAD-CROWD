# DEAD CROWD 1차 프로토타입 실행 가이드

기준 기획은 `DEAD_CROWD_상세_기획서_v1.0.md`입니다. 이 프로토타입은 임시 외형과 UI로 한 판의 흐름을 확인하기 위한 구현이며, v1.0의 모든 인수 조건을 통과한 완성 버전은 아닙니다.

## 실행

1. Unity `6000.3.20f1`에서 프로젝트를 열고 스크립트 컴파일이 끝날 때까지 기다립니다.
2. `Assets/Main/01_Scene/DeadCrowdPrototype.unity`를 엽니다. 아직 Scene이 없으면 `DEAD CROWD > Prototype > Create or Open Prototype`을 실행합니다.
3. Play를 누릅니다.
4. 혼자 확인할 때는 `SOLO PRACTICE (OFFLINE)`을 선택합니다. 대기실에서 `READY` 후 `HOST: START ROUND`를 누릅니다. 연습은 온라인 연결 검증을 대신하지 않습니다.
5. 온라인은 호스트가 `1 vs 1` 또는 `4 PLAYER FFA`를 선택하고 `CREATE INVITE ROOM`을 누릅니다. 다른 참가자는 같은 빌드에서 여섯 자리 코드를 입력하고 `JOIN WITH CODE`를 누릅니다.
6. 정원 2명 또는 4명이 모두 준비하면 호스트가 시작합니다. 3초 카운트다운 후 전투가 시작됩니다.

방 생성과 입장은 같은 Fusion App ID, `kr` 리전, 프로토타입 빌드 식별자를 사용합니다. 입장 클라이언트는 없는 방을 자동 생성하지 않습니다. 진행 중인 방은 신규 입장을 막습니다.

## 조작

| 입력 | 기능 |
| --- | --- |
| WASD | 카메라 수평 방향 기준 이동 |
| 마우스 | 카메라 회전 |
| Shift | 질주, 실제 이동 시 노출 누적 |
| 왼쪽 클릭 | 비조준 시 근접 공격 |
| 오른쪽 버튼 유지 + 왼쪽 클릭 | 몸 정렬 후 권총 발사 |
| R | 재장전 |
| Esc | 메뉴, 본인 전투 입력 차단. 온라인 시간은 계속 진행 |
| 사망 후 A / D | 생존자 관전 전환 |

공격은 새 클릭마다 한 번입니다. 실행 불가능한 클릭은 예약되지 않습니다. 정지 중 카메라만 돌리면 몸 방향은 유지됩니다. 플레이어와 NPC는 같은 임시 캡슐 외형과 정면 표시, 피격 크기를 사용합니다.

## 연결한 기능

- 비공개 초대 방, 2명·4명 정원, 참가자 준비와 호스트 시작
- 별도 저장된 시가지 블록아웃, 네 시작점, 공용 캐릭터 Prefab, 구운 NavMesh
- 호스트 기준 이동·회전·공통 몸 충돌
- 전방 부채꼴에서 가려지지 않은 가까운 대상 한 명을 타격하는 근접 공격, 타격 Tick의 후방 즉사
- 조준 정렬, 히트스캔 권총, 몸통→총구→조준점 차단, 탄약·재장전
- 행동 기반 노출, 100에서 노출 진입하고 0에서 해제
- 호스트 NPC 배회·소음 조사·추격·공격·마지막 위치 수색·기억 해제
- 같은 Tick의 타격 수집 후 피해 결산, 사망·관전·승패·시간 초과
- 결과에서 호스트의 대기실 복귀, 준비와 경기 상태 초기화
- 오프라인 연습, 최소 HUD와 감도·음량 메뉴

## 구조

| 파일 | 역할 |
| --- | --- |
| `CrowdRules.cs` | 초기 수치와 방향·노출·재장전·승패 계산 |
| `CrowdBootstrap.cs` | Fusion 연결 수명, Input System 입력, 로컬 카메라·임시 IMGUI |
| `CrowdMatch.cs` | 호스트의 라운드 상태·전체 시뮬레이션·피해 결산·AI |
| `CrowdActor.cs` | 공통 네트워크 상태·캐릭터 이동·경로·피격·임시 표현 |
| `CrowdPrototypeEditor.cs` | Scene·Prefab·재질·NavMesh 생성, 규칙·배치 확인, Windows 빌드 |
| `CrowdSmokeTest.cs` | 명시적인 개발 빌드 실행 인자를 받았을 때만 동작하는 자동 점검 |

중앙 호스트 시뮬레이션을 사용해 캐릭터 실행 순서에 따라 상호 공격이 사라지지 않게 했습니다. NPC AI와 난수는 클라이언트에서 실행하지 않습니다. 기존 SampleScene과 설치 패키지는 유지합니다.

## 검증 메뉴

- `DEAD CROWD > Prototype > Verify Rules and Scene`: 방향 경계, 노출 유지·해제, 탄약, 승패, 시작점의 NavMesh·거리·시야 차단을 검사합니다.
- `DEAD CROWD > Prototype > Build Windows Prototype`: 위 확인 후 `Builds/Prototype/DEAD-CROWD.exe`를 개발 빌드합니다.

개발 빌드에서 `--crowd-smoke solo`는 오프라인 자동 점검을, `--crowd-smoke host --crowd-code TSTABC`와 `--crowd-smoke client --crowd-code TSTABC`는 두 프로세스 온라인 점검을 실행합니다. 자동 점검은 입력을 생성하며, 오프라인 점검의 종료 단계에서만 시간 경계를 앞당깁니다. 일반 실행에서는 자동 점검을 사용하지 않습니다.

## 현재 제한과 별도 확인할 항목

- 플레이어 이동은 호스트 판정과 NetworkTransform 보간을 사용합니다. 본인 이동 예측은 아직 적용하지 않았으므로 지연이 있는 환경의 조작 반응은 별도 개선 대상입니다.
- 카메라 조준 전환은 임시 구현입니다. 애니메이션·사운드와 최종 캐릭터 모델은 준비하지 않았습니다. UI는 런타임 IMGUI로, 편집 가능한 완성형 Canvas UI가 아닙니다.
- 접속·Scene 로드 실패 처리는 SDK 시작 결과를 사용합니다. 참가자별 로딩 상태 UI, 로드 준비 응답의 별도 관리, 연결 오류의 모든 세부 분류는 후속 보완 대상입니다.
- AI와 물리 쿼리의 할당 최적화, 4인·30 NPC의 빌드 성능, RTT·손실 환경, 실제 두 PC 플레이, 전체 QA-001~071은 따로 검증해야 합니다.
- 운반·탈출·안전 구역·호스트 이전·재접속 복구는 포함하지 않습니다.

## 이번 작업의 확인 결과

2026-10-28, Unity `6000.3.20f1`, Fusion `2.1.3 Stable Build 2390`에서 확인했습니다.

| 확인 | 결과와 범위 |
| --- | --- |
| Unity 컴파일 | 검증용 프로젝트 사본에서 성공. 원본과 사본의 프로토타입 소스가 같은지 해시로 확인 |
| 규칙·Scene 검사 | 36개 통과. 방향 경계, 노출 유지·해제, 재장전 계산, 승패, 시작점 배치, Fusion Scene 등록 키 포함 |
| Windows 빌드 | Windows 64비트 개발 빌드 성공, `Builds/Prototype/DEAD-CROWD.exe`에 제공 |
| 오프라인 자동 실행 | NPC 20마리, 준비·시작, 권총 발사·피해·재장전, 질주 노출, 근접 명중, 시간 경계 종료, 대기실 복귀와 초기화 확인 |
| Photon 온라인 자동 실행 | 동일 PC의 Host·Client 두 프로세스가 `kr` 비공개 방에 접속. NPC 20마리, 준비·시작, 양쪽 근접 공격, 같은 승자 결과 수신, 대기실 복귀와 초기화 통과 |
| 실제 두 PC·4인 | 미실행 |
| 지연·손실·성능·전체 QA | 미실행 |
| 화면 시각 검증 | 숨김 실행의 화면 캡처가 검은 화면으로 기록되어 시각 검증 근거로 사용하지 않음. 실제 표시 창에서 확인 필요 |

원본 프로젝트가 사용자 Unity에서 열린 상태여서 `Builds/PrototypeVerification`에 만든 사본으로 컴파일·생성·빌드했습니다. 사본은 원본에 설치된 같은 버전의 패키지를 로컬 경로로 참조했습니다. 생성한 Scene·Prefab·재질·NavMesh와 대응하는 GUID는 원본으로 복사했습니다. 원본의 패키지 목록은 이 검증 절차 때문에 변경하지 않았습니다.

실행 점검에서 발견한 Scene 네트워크 오브젝트 미등록은 최초 Scene 저장 후 Fusion 베이크·재저장을 수행하도록 수정했습니다. 검증 메뉴도 등록에 필요한 `SortKey`가 0이 아닌지 검사합니다.

로그는 `Logs/CrowdBuildVerified.log`, `Logs/CrowdSoloVerified.log`, `Logs/CrowdHostSmokeFinal.log`, `Logs/CrowdClientSmokeFinal.log`에 있습니다. 로그·빌드·검증 사본은 기존 `.gitignore`의 `Logs`·`Builds` 규칙에 따라 Git 추적 대상에서 제외됩니다.

### 접속 후 자동 종료 수정

2026-10-28, `Connect_async`가 접속에 성공한 뒤에도 10초 취소 타이머를 유지하는 문제를 수정했습니다. Fusion 2.1.3은 해당 취소 토큰에 Runner 종료 콜백을 등록하므로, 성공 시 `CancelAfter(Timeout.Infinite)`로 접속 제한 시간을 해제합니다. 사용자 취소·나가기·종료 시의 명시적 취소는 유지합니다. 종료·연결 유실·참가자 이탈 원인을 개발 로그에도 기록합니다.

수정 후 Windows 개발 빌드와 규칙·Scene 검사 36개가 통과했습니다. `--crowd-smoke hold`로 방 생성 후 35초 대기실 유지를 확인했고, 별도 Host/Client 점검에서는 호스트가 20초 대기한 뒤 클라이언트가 접속해 경기 시작·같은 승자 확인·대기실 초기화까지 양쪽 모두 통과했습니다. 동일 PC의 프로세스 점검이며 실제 두 PC·4인 환경을 검증한 결과는 아닙니다.

수정 빌드는 `Builds/Prototype/DEAD-CROWD.exe`입니다. 이번 결과는 `Logs/CrowdDisconnectFixBuild.log`, `Logs/CrowdDisconnectFixHold.log`, `Logs/CrowdDisconnectFixHost.log`, `Logs/CrowdDisconnectFixClient.log`에 기록했습니다.
