using UnityEngine;
using UnityEngine.UI;

namespace Core.View
{
    /// <summary>
    /// 연출 시 특정 UI 요소를 하이라이트(딤드 위로 올림)하기 위한 컴포넌트
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class HighlightableUI : MonoBehaviour
    {
        private Canvas canvas;
        private GraphicRaycaster graphicRaycaster;
        private int originalOrder;
        private bool originalOverride;

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            graphicRaycaster = GetComponent<GraphicRaycaster>();
            
            // 초기 상태 저장
            originalOrder = canvas.sortingOrder;
            originalOverride = canvas.overrideSorting;
        }

        /// <summary>
        /// 하이라이트 상태를 설정합니다.
        /// </summary>
        /// <param name="active">true면 딤드 위로 올림, false면 원상 복구</param>
        /// <param name="highlightOrder">하이라이트 시 적용할 오더 (기본 501)</param>
        public void SetHighlight(bool active, int highlightOrder = 501)
        {
            if (canvas == null) return;

            if (active)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = highlightOrder;
                
                // 하이라이트 중에도 클릭이 가능해야 한다면 Raycaster 활성화 (선택 사항)
                if (graphicRaycaster != null) graphicRaycaster.enabled = true;
            }
            else
            {
                canvas.overrideSorting = originalOverride;
                canvas.sortingOrder = originalOrder;
            }
        }
    }
}
