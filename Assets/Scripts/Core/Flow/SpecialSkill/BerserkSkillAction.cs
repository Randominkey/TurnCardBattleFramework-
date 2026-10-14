using UnityEngine;

namespace Core.Flow.SpecialSkill
{
    /// <summary>
    /// 이로하 특수 스킬 (Berserk):
    /// 공격력/방어력 배율 버프를 활성화합니다. (승리 시 성공 컷인)
    /// </summary>
    public class BerserkSkillAction : SpecialSkillActionBase
    {
        public override string ActionName => "berserk";

        public override void Execute(SpecialSkillContext context)
        {
            var user = context.User;
            if (user != null)
            {
                Debug.Log($"[Special: Berserk] {user.tableData.id} Berserk activated with AttackMult={user.attackMultiplier}, DefMult={user.defenseMultiplier}");
            }
        }
    }
}
