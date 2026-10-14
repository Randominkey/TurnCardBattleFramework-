using System;
using UnityEngine;
using Core.Battle;

namespace Core.Flow.ChainAction
{
    /// <summary>
    /// 후부키의 DiceRoll 체인 결과 정산 및 연출 루틴을 전담하는 체인 액션 전략
    /// </summary>
    public class DiceRollChainAction : ChainActionBase
    {
        public override string ActionName => "DiceRoll";

        public override void OnRpsEnd(ChainActionContext ctx, int winner, Action onComplete)
        {
            if (ctx?.battleModel == null)
            {
                onComplete?.Invoke();
                return;
            }

            BattleCharacterModel diceUser = null;
            BattleCharacterModel diceOpponent = null;

            if (ctx.battleModel.player.lastDiceRoll.Value > 0)
            {
                diceUser = ctx.battleModel.player;
                diceOpponent = ctx.battleModel.enemy;
            }
            else if (ctx.battleModel.enemy.lastDiceRoll.Value > 0)
            {
                diceUser = ctx.battleModel.enemy;
                diceOpponent = ctx.battleModel.player;
            }

            if (diceUser != null)
            {
                int dice = diceUser.lastDiceRoll.Value;
                float effectValue = diceUser.lastDiceRollValue.Value;
                diceUser.lastDiceRoll.Value = 0;
                diceUser.lastDiceRollValue.Value = 0f;

                if (ctx.battleView != null && ctx.coroutineRunner != null)
                {
                    ctx.coroutineRunner.StartCoroutine(ctx.battleView.PlayDiceRollResolveRoutine(diceUser, diceOpponent, dice, effectValue, onComplete));
                }
                else
                {
                    BattleLogic.ProcessDiceRollResult(diceUser, diceOpponent, dice, effectValue);
                    onComplete?.Invoke();
                }
            }
            else
            {
                onComplete?.Invoke();
            }
        }
    }
}

