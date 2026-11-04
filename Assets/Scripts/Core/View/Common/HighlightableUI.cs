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
        private int originalLayerId;

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            graphicRaycaster = GetComponent<GraphicRaycaster>();
            
            // 초기 상태 저장
            if (canvas != null)
            {
                originalOrder = canvas.sortingOrder;
                originalOverride = canvas.overrideSorting;
                originalLayerId = canvas.sortingLayerID;
            }
        }

        /// <summary>
        /// 하이라이트 상태를 설정합니다.
        /// </summary>
        /// <param name="active">true면 딤드 위로 올림, false면 원상 복구</param>
        /// <param name="highlightOrder">하이라이트 시 적용할 오더 (기본 501)</param>
        /// <param name="allowInteraction">하이라이트 중 마우스 상호작용(호버/클릭) 허용 여부</param>
        /// <param name="layerName">하이라이트 시 적용할 Sorting Layer 이름 (null이면 기존 레이어 유지)</param>
        public void SetHighlight(bool active, int highlightOrder = 501, bool allowInteraction = true, string layerName = null)
        {
            if (canvas == null) return;

            var cg = GetComponent<CanvasGroup>();

            if (active)
            {
                canvas.overrideSorting = true;
                if (!string.IsNullOrEmpty(layerName))
                {
                    canvas.sortingLayerName = layerName;
                }
                canvas.sortingOrder = highlightOrder;
                
                if (graphicRaycaster != null) graphicRaycaster.enabled = allowInteraction;
                if (cg != null) cg.blocksRaycasts = allowInteraction;
            }
            else
            {
                canvas.overrideSorting = originalOverride;
                int highlightLayerId = SortingLayer.NameToID("TutorialHighlight");
                if (originalLayerId == highlightLayerId)
                {
                    int gameUiId = SortingLayer.NameToID("GameUI");
                    originalLayerId = gameUiId != 0 ? gameUiId : 0;
                }
                canvas.sortingLayerID = originalLayerId;
                canvas.sortingOrder = originalOrder;
                if (graphicRaycaster != null) graphicRaycaster.enabled = true;
                if (cg != null) cg.blocksRaycasts = true;
            }
        }
    }
}
