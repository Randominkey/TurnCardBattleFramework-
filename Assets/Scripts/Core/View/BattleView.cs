using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UniRx;
using Core.Battle;
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Table;
using DG.Tweening;
using System.Collections;

namespace Core.View
{
    public class BattleView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private BattleStatusView playerStatus;
        [SerializeField] private BattleStatusView enemyStatus;
        [SerializeField] private TextMeshProUGUI roundText;
        [SerializeField] private TextMeshProUGUI limitRoundText;
        [SerializeField] private Button submitButton;
        [SerializeField] private Button drawButton;
        [SerializeField] private Button specialSkillButton;

        [Header("Character Visuals")]
        [SerializeField] private RectTransform playerFigure;
        [SerializeField] private RectTransform enemyFigure;

        [Header("Card UI")]
        [SerializeField] private CardView playerCardPrefab;
        [SerializeField] private CardView enemyCardPrefab;
        [SerializeField] private HandContainer playerHandContainer;
        [SerializeField] private HandContainer playerTrayContainer; // 선택된 카드가 모이는 곳
        [SerializeField] private HandContainer enemyHandContainer;

        [Header("Animation Settings")]
        [SerializeField] private ClashLayoutGroup playerClashLayout;
        [SerializeField] private ClashLayoutGroup enemyClashLayout;
        [SerializeField] private RectTransform cardClashPoint;
        [SerializeField] private TextMeshProUGUI clashResultText;
        [SerializeField] private GameObject roundBanner; // 라운드 알림용 배너
        [SerializeField] private CanvasGroup inGameUIGroup; // [New] 캐릭터 제외 UI 그룹
        [SerializeField] private RectTransform playerHandArea; // [New] 플레이어 핸드 + 버튼 영역
        [SerializeField] private RectTransform enemyHandArea; // [New] 적 핸드 영역
        [SerializeField] private float uiAnimDuration = 0.5f; // [New] UI 애니메이션 기본 시간
        [SerializeField] private float slideOffset = 500f; // [New] 슬라이드 이동 거리

        [Header("Juice Settings")]
        [SerializeField] private CanvasGroup rootCanvasGroup; // 전체 배틀 UI 페이드용
        [SerializeField] private BattleProductionManager productionManager; // [New] 연출 매니저
        [SerializeField] private BattleProductionData productionData; // [New] 연출 데이터 (SO)
        [SerializeField] private RectTransform clashHighOrderRoot; // [New] 격돌 카드용 고오더 루트 (Order 501 이상)

        [Header("Tooltip Settings")]
        [SerializeField] private TooltipWindow tooltipPrefab;
        [SerializeField] private RectTransform tooltipRoot;
        [SerializeField] private BattleHelpWindow battleHelpWindow;

        // --- Tutorial Support ---
        public BattleStatusView PlayerStatus => playerStatus;
        public BattleStatusView EnemyStatus => enemyStatus;
        public HandContainer PlayerHandContainer => playerHandContainer;
        public Button SubmitButton => submitButton;
        public Button DrawButton => drawButton;
        public Button SpecialSkillButton => specialSkillButton;
        public RectTransform PlayerFigure => playerFigure;
        public RectTransform EnemyFigure => enemyFigure;
        public BattleProductionManager ProductionManager => productionManager; // 연출 매니저 접근용
        // ------------------------

        [SerializeField] private Image flashOverlay; // 피격 시 화면 번쩍임용 이미지
        [SerializeField] private float hitShakeDuration = 0.4f;
        [SerializeField] private float hitShakeStrength = 15f;
        [SerializeField] private AudioClip bgmClip;
        [SerializeField] private AudioClip submitSfx;
        [SerializeField] private AudioClip drawSfx;
        [SerializeField] private AudioClip roundStartSfx;
        [SerializeField] private AudioClip warningSfx;
        [SerializeField] private AudioClip hitSfx;
        [SerializeField] private AudioClip playerAttackSfx;
        [SerializeField] private AudioClip enemyAttackSfx;
        [SerializeField] private AudioClip specialSkillSfx;

        [Header("Special Skill Pose Settings")]
        [SerializeField] private float specialSkillPoseDuration = 1.0f;

        public Action<List<CardInstance>> OnSubmitCards;
        public Action OnDrawCard;
        
        private List<CardView> selectedCards = new List<CardView>();
        private List<CardView> allPlayerCards = new List<CardView>();
        private List<CardView> allEnemyCards = new List<CardView>();
        private ActionSequencer sequencer = new ActionSequencer();
        private BattleModel battleModel;

        private void Awake()
        {
            InitializeUIState(); // [New] 시작 시 UI 숨김

            if (submitButton != null)
            {
                submitButton.onClick.AddListener(OnClickSubmit);
                submitButton.gameObject.SetActive(false);
            }

            if (drawButton != null)
            {
                drawButton.onClick.AddListener(() => {
                    if (battleModel != null && battleModel.player.isFeverModeActive.Value)
                    {
                        // 피버 종료 버튼으로 동작
                        OnClickEndFever();
                    }
                    else
                    {
                        if (drawSfx != null) Core.Audio.SoundManager.Instance.PlaySe(drawSfx);
                        else Core.Audio.SoundManager.Instance.PlaySe("UI_Draw_Click");

                        ReturnTrayCardsToHand(); // [Fix] 드로우 시 트레이의 카드들을 핸드로 복귀
                        OnDrawCard?.Invoke();
                    }
                });
            }

            // 상단 메뉴 도움말 버튼 (인덱스 2번) 연결
            var helpBtn = GetMenuButton(1);
            if (helpBtn != null && battleHelpWindow != null)
            {
                helpBtn.onClick.AddListener(() => battleHelpWindow.Open());
            }

            if (specialSkillButton != null)
            {
                specialSkillButton.onClick.AddListener(OnClickSpecialSkill);
                specialSkillButton.interactable = false;
            }

            if (clashResultText != null) clashResultText.gameObject.SetActive(false);
            if (roundBanner != null) roundBanner.SetActive(false);
            if (flashOverlay != null) flashOverlay.gameObject.SetActive(false);

            // rootCanvasGroup이 설정되지 않았다면 컴포넌트 시도
            if (rootCanvasGroup == null) rootCanvasGroup = GetComponent<CanvasGroup>();

            // 툴팁 시스템 초기화
            if (tooltipPrefab != null)
            {
                TooltipManager.Instance.Setup(tooltipPrefab, tooltipRoot != null ? tooltipRoot : this.transform);
            }
        }

        private void Start()
        {
            if (bgmClip != null)
            {
                Core.Audio.SoundManager.Instance.PlayBgm(bgmClip);
            }
        }

        /// <summary>
        /// 라운드가 전환될 때 (Round End -> Round Start) 실행되는 간략한 시각적 효과
        /// </summary>
        public void PlayRoundTransition(Action onComplete)
        {
            if (rootCanvasGroup == null)
            {
                onComplete?.Invoke();
                return;
            }

            // 간략하게 화면을 깜빡이는 연출 (나중에 특수 연출로 보강 가능)
            Sequence seq = DOTween.Sequence();
            seq.Append(rootCanvasGroup.DOFade(0.3f, 0.2f));
            seq.Append(rootCanvasGroup.DOFade(1.0f, 0.2f));
            seq.OnComplete(() => onComplete?.Invoke());
        }

        public void ShowRoundBanner(int round, Action onComplete)
        {
            if (roundBanner == null)
            {
                onComplete?.Invoke();
                return;
            }

            var text = roundBanner.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.text = $"ROUND {round}";

            roundBanner.SetActive(true);
            roundBanner.transform.localScale = Vector3.zero;
            
            if (roundStartSfx != null) Core.Audio.SoundManager.Instance.PlaySe(roundStartSfx);
            else Core.Audio.SoundManager.Instance.PlaySe("UI_Round_Start");

            Sequence seq = DOTween.Sequence();
            seq.Append(roundBanner.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack));
            seq.AppendInterval(0.6f);
            seq.Append(roundBanner.transform.DOScale(0f, 0.3f).SetEase(Ease.InBack));
            seq.OnComplete(() => {
                roundBanner.SetActive(false);
                onComplete?.Invoke();
            });
        }

        public void ShowWarningMessage(string message)
        {
            if (clashResultText == null) return;

            if (warningSfx != null) Core.Audio.SoundManager.Instance.PlaySe(warningSfx);
            else Core.Audio.SoundManager.Instance.PlaySe("UI_Warning");

            // 기존 텍스트를 재활용하거나 별도 배너를 사용할 수 있음
            // 여기서는 중앙의 clashResultText를 활용하여 경고 메시지 출력
            clashResultText.text = message;
            clashResultText.color = Color.red;
            clashResultText.gameObject.SetActive(true);
            clashResultText.transform.localScale = Vector3.zero;

            Sequence seq = DOTween.Sequence();
            seq.Append(clashResultText.transform.DOScale(1.2f, 0.3f).SetEase(Ease.OutBack));
            seq.AppendInterval(1.0f);
            seq.Append(clashResultText.transform.DOScale(0f, 0.2f).SetEase(Ease.InBack));
            seq.OnComplete(() => {
                clashResultText.gameObject.SetActive(false);
                clashResultText.color = Color.white; // 색상 복구
            });
        }

        public void Bind(BattleModel model)
        {
            this.battleModel = model;

            // 상태창 바인딩 (HP, 게이지, 이름 등은 BattleStatusView에서 처리)
            if (playerStatus != null) playerStatus.Setup(model.player);
            if (enemyStatus != null) enemyStatus.Setup(model.enemy);

            // [Fix] 캐릭터 스탠딩 피규어 초기 스프라이트 바인딩 및 HP 상태 구독
            SetupCharacterFigure(playerFigure, model.player);
            SetupCharacterFigure(enemyFigure, model.enemy);
            
            model.currentRound.Subscribe(round => roundText.text = $"ROUND {round}").AddTo(this);
            if (limitRoundText != null) limitRoundText.text = model.battleTable.limitRound.ToString();

            // 플레이어 게이지 및 피버 상태 구독
            if (specialSkillButton != null)
            {
                var trigger = specialSkillButton.gameObject.GetComponent<TooltipTrigger>();
                if (trigger == null) trigger = specialSkillButton.gameObject.AddComponent<TooltipTrigger>();
                trigger.SetData(model.player.tableData.specialSkillId, TooltipTrigger.TooltipType.Skill);

                var pSkillRow = TableManager.Instance.GetTable<SkillTable>().Get(model.player.tableData.specialSkillId);
                if (pSkillRow != null && !string.IsNullOrEmpty(pSkillRow.iconName))
                {
                    var iconImg = specialSkillButton.transform.Find("Img_Icon")?.GetComponent<Image>()
                                  ?? specialSkillButton.transform.Find("Icon")?.GetComponent<Image>() 
                                  ?? specialSkillButton.transform.Find("Image_Skill_Icon")?.GetComponent<Image>();
                    if (iconImg != null)
                    {
                        Core.Utils.ResourceUtil.LoadSpriteWithFallback($"Sprites/SkillUI/{pSkillRow.iconName}", iconImg, model.player.tableData.KeyName);
                    }
                }

                model.player.specialGauge
                    .CombineLatest(model.player.isFeverModeActive, model.enemy.isFeverModeActive, (gauge, pFever, eFever) => 
                        model.player.IsGaugeFull() && !model.player.isSpecialActive && !pFever && !eFever)
                    .DistinctUntilChanged()
                    .Subscribe(canUse => {
                        specialSkillButton.interactable = canUse;
                    }).AddTo(this);
            }

            // [Fever] 피버 상태에 따른 UI 및 연출 변경 구독
            model.player.isFeverModeActive
                .DistinctUntilChanged()
                .Subscribe(isActive => {
                    if (drawButton != null)
                    {
                        //TODO: 하드코딩 제거
                        drawButton.image.sprite = isActive ? Resources.Load<Sprite>("Sprites/PlayScene/Spr_Icon_Button_FeverEnd") : Resources.Load<Sprite>("Sprites/PlayScene/Spr_PlayScene_Icon_Button_Draw");
                        var text = drawButton.GetComponentInChildren<TextMeshProUGUI>();
                        if (text != null) text.text = isActive ? "FEVER END" : "DRAW CARD"; //TODO: 텍스트가 아닌 버튼의 아이콘을 교체해야할 가능성이 있습니다.
                    }

                    // [New] 피버 연출 트리거 (지속형)
                    if (productionManager != null)
                    {
                        if (isActive) productionManager.StartPersistentProduction(ProductionType.Fever);
                        else productionManager.StopPersistentProduction(ProductionType.Fever);
                    }
                }).AddTo(this);

            // [Fever] 피버 스택 표시
            Observable.CombineLatest(model.player.feverStack, model.player.isFeverModeActive, (stack, isActive) => (stack, isActive))
                .Subscribe(x => {
                    if (x.isActive && x.stack > 0 && clashResultText != null)
                    {
                        clashResultText.DOKill();
                        clashResultText.text = $"FEVER STACK: {x.stack}";
                        clashResultText.gameObject.SetActive(true);
                        clashResultText.transform.localScale = Vector3.one;

                        // 연출: 팝업 후 1.5초 뒤 페이드 아웃 (자동 제거)
                        clashResultText.transform.DOScale(0f, 0.4f).SetDelay(1.2f).SetEase(Ease.InBack).OnComplete(() => {
                            clashResultText.gameObject.SetActive(false);
                            clashResultText.transform.localScale = Vector3.one;
                        });
                    }
                    else if (!x.isActive && clashResultText != null)
                    {
                        // 피버 종료 시 스택 관련 텍스트가 떠있다면 즉시 제거
                        if (clashResultText.text.Contains("FEVER STACK"))
                        {
                            clashResultText.DOKill();
                            clashResultText.gameObject.SetActive(false);
                            clashResultText.transform.localScale = Vector3.one;
                        }
                    }
                }).AddTo(this);

            // 플레이어 핸드 바인딩
            BindHand(model.player.hand, playerHandContainer, allPlayerCards, playerCardPrefab, OnCardClicked, false);
            // 적 핸드 바인딩 (클릭 이벤트 없음, 컴팩트 모드)
            BindHand(model.enemy.hand, enemyHandContainer, allEnemyCards, enemyCardPrefab, null, true);
        }

        /// <summary>
        /// 전체 배틀 UI의 인터랙션 활성화 여부를 설정합니다.
        /// Command 페이즈가 아닐 때 유저의 오조작을 방지하기 위해 사용합니다.
        /// </summary>
        public void SetInteractionEnabled(bool enabled)
        {
            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.blocksRaycasts = enabled;
                // 인터랙션이 꺼질 때 호버 연출 등을 초기화하기 위해 사용 가능
                if (!enabled)
                {
                    foreach (var card in allPlayerCards) if (card != null) card.ResetVisualState();
                }
            }
        }

        public void RefreshHandVisuals()
        {
            foreach (var card in allPlayerCards)
            {
                if (card != null) card.RefreshBrokenState();
            }
            foreach (var card in allEnemyCards)
            {
                if (card != null) card.RefreshBrokenState();
            }
        }

        private void ReturnTrayCardsToHand()
        {
            if (selectedCards.Count == 0) return;

            var tempCards = selectedCards.ToList();
            foreach (var card in tempCards)
            {
                card.SetSelected(false);
                card.SetInteractionActive(true);
                playerTrayContainer.RemoveCard(card, false);
                playerHandContainer.AddCard(card);
            }
            selectedCards.Clear();
            UpdateSubmitButton();
        }

        private void OnClickEndFever()
        {
            var executor = Core.Flow.FlowController.Instance.CurrentExecutor as Core.Flow.BattleExecutor;
            if (executor != null)
            {
                executor.ProcessPlayerEndFever();
            }
        }

        private void OnClickSpecialSkill()
        {
            if (battleModel == null || !battleModel.player.IsGaugeFull()) return;

            if (specialSkillSfx != null) Core.Audio.SoundManager.Instance.PlaySe(specialSkillSfx);
            else Core.Audio.SoundManager.Instance.PlaySe("Battle_Special_Active");

            var executor = Core.Flow.FlowController.Instance.CurrentExecutor as Core.Flow.BattleExecutor;
            if (executor != null)
            {
                executor.ExecuteSpecialSkill(battleModel.player);
            }
            
            // 버튼 즉시 비활성화
            if (specialSkillButton != null) specialSkillButton.interactable = false;

            // 추가 연출 트리거 가능 (예: 캐릭터 컷신 등)
            Debug.Log("[Battle] Player Special Skill Activated!");
        }

        /// <summary>
        /// 배틀 결과를 화면에 표시합니다.
        /// </summary>
        public void ShowBattleResult(bool isWin, Action onComplete)
        {
            if (productionManager == null)
            {
                onComplete?.Invoke();
                return;
            }

            var type = isWin ? ProductionType.Victory : ProductionType.Defeat;
            // 결과 화면은 무한히 유지되어야 하므로 매우 긴 시간을 주거나 
            // 별도의 정지 로직이 필요할 수 있지만, 여기서는 UI 버튼 조작으로 씬이 넘어가므로 
            // 충분한 시간을 주거나 코루틴만 태웁니다.
            StartCoroutine(productionManager.PlayProduction(type, CharacterKeyName.None, 999f));
        }

        /// <summary>
        /// [New] 특수 스킬 사용 시 캐릭터를 잠시 특별 포즈 스프라이트로 변경합니다.
        /// </summary>
        public IEnumerator PlaySpecialPose(BattleCharacterModel owner)
        {
            var row = owner.tableData;
            if (string.IsNullOrEmpty(row.specialSkillSpriteName) || row.specialSkillSpriteName == "-1") yield break;

            var sprite = Resources.Load<Sprite>($"Sprites/Characters/Battle/{row.specialSkillSpriteName}");
            if (sprite == null) yield break;

            bool isPlayer = (owner == battleModel.player);
            var figureImg = isPlayer ? playerFigure.GetComponent<Image>() : enemyFigure.GetComponent<Image>();
            if (figureImg == null) yield break;

            // 1. 스페셜 포즈 적용
            figureImg.sprite = sprite;

            // 2. 대기
            yield return new WaitForSeconds(specialSkillPoseDuration);

            // 3. 원래 스프라이트로 복구 (HP 상태 반영)
            string defaultName = GetDefaultSpriteName(owner);
            var defaultSprite = Resources.Load<Sprite>($"Sprites/Characters/Battle/{defaultName}");
            if (defaultSprite != null)
            {
                figureImg.sprite = defaultSprite;
            }
        }

        private List<CardView> playerClashViews = new List<CardView>();
        private List<CardView> enemyClashViews = new List<CardView>();

        private void BindHand(ReactiveCollection<CardInstance> hand, HandContainer container, List<CardView> viewList, CardView prefab, Action<CardView> onClick, bool isCompact)
        {
            hand.ObserveAdd().Subscribe(addEvent => {
                var card = Instantiate(prefab);
                card.Setup(addEvent.Value, onClick, container); // container 전달
                //card.SetCompactMode(isCompact);
                viewList.Add(card);
                container.AddCard(card);
            }).AddTo(this);

            hand.ObserveRemove().Subscribe(removeEvent => {
                // 플레이어 카드의 경우 OnClickSubmit에서 이미 선점(Pre-capture)했을 수 있으므로 체크
                if (!isCompact && playerClashViews.Any(c => c.CardInstance == removeEvent.Value))
                {
                    var capturedCard = playerClashViews.FirstOrDefault(c => c.CardInstance == removeEvent.Value && viewList.Contains(c));
                    if (capturedCard != null) viewList.Remove(capturedCard);
                    return;
                }

                // 적 카드는 여전히 이 이벤트에서 캡처 (선택 개념이 없으므로)
                var target = viewList.FirstOrDefault(c => (isCompact || c.IsSelected) && c.CardInstance == removeEvent.Value)
                             ?? viewList.FirstOrDefault(c => c.CardInstance == removeEvent.Value);

                if (target != null)
                {
                    viewList.Remove(target);
                    container.RemoveCard(target);
                    
                    var button = target.GetComponent<Button>();
                    if (button != null) button.interactable = false;

                    if (isCompact) enemyClashViews.Add(target);
                    else playerClashViews.Add(target);
                }
            }).AddTo(this);

            hand.ObserveReset().Subscribe(_ => {
                container.Clear();
                viewList.Clear();
                if (container == playerHandContainer)
                {
                    selectedCards.Clear();
                    UpdateSubmitButton();
                }
            }).AddTo(this);
        }

        private void OnCardClicked(CardView card)
        {
            if (card.CardInstance.IsBroken)
            {
                // Broken 상태면 흔들림 연출 등으로 피드백
                card.transform.DOShakePosition(0.3f, 10f);
                if (warningSfx != null) Core.Audio.SoundManager.Instance.PlaySe(warningSfx);
                return;
            }

            if (card.IsSelected)
            {
                // 선택 해제: 트레이에서 핸드로 복귀
                card.SetSelected(false); // 여기서 selectedCase가 꺼짐
                card.SetInteractionActive(true); // 핸드로 돌아오면 다시 상호작용 활성화
                selectedCards.Remove(card);
                
                playerTrayContainer.RemoveCard(card, false); 
                playerHandContainer.AddCard(card); 
            }
            else
            {
                // 피버 모드일 때는 1장으로 제한
                int maxAllowed = (battleModel != null && battleModel.player.isFeverModeActive.Value) ? 1 : 5;

                // 동일 종류 체크 (다르면 기존 트레이 비우기)
                if (selectedCards.Count > 0 && selectedCards[0].SkillData.element != card.SkillData.element)
                {
                    var tempCards = selectedCards.ToList();
                    foreach (var c in tempCards)
                    {
                        c.SetSelected(false);
                        c.SetInteractionActive(true);
                        playerTrayContainer.RemoveCard(c, false);
                        playerHandContainer.AddCard(c);
                    }
                    selectedCards.Clear();
                }

                if (selectedCards.Count < maxAllowed)
                {
                    // 선택: 핸드에서 트레이로 이동
                    card.SetSelected(true); // 트레이에 있는 동안 유지
                    card.ResetVisualState(); // [Fix] 호버된 상태의 위치를 0으로 강제 리셋
                    card.SetInteractionActive(false); // 트레이에 있을 때는 호버 연출(DOTween Pro) 차단
                    selectedCards.Add(card);

                    playerHandContainer.RemoveCard(card); 
                    playerTrayContainer.AddCard(card); 
                }
            }
            UpdateSubmitButton();
        }

        private void UpdateSubmitButton()
        {
            if (submitButton != null)
            {
                submitButton.gameObject.SetActive(selectedCards.Count > 0);
            }
        }

        private void OnClickSubmit()
        {
            if (selectedCards.Count == 0) return;

            if (submitSfx != null) Core.Audio.SoundManager.Instance.PlaySe(submitSfx);
            else Core.Audio.SoundManager.Instance.PlaySe("UI_Submit_Click");

            // [New] UI 슬라이드 아웃 연출 시작
            SlideOutHands(uiAnimDuration, null);

            // [핵심] 트레이에 있는 카드들을 선점하여 격돌 준비
            playerClashViews.Clear();
            foreach (var card in selectedCards)
            {
                playerClashViews.Add(card);
                playerTrayContainer.RemoveCard(card, false); // 트레이에서 즉시 제거
                var button = card.GetComponent<Button>();
                if (button != null) button.interactable = false;
            }

            var dataList = selectedCards.Select(c => c.CardInstance).ToList();
            OnSubmitCards?.Invoke(dataList);
            
            selectedCards.Clear();
            UpdateSubmitButton();
        }

        private void SetupCharacterFigure(RectTransform figureRoot, BattleCharacterModel model)
        {
            if (figureRoot == null || model == null) return;
            var img = figureRoot.GetComponent<Image>();
            if (img == null) return;

            UpdateFigureSprite(img, model);

            model.hpState
                .DistinctUntilChanged()
                .Subscribe(_ => UpdateFigureSprite(img, model))
                .AddTo(this);
        }

        private void UpdateFigureSprite(Image img, BattleCharacterModel model)
        {
            if (img == null || model == null || model.tableData == null) return;
            string defaultSpriteName = GetDefaultSpriteName(model);
            var keyName = model.tableData.KeyName;
            Core.Utils.ResourceUtil.LoadSpriteWithFallback($"Sprites/Characters/Battle/{defaultSpriteName}", img, keyName);
        }

        private string GetDefaultSpriteName(BattleCharacterModel model)
        {
            if (model == null || model.tableData == null) return "";
            int index = (int)model.hpState.Value;
            var names = model.tableData.DefaultSpriteNames;
            if (names == null || names.Length == 0) return "";
            if (index < 0 || index >= names.Length) return names[0];
            return names[index];
        }

        public void PlayDamageAnimation(int winnerIndex, BattleModel model, Action onApplyResults, Action onComplete, float customDamage = -1f, ProductionCommand successCommand = null)
        {
            // 1. 카드 충돌 연출 결정
            // [Fix] 한쪽만 카드를 제출하더라도 중앙에서 가시화되도록 조건 완화
            bool canClash = playerClashViews.Count > 0 || enemyClashViews.Count > 0;

            if (canClash)
            {
                // 승자 인덱스를 전달하여 무승부 시 체인 텍스트 생략 가능하게 함
                PlayCardClashEffect(model, winnerIndex, customDamage);
            }
            else
            {
                // 격돌이 스킵되더라도 리스트에 카드가 있다면 (예: 한쪽만 드로우 등)
                if (playerClashViews.Count > 0 || enemyClashViews.Count > 0)
                {
                    sequencer.AddCommand(new ActionCommand(() => {
                        foreach (var c in playerClashViews) if(c != null) c.transform.DOScale(0, 0.2f);
                        foreach (var c in enemyClashViews) if(c != null) c.transform.DOScale(0, 0.2f);
                    }));
                    sequencer.AddCommand(new DelayCommand(0.2f));
                }
            }

            // 2. 승자 결과에 따른 연출 분기
            if (winnerIndex == 0) // 무승부
            {
                if (!canClash) sequencer.AddCommand(new DelayCommand(0.4f));
                
                // 결과 적용 (게이지 등)
                sequencer.AddCommand(new ActionCommand(() => onApplyResults?.Invoke()));
            }
            else
            {
                bool isPlayerWinner = (winnerIndex == 1);
                var winnerModel = isPlayerWinner ? model.player : model.enemy;
                var loserModel = isPlayerWinner ? model.enemy : model.player;
                
                var winnerFig = isPlayerWinner ? playerFigure : enemyFigure;
                var loserFig = isPlayerWinner ? enemyFigure : playerFigure;

                var winnerImg = winnerFig.GetComponent<Image>();
                var loserImg = loserFig.GetComponent<Image>();

                // [Fix] 사용된 Element와 무관하게 기본 액션/피격 스프라이트 사용
                // 피격 스프라이트는 스페셜 스킬 활성화 여부에 따라 인덱스 결정 (0: 평상시, 1: 회복)
                string attackSpriteName = GetSafeSpriteName(winnerModel.tableData.AttackSpriteNames, 0);
                string hitSpriteName = GetSafeSpriteName(loserModel.tableData.HitSpriteNames, loserModel.GetHitSpriteIndex());
                string winnerDefaultName = GetDefaultSpriteName(winnerModel);
                string loserDefaultName = GetDefaultSpriteName(loserModel);

                var attackSprite = Core.Utils.ResourceUtil.LoadSpriteWithFallback($"Sprites/Characters/Battle/{attackSpriteName}", null, winnerModel.tableData.KeyName);
                var hitSprite = Core.Utils.ResourceUtil.LoadSpriteWithFallback($"Sprites/Characters/Battle/{hitSpriteName}", null, loserModel.tableData.KeyName);
                var winnerDefault = Core.Utils.ResourceUtil.LoadSpriteWithFallback($"Sprites/Characters/Battle/{winnerDefaultName}", null, winnerModel.tableData.KeyName);
                var loserDefault = Core.Utils.ResourceUtil.LoadSpriteWithFallback($"Sprites/Characters/Battle/{loserDefaultName}", null, loserModel.tableData.KeyName);

                // 공격 시작: 스프라이트 교체 및 결과(HP 감면) 적용
                var attackSfx = isPlayerWinner ? playerAttackSfx : enemyAttackSfx;
                if (attackSfx != null) sequencer.AddCommand(new SoundCommand(attackSfx));

                // [New] 스페셜 스킬 성공 연출이 있다면 시퀀서에 추가 (공격/피격 애니메이션 전)
                // LoseHeal 등의 경우 피격 전에 성공 연출이 나와야 자연스러움
                if (successCommand != null)
                {
                    sequencer.AddCommand(successCommand);
                }

                sequencer.AddCommand(new SpriteCommand(winnerImg, attackSprite));
                sequencer.AddCommand(new ActionCommand(() => onApplyResults?.Invoke()));

                sequencer.AddCommand(new MoveCommand(winnerFig, winnerFig.anchoredPosition + (isPlayerWinner ? Vector2.right : Vector2.left) * 50f, 0.1f));
                
                // 피격 연출 (캐릭터 흔들기 + 화면 전체 흔들기 + 화면 플래시)
                if (hitSfx != null) sequencer.AddCommand(new SoundCommand(hitSfx));
                else sequencer.AddCommand(new SoundCommand("Battle_Hit"));

                sequencer.AddCommand(new SpriteCommand(loserImg, hitSprite));
                sequencer.AddCommand(new ShakeCommand(loserFig, hitShakeDuration, hitShakeStrength));
                sequencer.AddCommand(new ShakeCommand(this.GetComponent<RectTransform>(), hitShakeDuration, hitShakeStrength * 0.5f));
                
                if (flashOverlay != null)
                {
                    sequencer.AddCommand(new FlashCommand(flashOverlay, new Color(1, 0, 0, 0.3f), 0.15f));
                }

                sequencer.AddCommand(new DelayCommand(0.5f));
                
                // 원상복귀
                sequencer.AddCommand(new ActionCommand(() => {
                    UpdateFigureSprite(winnerImg, winnerModel);
                    UpdateFigureSprite(loserImg, loserModel);
                }));
                sequencer.AddCommand(new MoveCommand(winnerFig, winnerFig.anchoredPosition, 0.1f));
            }

            // 3. 연출 실행 및 최종 정리
            sequencer.Play(() => {
                CleanupClashCards(); 
                onComplete?.Invoke();
            });
        }

        private string GetSafeSpriteName(string[] names, int index)
        {
            if (names == null || names.Length == 0) return "";
            if (index < 0 || index >= names.Length) return names[0];
            return names[index];
        }

        private void CleanupClashCards()
        {
            foreach (var card in playerClashViews) if (card != null) Destroy(card.gameObject);
            foreach (var card in enemyClashViews) if (card != null) Destroy(card.gameObject);
            playerClashViews.Clear();
            enemyClashViews.Clear();
        }

        // --- UI Animations ---

        public void InitializeUIState()
        {
            if (inGameUIGroup != null)
            {
                inGameUIGroup.alpha = 0;
                inGameUIGroup.blocksRaycasts = false;
            }
        }

        public void FadeInUI(float duration, Action onComplete)
        {
            if (inGameUIGroup == null) { onComplete?.Invoke(); return; }
            inGameUIGroup.DOFade(1f, duration).OnComplete(() => {
                inGameUIGroup.blocksRaycasts = true;
                onComplete?.Invoke();
            });
        }

        public void SlideOutHands(float duration, Action onComplete)
        {
            Sequence seq = DOTween.Sequence();
            if (playerHandArea != null) seq.Join(playerHandArea.DOAnchorPosY(-slideOffset, duration).SetEase(Ease.InCubic));
            if (enemyHandArea != null) seq.Join(enemyHandArea.DOAnchorPosY(slideOffset, duration).SetEase(Ease.InCubic));
            seq.OnComplete(() => onComplete?.Invoke());
        }

        public void SlideInHands(float duration, Action onComplete)
        {
            Sequence seq = DOTween.Sequence();
            if (playerHandArea != null) seq.Join(playerHandArea.DOAnchorPosY(0, duration).SetEase(Ease.OutCubic));
            if (enemyHandArea != null) seq.Join(enemyHandArea.DOAnchorPosY(0, duration).SetEase(Ease.OutCubic));
            seq.OnComplete(() => onComplete?.Invoke());
        }

        /// <summary>
        /// 상단 메뉴 영역에서 특정 인덱스의 버튼을 찾아 반환합니다.
        /// </summary>
        private Button GetMenuButton(int index)
        {
            // 상단 메뉴는 보통 Canvas의 하위 패널에 위치함. 
            // 0: Menu, 1: ?, 2: Help (기획서 9, 10P 참조)
            var menuRoot = transform.Find("TopMenu"); // 실제 이름 확인 필요
            if (menuRoot == null) return null;

            var buttons = menuRoot.GetComponentsInChildren<Button>(true);
            if (index >= 0 && index < buttons.Length) return buttons[index];
            return null;
        }

        private void PlayCardClashEffect(BattleModel model, int winnerIndex, float customDamage = -1f)
        {
            float moveDur = playerClashLayout != null ? playerClashLayout.MoveDuration : 0.4f;

            // 1. 모든 카드 활성화 및 고오더 루트 이동
            // 플레이어 카드
            foreach (var card in playerClashViews)
            {
                // [New] 격돌 카드들을 고오더 루트로 이동하여 하이라이트 효과 (딤드 위로)
                if (clashHighOrderRoot != null) card.transform.SetParent(clashHighOrderRoot, true);
                else card.transform.SetParent(this.transform, true);

                card.gameObject.SetActive(true);
                card.ResetVisualState();
                card.SetSelected(false);
            }
            if (playerClashLayout != null) playerClashLayout.ArrangeCards(playerClashViews, sequencer);

            // 적 카드
            foreach (var card in enemyClashViews)
            {
                if (clashHighOrderRoot != null) card.transform.SetParent(clashHighOrderRoot, true);
                else card.transform.SetParent(this.transform, true);

                card.gameObject.SetActive(true);
                card.ResetVisualState();
            }
            if (enemyClashLayout != null) enemyClashLayout.ArrangeCards(enemyClashViews, sequencer);
            
            sequencer.AddCommand(new DelayCommand(moveDur + 0.1f));

            // 2. 결과 연출 (SO 데이터 기반)
            if (productionManager != null)
            {
                float totalDmg = (customDamage >= 0) ? customDamage : 0;
                if (customDamage < 0)
                {
                    bool isRoundEndGame = model.currentRound.Value > model.battleTable.limitRound;
                    totalDmg = BattleLogic.CalculateDamage(model, winnerIndex, isRoundEndGame);
                }

                bool isPlayerWinner = (winnerIndex == 1);
                int chainCount = isPlayerWinner ? model.playerSelectedCards.Count : model.enemySelectedCards.Count;

                var data = new { winnerIndex, totalDmg, chainCount };
                
                // 격돌 결과에 따른 타입 분기
                ProductionType resultType = ProductionType.ClashDraw;
                if (winnerIndex == 1) resultType = ProductionType.ClashWin;
                else if (winnerIndex == 2) resultType = ProductionType.ClashLose;

                // 하이라이트 설정은 이제 SO에서 수행하므로 마스크를 넘기지 않음
                sequencer.AddCommand(new ProductionCommand(productionManager, resultType, CharacterKeyName.None, null, data));
            }
            else
            {
                sequencer.AddCommand(new ActionCommand(() => {
                    if (clashResultText != null)
                    {
                        if (winnerIndex == 0)
                        {
                            clashResultText.text = "DRAW";
                        }
                        else
                        {
                            bool isPlayerWinner = (winnerIndex == 1);
                            var winnerCards = isPlayerWinner ? model.playerSelectedCards : model.enemySelectedCards;
                            
                            float totalDmg = (customDamage >= 0) ? customDamage : 0;
                            if (customDamage < 0)
                            {
                                bool isRoundEndGame = model.currentRound.Value > model.battleTable.limitRound;
                                totalDmg = BattleLogic.CalculateDamage(model, winnerIndex, isRoundEndGame);
                            }

                            if (winnerCards.Count == 0 && totalDmg > 0)
                            {
                                clashResultText.text = $"FEVER BONUS\nTOTAL DMG: {totalDmg}";
                            }
                            else
                            {
                                clashResultText.text = $"{winnerCards.Count} CHAIN\nTOTAL DMG: {totalDmg}";
                            }
                        }
                        
                        clashResultText.gameObject.SetActive(true);
                        clashResultText.transform.localScale = Vector3.zero;
                        clashResultText.transform.DOScale(1.2f, 0.2f).SetEase(Ease.OutBack);
                    }
                }));
                sequencer.AddCommand(new DelayCommand(0.8f));
            }

            // 3. 격돌 효과 (흔들기)
            float shakeDur = productionData != null ? productionData.clashShakeDuration : 0.4f;
            sequencer.AddCommand(new ActionCommand(() => {
                foreach (var c in playerClashViews) c.transform.DOShakePosition(shakeDur, 30f);
                foreach (var c in enemyClashViews) c.transform.DOShakePosition(shakeDur, 30f);
            }));
            sequencer.AddCommand(new DelayCommand(shakeDur + 0.1f));
            
            // 4. 퇴장 연출
            float exitDur = productionData != null ? productionData.clashExitDuration : 0.2f;
            sequencer.AddCommand(new ActionCommand(() => {
                foreach (var c in playerClashViews) if(c != null) c.transform.DOScale(0, exitDur);
                foreach (var c in enemyClashViews) if(c != null) c.transform.DOScale(0, exitDur);
                if (clashResultText != null) clashResultText.gameObject.SetActive(false);
            }));
            
            sequencer.AddCommand(new DelayCommand(exitDur));
        }
    }
}