using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Core.Table;
using System;
using System.Collections.Generic;
using DG.Tweening;
using Core.Battle;
using UnityEngine.Serialization;

namespace Core.View
{
    public class CardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IPointerDownHandler
    {
        [Header("New Prefab References")]
        [SerializeField] private RectTransform positionRoot; // 연출용 루트 (DOTween Pro가 제어하는 대상)
        [SerializeField] private GameObject rockCase;
        [SerializeField] private GameObject scissorCase;
        [SerializeField] private GameObject paperCase;
        [SerializeField] private GameObject rainCase;
        [SerializeField] private GameObject lavaCase;
        [SerializeField] private GameObject foolCase;
        [SerializeField] private GameObject selectedCase;
        [FormerlySerializedAs("statePekoraSkill")]
        [SerializeField] private GameObject brokenObject;
        [SerializeField] private GameObject bombCase;
        [SerializeField] private TextMeshProUGUI bombText;
        private GameObject debugBombBadge;

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

        // [UI Optimization] Canvas 배치 분리 및 Sibling reorder 제거를 위한 서브 캔버스 제어
        private Canvas cardCanvas;
        private Canvas[] cachedChildCanvases;
        private EventTrigger cachedEventTrigger;
        private Graphic cachedRootGraphic;
        private Graphic[] cachedChildGraphics;
        private Image cachedPanelImage;
        private DOTweenAnimation cachedPositionTween;
        private Button cachedButton;
        private bool componentsCached = false;

        private int baseSortingOrder = 10;
        private bool isHovered = false;

        public bool IsSelected => isSelected;
        public bool IsCovered { get; private set; }

        private void CacheComponents()
        {
            if (componentsCached) return;
            componentsCached = true;

            EnsureCanvas();

            cachedEventTrigger = GetComponent<EventTrigger>();
            cachedButton = GetComponent<Button>();

            if (positionRoot != null)
            {
                cachedPositionTween = positionRoot.GetComponent<DOTweenAnimation>();
                var panelTransform = positionRoot.Find("Panel") as RectTransform;
                if (panelTransform != null)
                {
                    cachedPanelImage = panelTransform.GetComponent<Image>();
                }
            }

            cachedRootGraphic = GetComponent<Graphic>();
            if (cachedRootGraphic == null && cachedPanelImage != null)
            {
                cachedRootGraphic = cachedPanelImage;
            }

            // [WYSIWYG Hitbox Sync]
            // 루트 히트박스(100x140)보다 시각적 카드 배경(PositionRoot/Panel: 우측 +4px, 하단 -8px 등)이
            // 바깥으로 더 나와 있는 경우, 눈에 보이는 카드 외곽선에 마우스가 닿는 즉시 호버/클릭이 인식되도록
            // raycastPadding(음수 = 영역 확장: x=Left, y=Bottom, z=Right, w=Top)을 보정합니다.
            if (cachedRootGraphic != null && cachedPanelImage != null && cachedRootGraphic != cachedPanelImage)
            {
                RectTransform panelRect = cachedPanelImage.rectTransform;
                if (panelRect != null && panelRect.anchorMin == Vector2.zero && panelRect.anchorMax == Vector2.one)
                {
                    float leftPad = Mathf.Min(0f, panelRect.offsetMin.x);
                    float bottomPad = Mathf.Min(0f, panelRect.offsetMin.y);
                    float rightPad = Mathf.Min(0f, -panelRect.offsetMax.x);
                    float topPad = Mathf.Min(0f, -panelRect.offsetMax.y);
                    cachedRootGraphic.raycastPadding = new Vector4(leftPad, bottomPad, rightPad, topPad);
                }
            }

            var allCanvases = GetComponentsInChildren<Canvas>(true);
            var subCanvasList = new List<Canvas>(allCanvases.Length);
            for (int i = 0; i < allCanvases.Length; i++)
            {
                if (allCanvases[i] != null && allCanvases[i] != cardCanvas)
                {
                    subCanvasList.Add(allCanvases[i]);
                }
            }
            cachedChildCanvases = subCanvasList.ToArray();

            cachedChildGraphics = GetComponentsInChildren<Graphic>(true);
        }

        private void EnsureCanvas()
        {
            if (cardCanvas == null)
            {
                cardCanvas = GetComponent<Canvas>();
                if (cardCanvas == null)
                {
                    cardCanvas = gameObject.AddComponent<Canvas>();
                }
                cardCanvas.overrideSorting = true;
                if (cardCanvas.sortingLayerName == "Default" || string.IsNullOrEmpty(cardCanvas.sortingLayerName))
                {
                    cardCanvas.sortingLayerName = "GameUI";
                }
                if (GetComponent<GraphicRaycaster>() == null)
                {
                    gameObject.AddComponent<GraphicRaycaster>();
                }
            }
        }

        public void SetBaseSortingOrder(int order)
        {
            baseSortingOrder = order;
            if (!isHovered)
            {
                ApplySortingOrder(baseSortingOrder);
            }
        }

        private void ApplySortingOrder(int order)
        {
            CacheComponents();
            if (cardCanvas != null)
            {
                cardCanvas.sortingOrder = order;
            }

            // [Fix] FXRoot 등 자식 캔버스가 있을 경우, 캐싱된 배열을 순회하여 가비지 할당 및 탐색 비용 없이 동기화
            if (cachedChildCanvases != null)
            {
                int sortingLayerId = cardCanvas != null ? cardCanvas.sortingLayerID : 0;
                for (int i = 0; i < cachedChildCanvases.Length; i++)
                {
                    var c = cachedChildCanvases[i];
                    if (c != null)
                    {
                        c.overrideSorting = true;
                        if (cardCanvas != null)
                        {
                            c.sortingLayerID = sortingLayerId;
                        }
                        c.sortingOrder = order + 2;
                    }
                }
            }
        }

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
                lavaCase?.SetActive(false);
                brokenObject?.SetActive(false);
                bombCase?.SetActive(false);
                if (bombText != null) bombText.gameObject.SetActive(false);
                if (debugBombBadge != null) debugBombBadge.SetActive(false);
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
            CacheComponents();
        }

        /// <summary>
        /// 카드와의 상호작용(호버 연출, 클릭 등)을 일괄적으로 켜고 끕니다.
        /// </summary>
        public void SetInteractionActive(bool active, bool disableRaycast = false)
        {
            CacheComponents();

            if (cachedEventTrigger != null) cachedEventTrigger.enabled = active;

            // [UI Optimization & Frozen Card Bug Fix]
            // 루트의 기본 Graphic(배경/투명 히트박스)만 레이캐스트를 받도록 하고,
            // 하위 모든 시각적 요소(아이콘, 텍스트, 이펙트 등)는 raycastTarget을 꺼서
            // 1) 마우스 다운/업 시 자식 오브젝트로 분산되어 클릭이 씹히는 현상(굳은 카드) 방지
            // 2) 매 프레임 UI 레이캐스트 연산 최적화
            if (cachedRootGraphic != null)
            {
                cachedRootGraphic.raycastTarget = !disableRaycast;
            }

            if (cachedChildGraphics != null)
            {
                for (int i = 0; i < cachedChildGraphics.Length; i++)
                {
                    var g = cachedChildGraphics[i];
                    if (g != null && g != cachedRootGraphic)
                    {
                        g.raycastTarget = false;
                    }
                }
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
            CacheComponents();
            isHovered = false;
            ApplySortingOrder(baseSortingOrder);

            if (positionRoot != null)
            {
                if (cachedPositionTween != null) cachedPositionTween.DOComplete();

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
            CacheComponents();

            CardInstance = instance;
            onClickAction = onClick;
            ownerContainer = container;
            isPlayerCard = (onClickAction != null); // 클릭 액션이 있으면 플레이어 카드로 간주

            ApplySortingOrder(baseSortingOrder);

            var data = instance.TableData;

            // 1. 속성별 케이스 활성화 (Fool 등 오버라이드 지원)
            UpdateElementCase(instance.EffectiveElement);

            // 2. 현재 활성화된 케이스 내의 컴포넌트에 데이터 바인딩
            BindDataToActiveCase(data);

            // 3. 상태 이상(BreakCard, Rain, Lava, Bomb) 시각적 처리
            RefreshBrokenState();
            RefreshRainState();
            RefreshLavaState();
            RefreshBombState();

            if (cachedButton != null)
            {
                // [Fix] 클릭은 IPointerClickHandler(OnPointerClick)에서 전담하여 중복 및 씹힘 방지
                cachedButton.onClick.RemoveAllListeners();
            }

            // [Fix] 적 카드인 경우 초기부터 레이캐스트를 아예 차단
            if (!isPlayerCard)
            {
                SetInteractionActive(false, true);
            }
            else
            {
                SetInteractionActive(true, false);
            }
        }

        public void RefreshBrokenState()
        {
            CacheComponents();
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

            if (cachedPanelImage != null)
            {
                // Panel의 기본 색상은 흰색(1,1,1,1)이므로 안전하게 복구 및 변조 가능
                cachedPanelImage.color = isBroken ? new Color(0.4f, 0.4f, 0.4f, 1f) : Color.white;
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
            if (rainCase == null && positionRoot != null)
            {
                rainCase = positionRoot.Find("FXRoot/Case_Rain")?.gameObject 
                        ?? positionRoot.Find("Case_Rain")?.gameObject;
            }

            if (rainCase != null)
            {
                rainCase.SetActive(isRain);
            }
        }

        /// <summary>
        /// Lava 효과(용암 디버프) 시각적 처리 (기본 묵찌빠 위에 오버레이)
        /// </summary>
        public void RefreshLavaState()
        {
            bool isLava = CardInstance != null && CardInstance.IsLava;
            if (lavaCase == null && positionRoot != null)
            {
                lavaCase = positionRoot.Find("FXRoot/Case_Lava")?.gameObject 
                        ?? positionRoot.Find("Case_Lava")?.gameObject;
            }

            if (lavaCase != null)
            {
                lavaCase.SetActive(isLava);
            }
        }

        /// <summary>
        /// Bomb 효과(폭탄 디버프) 시각적 처리
        /// 프리팹에 bombCase/bombText가 할당되어 있으면 이를 우선 사용하고,
        /// 아직 UI 리소스가 준비되지 않은 테스트 단계에서는 가벼운 디버그 배지를 띄워 스택 수를 확인합니다.
        /// </summary>
        public void RefreshBombState()
        {
            int bombStack = CardInstance != null ? CardInstance.BombStack : 0;
            bool hasBomb = bombStack > 0;

            if (bombCase != null)
            {
                bombCase.SetActive(hasBomb);
            }

            if (bombText != null)
            {
                bombText.gameObject.SetActive(hasBomb);
                if (hasBomb)
                {
                    bombText.text = $"💣{bombStack}";
                }
            }
            else if (bombCase == null && hasBomb)
            {
                // [Test/Debug Fallback] 프리팹에 전용 bombCase/bombText가 아직 없을 때 테스트용 임시 텍스트 표시
                EnsureDebugBombBadge(bombStack);
            }
            else if (!hasBomb && debugBombBadge != null)
            {
                debugBombBadge.SetActive(false);
            }
        }

        private void EnsureDebugBombBadge(int stack)
        {
            if (debugBombBadge == null)
            {
                var go = new GameObject("Debug_BombBadge");
                go.transform.SetParent(positionRoot != null ? positionRoot : transform, false);
                var rect = go.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(1, 1);
                rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(1, 1);
                rect.anchoredPosition = new Vector2(-5, -5);
                rect.sizeDelta = new Vector2(50, 22);

                var img = go.AddComponent<Image>();
                img.color = new Color(0.85f, 0.15f, 0.15f, 0.9f);
                img.raycastTarget = false;

                var textGo = new GameObject("Text");
                textGo.transform.SetParent(go.transform, false);
                var textRect = textGo.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.sizeDelta = Vector2.zero;

                var tmp = textGo.AddComponent<TextMeshProUGUI>();
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontSize = 13;
                tmp.color = Color.white;
                tmp.raycastTarget = false;
                debugBombBadge = go;
            }

            debugBombBadge.SetActive(true);
            var badgeText = debugBombBadge.GetComponentInChildren<TextMeshProUGUI>();
            if (badgeText != null) badgeText.text = $"💣{stack}";
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
            RefreshLavaState();
            RefreshBombState();
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

        public void OnPointerDown(PointerEventData eventData)
        {
            // Unity UI EventSystem에서 마우스 다운 대상 인식을 보장하기 위해 빈 핸들러 유지
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            TriggerSelect();
        }

        private void TriggerSelect()
        {
            if (!isPlayerCard) return;
            if (CardInstance != null && CardInstance.IsBroken) return; // Broken 상태면 클릭 무시

            if (selectSfx != null) Core.Audio.SoundManager.Instance.PlaySe(selectSfx);
            else Core.Audio.SoundManager.Instance.PlaySe("UI_Card_Select");

            onClickAction?.Invoke(this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!isPlayerCard) return; // [Fix] 적 카드 등 상호작용 불가시 즉시 리턴

            // [Fix] 상호작용이 꺼진 상태(트레이 등)라면 호버 처리 생략 (캐싱된 참조 사용)
            if (cachedEventTrigger != null && !cachedEventTrigger.enabled) return;

            // [Latency Optimization] 정렬 오더 갱신보다 사운드 재생을 최우선으로 호출하여 오디오 큐에 즉시 등록
            if (hoverSfx != null) Core.Audio.SoundManager.Instance.PlaySe(hoverSfx);
            else Core.Audio.SoundManager.Instance.PlaySe("UI_Card_Select");

            // [UI Optimization] SetAsLastSibling()은 부모 Canvas 계층 전체를 Rebuild하여
            // 대규모 프레임 드랍을 유발하므로 절대 호출하지 않고, Sub-Canvas sortingOrder만 올립니다.
            isHovered = true;
            ApplySortingOrder(baseSortingOrder + 30);

            RefreshAppearance(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!isPlayerCard) return; // [Fix] 적 카드 등 상호작용 불가시 즉시 리턴

            isHovered = false;
            ApplySortingOrder(baseSortingOrder);

            RefreshAppearance(false);
        }
    }

}
