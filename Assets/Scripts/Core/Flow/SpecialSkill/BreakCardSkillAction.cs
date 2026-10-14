using UnityEngine;
using System.Linq;
using Core.Battle;

namespace Core.Flow.SpecialSkill
{
    /// <summary>
    /// 페코라 특수 스킬 (BreakCard):
    /// 상대방의 손패 중 count장만큼 duration 라운드 동안 사용할 수 없도록 잠금 디버프를 부여합니다.
    /// </summary>
    public class BreakCardSkillAction : SpecialSkillActionBase
    {
        public override string ActionName => "breakcard";

        public override void Execute(SpecialSkillContext context)
        {
            var skillRow = context.SkillRow;
            var opponent = context.Opponent;

            int breakCount = (skillRow != null && skillRow.ActionValues.Length > 0) ? (int)skillRow.ActionValues[0] : 1;
            int duration = (skillRow != null && skillRow.ActionValues.Length > 1) ? (int)skillRow.ActionValues[1] : 2;

            BreakOpponentCards(opponent, breakCount, duration);

            if (context.BattleView != null)
            {
                context.BattleView.RefreshHandVisuals();
            }
        }

        private void BreakOpponentCards(BattleCharacterModel target, int count, int duration)
        {
            if (target == null || target.hand.Count == 0) return;
            var availableCards = target.hand.Where(c => !c.IsBroken).ToList();
            if (availableCards.Count == 0) return;

            int actualCount = Mathf.Min(count, availableCards.Count);
            for (int i = 0; i < actualCount; i++)
            {
                int randIdx = Random.Range(0, availableCards.Count);
                availableCards[randIdx].BreakRemainingRound = duration;
                availableCards.RemoveAt(randIdx);
            }
        }
    }
}
