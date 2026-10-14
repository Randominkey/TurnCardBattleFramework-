using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Core.Table;

namespace Core.View
{
    /// <summary>
    /// 대화 로그 창 전체를 관리하는 클래스
    /// </summary>
    public class SpeakLogView : MonoBehaviour
    {
        [SerializeField] private GameObject windowRoot;
        [SerializeField] private Transform contentRoot;
        [SerializeField] private GameObject logItemPrefab;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private Button closeButton;

        /// <summary>
        /// 로그 창이 열려있는지 여부
        /// </summary>
        public bool IsLogOpen => windowRoot != null && windowRoot.activeSelf;

        private bool isInitialized = false;

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (isInitialized) return;

            // 초기 상태는 닫힘
            if (windowRoot != null) windowRoot.SetActive(false);
            
            // [Cleanup] 샘플 자식 오브젝트 삭제
            if (contentRoot != null)
            {
                foreach (Transform child in contentRoot)
                {
                    // 샘플 오브젝트(보통 이름에 Sample이나 Clone이 없는 것)만 삭제하거나 
                    // 실제 데이터 추가 전이라면 모두 삭제
                    Destroy(child.gameObject);
                }
            }

            // 닫기 버튼 이벤트 등록
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Close);
            }

            isInitialized = true;
        }

        /// <summary>
        /// 새로운 대사 로그를 추가합니다.
        /// </summary>
        public void AddLog(CharacterKeyName key, int nameId, int messageId, string hexCode = null)
        {
            if (!isInitialized) Initialize();
            
            if (logItemPrefab == null || contentRoot == null) return;

            var go = Instantiate(logItemPrefab, contentRoot);
            var itemView = go.GetComponent<SpeakLogItemView>();
            
            if (itemView != null)
            {
                itemView.SetLog(key, nameId, messageId, hexCode);
            }

            // 창이 열려있을 때만 스크롤 보정
            if (IsLogOpen)
            {
                ScrollToBottom();
            }
        }

        /// <summary>
        /// 로그 창을 엽니다.
        /// </summary>
        public void Open()
        {
            if (windowRoot != null)
            {
                windowRoot.SetActive(true);
                ScrollToBottom();
            }
        }

        /// <summary>
        /// 로그 창을 닫습니다.
        /// </summary>
        public void Close()
        {
            if (windowRoot != null)
            {
                windowRoot.SetActive(false);
            }
        }

        private void ScrollToBottom()
        {
            // 레이아웃 갱신 후 스크롤을 최하단으로 이동
            Canvas.ForceUpdateCanvases();
            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }
    }
}
