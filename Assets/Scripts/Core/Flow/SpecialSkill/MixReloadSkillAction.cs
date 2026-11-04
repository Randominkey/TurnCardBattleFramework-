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
                if (card == null) continue;

                // Rock, Scissors, Paper 기본 속성 카드만 교체 (Fool 등 특수 속성은 보존)
                var elem = card.EffectiveElement;
                if (elem != SkillElement.Rock && elem != SkillElement.Scissors && elem != SkillElement.Paper)
                {
                    continue;
                }

                int randomIdx = Random.Range(0, opponentSkillIds.Length);
                int skillId = opponentSkillIds[randomIdx];
                var skillRow = TableManager.Instance.GetTable<SkillTable>()?.Get(skillId);
                
                // [Fix] 인스턴스 교체(new CardInstance) 대신 기존 인스턴스의 스킬 속성을 갱신하여 인스턴스 일관성 및 디버프 상태 유지
                card.RerollSkill(skillRow);
            }

            if (context.BattleView != null)
            {
                context.BattleView.RefreshHandVisuals();
            }

            Debug.Log($"[Special: MixReload] {user.tableData.id} rerolled {opponent.tableData.id}'s RPS cards in place.");
        }
    }
}
