using UnityEngine;
using UnityEngine.UI;

namespace Core.View
{
    /// <summary>
    /// 툴팁 UI 프리팹을 제어하는 클래스.
    /// TextIdHolder를 통해 다국어 ID를 할당하며, 화면 사분면에 따라 피벗을 자동 조정합니다.
    /// </summary>
    public class TooltipWindow : MonoBehaviour
    {
        [Header("UI References (TextIdHolders)")]
        [SerializeField] private TextIdHolder titleHolder;
        [SerializeField] private TextIdHolder subTitleHolder;
        [SerializeField] private TextIdHolder nameHolder;
        [SerializeField] private TextIdHolder descHolder;
        
        [Header("Layout")]
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private CanvasGroup canvasGroup;

        private void Awake()
        {
            if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            
            //// 프리팹 구조에 기반한 자동 할당 시도 (인스펙터 미할당 대비)
            //if (titleHolder == null) titleHolder = transform.Find("Txt_Title")?.GetComponent<TextIdHolder>();
            //if (subTitleHolder == null) subTitleHolder = transform.Find("Txt_Desc_SpecialSkill")?.GetComponent<TextIdHolder>();
            //if (nameHolder == null) nameHolder = transform.Find("ContentsRoot/Txt_SkillName")?.GetComponent<TextIdHolder>();
            //if (descHolder == null) descHolder = transform.Find("ContentsRoot/Txt_SkillDesc")?.GetComponent<TextIdHolder>();

            Hide();
        }

        /// <summary>
        /// StringTable ID를 받아 툴팁의 내용을 설정합니다.
        /// </summary>
        public void SetContent(int titleId, int subTitleId, int nameId, int descId)
        {
            if (titleHolder != null) titleHolder.SetId(titleId);
            if (subTitleHolder != null) subTitleHolder.SetId(subTitleId);
            if (nameHolder != null) nameHolder.SetId(nameId);
            if (descHolder != null) descHolder.SetId(descId);

            // 내용에 맞춰 레이아웃 갱신
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }

        public void Show()
        {
            gameObject.SetActive(true);
            if (canvasGroup != null) canvasGroup.alpha = 1;
            UpdatePosition();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            if (canvasGroup != null) canvasGroup.alpha = 0;
        }

        private void LateUpdate()
        {
            if (gameObject.activeSelf)
            {
                UpdatePosition();
            }
        }

        private void UpdatePosition()
        {
            Vector2 mousePos = Input.mousePosition;
            Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);

            // [지능형 위치 조정] 마우스 위치에 따라 피벗을 반전시켜 화면 안쪽으로 펼쳐지게 함
            float pivotX = mousePos.x > screenCenter.x ? 1.05f : -0.05f;
            float pivotY = mousePos.y > screenCenter.y ? 1.05f : -0.05f;

            rectTransform.pivot = new Vector2(pivotX, pivotY);

            // [Fix] Screen Space - Camera 모드 대응을 위해 좌표 변환 로직 수정
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                RectTransform parentRect = transform.parent as RectTransform;
                if (parentRect != null)
                {
                    // Overlay 모드면 camera는 null, Camera 모드면 canvas.worldCamera 사용
                    Camera cam = (canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas.worldCamera;
                    
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, mousePos, cam, out Vector2 localPoint))
                    {
                        rectTransform.anchoredPosition = localPoint;
                    }
                }
                else
                {
                    // 부모가 RectTransform이 아닐 경우 (Fallback)
                    rectTransform.position = mousePos;
                }
            }
        }
    }
}
