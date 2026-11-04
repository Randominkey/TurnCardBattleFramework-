using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Core.Battle;
using Core.Table;

namespace Core.Flow.ChainAction
{
    /// <summary>
    /// 오토노세 카나데의 ChangeFoolCard 체인 액션:
    /// 가위바위보 승리 시 상대 손패의 카드들을 대상으로 최대 FoolCardCount장까지 순회하며,
    /// 각 카드마다 개별적으로 FoolCardProb 확률 검사 통과 시 해당 카드를 Fool(8001) 카드로 영구 변환합니다.
    /// </summary>
    public class ChangeFoolCardChainAction : ChainActionBase
    {
        public override string ActionName => "ChangeFoolCard";

        public override void OnAttackWin(ChainActionContext ctx, int winner, List<CardInstance> winnerCards)
        {
            if (ctx?.battleModel == null || winner == 0 || winnerCards == null || winnerCards.Count < 2) return;

            var winnerModel = (winner == 1) ? ctx.battleModel.player : ctx.battleModel.enemy;
            var loserModel = (winner == 1) ? ctx.battleModel.enemy : ctx.battleModel.player;
            if (winnerModel == null || loserModel == null) return;

            var chainRow = TableManager.Instance.GetTable<ChainTable>()?.Get(winnerModel.tableData.chainId);
            if (chainRow == null || !chainRow.action.Equals("ChangeFoolCard", StringComparison.OrdinalIgnoreCase)) return;

            int chainCount = winnerCards.Count;
            float[] values = chainCount switch
            {
                2 => chainRow.Chain2Values,
                3 => chainRow.Chain3Values,
                4 => chainRow.Chain4Values,
                5 => chainRow.Chain5Values,
                _ => null
            };

            // [0]: FoolCardProb, [1]: FoolCardCount
            float foolProb = (values != null && values.Length > 0 && values[0] >= 0) ? values[0] : 0.5f;
            int foolCount = (values != null && values.Length > 1 && values[1] > 0) ? (int)values[1] : 1;

            int foolCardId = (chainRow.ChainSetValues != null && chainRow.ChainSetValues.Length > 0 && chainRow.ChainSetValues[0] > 0)
                ? (int)chainRow.ChainSetValues[0]
                : 8001;
            var foolSkillRow = TableManager.Instance.GetTable<SkillTable>()?.Get(foolCardId);

            var normalCards = loserModel.hand.Where(c => !c.IsFool).ToList();
            if (normalCards.Count == 0)
            {
                Debug.Log($"[Chain: ChangeFoolCard] {loserModel.tableData.id} already has all cards as Fool.");
                return;
            }

            var targets = normalCards.OrderBy(_ => UnityEngine.Random.value).Take(foolCount).ToList();
            int appliedCount = 0;

            foreach (var card in targets)
            {
                float roll = UnityEngine.Random.value;
                if (roll <= foolProb)
                {
                    card.ConvertToFool(foolSkillRow);
                    appliedCount++;
                    Debug.Log($"[Chain: ChangeFoolCard] Success (Roll: {roll:F2} <= Prob: {foolProb:F2})! Converted {loserModel.tableData.id}'s card to Fool (Id: {foolCardId}).");
                }
                else
                {
                    Debug.Log($"[Chain: ChangeFoolCard] Failed on card {card.EffectiveElement} (Roll: {roll:F2} > Prob: {foolProb:F2}).");
                }
            }

            if (appliedCount > 0 && ctx.battleView != null)
            {
                ctx.battleView.RefreshHandVisuals();
            }
        }
    }
}
