# DEAD CROWD 개발 환경

2026-10-28 저장소 파일과 실제 빌드에서 확인한 기준입니다.

| 항목 | 기준 |
| --- | --- |
| Unity | 6000.3.20f1, revision c9ba695d4f07 |
| 초기 빌드 | Windows 64비트 개발 빌드 |
| Fusion | 2.1.3 Stable Build 2390 |
| 토폴로지 | Host Mode, 접속자는 Client |
| 입력 | Input System 1.19.0, Active Input Handling은 Input System |
| UI | uGUI 2.0.0의 TextMeshPro, TMP_InputField, InputSystemUIInputModule, 1920×1080 기준 CanvasScaler |
| UI 언어·폰트 | 영어, 프로젝트 내 Liberation Sans SDF 폰트·아틀라스 사용 |
| 렌더링 | URP 17.3.0 |
| 내비게이션 | AI Navigation 2.0.13 |
| 공통 리전 | kr |
| App ID | 저장소 PhotonAppSettings에 설정. 문서에는 값 자체를 중복 기록하지 않음 |
| Peer Mode | Single, 프로세스 하나에 peer 하나 |
| 기본 정원 | 4명. 방 생성 시 1vs1은 2명, 개인전은 4명 |
| 시뮬레이션·송신 | 60Hz, 매 Tick 송신 |
| 과거 히트박스 보정·호스트 이전 | 초기 범위 제외 |
| 직렬화·백그라운드 실행 | Force Text, Run In Background 활성 |

현재 프로토콜 식별자는 `dead-crowd-development-12`입니다. 현재 소스로 다시 빌드한 프로토타입 Scene과 본 개발 Scene이 이 값을 공유합니다. 참가자 모두 새 개발 빌드를 사용해야 합니다. 설정 자산과 맵·Scene 충돌 서명도 별도로 확인합니다.

초기 수치의 문서 기준은 상세 기획서 v1.0 제17절입니다. D4부터 런타임은 ContentCatalog가 선택한 GameSettings/MapProfile의 세션 스냅샷을 사용합니다. 규칙·맵·Scene 충돌 서명을 연결과 로드 응답에 확인하며, Seed와 맵 버전/서명을 로그에 남깁니다. 피격·행동·발사는 권한 사건 Sequence를 사용합니다. 모든 사건을 묶는 정식 리플레이와 D6 성능 자료는 아직 없습니다.

자동 검증은 같은 PC의 복수 프로세스입니다. 최소 하드웨어 사양, 호스트 Tick P95·대역폭·GC, 실제 두 PC·4인·패킷 손실의 성능을 측정한 자료는 아닙니다.
