using System;
using UnityEngine;
using Core.Battle;

namespace Core.Flow.ChainAction
{
    /// <summary>
    /// 드로우 시 상대에게 확률적으로 대미지를 입히는 체인 액션 전략
    /// </summary>
    public class DrawDamageChainAction : ChainActionBase
    {
        public override string ActionName => "DrawDamage";

        public override void OnDraw(ChainActionContext ctx, BattleCharacterModel drawTarget)
        {
            if (drawTarget == null || ctx?.battleModel == null) return;

            var opponent = (drawTarget == ctx.battleModel.player) ? ctx.battleModel.enemy : ctx.battleModel.player;
            if (opponent.drawDamageProb.Value > 0)
            {
                if (UnityEngine.Random.value < opponent.drawDamageProb.Value)
                {
                    int damage = opponent.drawDamageValue.Value;
                    drawTarget.TakeDamage(damage);
                    Debug.Log($"[Chain: DrawDamage] {damage} damage dealt to {drawTarget.tableData.id} on Draw!");
                }
            }
        }
    }
}

