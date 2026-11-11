# DEAD LETTER UI 개선 1단계 분석

## 공통 폰트 준비 결과

- 공식 Galmuri 저장소의 `GalmuriMono11.ttf` v2.40.4를 사용한다.
- 라이선스는 SIL Open Font License 1.1이며 원문을 폰트와 같은 폴더에 보관한다.
- `GalmuriMono11 TMP.asset`은 Dynamic + Multi Atlas 방식이며 원본 TTF 참조를 유지한다.
- 빌드 시 동적 데이터를 지우지 않도록 TMP Settings를 구성했다.
- 한글 완성형 전체(U+AC00~U+D7A3), 기본 영문/숫자/기호 및 실제 UI 표본 문자열을 자동 검증한다.
- GalmuriMono11 자체가 필요한 글리프를 포함하므로 별도 Fallback은 지정하지 않았다.
- 안전한 별도 씬 `UI_Font_Test`에서 한글·영문·숫자·특수문자와 16:9 배치를 검증한다.

## 공통 디자인 기준

- 기준 해상도: 1920×1080, `CanvasScaler.ScaleWithScreenSize`, Match 0.5
- 기본 폰트: GalmuriMono11 / TMP Dynamic / Multi Atlas
- 배경: `#050505`에 가까운 검정, 보조 배경은 짙은 갈색
- 본문: 밝은 베이지, 강조: 차분한 주황, 모니터: 어두운 초록
- 권장 크기: 제목 36, 소제목 24, 본문 18, 보조문 16
- 픽셀 폰트는 정수 크기와 정수 위치를 우선하고 작은 크기에서 과도한 자동 축소를 피한다.
- 상태 색상은 성공/정상에 차분한 초록, 경고/오류에 탁한 적색을 사용한다. 색만으로 상태를 구분하지 않고 반드시 문구나 아이콘을 함께 표시한다.

## UI 및 대사 기획 자료 비교

두 참고 자료는 향후 UI와 대화 시스템의 목표 화면으로 분류하고, 현재 구현 상태를 설명하는 근거로 사용하지 않는다. 기존 UI 개선 1단계 지시와 충돌하는 내용은 적용하지 않는다.

### 반영한 기준

- 기본 HUD의 목표 배치는 DAY 좌측 상단, 업무 목록 우측 상단, 대화창 좌측 하단, 조준점 중앙으로 정리한다.
- 대화창은 화자 이름, 본문, 다음 대사 표시와 향후 2개 선택지를 수용할 수 있어야 한다.
- 대화 데이터는 인형탈, 음주운전, 모범생, 부실공사, 익사자 5종과 1~3일차 변화를 지원하는 방향으로 분리해야 한다.
- 모니터 화면은 신원 정보, 조회 결과, 일치/불일치, 오류 문구를 표현할 수 있어야 한다.
- 공통 테마에 보조 글자, 성공, 위험 색상을 추가하고 폰트 테스트 문자열에 등장인물명과 상태 문구를 포함한다.
- 향후 일시정지, DAY 시작/종료, 로딩, 경고/시스템 오류 화면도 같은 폰트와 색상 체계를 재사용한다.

### 적용하지 않은 충돌 항목

- 참고 PDF의 `E키 조사`는 현재 확정 입력인 우클릭/F와 충돌하므로 채택하지 않는다.
- 참고 PDF의 발송 국가 입력 화면은 폐지된 국가 코드 시스템이므로 다시 구현하지 않는다.
- 오브젝트 확대 관찰, 인벤토리, 공포 경고·글리치·은신·게임오버는 1단계 및 기존 7단계 UI 개선 범위 밖이므로 구현하지 않는다.
- 참고 문서의 대사와 3일차 미완 내용은 향후 대화 데이터 설계 자료로만 보관하고 이번 단계에서 런타임 대사를 교체하지 않는다.

## 현재 UI 구조

### 기본 HUD

- `FirstPersonInteractor`: 중앙 Raycast, 거리 3m, 우클릭/F 입력 및 IMGUI 상호작용 안내. 별도 Canvas/Prefab 없음.
- `LetterTutorialHUD`: 우측 상단 DAY, 완료 건수, 단계별 업무 토글(T키/버튼). `LetterTaskState`를 직접 조회하며 IMGUI 사용.
- 고정 조준점은 현재 구현되어 있지 않다.
- 민원 처리 개수는 `LetterTutorialHUD`에서 `CompletedRequests/RequiredRequestsPerDay`로 표시한다.
- 관련 Canvas/Prefab은 없으며 각 스크립트가 IMGUI를 직접 그린다.

### 대화 시스템

- `TestCustomerNPC`: 도착/대기/접수/완료 상태와 대사를 한 클래스에서 관리하고 IMGUI 대화 패널을 표시한다.
- 이름은 `customerDisplayName`, 대사는 단일 문자열과 표시 종료 시각으로 관리한다.
- 플레이어 선택지 및 별도 대화 진행 입력은 없다. 월드 상호작용 우클릭/F로 상태가 진행된다.
- `DayCycleManager`와 `LetterTaskState`에 직접 연결되어 업무 상태를 갱신한다.
- 관련 Canvas/Prefab과 선택지 데이터 구조는 없다.
- 기획 자료에는 5종 민원인의 3일치 대사와 2개 선택지가 정의되어 있어, 향후 `TestCustomerNPC`의 표시·이동 책임과 대화 데이터/분기 책임을 분리할 필요가 있다.
- 현재 `DayCycleManager`의 다섯 번째 민원인은 `불량이`지만 최신 대사 기획은 `익사자`로 정의되어 있다. 게임 데이터 변경이므로 이번 단계에서는 수정하지 않고 대화 시스템 단계의 확인 항목으로 남긴다.

### 모니터 UI

- 창구와 작업실에 CRT 모니터 그래픽 오브젝트는 있으나 신원 확인용 UI/Canvas/Script는 없다.
- 폐지된 국가 코드 입력 코드와 참조는 발견되지 않았다. 다시 구현하지 않는다.
- 기획 자료의 신원 확인 화면은 향후 참고하되 이름·식별 정보·일치/불일치·오류 상태만 대상으로 삼고 국가 입력 흐름은 제외한다.

### 편지 작업 UI

- `LetterWorkstation`: IMGUI 전체 화면 작업 UI, ESC 닫기, 플레이어/Interactor 비활성화 및 커서 해제를 담당한다.
- `LetterTaskState`: 편지 ID, 민원인 ID/표시명, 필수·부착 우표, 도장, 발송, 작업대 열림 상태를 보존한다.
- 우표 6종(불/물/전기/충격/약/술), 같은 우표 재선택 시 제거, 좌측 하단 부착, 우측 하단 도장 기능이 구현되어 있다.
- `MailDropBox`가 정확한 우표 조합과 도장을 확인한 뒤 발송 상태를 설정한다.
- 별도 Canvas/Prefab은 없고 스크립트와 런타임 생성 오브젝트에 의존한다.

### 일시정지 UI

- 게임 씬용 일시정지 메뉴와 설정 패널은 없다.
- 게임 중 ESC는 `FirstPersonController`에서 커서 잠금만 해제하고 `Time.timeScale`은 변경하지 않는다.
- 작업대가 열린 동안 ESC는 작업대 닫기에 사용된다.
- 메인 메뉴 옵션은 `MainMenuController`가 런타임 uGUI로 생성하며 게임 씬 일시정지와는 별개다.
- 게임 씬용 Canvas/Prefab은 없다.

### DAY 전환 UI

- `DaySleepSofa`: 하루 5건 완료 여부를 확인하고 메시지를 IMGUI로 표시한다.
- `DayCycleManager.TryAdvanceDay`: 민원인을 숨기고 `LetterTaskState.BeginNextDay` 후 다음 날 민원 순서를 준비한다.
- 전용 암전, DAY 시작 화면, 전환 애니메이션은 없다.
- 상태 초기화와 다음 날짜 진행은 구현되어 있다.

## 입력과 충돌 위험

- 플레이어 이동/시점: Input System의 Keyboard/Mouse를 매 프레임 직접 조회한다.
- 월드 상호작용: 우클릭/F, 커서가 잠긴 동안만 Raycast 안내가 표시된다.
- 작업대 UI: 플레이어와 Interactor를 비활성화하고 커서를 표시하므로 월드 입력 충돌을 피한다.
- 대화, 튜토리얼, 소파 메시지, 우편함 메시지는 각자 IMGUI를 그리므로 표시 영역 중첩 가능성이 있다.
- 일반 게임 중 ESC는 커서 해제만 수행한다. 향후 일시정지 UI가 추가되면 ESC 소유권을 중앙에서 조정해야 한다.
- UI가 둘 이상 열리는 것을 통제하는 공통 상태/스택은 없다. 2~7단계에서 공통 UI 상태 관리자를 도입하거나 최소한 각 화면의 열림 조건을 상호 배타적으로 만들어야 한다.
- 향후 대화 선택지가 추가되면 F/우클릭의 대사 진행과 선택 확정, 모니터/작업대 클릭 입력이 경쟁할 수 있다. 선택지 단계에서는 입력 소유자를 명시하고 월드 상호작용을 잠가야 한다.

## 2단계 HUD 개선 권장 순서

1. `DeadLetterUITheme`를 사용하는 HUD 전용 Canvas/Prefab을 만든다.
2. `FirstPersonInteractor`의 IMGUI 안내를 HUD View로 분리하되 Raycast·입력 로직은 유지한다.
3. `LetterTutorialHUD`의 상태 계산은 유지하고 표시만 TMP/uGUI로 이전한다.
4. 조준점을 추가하고 상호작용 가능 상태에서만 색 또는 모양을 바꾼다.
5. DAY 좌측 상단, 업무 목록 우측 상단, 대화 영역 좌측 하단의 안전 여백을 먼저 정의한다.
6. 16:9와 16:10에서 앵커, 글자 크기, 대화창과 업무 목록의 겹침을 확인한다.
7. HUD가 작업대/대화/향후 일시정지 화면과 동시에 표시되지 않도록 가시성 규칙을 정한다.

## 이번 단계에서 변경하지 않은 항목

- 기존 HUD, 대화, 편지 업무, DAY, 문, 메인 메뉴의 코드와 씬 참조
- 메인 메뉴 로고와 영상
- 입력 처리 방식과 게임플레이 상태 전환

## 생성 및 수정 파일

- `Assets/Resources/UI/Fonts/GalmuriMono11/`: 런타임 IMGUI/TMP 공용 원본 폰트, OFL 라이선스, TMP Font Asset
- `Assets/TextMesh Pro/`: Unity 패키지의 TMP Essential Resources 및 공통 TMP Settings
- `Assets/Scripts/UI/DeadLetterUITheme.cs`: 공통 폰트·크기·색상 데이터
- `Assets/UI/Common/DeadLetterUITheme.asset`: 프로젝트 공통 UI 테마 에셋
- `Assets/Scenes/UI_Font_Test.unity`: 실제 게임 씬과 분리된 폰트 확인 씬
- `Assets/UI/Tests/UI_Font_Test_Preview.png`: 1920×1080 렌더 검수 결과
- `Assets/Editor/UIStage1Setup.cs`: 위 에셋을 재생성하고 글리프를 검증하는 편집기 도구

`UI_Font_Test`는 Build Settings에 추가하지 않았으며 기존 게임 씬과 런타임 UI에는 참조 변경이 없다.
