using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Core.Battle;
using Core.Table;

namespace Core.Flow.ChainAction
{
    /// <summary>
    /// 사쿠라 미코의 Lava 체인 액션:
    /// 가위바위보 승리 시 상대 손패의 카드들을 대상으로 최대 LavaCount장까지 순회하며,
    /// 각 카드마다 개별적으로 LavaProb 확률 검사 통과 시 Lava(용암) 효과를 부여합니다.
    /// </summary>
    public class LavaChainAction : ChainActionBase
    {
        public override string ActionName => "Lava";

        public override void OnAttackWin(ChainActionContext ctx, int winner, List<CardInstance> winnerCards)
        {
            if (ctx?.battleModel == null || winner == 0 || winnerCards == null || winnerCards.Count < 2) return;

            var winnerModel = (winner == 1) ? ctx.battleModel.player : ctx.battleModel.enemy;
            var loserModel = (winner == 1) ? ctx.battleModel.enemy : ctx.battleModel.player;
            if (winnerModel == null || loserModel == null) return;

            var chainRow = TableManager.Instance.GetTable<ChainTable>()?.Get(winnerModel.tableData.chainId);
            if (chainRow == null || !chainRow.action.Equals("Lava", StringComparison.OrdinalIgnoreCase)) return;

            int chainCount = winnerCards.Count;
            float[] values = chainCount switch
            {
                2 => chainRow.Chain2Values,
                3 => chainRow.Chain3Values,
                4 => chainRow.Chain4Values,
                5 => chainRow.Chain5Values,
                _ => null
            };

            // [0]: LavaProb, [1]: LavaCount
            float lavaProb = (values != null && values.Length > 0 && values[0] >= 0) ? values[0] : 0.5f;
            int lavaCount = (values != null && values.Length > 1 && values[1] > 0) ? (int)values[1] : 1;

            var uninfectedCards = loserModel.hand.Where(c => !c.IsLava).ToList();
            if (uninfectedCards.Count == 0)
            {
                Debug.Log($"[Chain: Lava] {loserModel.tableData.id} already has all cards as Lava.");
                return;
            }

            // 무작위 순서로 셔플
            var targets = uninfectedCards.OrderBy(_ => UnityEngine.Random.value).Take(lavaCount).ToList();
            int appliedCount = 0;

            foreach (var card in targets)
            {
                float roll = UnityEngine.Random.value;
                if (roll <= lavaProb)
                {
                    card.ApplyLavaEffect();
                    appliedCount++;
                    Debug.Log($"[Chain: Lava] Success (Roll: {roll:F2} <= Prob: {lavaProb:F2})! Applied Lava to {loserModel.tableData.id}'s {card.EffectiveElement} card.");
                }
                else
                {
                    Debug.Log($"[Chain: Lava] Failed on card {card.EffectiveElement} (Roll: {roll:F2} > Prob: {lavaProb:F2}).");
                }
            }

            if (appliedCount > 0 && ctx.battleView != null)
            {
                ctx.battleView.RefreshHandVisuals();
            }
        }
    }
}
