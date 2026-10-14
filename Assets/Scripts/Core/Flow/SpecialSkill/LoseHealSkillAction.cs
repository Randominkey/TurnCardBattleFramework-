using Core.Battle;
using Core.Table;

namespace Core.Flow.SpecialSkill
{
    /// <summary>
    /// 마린 특수 스킬 (LoseHeal):
    /// 가위바위보 패배 시 체력을 회복하는 조건부(Require) 스킬입니다.
    /// 격돌에서 '패배'했을 때 성공 연출을 출력합니다.
    /// </summary>
    public class LoseHealSkillAction : SpecialSkillActionBase
    {
        public override string ActionName => "loseheal";

        public override void Execute(SpecialSkillContext context)
        {
            // LoseHeal은 격돌 패배 시 정산되므로, 발동 시점에는 Require 상태로 대기합니다.
        }

        public override bool CheckSuccess(SpecialSkillContext context, int winnerIndex)
        {
            if (context?.User == null || context.BattleModel == null) return false;
            if (winnerIndex == 0) return false; // 무승부는 실패

            bool isUserWinner = (context.User == context.BattleModel.player && winnerIndex == 1) ||
                                (context.User == context.BattleModel.enemy && winnerIndex == 2);

            // 패배했을 때 성공
            return !isUserWinner;
        }
    }
}
