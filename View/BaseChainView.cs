using UnityEngine;
using Core.Battle;

namespace Core.View
{
    /// <summary>
    /// 캐릭터별 고유 체인 시스템 UI의 기반 클래스
    /// </summary>
    public abstract class BaseChainView : MonoBehaviour
    {
        [SerializeField] protected TooltipTrigger tooltipTrigger;

        public abstract void Setup(BattleCharacterModel model);
    }
}
