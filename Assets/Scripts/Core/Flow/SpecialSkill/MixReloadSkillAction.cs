using UnityEngine;
using Core.Battle;
using Core.Table;

namespace Core.Flow.SpecialSkill
{
    /// <summary>
    /// 카나데 특수 스킬 (MixReload):
    /// 상대방의 모든 손패를 무작위로 교체합니다. (Fool 카드는 제외)
    /// </summary>
    public class MixReloadSkillAction : SpecialSkillActionBase
    {
        public override string ActionName => "mixreload";

        public override void Execute(SpecialSkillContext context)
        {
            var user = context.User;
            var opponent = context.Opponent;
            if (opponent == null) return;

            var opponentSkillIds = opponent.tableData.SkillIds;
            if (opponentSkillIds == null || opponentSkillIds.Length == 0) return;

            for (int i = 0; i < opponent.hand.Count; i++)
            {
                var card = opponent.hand[i];
                if (card.IsFool) continue;

                int randomIdx = Random.Range(0, opponentSkillIds.Length);
                int skillId = opponentSkillIds[randomIdx];
                var skillRow = TableManager.Instance.GetTable<SkillTable>().Get(skillId);
                opponent.hand[i] = new CardInstance(skillRow);
            }

            if (context.BattleView != null)
            {
                context.BattleView.RefreshHandVisuals();
            }

            Debug.Log($"[Special: MixReload] {user.tableData.id} rerolled {opponent.tableData.id}'s hand (excluding Fools).");
        }
    }
}
