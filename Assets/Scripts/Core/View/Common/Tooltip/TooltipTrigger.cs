using UnityEngine;
using UnityEngine.EventSystems;

namespace Core.View
{
    /// <summary>
    /// UI 요소에 부착하여 마우스 오버 시 툴팁을 트리거하는 컴포넌트
    /// </summary>
    public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public enum TooltipType { Skill, Chain, Environment }

        [SerializeField] private TooltipType type;
        [SerializeField] private int id;
        private Core.Battle.EnvironmentType environmentType = Core.Battle.EnvironmentType.None;

        /// <summary>
        /// 트리거 데이터를 설정합니다.
        /// </summary>
        public void SetData(int targetId, TooltipType tooltipType)
        {
            this.id = targetId;
            this.type = tooltipType;
        }

        public void SetEnvironment(Core.Battle.EnvironmentType envType)
        {
            this.type = TooltipType.Environment;
            this.environmentType = envType;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (type == TooltipType.Environment)
            {
                if (environmentType != Core.Battle.EnvironmentType.None)
                {
                    TooltipManager.Instance.ShowEnvironment(environmentType);
                }
                return;
            }

            if (id <= 0) return;

            if (type == TooltipType.Skill)
            {
                TooltipManager.Instance.ShowSkill(id);
            }
            else if (type == TooltipType.Chain)
            {
                TooltipManager.Instance.ShowChain(id);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            TooltipManager.Instance.Hide();
        }

        private void OnDisable()
        {
            // [Fix] 오브젝트가 비활성화/파괴될 때 툴팁 매니저가 이미 파괴되었다면 새로 생성하지 않음
            if (TooltipManager.HasInstance)
            {
                TooltipManager.Instance.Hide();
            }
        }
    }
}
