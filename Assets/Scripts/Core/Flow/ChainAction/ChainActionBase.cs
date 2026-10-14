using System;
using System.Collections.Generic;
using Core.Battle;

namespace Core.Flow.ChainAction
{
    /// <summary>
    /// 개별 체인(Chain: Fever, Rain, DiceRoll, DrawDamage 등) 액션 전략 추상 기본 클래스
    /// </summary>
    public abstract class ChainActionBase
    {
        public abstract string ActionName { get; }

        /// <summary>
        /// [타이밍 1] 드로우 행위 시 호출
        /// </summary>
        public virtual void OnDraw(ChainActionContext ctx, BattleCharacterModel drawTarget) { }

        /// <summary>
        /// [타이밍 2] 가위바위보 승리 시 체인 효과 적용 (2장 이상 조합 승리 시)
        /// </summary>
        public virtual void OnAttackWin(ChainActionContext ctx, int winner, List<CardInstance> winnerCards) { }

        /// <summary>
        /// [타이밍 3] 가위바위보 및 애니메이션 종료 후 호출 (비동기 연출 고려를 위해 onComplete 콜백 지원)
        /// </summary>
        public virtual void OnRpsEnd(ChainActionContext ctx, int winner, Action onComplete)
        {
            onComplete?.Invoke();
        }

        /// <summary>
        /// [타이밍 4] 카드 제출 시 호출 (필드 Rain 카드 합산 등)
        /// </summary>
        public virtual void OnCardSubmitted(ChainActionContext ctx) { }
    }
}

