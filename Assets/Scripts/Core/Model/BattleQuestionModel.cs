using System.Collections.Generic;
using UniRx;
using Core.Table;
using UnityEngine;

namespace Core.Battle
{
    /// <summary>
    /// 배틀 내 질문(Question / NPC 다음 행동 힌트) 시스템의 런타임 상태를 관리하는 모델.
    /// 배틀 시작/재시작 시 BattleModel과 함께 새로 생성되므로 모든 대사 버튼이 활성화 상태로 시작합니다.
    /// </summary>
    public class BattleQuestionModel
    {
        private readonly BattleModel battleModel;
        private readonly List<QuestionChoiceData> questions;
        private readonly bool[] usedFlags;
        private readonly int[] assignedQuestionIds;

        public IReadOnlyList<QuestionChoiceData> Questions => questions;
        public int QuestionCount => questions.Count;
        public IReadOnlyList<int> AssignedQuestionIds => assignedQuestionIds;

        /// <summary>
        /// 남은 사용 가능 질문 횟수
        /// </summary>
        public ReactiveProperty<int> remainingQuestionCount { get; }

        /// <summary>
        /// 현재 라운드에 출력 중인 NPC 대답 유무 (유저가 카드를 선택해 클래시 연출로 넘어가기 직전까지 유지)
        /// </summary>
        public ReactiveProperty<bool> hasActiveAnswer { get; } = new ReactiveProperty<bool>(false);

        /// <summary>
        /// 가장 최근에 출력된 질문 대답 데이터
        /// </summary>
        public ReactiveProperty<QuestionAnswerResult> latestAnswer { get; } = new ReactiveProperty<QuestionAnswerResult>();

        public BattleQuestionModel(BattleModel battleModel)
        {
            this.battleModel = battleModel;
            this.assignedQuestionIds = battleModel?.battleTable?.QuestionIds;

            var loadedQuestions = QuestionDataProvider.GetQuestions(this.assignedQuestionIds);
            this.questions = new List<QuestionChoiceData>(loadedQuestions);
            this.usedFlags = new bool[this.questions.Count];
            this.remainingQuestionCount = new ReactiveProperty<int>(this.questions.Count);
        }

        /// <summary>
        /// 특정 인덱스의 질문 대사 버튼이 이미 소모(비활성화)되었는지 확인합니다.
        /// </summary>
        public bool IsQuestionUsed(int questionIndex)
        {
            if (questionIndex < 0 || questionIndex >= usedFlags.Length) return true;
            return usedFlags[questionIndex];
        }

        /// <summary>
        /// 질문 시점의 NPC 사전 결정사항(enemyChoseDraw, enemySelectedCards)을 바탕으로
        /// 4가지 상황(바위 / 가위 / 보 / 드로우) 중 하나를 판별합니다.
        /// - Rain 카드는 버프가 묻은 카드이므로 원본 속성(TableData.element: 가위/바위/보) 그대로 판별합니다.
        /// - Fool 카드 등 가위/바위/보가 아닌 특수 카드는 예외 처리로서 Draw와 동일하게 취급합니다.
        /// </summary>
        public QuestionSituation EvaluateCurrentEnemySituation()
        {
            if (battleModel == null) return QuestionSituation.Draw;

            if (battleModel.enemyChoseDraw ||
                battleModel.enemySelectedCards == null ||
                battleModel.enemySelectedCards.Count == 0)
            {
                return QuestionSituation.Draw;
            }

            CardInstance firstCard = battleModel.enemySelectedCards[0];
            if (firstCard == null || firstCard.IsFool || firstCard.TableData == null)
            {
                return QuestionSituation.Draw;
            }

            // Rain 버프가 묻어 있어도 원본 속성은 TableData.element(Rock/Scissors/Paper)를 유지함
            switch (firstCard.TableData.element)
            {
                case SkillElement.Rock:
                    return QuestionSituation.Rock;
                case SkillElement.Scissors:
                    return QuestionSituation.Scissors;
                case SkillElement.Paper:
                    return QuestionSituation.Paper;
                default:
                    return QuestionSituation.Draw;
            }
        }

        /// <summary>
        /// 대사 버튼을 클릭했을 때 호출됩니다.
        /// 이미 소모된 버튼이면 false를 반환하며 아무 동작도 하지 않습니다.
        /// </summary>
        public bool TryAskQuestion(int questionIndex, out QuestionAnswerResult result, float? customRandom01 = null)
        {
            result = default;

            if (questionIndex < 0 || questionIndex >= questions.Count)
                return false;

            // 비활성화(소모된) 버튼은 클릭 시 어떤 반응도 일어나지 않음
            if (usedFlags[questionIndex])
                return false;

            // 버튼 소모 처리
            usedFlags[questionIndex] = true;
            remainingQuestionCount.Value = Mathf.Max(0, remainingQuestionCount.Value - 1);

            // 현재 NPC의 행동 계획 상황 판별
            QuestionSituation situation = EvaluateCurrentEnemySituation();

            int enemyId = battleModel?.enemy?.tableData != null ? battleModel.enemy.tableData.id : 102;
            int targetQuestionTableId = (assignedQuestionIds != null && questionIndex >= 0 && questionIndex < assignedQuestionIds.Length)
                ? assignedQuestionIds[questionIndex]
                : -1;

            // 진실(60%) / 거짓(38%) / 이스터에그(2%) 확률 추첨 (테이블 행별 AnswerProp 기반)
            float roll = customRandom01 ?? Random.value;
            QuestionAnswerType answerType = QuestionDataProvider.RollAnswerType(enemyId, questionIndex, roll, targetQuestionTableId);

            result = QuestionDataProvider.GetAnswer(enemyId, questionIndex, situation, answerType, targetQuestionTableId);
            latestAnswer.Value = result;
            hasActiveAnswer.Value = true;

            Debug.Log($"[Question] Q:{questionIndex} (TableId:{targetQuestionTableId}) | EnemyId:{enemyId} | EnemySituation:{situation} | Roll:{roll:F3} -> {answerType} | Answer:\"{result.answerText}\" (StringId:{result.answerStringId})");
            return true;
        }

        /// <summary>
        /// 카드 클래시 애니메이션 진입 직전(또는 라운드 종료 시) 상대의 대답 말풍선을 닫습니다.
        /// </summary>
        public void ClearActiveAnswer()
        {
            hasActiveAnswer.Value = false;
        }
    }
}

