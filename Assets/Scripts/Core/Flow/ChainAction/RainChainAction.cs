using System;
using System.Linq;
using UnityEngine;
using Core.Battle;
using Core.Table;

namespace Core.Flow.ChainAction
{
    /// <summary>
    /// 코보의 Rain 스택 누적, 패 파괴 및 스킬 봉쇄(RainTime)를 전담하는 체인 액션 전략
    /// </summary>
    public class RainChainAction : ChainActionBase
    {
        public override string ActionName => "Rain";

        public override void OnCardSubmitted(ChainActionContext ctx)
        {
            if (ctx?.battleModel == null) return;

            int pRain = ctx.battleModel.playerSelectedCards != null ? ctx.battleModel.playerSelectedCards.Count(c => c.IsRain) : 0;
            int eRain = ctx.battleModel.enemySelectedCards != null ? ctx.battleModel.enemySelectedCards.Count(c => c.IsRain) : 0;
            int totalRain = pRain + eRain;

            if (totalRain <= 0) return;

            ApplyRainStackIfRainChain(ctx.battleModel.player, totalRain, pRain, eRain);
            ApplyRainStackIfRainChain(ctx.battleModel.enemy, totalRain, pRain, eRain);
        }

        public override void OnRpsEnd(ChainActionContext ctx, int winner, Action onComplete)
        {
            if (ctx?.battleModel != null)
            {
                CheckRainTime(ctx, ctx.battleModel.player, ctx.battleModel.enemy);
                CheckRainTime(ctx, ctx.battleModel.enemy, ctx.battleModel.player);
            }
            onComplete?.Invoke();
        }

        private void CheckRainTime(ChainActionContext ctx, BattleCharacterModel charModel, BattleCharacterModel opponent)
        {
            if (charModel == null || opponent == null) return;
            var chainRow = TableManager.Instance.GetTable<ChainTable>().Get(charModel.tableData.chainId);
            if (chainRow == null || !chainRow.action.Equals("Rain", StringComparison.OrdinalIgnoreCase)) return;

            int maxStack = (chainRow.ChainSetValues != null && chainRow.ChainSetValues.Length > 0 && chainRow.ChainSetValues[0] > 0)
                           ? (int)chainRow.ChainSetValues[0]
                           : charModel.maxRainStack;

            if (charModel.rainStack.Value >= maxStack && !charModel.isRainTimeActive.Value)
            {
                charModel.ActivateRainTime();

                // 1. 코보 상대방의 손패 전체 삭제
                var cardsToRemove = opponent.hand.ToList();
                foreach (var card in cardsToRemove)
                {
                    opponent.hand.Remove(card);
                }

                // 2. 상대방의 특수 스킬 1라운드간 봉쇄
                opponent.BlockSpecialSkill(1);

                // 3. RainStack 초기화
                charModel.rainStack.Value = 0;

                if (ctx?.battleView != null)
                {
                    ctx.battleView.RefreshHandVisuals();
                    ctx.battleView.ShowWarningMessage("RAIN TIME!");
                }

                Debug.Log($"[Chain: RainTime] {charModel.tableData.id} triggered RainTime! Cleared {opponent.tableData.id}'s entire hand.");
            }
        }

        private void ApplyRainStackIfRainChain(BattleCharacterModel character, int totalRain, int pRain, int eRain)
        {
            if (character == null) return;
            var chainRow = TableManager.Instance.GetTable<ChainTable>().Get(character.tableData.chainId);
            bool isRainChain = chainRow != null && chainRow.action.Equals("Rain", StringComparison.OrdinalIgnoreCase);
            if (isRainChain)
            {
                character.AddRainStack(totalRain);
                Debug.Log($"[Chain: Rain] {character.tableData.id} gained {totalRain} RainStack (Player: {pRain}, Enemy: {eRain})! Current: {character.rainStack.Value}");
            }
        }

        public SkillTableRow GetFoolSkillRow(BattleCharacterModel charModel)
        {
            if (charModel == null) return null;
            var chainRow = TableManager.Instance.GetTable<ChainTable>().Get(charModel.tableData.chainId);
            int foolCardId = (chainRow != null && chainRow.ChainSetValues != null && chainRow.ChainSetValues.Length > 1 && chainRow.ChainSetValues[1] > 0)
                           ? (int)chainRow.ChainSetValues[1]
                           : -1;

            SkillTableRow foolSkillRow = null;
            if (foolCardId > 0)
            {
                foolSkillRow = TableManager.Instance.GetTable<SkillTable>()?.Get(foolCardId);
            }
            if (foolSkillRow == null)
            {
                foolSkillRow = TableManager.Instance.GetTable<SkillTable>()?.rows?.FirstOrDefault(r => r.element == SkillElement.Fool);
            }
            return foolSkillRow;
        }
    }
}

