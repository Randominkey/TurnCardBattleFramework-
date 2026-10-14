using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Core.Battle;
using Core.Table;

namespace Core.Flow.SpecialSkill
{
    /// <summary>
    /// 후부키 특수 스킬 (AlignReload):
    /// 자신의 손패를 모두 버리고, 버린 카드 수(N)만큼 상대의 최다 보유 속성을 이기는 유리한 속성 위주로 재드로우합니다.
    /// </summary>
    public class AlignReloadSkillAction : SpecialSkillActionBase
    {
        public override string ActionName => "alignreload";

        public override void Execute(SpecialSkillContext context)
        {
            var user = context.User;
            var opponent = context.Opponent;
            if (user == null || opponent == null) return;

            int discardCount = user.hand.Count;
            user.hand.Clear();

            if (discardCount <= 0)
            {
                Debug.Log($"[Special: AlignReload] {user.tableData.id} had 0 cards in hand. Discarded and drew 0 cards.");
                if (context.BattleView != null) context.BattleView.RefreshHandVisuals();
                return;
            }

            // 1. 상대 손패 속성 카운트 (Rock, Scissors, Paper)
            int rockCount = opponent.hand.Count(c => c.EffectiveElement == SkillElement.Rock);
            int scissorsCount = opponent.hand.Count(c => c.EffectiveElement == SkillElement.Scissors);
            int paperCount = opponent.hand.Count(c => c.EffectiveElement == SkillElement.Paper);

            int maxCount = Mathf.Max(rockCount, Mathf.Max(scissorsCount, paperCount));

            List<SkillElement> majorCandidates = new List<SkillElement>();
            if (rockCount == maxCount && maxCount > 0) majorCandidates.Add(SkillElement.Rock);
            if (scissorsCount == maxCount && maxCount > 0) majorCandidates.Add(SkillElement.Scissors);
            if (paperCount == maxCount && maxCount > 0) majorCandidates.Add(SkillElement.Paper);

            if (majorCandidates.Count == 0)
            {
                majorCandidates.AddRange(new[] { SkillElement.Rock, SkillElement.Scissors, SkillElement.Paper });
            }

            SkillElement selectedMajor = majorCandidates[Random.Range(0, majorCandidates.Count)];

            // "유리한 카드속성" (selectedMajor를 이기는 속성)
            SkillElement winningElement = selectedMajor switch
            {
                SkillElement.Rock => SkillElement.Paper,
                SkillElement.Scissors => SkillElement.Rock,
                SkillElement.Paper => SkillElement.Scissors,
                _ => SkillElement.Rock
            };

            // "유리한 카드속성" 이외의 묵찌빠 2가지 속성
            SkillElement otherElement1, otherElement2;
            switch (winningElement)
            {
                case SkillElement.Rock:
                    otherElement1 = SkillElement.Scissors;
                    otherElement2 = SkillElement.Paper;
                    break;
                case SkillElement.Scissors:
                    otherElement1 = SkillElement.Rock;
                    otherElement2 = SkillElement.Paper;
                    break;
                case SkillElement.Paper:
                default:
                    otherElement1 = SkillElement.Rock;
                    otherElement2 = SkillElement.Scissors;
                    break;
            }

            // 2. 드로우 장수 결정
            int winningDrawCount = 0;
            int other1DrawCount = 0;
            int other2DrawCount = 0;

            if (discardCount >= 3)
            {
                other1DrawCount = 1;
                other2DrawCount = 1;
                winningDrawCount = discardCount - 2;
            }
            else if (discardCount == 2)
            {
                other1DrawCount = 1;
                other2DrawCount = 1;
                winningDrawCount = 0;
            }
            else // discardCount == 1
            {
                winningDrawCount = 1;
            }

            // 실제 드로우 실행
            for (int i = 0; i < other1DrawCount; i++) DrawSpecificElementCard(context, otherElement1);
            for (int i = 0; i < other2DrawCount; i++) DrawSpecificElementCard(context, otherElement2);
            for (int i = 0; i < winningDrawCount; i++) DrawSpecificElementCard(context, winningElement);

            if (context.BattleView != null) context.BattleView.RefreshHandVisuals();
            Debug.Log($"[Special: AlignReload] {user.tableData.id} discarded {discardCount} cards and reloaded -> Opponent Major: {selectedMajor}, Winning: {winningElement} x{winningDrawCount}, Other1({otherElement1}) x{other1DrawCount}, Other2({otherElement2}) x{other2DrawCount}.");
        }

        private void DrawSpecificElementCard(SpecialSkillContext context, SkillElement targetElement)
        {
            var user = context.User;
            SkillTableRow matchedRow = null;
            if (user.tableData.SkillIds != null)
            {
                foreach (var skillId in user.tableData.SkillIds)
                {
                    var row = TableManager.Instance.GetTable<SkillTable>().Get(skillId);
                    if (row != null && row.element == targetElement)
                    {
                        matchedRow = row;
                        break;
                    }
                }
            }

            if (matchedRow == null && user.tableData.SkillIds != null && user.tableData.SkillIds.Length > 0)
            {
                matchedRow = TableManager.Instance.GetTable<SkillTable>().Get(user.tableData.SkillIds[0]);
            }

            if (matchedRow != null)
            {
                var card = new CardInstance(matchedRow);
                user.hand.Add(card);
                context.ChainHandler?.OnDraw(user);
            }
        }
    }
}
