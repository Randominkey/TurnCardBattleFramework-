using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Core.View
{
    /// <summary>
    /// 배틀 씬의 도움말 기능을 담당하는 팝업 윈도우
    /// </summary>
    public class BattleHelpWindow : MonoBehaviour
    {
        [System.Serializable]
        public struct HelpPage
        {
            public Sprite image;
            public int textId; // StringTable ID
        }

        [Header("UI References")]
        [SerializeField] private Image imgFrame;
        [SerializeField] private TextIdHolder txtDesc;
        [SerializeField] private Transform pageIndicatorRoot;
        [SerializeField] private GameObject indicatorPrefab;

        [Header("Button Cases (Objects to toggle)")]
        [SerializeField] private GameObject caseFirst;
        [SerializeField] private GameObject caseMid;
        [SerializeField] private GameObject caseLast;

        [Header("Individual Buttons")]
        [SerializeField] private Button[] nextButtons;
        [SerializeField] private Button[] prevButtons;
        [SerializeField] private Button closeButton;

        [Header("Data")]
        [SerializeField] private List<HelpPage> pages = new List<HelpPage>();

        private int currentPage = 0;
        private List<Transform> indicators = new List<Transform>();

        private void Awake()
        {
            // 버튼 이벤트 연결
            if (nextButtons != null)
            {
                foreach (var b in nextButtons) b.onClick.AddListener(NextPage);
            }
            
            if (prevButtons != null)
            {
                foreach (var b in prevButtons) b.onClick.AddListener(PrevPage);
            }
            
            if (closeButton != null) closeButton.onClick.AddListener(Close);

            // [New] 페이지 수에 맞게 인디케이터 동적 생성
            SetupIndicators();
        }

        private void SetupIndicators()
        {
            if (pageIndicatorRoot == null || indicatorPrefab == null) return;

            // [Fix] 기존 잔여 오브젝트 확실히 제거
            foreach (Transform child in pageIndicatorRoot)
            {
                Destroy(child.gameObject);
            }
            indicators.Clear();

            // 페이지 개수만큼 생성
            for (int i = 0; i < pages.Count; i++)
            {
                var go = Instantiate(indicatorPrefab, pageIndicatorRoot);
                indicators.Add(go.transform);
            }
        }

        /// <summary>
        /// 도움말 창을 열고 첫 페이지로 초기화합니다.
        /// </summary>
        public void Open()
        {
            gameObject.SetActive(true);
            currentPage = 0;
            UpdatePage();
        }

        /// <summary>
        /// 도움말 창을 닫습니다.
        /// </summary>
        public void Close()
        {
            gameObject.SetActive(false);
        }

        private void NextPage()
        {
            if (currentPage < pages.Count - 1)
            {
                currentPage++;
                UpdatePage();
            }
        }

        private void PrevPage()
        {
            if (currentPage > 0)
            {
                currentPage--;
                UpdatePage();
            }
        }

        private void UpdatePage()
        {
            if (pages == null || pages.Count == 0) return;

            // 1. 이미지 및 텍스트 갱신
            var page = pages[currentPage];
            if (imgFrame != null) imgFrame.sprite = page.image;
            if (txtDesc != null) txtDesc.SetId(page.textId);

            // 2. 버튼 케이스 토글 (기획서 10P 대응)
            bool isFirst = currentPage == 0;
            bool isLast = currentPage == pages.Count - 1;
            bool isMid = !isFirst && !isLast;

            if (pages.Count == 1)
            {
                isFirst = false;
                isMid = false;
                isLast = true;
            }

            if (caseFirst != null) caseFirst.SetActive(isFirst);
            if (caseMid != null) caseMid.SetActive(isMid);
            if (caseLast != null) caseLast.SetActive(isLast);

            // 3. [New] 인디케이터 상태 토글 (프리팹 자식 오브젝트 제어)
            for (int i = 0; i < indicators.Count; i++)
            {
                var dot = indicators[i];
                if (dot.childCount >= 2)
                {
                    bool isCurrent = (i == currentPage);
                    // 0번 자식: Default (Off), 1번 자식: Active (On)
                    dot.GetChild(0).gameObject.SetActive(!isCurrent); 
                    dot.GetChild(1).gameObject.SetActive(isCurrent);  
                }
            }
        }
    }
}
