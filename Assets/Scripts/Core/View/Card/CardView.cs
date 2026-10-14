using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Core.Table;
using System;
using DG.Tweening;
using Core.Battle;
using UnityEngine.Serialization;

namespace Core.View
{
    public class CardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("New Prefab References")]
        [SerializeField] private RectTransform positionRoot; // 연출용 루트 (DOTween Pro가 제어하는 대상)
        [SerializeField] private GameObject rockCase;
        [SerializeField] private GameObject scissorCase;
        [SerializeField] private GameObject paperCase;
        [SerializeField] private GameObject rainCase;
        [SerializeField] private GameObject foolCase;
        [SerializeField] private GameObject selectedCase;
        [FormerlySerializedAs("statePekoraSkill")]
        [SerializeField] private GameObject brokenObject;

        //[Header("Legacy References (Will be mapped to active case)")]
        //[SerializeField] private Image visualBody;
        //[SerializeField] private TextMeshProUGUI nameText;
        //[SerializeField] private Image iconImage;
        //[SerializeField] private Image elementImage;
        //[SerializeField] private GameObject selectionFrame;

        [Header("Juice Settings")]
        [SerializeField] private AudioClip hoverSfx;
        [SerializeField] private AudioClip selectSfx;

        public CardInstance CardInstance { get; private set; }
        public SkillTableRow SkillData => CardInstance?.TableData;

        private Action<CardView> onClickAction;
        private HandContainer ownerContainer;
        private bool isSelected = false;
        private bool isPlayerCard = false; // 플레이어 카드 여부 (상호작용 가능 여부)

        public bool IsSelected => isSelected;
        public bool IsCovered { get; private set; }

        /// <summary>
        /// 카드의 속성 및 기믹 케이스를 숨기거나 복원합니다 (에디터 디버그 및 블라인드 모드).
        /// </summary>
        public void SetCovered(bool covered)
        {
            IsCovered = covered;

            if (covered)
            {
                rockCase?.SetActive(false);
                scissorCase?.SetActive(false);
                paperCase?.SetActive(false);
                foolCase?.SetActive(false);
                rainCase?.SetActive(false);
                brokenObject?.SetActive(false);
            }
            else
            {
                RefreshVisuals();
            }
        }

        private void Awake()
        {
            //if (selectionFrame != null) selectionFrame.SetActive(false);
            if (selectedCase != null) selectedCase.SetActive(false);
        }

        /// <summary>
        /// 카드와의 상호작용(호버 연출, 클릭 등)을 일괄적으로 켜고 끕니다.
        /// </summary>
        public void SetInteractionActive(bool active, bool disableRaycast = false)
        {
            var trigger = GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (trigger != null) trigger.enabled = active;

            // [Fix] 레이캐스트 차단 로직 세분화
            if (disableRaycast)
            {
                // 완전히 차단 (적 카드 등)
                var graphics = GetComponentsInChildren<Graphic>(true);
                foreach (var g in graphics) g.raycastTarget = false;
            }
            else
            {
                // [Change] disableRaycast가 false라면, 최소한 클릭은 가능해야 하므로 raycastTarget을 항상 켬
                // active가 false인 경우(트레이 등)에는 호버 연출만 안되는 것임
                var graphics = GetComponentsInChildren<Graphic>(true);
                foreach (var g in graphics) g.raycastTarget = true;
            }

            if (!active) ResetVisualState();
        }

        /// <summary>
        /// 소속된 컨테이너 정보를 갱신합니다. (핸드 <-> 트레이 이동 대응)
        /// </summary>
        public void SetOwnerContainer(HandContainer container)
        {
            ownerContainer = container;
        }

        /// <summary>
        /// 호버 중 클릭 등으로 인해 연출이 꼬이는 것을 방지하기 위해 강제로 위치를 리셋합니다.
        /// </summary>
        public void ResetVisualState()
        {
            if (positionRoot != null)
            {
                // [Fix] DOTweenAnimation 컴포넌트가 없을 수 있으므로 null 체크 추가
                var anim = positionRoot.GetComponent<DOTweenAnimation>();
                if (anim != null) anim.DOComplete();

                positionRoot.DOKill();
                positionRoot.anchoredPosition = Vector2.zero;
            }
        }

        private void RefreshAppearance(bool isHovered)
        {
            // [Note] 모든 시각적 트윈 로직은 제거됨 (DOTween Pro에서 처리)
        }

        public void Setup(CardInstance instance, Action<CardView> onClick, HandContainer container = null)
        {
            if (instance == null) return;
            CardInstance = instance;
            onClickAction = onClick;
            ownerContainer = container;
            isPlayerCard = (onClickAction != null); // 클릭 액션이 있으면 플레이어 카드로 간주

            var data = instance.TableData;

            // 1. 속성별 케이스 활성화 (Fool 등 오버라이드 지원)
            UpdateElementCase(instance.EffectiveElement);

            // 2. 현재 활성화된 케이스 내의 컴포넌트에 데이터 바인딩
            BindDataToActiveCase(data);

            // 3. 상태 이상(BreakCard, Rain) 시각적 처리
            RefreshBrokenState();
            RefreshRainState();

            var button = GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                if (isPlayerCard)
                {
                    button.onClick.AddListener(() =>
                    {
                        if (CardInstance.IsBroken) return; // Broken 상태면 클릭 무시

                        if (selectSfx != null) Core.Audio.SoundManager.Instance.PlaySe(selectSfx);
                        else Core.Audio.SoundManager.Instance.PlaySe("UI_Card_Select");

                        onClickAction.Invoke(this);
                    });
                }
            }

            // [Fix] 적 카드인 경우 초기부터 레이캐스트를 아예 차단
            if (!isPlayerCard)
            {
                SetInteractionActive(false, true);
            }
        }

        public void RefreshBrokenState()
        {
            bool isBroken = CardInstance != null && CardInstance.IsBroken;

            // [Fix] 모든 자식 Graphic의 color를 직접 건드리면 RPS 아이콘 고유의 색상이 파괴됨.

            if (brokenObject != null)
            {
                brokenObject.SetActive(isBroken);
            }
            else
            {
                // 대신 CanvasGroup의 alpha를 조절하고, 배경(Panel)만 Surgical하게 수정하여 원본 색상을 유지합니다.
                var cg = GetComponent<CanvasGroup>();
                if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();

                cg.alpha = isBroken ? 0.6f : 1.0f;
            }

            if (positionRoot != null)
            {
                // 프리팹 구조상 PositionRoot 하위의 Panel을 찾아 배경만 어둡게 처리
                var panel = positionRoot.Find("Panel")?.GetComponent<Image>();
                if (panel != null)
                {
                    // Panel의 기본 색상은 흰색(1,1,1,1)이므로 안전하게 복구 및 변조 가능
                    panel.color = isBroken ? new Color(0.4f, 0.4f, 0.4f, 1f) : Color.white;
                }
            }

            // [Fix] 적의 카드는 Broken 여부와 상관없이 항상 상호작용 및 레이캐스트 비활성화 유지
            if (isPlayerCard)
            {
                SetInteractionActive(!isBroken);
            }
            else
            {
                SetInteractionActive(false, true);
            }
        }

        /// <summary>
        /// Rain 효과(빗방울 디버프) 시각적 처리 (Pekora의 Broken 상태와 유사하게 기본 묵찌빠 위에 오버레이)
        /// </summary>
        public void RefreshRainState()
        {
            bool isRain = CardInstance != null && CardInstance.IsRain;
            if (rainCase != null)
            {
                rainCase.SetActive(isRain);
            }
        }

        public void RefreshVisuals()
        {
            if (CardInstance == null) return;
            if (IsCovered)
            {
                SetCovered(true);
                return;
            }
            UpdateElementCase(CardInstance.EffectiveElement);
            BindDataToActiveCase(CardInstance.TableData);
            RefreshBrokenState();
            RefreshRainState();
        }

        private void UpdateElementCase(SkillElement element)
        {
            rockCase?.SetActive(element == SkillElement.Rock);
            scissorCase?.SetActive(element == SkillElement.Scissors);
            paperCase?.SetActive(element == SkillElement.Paper);
            foolCase?.SetActive(element == SkillElement.Fool);
        }

        private void BindDataToActiveCase(SkillTableRow data)
        {
            if (data == null) return;

            // 현재 활성화된 케이스 찾기
            GameObject activeCase = null;
            if (rockCase != null && rockCase.activeSelf) activeCase = rockCase;
            else if (scissorCase != null && scissorCase.activeSelf) activeCase = scissorCase;
            else if (paperCase != null && paperCase.activeSelf) activeCase = paperCase;
            else if (foolCase != null && foolCase.activeSelf) activeCase = foolCase;

            if (activeCase == null) return;

            // 케이스 내부의 텍스트 업데이트 (아이콘 스프라이트는 각 케이스 프리팹에 배치된 고유 스프라이트 사용)
            var activeNameText = activeCase.GetComponentInChildren<TextMeshProUGUI>();
            var nameRow = TableManager.Instance.GetTable<StringTable>().Get(data.nameStringId);
            if (activeNameText != null)
            {
                if (nameRow != null)
                    activeNameText.text = nameRow.Get(TableManager.Instance.currentLanguage.Value); //TODO: Text Id holder로 수정
                else
                    activeNameText.text = (data.element == SkillElement.Fool ? "Fool" : data.element.ToString());
            }
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;

            // 기존 selectionFrame과 신규 selectedCase 모두 지원
            //if (selectionFrame != null) selectionFrame.SetActive(isSelected);
            if (selectedCase != null) selectedCase.SetActive(isSelected);

            RefreshAppearance(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!isPlayerCard) return; // [Fix] 적 카드 등 상호작용 불가시 즉시 리턴

            // [Fix] 상호작용이 꺼진 상태(트레이 등)라면 호버 처리 생략
            var trigger = GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (trigger != null && !trigger.enabled) return;

            transform.SetAsLastSibling();
            if (hoverSfx != null) Core.Audio.SoundManager.Instance.PlaySe(hoverSfx);
            else Core.Audio.SoundManager.Instance.PlaySe("UI_Card_Hover");
            RefreshAppearance(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!isPlayerCard) return; // [Fix] 적 카드 등 상호작용 불가시 즉시 리턴

            if (ownerContainer != null) ownerContainer.SyncSiblingIndices();
            RefreshAppearance(false);
        }
    }

}
