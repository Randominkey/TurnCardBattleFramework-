# 📋 TurnCardBattleFramework 포트폴리오 구축 세션 로그 (Session Log)

> **안내**: 본 문서는 `Assets/Demo/` 폴더 내에 로컬 전용으로 보관되며, 메인 프로젝트(`ProjectH`)의 Git 형상관리에서 영구 제외되어 팀원들에게 일절 노출되지 않습니다.

---

## 1. 세션 목표 (Session Objectives)

1. **상용화 예정 인디 프로젝트(`ProjectH`)의 코어 아키텍처 포트폴리오화**:
   - 약 2년간 1인 클라이언트 프로그래머로서 개발한 턴제 카드 배틀 코어 엔진을 채용 포트폴리오로 전환.
2. **미공개 IP 및 저작권(Hololive 등) 100% 보호**:
   - 캐릭터명, 일러스트, 음성, 스토리 대사 등 비공개 자산 노출 원천 차단.
3. **스텔스 동기화(Stealth Sync) 파이프라인 구축**:
   - `ProjectH` 단일 작업 폴더를 유지하면서, 팀원 모르게 독립된 공개 GitHub 저장소([TurnCardBattleFramework-](https://github.com/Randominkey/TurnCardBattleFramework-))로 클린 동기화.
4. **면접관을 위한 즉시 실행 가능한 독립 프로젝트 인프라 제공**:
   - 단순 코드 조각이 아닌, 클론 후 Unity 2021.3.1f1로 열어 즉시 배틀 루프를 테스트할 수 있는 온전한 Unity 프로젝트 구조 확립.
5. **이력서 및 경력기술서 마스터 템플릿 작성**:
   - 2년 1인 개발 성과 및 이전 회사(크레버스, CMS에듀) 경력을 STAR 구조로 문서화.

---

## 2. 세부 작업 내역 (Work Completed)

### [Phase 1] 에셋 디커플링 및 코드 리팩토링 (코드 무결성 유지)
- **에셋 제공자 인터페이스 설계 (`IAssetProvider.cs`)**:
  - `IAssetProvider` 인터페이스 및 기본 구현체 `DefaultResourceAssetProvider` 생성.
  - `ResourceUtil.AssetProvider` 정적 라우팅 프로퍼티 추가.
  - `TableBase.cs`의 하드코딩된 `Resources.Load`를 `AssetProvider.LoadXmlData`로 전환하여 런타임/테스트 환경에서 데이터 공급원 교체 가능하도록 추상화.
  - 결손 스프라이트 로드 시 에러 크래시 대신 색상 박스/더미로 대체하는 Fallback 처리(`ResourceUtil.LoadSpriteWithFallback`).
- **IP/캐릭터 아이덴티티 제거 및 프리팹 직렬화 호환성 보존**:
  - `CardView.cs`: `statePekoraSkill` 변수명을 `brokenObject`로 일반화하되, 기존 프리팹 인스펙터 바인딩이 깨지지 않도록 `[FormerlySerializedAs("statePekoraSkill")]` 적용.
  - `BattleLogic.cs`: 하드코딩된 캐릭터 체인 ID 체크(`chainId == 1002`) 및 캐릭터명 주석을 제거하고, 범용 액션 타입 체크(`chainRow.action.Equals("DamageUp")`)로 전환.
  - `BattleStatusView.cs`: 특정 캐릭터명이 언급된 주석 클린업.

---

### [Phase 2] 로컬 격리 데모 환경 구축 (`Assets/Demo/`)
- **순수 게임플레이 규칙 XML 테이블 7종 배치 (`Assets/Demo/Resources/XMLData/`)**:
  - 캐릭터명, 일러스트, 스토리 대사가 배제된 순수 배틀 규칙 데이터:
    - `BattleTable.xml`: 배틀 1001 및 1002 정의 (플레이어 vs AI 라이벌).
    - `BattleCharacterTable.xml`: 캐릭터 101(플레이어), 102(AI 라이벌) 정의.
    - `SkillTable.xml`: 가위/바위/보 기본 공격 및 특수 스킬(LoseHeal, Berserk) 8종.
    - `ChainTable.xml`: Fever(1001), DamageUp(1002) 체인 효과.
    - `BattleHpTable.xml`: 체력 임계 구간 설정.
    - `GameSetTable.xml`: 턴 제한, 드로우 룰 설정.
    - `StringTable.xml`: 가위, 바위, 보 및 스킬명 다국어 텍스트(KR, EN, JP).
- **단독 구동형 데모 씬 & 프리팹 배치**:
  - `Assets/Demo/Scenes/DemoBattleScene.unity`:
    - `DevBootstrapper`가 배틀 1001로 직접 자동 직행하도록 설정.
    - `BattleView`와 `BattleStatusView`가 연결된 캔버스 기반 씬.
  - `Assets/Demo/Prefabs/DemoCard.prefab`:
    - 캐릭터 의존성이 전혀 없는 uGUI 기반 가위/바위/보 카드 프리팹.
  - `Assets/Demo/Sprites/`:
    - 순수 가위바위보 아이콘 3종 및 카드 패널 베이스/마스크 스프라이트.

---

### [Phase 3] 스텔스 Git 형상관리 및 임시 인덱스 푸시 파이프라인
- **팀 프로젝트(`ProjectH`) 스텔스 격리**:
  - `c:\dev\Unity\ProjectH\.git\config`에만 `portfolio` 원격 저장소 등록 (팀 원격 `origin`에는 미반영).
  - `.git/info/exclude`에 아래 경로 등록하여 팀 Git에서 100% 추적 제외:
    - `Assets/Demo/` 및 `Assets/Demo.meta`
    - `scripts/sync_portfolio.bat`, `sync_portfolio.ps1`
    - `scripts/*.md` (포트폴리오 리드미 및 이력서 문서)
- **임시 인덱스(`GIT_INDEX_FILE`) 기반 원클릭 패키징**:
  - `scripts/sync_portfolio.ps1` 및 `sync_portfolio.bat` 구현.
  - 팀 프로젝트의 메인 인덱스를 건드리지 않고, 임시 인덱스(`.git/temp_portfolio_index`)에서 아래 항목만 선별 추출:
    - `Assets/Scripts/Core` (코어 아키텍처)
    - `Assets/Plugins` (UniRx, DOTween)
    - `Assets/Demo` (독립 실행 데모 씬 및 테이블)
    - `Packages/manifest.json` (패키지 의존성)
    - `ProjectSettings/ProjectVersion.txt` (Unity 2021.3.1f1 지정)
    - `.gitignore` (유니티 표준)
    - `README.md` (아키텍처 다이어그램 및 퀵스타트 가이드 주입)
- **과거 히스토리 세탁 및 푸시 결과 ([TurnCardBattleFramework-](https://github.com/Randominkey/TurnCardBattleFramework-))**:
  - 68개의 이전 커밋 내역(팀원 이름, 비공개 캐릭터 커밋 메시지)을 완전히 지우고, 단 2개의 클린한 커밋으로 유지:
    1. `f6f0563`: Initial commit: Modular Turn-Based Card Battle Core Framework
    2. `231683c`: feat: Standalone playable demo scene, decoupled tables, and core battle architecture

---

### [Phase 4] 마스터 이력서 및 경력기술서 작성
- **문서 위치**: [scripts/RESUME_AND_CAREER_DESCRIPTION.md](file:///c:/dev/Unity/ProjectH/scripts/RESUME_AND_CAREER_DESCRIPTION.md)
- **핵심 내용**:
  - 기본 정보: 김민기 / wzwing9@gmail.com / 포트폴리오 저장소 2종 연동.
  - 보유 기술: Unity uGUI(캔버스 최적화, 커스텀 레이아웃, 드로우콜 제어), UniRx, FSM, 커맨드 패턴.
  - 프로젝트 1 (2D 턴제 카드 배틀 RPG): 1인 클라이언트 개발 6대 성과를 STAR 구조로 기술.
  - 프로젝트 2 (크레버스 [수학퍼즐]): uGUI 기반 보드/타일 인터랙션, 드래그 앤 드롭 프레임워크, 수학적 알고리즘 상태 머신.
  - 프로젝트 3 (CMS에듀 R&D): 유아 사고력 수학 교재/교구 연구 및 WMO/CMDF 행사 기획.
  - 학력: 서울대학교 자유전공학부 (수학 / 심리학 2중전공 학사 졸업).

---

## 3. 향후 유지보수 가이드

- **전투 로직 업데이트 시**:
  - 메인 프로젝트에서 평소처럼 개발 진행.
  - 포트폴리오에 반영하고 싶을 때만 `scripts/sync_portfolio.bat`을 실행하여 원클릭 푸시.
- **이력서 제출 시**:
  - `scripts/RESUME_AND_CAREER_DESCRIPTION.md`를 열고 지원 회사 공고(JD)에 맞게 키워드 강조 후 PDF로 변환하여 제출.
