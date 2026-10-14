using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Tutorial
{
    [System.Flags]
    public enum TutorialTargetType
    {
        None = 0,
        PlayerStatus        = 1 << 0,  // 마린(플레이어) 체력/스탯
        EnemyStatus         = 1 << 1,  // 상대방(적) 체력/스탯
        PlayerHand          = 1 << 2,  // 플레이어 핸드 카드들
        EnemyHand           = 1 << 3,  // 적 핸드 카드들
        PlayerTray          = 1 << 4,  // 플레이어 제출 트레이
        EnemyTray           = 1 << 5,  // 적 제출/격돌 영역
        SubmitButton        = 1 << 6,  // 제출 버튼
        DrawButton          = 1 << 7,  // 드로우 버튼
        SpecialSkillButton  = 1 << 8,  // 특수 스킬 버튼
        PlayerFigure        = 1 << 9,  // 플레이어 캐릭터 일러스트/피규어
        EnemyFigure         = 1 << 10, // 적 캐릭터 일러스트/피규어
        RoundInfo           = 1 << 11, // 라운드 번호/제한 라운드 텍스트

        // 편의용 프리셋
        AllStatus           = PlayerStatus | EnemyStatus,
        AllHands            = PlayerHand | EnemyHand,
        AllFigures          = PlayerFigure | EnemyFigure
    }

    public enum TutorialTriggerPhase
    {
        Immediate,           // 이전 스텝 완료 후 즉시 또는 시나리오 시작 시
        RoundCommandStart,   // 특정 라운드의 Command 페이즈 진입 시
        SpecialSkillCastEnd, // 특수 스킬 연출 종료 시
        FeverModeStart,      // 최초 피버 모드 진입 시
        SpecialGaugeFull     // 스페셜 스킬 게이지 100% 도달 시
    }

    public enum TutorialReleaseCue
    {
        ReadDialogue,        // 대사 확인(클릭/엔터)으로 즉시 완료
        TraySelectElement,   // 동일 속성 카드 N장 안착 검증
        SubmitClicked,       // 제출 버튼 클릭
        SpecialSkillClicked  // 특수 스킬 버튼 클릭
    }

    [Serializable]
    public class TutorialStepData
    {
        [Header("대사 & 다국어")]
        [Tooltip("다국어 StringTable 연동 ID (0이면 fallbackMessage 사용)")]
        public int messageStringId;
        [TextArea(2, 4)]
        [Tooltip("개발/테스트용 또는 미번역 시 출력될 기본 대사")]
        public string fallbackMessage;

        [Header("타겟 & 연출")]
        public TutorialTargetType targetType = TutorialTargetType.None;
        public NarratorPlacement placement = NarratorPlacement.Auto;
        [Tooltip("딤드 위에서 해당 UI와 상호작용을 허용할지 여부")]
        public bool allowInteraction = false;

        [Header("진입 트리거 (Trigger)")]
        public TutorialTriggerPhase triggerPhase = TutorialTriggerPhase.Immediate;
        [Tooltip("트리거에 필요한 값 (예: RoundCommandStart일 때 라운드 번호)")]
        public int triggerValue = 1;
        [Tooltip("스텝 시작 전 연출 대기 시간(초) (0보다 크면 딤드를 끄고 연출 대기 후 스텝 시작)")]
        public float delayBeforeStep = 0f;

        [Header("해제 조건 (Release Cue)")]
        public TutorialReleaseCue releaseCue = TutorialReleaseCue.ReadDialogue;
        [Tooltip("해제 조건에 필요한 수량 (예: TraySelectElement일 때 필요한 동일 속성 카드 장수)")]
        public int targetCount = 1;
    }

    [CreateAssetMenu(fileName = "TutorialScenario", menuName = "ProjectH/Tutorial/Scenario")]
    public class TutorialScenarioSO : ScriptableObject
    {
        [Header("시나리오 식별")]
        [Tooltip("시나리오 완료 여부 추적을 위한 고유 키 (예: Battle_Iroha_1002)")]
        public string scenarioKey = "Battle_Tutorial";

        [Tooltip("배틀 씬 진입 시 바로 자동 실행할지 여부")]
        public bool autoPlayOnStart = false;

        [Header("스텝 목록")]
        public List<TutorialStepData> steps = new List<TutorialStepData>();
    }
}
