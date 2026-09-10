using System;
using UnityEngine;
using Core.Table;

namespace Core.View
{
    /// <summary>
    /// BattleProductionManager를 사용하여 연출을 실행하는 시퀀서 커맨드
    /// </summary>
    public class ProductionCommand : IActionCommand
    {
        private BattleProductionManager manager;
        private ProductionType type;
        private CharacterKeyName subKey;
        private float? duration;
        private object data;

        public ProductionCommand(BattleProductionManager manager, ProductionType type, CharacterKeyName subKey = CharacterKeyName.None, float? duration = null, object data = null)
        {
            this.manager = manager;
            this.type = type;
            this.subKey = subKey;
            this.duration = duration;
            this.data = data;
        }

        public void Execute(Action onComplete)
        {
            if (manager == null)
            {
                onComplete?.Invoke();
                return;
            }

            // 매니저의 코루틴을 실행하고 완료 시 콜백 호출
            manager.StartCoroutine(ExecuteSequence(onComplete));
        }

        private System.Collections.IEnumerator ExecuteSequence(Action onComplete)
        {
            yield return manager.PlayProduction(type, subKey, duration, data);
            onComplete?.Invoke();
        }
    }
}
