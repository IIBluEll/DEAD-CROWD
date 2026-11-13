# DEAD-CROWD
술래잡기

## 기획 문서

- [현재 개발 기준: 상세 기획서 v1.0](Docs/DEAD_CROWD_상세_기획서_v1.0.md)
- [Word 문서: 상세 기획서 v1.0](Docs/DEAD_CROWD_상세_기획서_v1.0.docx)
- [이전 기획: 전투 중심 v0.5](Docs/DEAD_CROWD_기획서_v0.5.md)
- [이전 기획: v0.4](Docs/DEAD_CROWD_기획서_v0.4.md)

첫 버전은 PC·Photon Fusion 2 Host Mode를 기준으로, 초대 코드 방에서 1vs1·4인 개인전을 진행합니다. 작은 시가지에서 수동 위장, 노출 게이지, 근접·권총 전투와 최후 생존 승리를 검증합니다. 운반·탈출 목표는 후속 개발로 미룹니다.

## 프로토타입

- [1차 프로토타입 실행 가이드](Docs/DEAD_CROWD_프로토타입_실행_가이드.md)
- 실행 Scene: `Assets/Main/01_Scene/DeadCrowdPrototype.unity`
- 생성·검증·Windows 빌드 메뉴: `DEAD CROWD > Prototype`

## 본 개발

- [본 개발 작업계획과 현재 구현 범위](Docs/DEAD_CROWD_본개발_작업계획.md)
- 시작 씬은 `Assets/Main/01_Scene/MainMenu.unity`입니다. 방에 입장하거나 혼자 연습을 시작하면 `InGame.unity`를 불러옵니다.
- 메인 메뉴와 로비는 한국어·영어를 지원합니다. 공개 방 목록, 빠른 참가, 초대 코드 입장을 사용할 수 있습니다.
- 네트워크 프로토콜은 `dead-crowd-development-14`입니다. 참가자 모두 같은 개발 빌드를 사용해야 합니다.
- 생성·검증·Windows 빌드 메뉴: `DEAD CROWD > Development`

- [수치·캐릭터·소리·맵 커스텀 가이드](Docs/DEAD_CROWD_커스텀_가이드.md)
- `InGame` 콘텐츠는 `Assets/Main/06_Content/InGame/InGameCatalog.asset`에서 설정합니다.
- 설정 선택·기본 자산·맵 편집/내비게이션 메뉴: `DEAD CROWD > Content`

- [질주·노출 회복·탈출 동선 v7 변경](Docs/DEAD_CROWD_밸런스_변경_v7.md)
- [혼자 플레이: Solo Practice](Docs/DEAD_CROWD_솔로_플레이.md) — 타이틀에서 서버 접속 없이 시작하고, 결과 화면에서 다시 플레이할 수 있습니다.
- [후방 즉사 판정 v8 변경](Docs/DEAD_CROWD_후방_판정_v8.md)
- [노출 회복 대기 분리 v9 변경](Docs/DEAD_CROWD_노출_회복_v9.md)

## Synty 타운 시험 씬

`Assets/Main/01_Scene/DeadCrowdSyntyTown.unity`를 Unity에서 열고 Play를 누른 뒤 **Solo Practice**를 선택합니다. PolygonTown Demo의 전체 지형과 오브젝트 배치를 사용합니다. 플레이어와 NPC는 PolygonCityZombies 외형 12종과 Humanoid 모션을 공용으로 사용합니다. 라운드 Seed와 캐릭터 ID로 외형을 정합니다.

타운 전용 `TownGameSettings`는 솔로·1vs1에서 NPC 100마리, 4인전에서 140마리를 생성합니다. 시작점 10m 안에 참가자당 8마리를 먼저 배치한 뒤, 나머지를 도로와 건물이 있는 구역에 분산합니다. `TownCatalog.asset`에서 맵과 설정을 선택하며 전체 NavMesh는 씬의 `Polygon Town Demo` 루트에 연결돼 있습니다.
