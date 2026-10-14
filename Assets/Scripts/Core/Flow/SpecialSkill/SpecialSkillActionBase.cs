using Core.Table;

namespace Core.Flow.SpecialSkill
{
    /// <summary>
    /// 캐릭터별 고유 스페셜 스킬 효과 및 격돌 성공 조건을 정의하는 단일 추상 베이스 클래스
    /// </summary>
    public abstract class SpecialSkillActionBase
    {
        /// <summary>
        /// SkillTable의 action 문자열 (소문자 매칭, 예: "breakcard", "berserk", "mixreload")
        /// </summary>
        public abstract string ActionName { get; }

        /// <summary>
        /// 스페셜 스킬의 고유 효과를 실행합니다.
        /// </summary>
        public abstract void Execute(SpecialSkillContext context);

        /// <summary>
        /// 해당 스킬의 격돌 후 '성공 연출' 출력 조건이 만족되었는지 검사합니다.
        /// 기본 구현: Require 타입 스킬이며 시전자가 격돌에서 승리했을 때 true
        /// </summary>
        public virtual bool CheckSuccess(SpecialSkillContext context, int winnerIndex)
        {
            if (context?.SkillRow == null || context.User == null || context.BattleModel == null) return false;
            if (context.SkillRow.element != SkillElement.Require) return false;

            bool isUserWinner = (context.User == context.BattleModel.player && winnerIndex == 1) ||
                                (context.User == context.BattleModel.enemy && winnerIndex == 2);
            return isUserWinner;
        }
    }
}
