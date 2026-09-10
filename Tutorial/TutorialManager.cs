using UnityEngine;
using Core.View;
using System.Collections.Generic;
using System;
using UniRx;

namespace Core.Tutorial
{
    [Serializable]
    public class TutorialStep
    {
        public string message;
        public RectTransform targetUI;
        public Action onStepStart;
        public Func<bool> validation;
    }

    public class TutorialManager : MonoBehaviour
    {
        public static TutorialManager Instance { get; private set; }

        [SerializeField] private TutorialOverlayView overlayView;
        [SerializeField] private TutorialNarratorView narratorView;

        private List<TutorialStep> steps = new List<TutorialStep>();
        private int currentStepIndex = -1;
        private bool isTutorialActive = false;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void StartTutorial(BattleView battleView)
        {
            isTutorialActive = true;
            steps.Clear();

            // Step 0: 스킬 게이지 90% 세팅 (대사 X)
            steps.Add(new TutorialStep
            {
                message = null, // 메시지가 null이면 대화창을 띄우지 않음
                targetUI = null,
                onStepStart = () => {
                    var executor = Flow.FlowController.Instance.CurrentExecutor as Flow.BattleExecutor;
                    // TODO: BattleExecutor나 Model을 통해 게이지 조작 로직 추가 예정 (Phase 2)
                    Debug.Log("[Tutorial] Step 0: Set Gauge to 90%");
                }
            });

            // Step 1: 아군 HP, 적 HP 설명 (대사 O)
            steps.Add(new TutorialStep
            {
                message = "반가워요! 게임의 기본 규칙을 알려드릴게요.\n상단의 HP 바는 당신과 적의 남은 체력을 나타냅니다.",
                targetUI = battleView.PlayerStatus.GetComponent<RectTransform>()
            });

            // Step 2: 카드 설명 (대사 O)
            steps.Add(new TutorialStep
            {
                message = "하단에는 당신이 사용할 수 있는 카드들이 있습니다.\n가위, 바위, 보 중 같은 종류의 카드를 모아서 낼 수 있어요.",
                targetUI = battleView.PlayerHandContainer.GetComponent<RectTransform>()
            });

            currentStepIndex = -1;
            overlayView.Show();
            NextStep();
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

            if (step.targetUI != null)
            {
                overlayView.Highlight(step.targetUI);
            }
            else
            {
                overlayView.ClearHighlight();
            }

            if (string.IsNullOrEmpty(step.message))
            {
                // 메시지가 없으면 하이라이트만 하고(또는 건너뛰고) 다음 단계로 진행하거나 검증 대기
                if (step.validation == null || step.validation())
                {
                    NextStep();
                }
            }
            else
            {
                narratorView.ShowMessage(step.message, null, () => {
                    // 대사가 끝나고 클릭했을 때의 처리
                    if (step.validation == null || step.validation())
                    {
                        NextStep();
                    }
                });
            }
        }

        private void EndTutorial()
        {
            isTutorialActive = false;
            overlayView.Hide();
            narratorView.Hide();
            Debug.Log("Tutorial Phase 1 Completed!");
        }
    }
}
