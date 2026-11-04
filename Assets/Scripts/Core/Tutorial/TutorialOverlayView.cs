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
        private const string DIMMED_LAYER_NAME = "TutorialDimmed";
        private const string HIGHLIGHT_LAYER_NAME = "TutorialHighlight";
        private const int OVERLAY_SORTING_ORDER = 600;
        private const int HIGHLIGHT_SORTING_ORDER = 601;

        [Header("Settings")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image dimmedBackground;
        [SerializeField] private float animDuration = 0.3f;
        [SerializeField] private Color dimmedColor = new Color(0f, 0f, 0f, 0.65f);

        // 하이라이트된 개별 Canvas 상태 추적용 레코드
        private class CanvasRecord
        {
            public Canvas canvas;
            public int originalLayerId;
            public int originalOrder;
            public bool originalOverride;
            public bool dynamicallyAdded;
            public GraphicRaycaster raycaster;
            public bool originalRaycasterEnabled;
            public bool raycasterDynamicallyAdded;
        }

        // 하이라이트된 UI 요소 전체 복원을 위한 추적 레코드
        private class HighlightRecord
        {
            public GameObject targetGo;
            public HighlightableUI highlightUI;
            public CanvasGroup targetCanvasGroup;
            public bool originalBlocksRaycasts;
            public List<CanvasRecord> canvasRecords = new List<CanvasRecord>();
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
                myCanvas.sortingLayerName = DIMMED_LAYER_NAME;
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
                record.highlightUI.SetHighlight(true, HIGHLIGHT_SORTING_ORDER, allowInteraction, HIGHLIGHT_LAYER_NAME);
            }

            foreach (var cr in record.canvasRecords)
            {
                if (cr.raycaster != null)
                {
                    cr.raycaster.enabled = allowInteraction;
                }
            }
        }

        private void PromoteToHighlight(GameObject go, bool allowInteraction)
        {
            if (go == null) return;
            var record = new HighlightRecord { targetGo = go };

            // 1. CanvasGroup이 있는 경우 blocksRaycasts 제어
            var cg = go.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                record.targetCanvasGroup = cg;
                record.originalBlocksRaycasts = cg.blocksRaycasts;
                cg.blocksRaycasts = allowInteraction;
            }

            // 2. 타겟 본체에 Canvas가 없는 경우 동적으로 Canvas & GraphicRaycaster 추가
            Canvas rootCanvas = go.GetComponent<Canvas>();
            bool rootCanvasAdded = false;
            GraphicRaycaster rootRaycaster = go.GetComponent<GraphicRaycaster>();
            bool rootRaycasterAdded = false;

            if (rootCanvas == null)
            {
                rootCanvas = go.AddComponent<Canvas>();
                rootCanvasAdded = true;
            }
            if (rootRaycaster == null)
            {
                rootRaycaster = go.AddComponent<GraphicRaycaster>();
                rootRaycasterAdded = true;
            }

            int highlightLayerId = SortingLayer.NameToID(HIGHLIGHT_LAYER_NAME);
            int gameUiLayerId = SortingLayer.NameToID("GameUI");

            // 3. [중요] 상태 변경 이전에 모든 Canvas의 원래 상태를 '먼저' 백업!
            var allCanvases = go.GetComponentsInChildren<Canvas>(true);
            foreach (var c in allCanvases)
            {
                if (c == null) continue;

                int origLayer = c.sortingLayerID;
                // 혹시 이미 하이라이트 레이어로 남아있는 경우 GameUI 레이어로 복원하도록 안전 보정
                if (origLayer == highlightLayerId)
                {
                    origLayer = gameUiLayerId != 0 ? gameUiLayerId : 0;
                }

                var cr = new CanvasRecord
                {
                    canvas = c,
                    originalLayerId = origLayer,
                    originalOrder = c.sortingOrder,
                    originalOverride = (c == rootCanvas && rootCanvasAdded) ? false : c.overrideSorting,
                    dynamicallyAdded = (c == rootCanvas && rootCanvasAdded)
                };

                var gr = c.GetComponent<GraphicRaycaster>();
                if (gr != null)
                {
                    cr.raycaster = gr;
                    cr.originalRaycasterEnabled = gr.enabled;
                    cr.raycasterDynamicallyAdded = (gr == rootRaycaster && rootRaycasterAdded);
                }

                record.canvasRecords.Add(cr);
            }

            // 4. 백업이 완료된 후, 이제 하이라이트 레이어로 승격 적용!
            foreach (var cr in record.canvasRecords)
            {
                var c = cr.canvas;
                if (c == null) continue;

                c.overrideSorting = true;
                c.sortingLayerName = HIGHLIGHT_LAYER_NAME;

                // 동적으로 추가된 루트 캔버스인 경우 기본 하이라이트 오더(601) 부여
                // 기존 캔버스(CardView의 10, FXRoot의 12 등)는 고유 상대 순서를 유지
                if (cr.dynamicallyAdded)
                {
                    c.sortingOrder = HIGHLIGHT_SORTING_ORDER;
                }

                if (cr.raycaster != null)
                {
                    cr.raycaster.enabled = allowInteraction;
                }
            }

            // 5. HighlightableUI가 있는 경우 추적 등록
            var hUI = go.GetComponent<HighlightableUI>();
            if (hUI != null)
            {
                record.highlightUI = hUI;
            }

            activeHighlights.Add(record);
        }

        private void RevertHighlightRecord(HighlightRecord record)
        {
            if (record == null) return;

            // 1. CanvasGroup 원복
            if (record.targetCanvasGroup != null)
            {
                record.targetCanvasGroup.blocksRaycasts = record.originalBlocksRaycasts;
            }

            int highlightLayerId = SortingLayer.NameToID(HIGHLIGHT_LAYER_NAME);
            int gameUiLayerId = SortingLayer.NameToID("GameUI");

            // 2. 하위 캔버스들 역순으로 정확히 원래 레이어 및 오더로 복원
            for (int i = record.canvasRecords.Count - 1; i >= 0; i--)
            {
                var cr = record.canvasRecords[i];
                if (cr.canvas == null) continue;

                if (cr.dynamicallyAdded)
                {
                    cr.canvas.overrideSorting = false;
                    cr.canvas.enabled = false;
                    if (cr.raycaster != null)
                    {
                        cr.raycaster.enabled = false;
                        Destroy(cr.raycaster);
                    }
                    Destroy(cr.canvas);
                }
                else
                {
                    int targetLayerId = cr.originalLayerId;
                    if (targetLayerId == highlightLayerId)
                    {
                        targetLayerId = gameUiLayerId != 0 ? gameUiLayerId : 0;
                    }

                    cr.canvas.overrideSorting = cr.originalOverride;
                    cr.canvas.sortingLayerID = targetLayerId;
                    cr.canvas.sortingOrder = cr.originalOrder;

                    if (cr.raycaster != null)
                    {
                        if (cr.raycasterDynamicallyAdded)
                        {
                            Destroy(cr.raycaster);
                        }
                        else
                        {
                            cr.raycaster.enabled = cr.originalRaycasterEnabled;
                        }
                    }
                }
            }

            // 3. HighlightableUI 복원 (내부 캐시 동기화)
            if (record.highlightUI != null)
            {
                record.highlightUI.SetHighlight(false);
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
