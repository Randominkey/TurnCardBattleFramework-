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
        /// <param name="allowInteraction">하이라이트 중 마우스 상호작용(호버/클릭) 허용 여부</param>
        public void SetHighlight(bool active, int highlightOrder = 501, bool allowInteraction = true)
        {
            if (canvas == null) return;

            var cg = GetComponent<CanvasGroup>();

            if (active)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = highlightOrder;
                
                if (graphicRaycaster != null) graphicRaycaster.enabled = allowInteraction;
                if (cg != null) cg.blocksRaycasts = allowInteraction;
            }
            else
            {
                canvas.overrideSorting = originalOverride;
                canvas.sortingOrder = originalOrder;
                if (graphicRaycaster != null) graphicRaycaster.enabled = true;
                if (cg != null) cg.blocksRaycasts = true;
            }
        }
    }
}
