using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Core.View;
using System.Collections.Generic;

namespace Core.Tutorial
{
    /// <summary>
    /// 튜토리얼 중 화면을 딤드(SortingOrder 600) 처리하고,
    /// 1개 이상의 특정 UI 요소를 하이라이트(SortingOrder 601)로 승격시켜 터치를 허용하는 오버레이 뷰.
    /// 하이라이트되지 않은 모든 뒤쪽 UI(0~500)의 마우스 호버 및 클릭을 완벽히 차단합니다.
    /// </summary>
    [RequireComponent(typeof(Canvas), typeof(GraphicRaycaster))]
    public class TutorialOverlayView : MonoBehaviour
    {
        private const int OVERLAY_SORTING_ORDER = 600;
        private const int HIGHLIGHT_SORTING_ORDER = 601;

        [Header("Settings")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image dimmedBackground;
        [SerializeField] private float animDuration = 0.3f;
        [SerializeField] private Color dimmedColor = new Color(0f, 0f, 0f, 0.65f);

        // 하이라이트된 요소들의 원래 상태 복원을 위한 추적 리스트
        private class HighlightRecord
        {
            public GameObject targetGo;
            public HighlightableUI highlightUI;
            public Canvas dynamicCanvas;
            public GraphicRaycaster dynamicRaycaster;
            public CanvasGroup targetCanvasGroup;
            public bool originalBlocksRaycasts;
            public int originalOrder;
            public bool originalOverride;
            public bool dynamicallyAdded;
        }

        private List<HighlightRecord> activeHighlights = new List<HighlightRecord>();
        private Canvas myCanvas;
        private GraphicRaycaster myRaycaster;

        private void Awake()
        {
            myCanvas = GetComponent<Canvas>();
            if (myCanvas != null)
            {
                myCanvas.overrideSorting = true;
                myCanvas.sortingOrder = OVERLAY_SORTING_ORDER;
            }

            myRaycaster = GetComponent<GraphicRaycaster>();
            if (myRaycaster != null)
            {
                myRaycaster.enabled = true;
            }

            if (dimmedBackground != null)
            {
                dimmedBackground.color = dimmedColor;
                dimmedBackground.raycastTarget = true; // 뒤쪽 UI로 레이캐스트가 뚫리지 않도록 100% 보장
            }

            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            Hide();
        }

        public void Show()
        {
            gameObject.SetActive(true);
            if (myRaycaster != null) myRaycaster.enabled = true;
            if (dimmedBackground != null) dimmedBackground.raycastTarget = true;

            if (canvasGroup != null)
            {
                canvasGroup.DOKill();
                canvasGroup.alpha = 0f;
                canvasGroup.DOFade(1f, animDuration).SetUpdate(true);
            }
        }

        public void Hide()
        {
            ClearHighlight();
            if (canvasGroup != null)
            {
                canvasGroup.DOKill();
                canvasGroup.DOFade(0f, animDuration).SetUpdate(true).OnComplete(() => {
                    gameObject.SetActive(false);
                });
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 특정 UI 요소들을 딤드 레이어(600) 위쪽인 601로 승격시키고, 필요에 따라 터치를 허용/차단합니다.
        /// 동일한 타겟이 연속으로 전달되면 파괴/재생성하지 않고 상호작용 상태만 갱신하여 깜빡임과 파괴 버그를 방지합니다.
        /// </summary>
        public void Highlight(bool allowInteraction, params Component[] targets)
        {
            if (targets == null || targets.Length == 0)
            {
                ClearHighlight();
                return;
            }

            var goList = new List<GameObject>();
            foreach (var t in targets)
            {
                if (t != null && t.gameObject != null && !goList.Contains(t.gameObject))
                {
                    goList.Add(t.gameObject);
                }
            }
            Highlight(allowInteraction, goList.ToArray());
        }

        /// <summary>
        /// 기본 오버로드 (기존 호환용)
        /// </summary>
        public void Highlight(params Component[] targets)
        {
            Highlight(true, targets);
        }

        public void Highlight(bool allowInteraction, params GameObject[] targets)
        {
            if (targets == null || targets.Length == 0)
            {
                ClearHighlight();
                return;
            }

            var newSet = new HashSet<GameObject>();
            foreach (var t in targets)
            {
                if (t != null) newSet.Add(t);
            }

            // 1. 새 목록에 없는 기존 하이라이트는 원복 및 제거
            for (int i = activeHighlights.Count - 1; i >= 0; i--)
            {
                var record = activeHighlights[i];
                if (record.targetGo == null || !newSet.Contains(record.targetGo))
                {
                    RevertHighlightRecord(record);
                    activeHighlights.RemoveAt(i);
                }
                else
                {
                    // 2. 새 목록에도 있는 기존 타겟은 상호작용 상태만 갱신 (Destroy 방지!)
                    UpdateHighlightInteraction(record, allowInteraction);
                    newSet.Remove(record.targetGo); // 이미 처리됨
                }
            }

            // 3. 완전히 새로 추가된 타겟들만 승격 처리
            foreach (var go in newSet)
            {
                PromoteToHighlight(go, allowInteraction);
            }
        }

        public void Highlight(params GameObject[] targets)
        {
            Highlight(true, targets);
        }

        private void UpdateHighlightInteraction(HighlightRecord record, bool allowInteraction)
        {
            if (record.targetCanvasGroup != null)
            {
                record.targetCanvasGroup.blocksRaycasts = allowInteraction;
            }

            if (record.highlightUI != null)
            {
                record.highlightUI.SetHighlight(true, HIGHLIGHT_SORTING_ORDER, allowInteraction);
            }
            else if (record.dynamicRaycaster != null)
            {
                record.dynamicRaycaster.enabled = allowInteraction;
            }
        }

        private void PromoteToHighlight(GameObject go, bool allowInteraction)
        {
            if (go == null) return;
            var record = new HighlightRecord { targetGo = go };

            // CanvasGroup이 있는 경우 blocksRaycasts 제어
            var cg = go.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                record.targetCanvasGroup = cg;
                record.originalBlocksRaycasts = cg.blocksRaycasts;
                cg.blocksRaycasts = allowInteraction;
            }

            // 1. 이미 HighlightableUI 컴포넌트가 붙어있는 경우
            var hUI = go.GetComponent<HighlightableUI>();
            if (hUI != null)
            {
                record.highlightUI = hUI;
                hUI.SetHighlight(true, HIGHLIGHT_SORTING_ORDER, allowInteraction);
                activeHighlights.Add(record);
                return;
            }

            // 2. HighlightableUI는 없지만 기존에 자체 Canvas가 붙어있었던 경우
            var canvas = go.GetComponent<Canvas>();
            if (canvas != null)
            {
                record.dynamicCanvas = canvas;
                record.originalOrder = canvas.sortingOrder;
                record.originalOverride = canvas.overrideSorting;
                record.dynamicallyAdded = false;

                canvas.overrideSorting = true;
                canvas.sortingOrder = HIGHLIGHT_SORTING_ORDER;

                var raycaster = go.GetComponent<GraphicRaycaster>();
                if (raycaster != null)
                {
                    record.dynamicRaycaster = raycaster;
                    raycaster.enabled = allowInteraction;
                }

                activeHighlights.Add(record);
                return;
            }

            // 3. Canvas가 전혀 없는 일반 UI인 경우 동적으로 추가하여 승격
            canvas = go.AddComponent<Canvas>();
            record.dynamicCanvas = canvas;
            record.originalOrder = 0;
            record.originalOverride = false;
            record.dynamicallyAdded = true;

            canvas.overrideSorting = true;
            canvas.sortingOrder = HIGHLIGHT_SORTING_ORDER;

            var gr = go.AddComponent<GraphicRaycaster>();
            gr.enabled = allowInteraction;
            record.dynamicRaycaster = gr;

            activeHighlights.Add(record);
        }

        private void RevertHighlightRecord(HighlightRecord record)
        {
            if (record == null) return;

            if (record.targetCanvasGroup != null)
            {
                record.targetCanvasGroup.blocksRaycasts = record.originalBlocksRaycasts;
            }

            if (record.highlightUI != null)
            {
                record.highlightUI.SetHighlight(false);
            }
            else if (record.dynamicCanvas != null)
            {
                if (record.dynamicallyAdded)
                {
                    record.dynamicCanvas.overrideSorting = false;
                    record.dynamicCanvas.enabled = false;
                    if (record.dynamicRaycaster != null)
                    {
                        record.dynamicRaycaster.enabled = false;
                        Destroy(record.dynamicRaycaster);
                    }
                    Destroy(record.dynamicCanvas);
                }
                else
                {
                    record.dynamicCanvas.overrideSorting = record.originalOverride;
                    record.dynamicCanvas.sortingOrder = record.originalOrder;
                    if (record.dynamicRaycaster != null)
                    {
                        record.dynamicRaycaster.enabled = true;
                    }
                }
            }
        }

        /// <summary>
        /// 현재 승격된 모든 UI들을 원래 SortingOrder 및 상호작용 상태로 즉시 복구합니다.
        /// </summary>
        public void ClearHighlight()
        {
            foreach (var record in activeHighlights)
            {
                RevertHighlightRecord(record);
            }
            activeHighlights.Clear();
        }
    }
}
