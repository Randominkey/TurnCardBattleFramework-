using UnityEngine;
using System.Linq;
using Core.Battle;
using Core.Table;

namespace Core.Flow.SpecialSkill
{
    /// <summary>
    /// 코보 특수 스킬 (RainDraw):
    /// 묵/찌/빠 중 카드 1장을 드로우한 뒤 Rain 효과를 부여합니다.
    /// 손패가 최대치인 경우 일반 카드 중 1장을 제거하고 드로우하며, 모두 Rain 카드라면 발동을 다음 라운드로 이관합니다.
    /// </summary>
    public class RainDrawSkillAction : SpecialSkillActionBase
    {
        public override string ActionName => "raindraw";

        public override void Execute(SpecialSkillContext context)
        {
            var user = context.User;
            if (user == null) return;

            var gameSet = TableManager.Instance.GetTable<GameSetTable>().Get(0);
            int maxCards = gameSet?.maxCards ?? 8;

            if (user.hand.Count >= maxCards)
            {
                var nonRainCards = user.hand.Where(c => !c.IsRain).ToList();
                if (nonRainCards.Count == 0)
                {
                    // Fail-safe: 모든 카드가 이미 Rain 카드인 경우 스킬 사용을 취소하고 다음 라운드로 이관 (게이지 복원)
                    user.specialGauge.Value = user.tableData.specialSkillGauge;
                    user.DeactivateSpecialSkill();
                    Debug.LogWarning($"[Special: RainDraw] {user.tableData.id}'s hand is completely filled with Rain cards! Skill deferred to next round.");
                    return;
                }

                // 일반 카드 중 무작위 1장 제거
                int removeIdx = Random.Range(0, nonRainCards.Count);
                var cardToRemove = nonRainCards[removeIdx];
                user.hand.Remove(cardToRemove);
                Debug.Log($"[Special: RainDraw] {user.tableData.id} hand is full. Discarded 1 non-Rain card to make space.");
            }

            // 1순위: user의 SkillIds 중 무작위 1장 선택하여 Rain 효과 부여
            SkillTableRow skillRow = null;
            if (user.tableData.SkillIds != null && user.tableData.SkillIds.Length > 0)
            {
                int randomIdx = Random.Range(0, user.tableData.SkillIds.Length);
                int skillId = user.tableData.SkillIds[randomIdx];
                skillRow = TableManager.Instance.GetTable<SkillTable>()?.Get(skillId);
            }
            if (skillRow == null)
            {
                skillRow = TableManager.Instance.GetTable<SkillTable>()?.rows?.FirstOrDefault();
            }

            var card = new CardInstance(skillRow);
            card.ApplyRainEffect();

            user.hand.Add(card);
            context.ChainHandler?.OnDraw(user);

            if (context.BattleView != null) context.BattleView.RefreshHandVisuals();
            Debug.Log($"[Special: RainDraw] {user.tableData.id} drew a {card.EffectiveElement} card with Rain effect.");
        }
    }
}
