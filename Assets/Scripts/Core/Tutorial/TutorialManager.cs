using UnityEngine;
using Core.View;
using Core.Flow;
using System.Collections.Generic;
using System;
using UniRx;

namespace Core.Tutorial
{
    [Serializable]
    public class TutorialStep
    {
        public int stringId; // 0이 아니면 TextIdHolder(다국어) 우선 사용
        public string message;
        public Component[] highlightTargets;
        public NarratorPlacement placement = NarratorPlacement.Auto;
        public bool allowInteraction = false; // HTML 시나리오의 상호작용허용 규칙과 1:1 매칭
        public Action onStepStart;
        public Func<bool> validation;
        public bool waitForUserAction;
        public bool waitForEvent; // 라운드 시작이나 연출 타이머 대기 스텝 (자동 진행 금지)
        public TutorialTriggerPhase triggerPhase = TutorialTriggerPhase.Immediate;
        public int triggerValue = 1;
    }

    public class TutorialManager : MonoBehaviour
    {
        public static TutorialManager Instance { get; private set; }

        [Header("ScriptableObject Scenario")]
        [SerializeField] private TutorialScenarioSO defaultScenario;

        [Header("Global Feature Tutorials (전역 가이드)")]
        [SerializeField] private TutorialScenarioSO feverTutorialScenario;

        [Header("Debug Settings")]
        [Tooltip("에디터 플레이 시 완료 여부와 무관하게 항상 튜토리얼을 재생할지 여부")]
        [SerializeField] private bool ignoreCompletedInEditor = true;

        [Header("Views")]
        [SerializeField] private TutorialOverlayView overlayView;
        [SerializeField] private TutorialNarratorView narratorView;

        private List<TutorialStep> steps = new List<TutorialStep>();
        private int currentStepIndex = -1;
        private bool isTutorialActive = false;
        private BattleView currentBattleView;
        private IDisposable roundObserver;
        private string activeScenarioKey;
        private Action onInterceptResumeAction;

        public bool IsTutorialActive => isTutorialActive;
        public TutorialScenarioSO DefaultScenario => defaultScenario;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        /// <summary>
        /// 튜토리얼을 시작합니다. ScriptableObject가 할당되어 있으면 SO 데이터 기반으로, 없으면 기존 하드코딩 시나리오로 구동합니다.
        /// </summary>
        public void StartTutorial(BattleView battleView, TutorialScenarioSO customScenario = null, bool forcePlay = false)
        {
            var scenarioToPlay = customScenario != null ? customScenario : defaultScenario;

            // 이미 완료된 시나리오인지 확인 (에디터 디버그 및 강제 재생 지원)
            if (scenarioToPlay != null && !string.IsNullOrEmpty(scenarioToPlay.scenarioKey))
            {
#if UNITY_EDITOR
                bool skipCompletedCheck = ignoreCompletedInEditor || forcePlay;
#else
                bool skipCompletedCheck = forcePlay;
#endif
                if (!skipCompletedCheck && TutorialProgress.IsCompleted(scenarioToPlay.scenarioKey))
                {
                    Debug.Log($"[Tutorial] Scenario '{scenarioToPlay.scenarioKey}' already completed. Skipping.");
                    return;
                }
                activeScenarioKey = scenarioToPlay.scenarioKey;
            }
            else
            {
                activeScenarioKey = "Battle_Iroha_1002";
            }

            currentBattleView = battleView;
            isTutorialActive = true;
            steps.Clear();

            if (scenarioToPlay != null && scenarioToPlay.steps != null && scenarioToPlay.steps.Count > 0)
            {
                Debug.Log($"[Tutorial] Loading scenario from ScriptableObject: {scenarioToPlay.name} (Steps: {scenarioToPlay.steps.Count})");
                BuildFromScenarioSO(scenarioToPlay, battleView);
            }
            else
            {
                Debug.LogWarning("[Tutorial] No valid TutorialScenarioSO assigned to TutorialManager! Please assign an SO in the Inspector.");
                return;
            }

            currentStepIndex = -1;
            if (overlayView != null)
            {
                overlayView.ClearHighlight();
                overlayView.Show();
            }
            NextStep();
        }

        /// <summary>
        /// ScriptableObject 데이터 기반으로 런타임 튜토리얼 스텝 구성
        /// </summary>
        private void BuildFromScenarioSO(TutorialScenarioSO scenario, BattleView battleView)
        {
            // #0: 특수 스킬 게이지 50% 세팅 단계 (게임 기획 룰 반영)
            steps.Add(new TutorialStep
            {
                message = null,
                highlightTargets = null,
                allowInteraction = false,
                onStepStart = () => {
                    var model = battleView?.PlayerStatus?.CharacterModel;
                    if (model != null)
                    {
                        // [TODO: 기획 테이블 반영 시 롤백 필요]
                        // BattleTable 또는 BattleCharacterTable에 초기 스페셜 게이지(InitialSpecialGauge) 칼럼 추가 기획 승인 시
                        // 아래의 강제 50% 세팅을 제거하고 테이블 데이터 기반 초기화로 되돌릴 것!
                        model.specialGauge.Value = model.tableData.specialSkillGauge * 0.5f;
                    }
                    Debug.Log("[Tutorial] SO Step 0: Special Gauge forcefully set to 50% (Pending table column approval)");
                }
            });

            for (int i = 0; i < scenario.steps.Count; i++)
            {
                var stepData = scenario.steps[i];
                bool needsShowOverlay = false;

                // 1. 스텝 시작 전 연출 대기 시간(초)이 있는 경우 (예: 3R 제출 후 연출 대기)
                if (stepData.delayBeforeStep > 0f)
                {
                    float delay = stepData.delayBeforeStep;
                    steps.Add(new TutorialStep
                    {
                        message = null,
                        highlightTargets = null,
                        allowInteraction = false,
                        waitForEvent = true,
                        onStepStart = () => {
                            overlayView?.Hide();
                            narratorView?.Hide();
                            Observable.Timer(TimeSpan.FromSeconds(delay))
                                .Subscribe(_ => NextStep())
                                .AddTo(this);
                        }
                    });
                    needsShowOverlay = true;
                }
                // 2. 특정 라운드 진입 대기인 경우 (예: 2R, 3R 시작 대기)
                else if (stepData.triggerPhase == TutorialTriggerPhase.RoundCommandStart)
                {
                    int targetRound = stepData.triggerValue;
                    steps.Add(new TutorialStep
                    {
                        message = null,
                        highlightTargets = null,
                        allowInteraction = false,
                        waitForEvent = true,
                        onStepStart = () => {
                            overlayView?.Hide();
                            narratorView?.Hide();
                            WaitForNextRoundStart(targetRound);
                        }
                    });
                    needsShowOverlay = true;
                }

                // 3. 특수 스킬 연출 종료 진입인 경우 또는 직전 스텝이 SpecialSkillClicked였던 경우
                bool isAfterSpecialSkill = (i > 0 && scenario.steps[i - 1].releaseCue == TutorialReleaseCue.SpecialSkillClicked);
                if (stepData.triggerPhase == TutorialTriggerPhase.SpecialSkillCastEnd || isAfterSpecialSkill)
                {
                    needsShowOverlay = true;
                }

                // 4. 본 스텝 구성 (대사 및 상호작용 안내)
                var step = new TutorialStep
                {
                    stringId = stepData.messageStringId,
                    message = stepData.fallbackMessage,
                    placement = stepData.placement,
                    allowInteraction = stepData.allowInteraction,
                    triggerPhase = stepData.triggerPhase,
                    triggerValue = stepData.triggerValue,
                    highlightTargets = ResolveTargetComponents(stepData.targetType, battleView),
                    onStepStart = needsShowOverlay ? () => overlayView?.Show() : null
                };

                // 해제 조건(Release Cue) 매핑
                switch (stepData.releaseCue)
                {
                    case TutorialReleaseCue.ReadDialogue:
                        step.waitForUserAction = false;
                        break;
                    case TutorialReleaseCue.TraySelectElement:
                        step.waitForUserAction = true;
                        int count = stepData.targetCount;
                        step.validation = () => ValidateTrayElement(battleView, count);
                        break;
                    case TutorialReleaseCue.SubmitClicked:
                        step.waitForUserAction = true;
                        step.validation = null;
                        break;
                    case TutorialReleaseCue.SpecialSkillClicked:
                        step.waitForUserAction = true;
                        step.validation = () => {
                            var model = battleView.PlayerStatus?.CharacterModel;
                            return model != null && model.isSpecialActive;
                        };
                        break;
                }

                steps.Add(step);
            }
        }

        private Component[] ResolveTargetComponents(TutorialTargetType targetType, BattleView battleView)
        {
            if (battleView == null || targetType == TutorialTargetType.None) return null;

            var targets = new List<Component>();

            if ((targetType & TutorialTargetType.PlayerStatus) != 0 && battleView.PlayerStatus != null)
                targets.Add(battleView.PlayerStatus);

            if ((targetType & TutorialTargetType.EnemyStatus) != 0 && battleView.EnemyStatus != null)
                targets.Add(battleView.EnemyStatus);

            if ((targetType & TutorialTargetType.PlayerHand) != 0 && battleView.PlayerHandContainer != null)
                targets.Add(battleView.PlayerHandContainer);

            if ((targetType & TutorialTargetType.EnemyHand) != 0 && battleView.EnemyHandContainer != null)
                targets.Add(battleView.EnemyHandContainer);

            if ((targetType & TutorialTargetType.PlayerTray) != 0 && battleView.PlayerTrayContainer != null)
                targets.Add(battleView.PlayerTrayContainer);

            if ((targetType & TutorialTargetType.EnemyTray) != 0 && battleView.EnemyClashLayout != null)
                targets.Add(battleView.EnemyClashLayout);

            if ((targetType & TutorialTargetType.SubmitButton) != 0 && battleView.SubmitButton != null)
                targets.Add(battleView.SubmitButton);

            if ((targetType & TutorialTargetType.DrawButton) != 0 && battleView.DrawButton != null)
                targets.Add(battleView.DrawButton);

            if ((targetType & TutorialTargetType.SpecialSkillButton) != 0 && battleView.SpecialSkillButton != null)
                targets.Add(battleView.SpecialSkillButton);

            if ((targetType & TutorialTargetType.PlayerFigure) != 0 && battleView.PlayerFigure != null)
                targets.Add(battleView.PlayerFigure);

            if ((targetType & TutorialTargetType.EnemyFigure) != 0 && battleView.EnemyFigure != null)
                targets.Add(battleView.EnemyFigure);

            if ((targetType & TutorialTargetType.RoundInfo) != 0 && battleView.RoundText != null)
                targets.Add(battleView.RoundText);

            return targets.Count > 0 ? targets.ToArray() : null;
        }

        private bool ValidateTrayElement(BattleView battleView, int requiredCount)
        {
            if (battleView == null || battleView.PlayerTrayContainer == null) return false;
            var cards = battleView.PlayerTrayContainer.Cards;
            if (cards == null || cards.Count != requiredCount) return false;

            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] == null || cards[i].SkillData == null) return false;
            }

            // 모든 카드가 동일 속성인지 검증
            var firstElement = cards[0].SkillData.element;
            for (int i = 1; i < cards.Count; i++)
            {
                if (cards[i].SkillData.element != firstElement) return false;
            }
            return true;
        }

        private void WaitForNextRoundStart(int targetRound)
        {
            roundObserver?.Dispose();

            roundObserver = Observable.Interval(TimeSpan.FromMilliseconds(50))
                .TakeWhile(_ => isTutorialActive)
                .Subscribe(_ => {
                    var executor = FlowController.Instance.CurrentExecutor as BattleExecutor;
                    if (executor != null && executor.BattleModel != null)
                    {
                        if (executor.BattleModel.currentRound.Value == targetRound && 
                            executor.CurrentPhase == BattlePhase.Command)
                        {
                            roundObserver?.Dispose();
                            if (overlayView != null) overlayView.Show();
                            NextStep();
                        }
                    }
                })
                .AddTo(this);
        }

        /// <summary>
        /// 배틀 페이즈 전환 시 튜토리얼이 인터셉트하여 배틀 진행을 일시 정지할지 검사합니다.
        /// </summary>
        public bool TryInterceptPhase(BattlePhase phase, int roundNumber, Action onResume)
        {
            if (!isTutorialActive) return false;

            // 현재 스텝이 이벤트 대기 중이고, 목표 라운드 도달 시
            if (currentStepIndex >= 0 && currentStepIndex < steps.Count)
            {
                var step = steps[currentStepIndex];
                if (step.waitForEvent && step.triggerPhase == TutorialTriggerPhase.RoundCommandStart && step.triggerValue == roundNumber && phase == BattlePhase.Command)
                {
                    onInterceptResumeAction = onResume;
                    if (overlayView != null) overlayView.Show();
                    NextStep();
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 피버 모드나 특수 스킬 최초 발동 등 특정 조건 발생 시 튜토리얼 인터셉트
        /// </summary>
        public bool TryInterceptCondition(TutorialTriggerPhase condition, Action onResume)
        {
            if (condition == TutorialTriggerPhase.FeverModeStart)
            {
                string feverKey = feverTutorialScenario != null && !string.IsNullOrEmpty(feverTutorialScenario.scenarioKey) 
                    ? feverTutorialScenario.scenarioKey 
                    : "Tutorial_Global_Fever";

                if (TutorialProgress.IsCompleted(feverKey)) return false;

                Debug.Log($"[Tutorial] Global Feature Intercepted: {condition}! Starting Fever Guide.");
                StartGlobalFeatureTutorial(feverTutorialScenario, feverKey, onResume);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 전역 피처 가이드(피버 등)를 배틀 중 인터셉트하여 팝업하고 완료 시 재개
        /// </summary>
        private void StartGlobalFeatureTutorial(TutorialScenarioSO scenario, string key, Action onResume)
        {
            if (overlayView != null)
            {
                overlayView.ClearHighlight();
                overlayView.Show();
            }

            Action onFinish = () => {
                TutorialProgress.SetCompleted(key, true);
                if (overlayView != null) overlayView.Hide();
                if (narratorView != null) narratorView.Hide();
                onResume?.Invoke();
                Debug.Log($"[Tutorial] Global Feature Guide '{key}' Finished.");
            };

            if (scenario != null && scenario.steps != null && scenario.steps.Count > 0)
            {
                var step = scenario.steps[0];
                narratorView.SetPlacement(step.placement, null);
                if (step.messageStringId > 0)
                {
                    narratorView.ShowMessage(step.messageStringId, null, onFinish, true);
                }
                else
                {
                    narratorView.ShowMessage(step.fallbackMessage, null, onFinish, true);
                }
            }
            else
            {
                narratorView.SetPlacement(NarratorPlacement.Center, null);
                narratorView.ShowMessage(
                    "마린의 피버 게이지가 모두 차올라 피버 모드로 진입했습니다!\n피버 모드에서는 승리할 때마다 피버 스택이 쌓여 상대에게 강력한 추가 피해를 입힙니다!",
                    null,
                    onFinish,
                    true
                );
            }
        }

        public void NextStep()
        {
            currentStepIndex++;

            if (currentStepIndex >= steps.Count)
            {
                EndTutorial();
                return;
            }

            var step = steps[currentStepIndex];
            step.onStepStart?.Invoke();

            // 1. 하이라이트 UI 설정
            if (step.highlightTargets != null && step.highlightTargets.Length > 0)
            {
                overlayView.Highlight(step.allowInteraction, step.highlightTargets);
            }
            else
            {
                overlayView.ClearHighlight();
            }

            // 2. 대사창 출력 및 위치 지정
            if (step.stringId <= 0 && string.IsNullOrEmpty(step.message))
            {
                narratorView.Hide();
                if (!step.waitForUserAction && !step.waitForEvent)
                {
                    NextStep();
                }
            }
            else
            {
                RectTransform targetRect = null;
                if (step.highlightTargets != null && step.highlightTargets.Length > 0 && step.highlightTargets[0] != null)
                {
                    targetRect = step.highlightTargets[0].GetComponent<RectTransform>();
                }

                narratorView.SetPlacement(step.placement, targetRect);

                Action onComplete = () => {
                    if (!step.waitForUserAction)
                    {
                        NextStep();
                    }
                };

                // StringIdHolder 다국어 지원: stringId가 있으면 stringId 우선
                if (step.stringId > 0)
                {
                    narratorView.ShowMessage(step.stringId, null, onComplete, allowAdvanceOnClick: !step.waitForUserAction);
                }
                else
                {
                    narratorView.ShowMessage(step.message, null, onComplete, allowAdvanceOnClick: !step.waitForUserAction);
                }
            }
        }

        public void NotifySubmitClicked()
        {
            if (!isTutorialActive) return;
            if (overlayView != null) overlayView.Hide();
            if (narratorView != null) narratorView.Hide();
            NotifyUserActionCompleted();
        }

        private bool waitForSpecialCastEnd = false;

        /// <summary>
        /// 특수 스킬 버튼을 클릭했을 때 호출 - 연출 중에는 딤드와 대화창을 숨기고 연출 종료를 대기
        /// </summary>
        public void NotifySpecialSkillClicked()
        {
            if (!isTutorialActive) return;
            if (overlayView != null) overlayView.Hide();
            if (narratorView != null) narratorView.Hide();
            waitForSpecialCastEnd = true;
            Debug.Log("[Tutorial] Special skill clicked. Waiting for cast production to finish...");
        }

        /// <summary>
        /// 특수 스킬 발동 연출(컷신/포즈)이 완전히 종료되었을 때 BattleExecutor로부터 호출
        /// </summary>
        public void NotifySpecialSkillCastFinished()
        {
            if (!isTutorialActive) return;
            if (waitForSpecialCastEnd)
            {
                waitForSpecialCastEnd = false;
                Debug.Log("[Tutorial] Special skill cast finished! Advancing to next tutorial step.");
                NextStep();
            }
        }

        public void NotifyUserActionCompleted()
        {
            if (!isTutorialActive || currentStepIndex < 0 || currentStepIndex >= steps.Count) return;

            var step = steps[currentStepIndex];
            if (step.waitForUserAction)
            {
                if (step.validation == null || step.validation())
                {
                    NextStep();
                }
            }
        }

        private void EndTutorial()
        {
            isTutorialActive = false;
            roundObserver?.Dispose();
            if (overlayView != null) overlayView.Hide();
            if (narratorView != null) narratorView.Hide();

            if (!string.IsNullOrEmpty(activeScenarioKey))
            {
                TutorialProgress.SetCompleted(activeScenarioKey, true);
            }

            // 인터셉트로 인해 배틀 진행이 보류되어 있던 콜백이 있다면 재개
            if (onInterceptResumeAction != null)
            {
                var resume = onInterceptResumeAction;
                onInterceptResumeAction = null;
                resume.Invoke();
            }

            Debug.Log($"[Tutorial] Tutorial '{activeScenarioKey}' Completed! Returning to normal battle.");
        }
    }
}
