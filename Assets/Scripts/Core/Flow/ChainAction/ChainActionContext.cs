using System;
using UnityEngine;
using Core.Battle;
using Core.View;

namespace Core.Flow.ChainAction
{
    /// <summary>
    /// 체인 액션 전략 실행에 필요한 컨텍스트 데이터
    /// </summary>
    public class ChainActionContext
    {
        public BattleModel battleModel;
        public BattleView battleView;
        public MonoBehaviour coroutineRunner;
        public Action onBattleEndCheck;
    }
}

