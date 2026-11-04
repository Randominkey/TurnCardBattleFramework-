using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Core.Battle;
using Core.Table;

namespace Core.Flow.ChainAction
{
    /// <summary>
    /// 우사다 페코라의 Bomb 체인 액션:
    /// 가위바위보 승리 시 상대 손패의 카드들을 대상으로 최대 BombCount장까지 순회하며,
    /// 해당 카드의 폭탄 스택이 MaxBombStack 미만인 경우 각 카드마다 개별적으로 BombProb 확률 검사를 수행합니다.
    /// 통과 시 해당 카드에 폭탄(BombData)을 추가합니다.
    /// </summary>
    public class BombChainAction : ChainActionBase
    {
        public override string ActionName => "Bomb";

        public override void OnAttackWin(ChainActionContext ctx, int winner, List<CardInstance> winnerCards)
        {
            if (ctx?.battleModel == null || winner == 0 || winnerCards == null || winnerCards.Count < 2) return;

            var winnerModel = (winner == 1) ? ctx.battleModel.player : ctx.battleModel.enemy;
            var loserModel = (winner == 1) ? ctx.battleModel.enemy : ctx.battleModel.player;
            if (winnerModel == null || loserModel == null) return;

            var chainRow = TableManager.Instance.GetTable<ChainTable>()?.Get(winnerModel.tableData.chainId);
            if (chainRow == null || !chainRow.action.Equals("Bomb", StringComparison.OrdinalIgnoreCase)) return;

            // ChainSetValues[0]: MaxBombStack
            int maxBombStack = (chainRow.ChainSetValues != null && chainRow.ChainSetValues.Length > 0 && chainRow.ChainSetValues[0] > 0)
                ? (int)chainRow.ChainSetValues[0]
                : 3;

            int chainCount = winnerCards.Count;
            float[] values = chainCount switch
            {
                2 => chainRow.Chain2Values,
                3 => chainRow.Chain3Values,
                4 => chainRow.Chain4Values,
                5 => chainRow.Chain5Values,
                _ => null
            };

            // [0]: BombProb, [1]: BombCount, [2]: DamageValue
            float bombProb = (values != null && values.Length > 0 && values[0] >= 0) ? values[0] : 0.5f;
            int bombCount = (values != null && values.Length > 1 && values[1] > 0) ? (int)values[1] : 1;
            float damageValue = (values != null && values.Length > 2 && values[2] > 0) ? values[2] : 1f;

            // 최대 스택 미만인 카드만 대상
            var eligibleCards = loserModel.hand.Where(c => c.BombStack < maxBombStack).ToList();
            if (eligibleCards.Count == 0)
            {
                Debug.Log($"[Chain: Bomb] {loserModel.tableData.id} already has all cards at MaxBombStack ({maxBombStack}).");
                return;
            }

            // 무작위 순서로 셔플 후 BombCount 장 선택
            var targets = eligibleCards.OrderBy(_ => UnityEngine.Random.value).Take(bombCount).ToList();
            int appliedCount = 0;

            foreach (var card in targets)
            {
                float roll = UnityEngine.Random.value;
                if (roll <= bombProb)
                {
                    card.AddBomb(new BombData(damageValue));
                    appliedCount++;
                    Debug.Log($"[Chain: Bomb] Success (Roll: {roll:F2} <= Prob: {bombProb:F2})! Added Bomb to {loserModel.tableData.id}'s card. (Stack: {card.BombStack}/{maxBombStack}, Dmg: {damageValue})");
                }
                else
                {
                    Debug.Log($"[Chain: Bomb] Failed on card {card.EffectiveElement} (Roll: {roll:F2} > Prob: {bombProb:F2}).");
                }
            }

            if (appliedCount > 0 && ctx.battleView != null)
            {
                ctx.battleView.RefreshHandVisuals();
            }
        }
    }
}
