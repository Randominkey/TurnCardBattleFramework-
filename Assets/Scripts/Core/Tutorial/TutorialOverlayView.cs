using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace Core.Tutorial
{
    /// <summary>
    /// 튜토리얼 중 특정 UI를 하이라이트하고 나머지를 차단하는 오버레이 뷰.
    /// 외부 라이브러리 없이 4개의 패널(상, 하, 좌, 우)을 사용하여 중앙에 구멍을 만드는 방식을 사용합니다.
    /// </summary>
    [ExecuteAlways]
    public class TutorialOverlayView : MonoBehaviour
    {
        [Header("Panel References")]
        [SerializeField] private RectTransform topPanel;
        [SerializeField] private RectTransform bottomPanel;
        [SerializeField] private RectTransform leftPanel;
        [SerializeField] private RectTransform rightPanel;

        [Header("Settings")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float animDuration = 0.4f;
        [SerializeField] private Vector2 padding = new Vector2(10, 10);

        [Header("Editor Debug")]
        [SerializeField] private RectTransform editorTestTarget;

        private RectTransform myRectTransform;

        private void Awake()
        {
            myRectTransform = GetComponent<RectTransform>();
            if (Application.isPlaying)
            {
                if (canvasGroup != null) canvasGroup.alpha = 0;
                gameObject.SetActive(false);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying && editorTestTarget != null)
            {
                // 에디터에서 실시간 반영
                Highlight(editorTestTarget, true);
            }
        }
#endif

        public void Show()
        {
            gameObject.SetActive(true);
            if (canvasGroup != null)
            {
                canvasGroup.DOFade(1f, animDuration);
            }
        }

        public void Hide()
        {
            if (canvasGroup != null)
            {
                canvasGroup.DOFade(0f, animDuration).OnComplete(() => gameObject.SetActive(false));
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 특정 UI 요소를 하이라이트합니다.
        /// </summary>
        /// <param name="target">하이라이트 대상 RectTransform</param>
        /// <param name="immediate">즉시 이동 여부 (에디터에서는 항상 true)</param>
        public void Highlight(RectTransform target, bool immediate = false)
        {
            if (target == null) return;
            if (myRectTransform == null) myRectTransform = GetComponent<RectTransform>();

            // 대상의 월드 좌표 사각형을 오버레이의 로컬 좌표계로 변환
            Vector3[] corners = new Vector3[4];
            target.GetWorldCorners(corners);

            // corners: [0] bottom-left, [1] top-left, [2] top-right, [3] bottom-right
            Vector2 min = myRectTransform.InverseTransformPoint(corners[0]);
            Vector2 max = myRectTransform.InverseTransformPoint(corners[2]);

            // 패딩 적용
            min -= padding;
            max += padding;

            UpdatePanels(min, max, immediate || !Application.isPlaying);
        }

        private void UpdatePanels(Vector2 min, Vector2 max, bool immediate)
        {
            if (myRectTransform == null) myRectTransform = GetComponent<RectTransform>();
            Rect parentRect = myRectTransform.rect;

            // 각 패널의 목표 앵커 포지션과 사이즈 계산
            SetPanel(topPanel, new Vector2(parentRect.xMin, max.y), new Vector2(parentRect.width, parentRect.yMax - max.y), immediate);
            SetPanel(bottomPanel, new Vector2(parentRect.xMin, parentRect.yMin), new Vector2(parentRect.width, min.y - parentRect.yMin), immediate);
            SetPanel(leftPanel, new Vector2(parentRect.xMin, min.y), new Vector2(min.x - parentRect.xMin, max.y - min.y), immediate);
            SetPanel(rightPanel, new Vector2(max.x, min.y), new Vector2(parentRect.xMax - max.x, max.y - min.y), immediate);
        }

        private void SetPanel(RectTransform panel, Vector2 pos, Vector2 size, bool immediate)
        {
            if (panel == null) return;

            // 앵커와 피벗을 하단-왼쪽(0,0)으로 고정하여 좌표 계산을 단순화
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0, 0);

            if (immediate)
            {
                panel.anchoredPosition = pos;
                panel.sizeDelta = size;
            }
            else
            {
                panel.DOKill(); // 기존 애니메이션 중단
                panel.DOAnchorPos(pos, animDuration).SetEase(Ease.OutCubic);
                panel.DOSizeDelta(size, animDuration).SetEase(Ease.OutCubic);
            }
        }
        
        public void ClearHighlight()
        {
            UpdatePanels(Vector2.zero, Vector2.zero, false);
        }
    }
}
