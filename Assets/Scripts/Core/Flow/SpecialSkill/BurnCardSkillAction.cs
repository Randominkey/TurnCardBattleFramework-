using UnityEngine;
using Core.Battle;

namespace Core.Flow.SpecialSkill
{
    /// <summary>
    /// 사쿠라 미코 특수 스킬 (BurnCard / 아직 안졌다니에…):
    /// 상대방 손패 중 Lava(용암) 효과가 부여된 모든 카드를 전량 삭제(소멸)하고,
    /// 시전자 본인에게 ActionValues[0] 만큼의 자해 데미지를 입힙니다 (NowHp <= BurnDamage 시 자살 방지).
    /// </summary>
    public class BurnCardSkillAction : SpecialSkillActionBase
    {
        public override string ActionName => "burncard";

        public override void Execute(SpecialSkillContext context)
        {
            var user = context.User;
            var opponent = context.Opponent;
            var skillRow = context.SkillRow;

            if (user == null || opponent == null) return;

            // 1. 상대방 손패의 Lava 카드 전량 삭제
            var lavaCards = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(opponent.hand, c => c.IsLava));
            foreach (var card in lavaCards)
            {
                opponent.hand.Remove(card);
            }
            int removedCount = lavaCards.Count;
            Debug.Log($"[Special: BurnCard] {user.tableData.id} burned and removed {removedCount} Lava cards from {opponent.tableData.id}'s hand.");

            // 2. 시전자 자해 대미지 정산 (SkillTable.ActionValues[0])
            float burnDamage = (skillRow != null && skillRow.ActionValues != null && skillRow.ActionValues.Length > 0)
                ? Mathf.Max(0f, skillRow.ActionValues[0])
                : 0f;

            if (burnDamage > 0f)
            {
                // NowHp <= BurnDamage 이면 0 (자해로 인한 사망 방지)
                if (user.hp.Value <= burnDamage)
                {
                    burnDamage = 0f;
                    Debug.Log($"[Special: BurnCard] {user.tableData.id}'s HP ({user.hp.Value}) is too low to take BurnDamage ({burnDamage}). Damage prevented.");
                }
                else
                {
                    user.TakeDamage(burnDamage);
                    Debug.Log($"[Special: BurnCard] {user.tableData.id} took {burnDamage} self-damage. Remaining HP: {user.hp.Value}");
                }
            }

            // 3. 손패 비주얼 갱신
            if (context.BattleView != null)
            {
                context.BattleView.RefreshHandVisuals();
            }
        }
    }
}
