using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UniRx;
using Core.Battle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core.Table;
using DG.Tweening;

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

        [Header("Environment UI")]
        [Tooltip("씬에 직접 배치한 환경 변화 요소 아이콘 Image 컴포넌트를 매핑합니다.")]
        [SerializeField] private Image environmentIcon;
        [Tooltip("아이리스의 희망 아이콘 스프라이트 (선택 사항, 미할당 시 기존 스프라이트 유지)")]
        [SerializeField] private Sprite irysHopeSprite;
        [Tooltip("무메이의 문명 아이콘 스프라이트 (선택 사항, 미할당 시 기존 스프라이트 유지)")]
        [SerializeField] private Sprite mumeiCivilizationSprite;

        // --- Tutorial Support ---
        public BattleStatusView PlayerStatus => playerStatus;
        public BattleStatusView EnemyStatus => enemyStatus;
        public HandContainer PlayerHandContainer => playerHandContainer;
        public HandContainer EnemyHandContainer => enemyHandContainer;
        public HandContainer PlayerTrayContainer => playerTrayContainer;
        public ClashLayoutGroup PlayerClashLayout => playerClashLayout;
        public ClashLayoutGroup EnemyClashLayout => enemyClashLayout;
        public Button SubmitButton => submitButton;
        public Button DrawButton => drawButton;
        public Button SpecialSkillButton => specialSkillButton;
        public TextMeshProUGUI RoundText => roundText;
        public RectTransform PlayerFigure => playerFigure;
        public RectTransform EnemyFigure => enemyFigure;
        public BattleProductionManager ProductionManager => productionManager; // 연출 매니저 접근용
        // ------------------------

        [SerializeField] private Image flashOverlay; // 피격 시 화면 번쩍임용 이미지
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
        
        private ActionSequencer sequencer = new ActionSequencer();
        private BattleModel battleModel;
        private BattleCombatPresenter combatPresenter;
        private BattleHandPresenter handPresenter;

        public BattleCombatPresenter CombatPresenter => combatPresenter;
        public BattleHandPresenter HandPresenter => handPresenter;

        private void Awake()
        {
            InitializeUIState(); // [New] 시작 시 UI 숨김

            combatPresenter = new BattleCombatPresenter(new BattleCombatContext
            {
                playerFigure = playerFigure,
                enemyFigure = enemyFigure,
                playerClashLayout = playerClashLayout,
                enemyClashLayout = enemyClashLayout,
                cardClashPoint = cardClashPoint,
                clashHighOrderRoot = clashHighOrderRoot,
                clashResultText = clashResultText,
                flashOverlay = flashOverlay,
                productionManager = productionManager,
                productionData = productionData,
                rootCanvasGroup = rootCanvasGroup,
                viewTransform = this.transform,
                playerAttackSfx = playerAttackSfx,
                enemyAttackSfx = enemyAttackSfx,
                hitSfx = hitSfx,
                updateFigureSprite = UpdateFigureSprite,
                getDefaultSpriteName = GetDefaultSpriteName
            });

            handPresenter = new BattleHandPresenter(new BattleHandContext
            {
                viewOwner = this,
                playerCardPrefab = playerCardPrefab,
                enemyCardPrefab = enemyCardPrefab,
                playerHandContainer = playerHandContainer,
                playerTrayContainer = playerTrayContainer,
                enemyHandContainer = enemyHandContainer,
                submitButton = submitButton,
                submitSfx = submitSfx,
                warningSfx = warningSfx,
                getBattleModel = () => battleModel,
                getCombatPresenter = () => combatPresenter,
                onSubmitCards = (cards) => OnSubmitCards?.Invoke(cards),
                onSlideOutHands = () => SlideOutHands(uiAnimDuration, null)
            });

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
            // BGM 재생은 BattleTable.xml의 설정을 기반으로 BattleExecutor가 총괄합니다.
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

        /// <summary>
        /// [New 261013] 스테이지 시작 시 부여된 환경 변화 요소 배너 연출
        /// </summary>
        public void ShowEnvironmentBanner(EnvironmentType envType, Action onComplete = null)
        {
            if (envType == EnvironmentType.None || clashResultText == null)
            {
                onComplete?.Invoke();
                return;
            }

            string title = "";
            string desc = "";
            Color titleColor = Color.white;

            switch (envType)
            {
                case EnvironmentType.IrysHope:
                    title = "🕊️ [아이리스의 희망]";
                    desc = "패가 항상 가득 차며, 쓴 만큼 보충됩니다!";
                    titleColor = new Color(0.6f, 0.8f, 1f);
                    break;
                case EnvironmentType.MumeiCivilization:
                    title = "🦉 [무메이의 문명]";
                    desc = "스킬 게이지 충전 속도가 2배로 증가합니다!";
                    titleColor = new Color(1f, 0.85f, 0.4f);
                    break;
            }

            clashResultText.text = $"<size=120%><color=#{ColorUtility.ToHtmlStringRGB(titleColor)}>{title}</color></size>\n<size=70%>{desc}</size>";
            clashResultText.gameObject.SetActive(true);
            clashResultText.transform.localScale = Vector3.zero;

            Core.Audio.SoundManager.Instance?.PlaySe("UI_Round_Start");

            Sequence seq = DOTween.Sequence();
            seq.Append(clashResultText.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack));
            seq.AppendInterval(1.2f);
            seq.Append(clashResultText.transform.DOScale(0f, 0.3f).SetEase(Ease.InBack));
            seq.OnComplete(() => {
                clashResultText.gameObject.SetActive(false);
                onComplete?.Invoke();
            });
        }

        /// <summary>
        /// [New 261013] 씬에 배치된 환경 아이콘을 활성화하고 TooltipTrigger를 자동 구성
        /// </summary>
        private void SetupEnvironmentIcon(EnvironmentType envType)
        {
            if (environmentIcon == null) return;

            if (envType == EnvironmentType.None)
            {
                environmentIcon.gameObject.SetActive(false);
                return;
            }

            environmentIcon.gameObject.SetActive(true);

            // 스프라이트 교체 (할당된 경우)
            if (envType == EnvironmentType.IrysHope && irysHopeSprite != null)
            {
                environmentIcon.sprite = irysHopeSprite;
            }
            else if (envType == EnvironmentType.MumeiCivilization && mumeiCivilizationSprite != null)
            {
                environmentIcon.sprite = mumeiCivilizationSprite;
            }

            // 툴팁 트리거 연결
            var trigger = environmentIcon.GetComponent<TooltipTrigger>();
            if (trigger == null) trigger = environmentIcon.gameObject.AddComponent<TooltipTrigger>();
            
            trigger.SetData((int)envType, TooltipTrigger.TooltipType.Environment);
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
            
            model.currentRound.Subscribe(round => {
                if (roundText != null) roundText.text = $"ROUND {round}";
            }).AddTo(this);
            if (limitRoundText != null) limitRoundText.text = model.battleTable.limitRound.ToString();

            // [New 261013] 환경 변화 요소 아이콘 및 툴팁 바인딩
            model.currentEnvironment.Subscribe(env => {
                SetupEnvironmentIcon(env);
                if (env == EnvironmentType.IrysHope && drawButton != null)
                {
                    drawButton.interactable = false;
                }
            }).AddTo(this);

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

                Observable.CombineLatest(
                    model.player.specialGauge,
                    model.player.isFeverModeActive,
                    model.enemy.isFeverModeActive,
                    model.player.isSpecialSkillBlocked,
                    (gauge, pFever, eFever, blocked) => 
                        model.player.IsGaugeFull() && !model.player.isSpecialActive && !pFever && !eFever && !blocked)
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
                        if (!string.IsNullOrEmpty(clashResultText.text) && clashResultText.text.Contains("FEVER STACK"))
                        {
                            clashResultText.DOKill();
                            clashResultText.gameObject.SetActive(false);
                            clashResultText.transform.localScale = Vector3.one;
                        }
                    }
                }).AddTo(this);

            // [New] 핸드 바인딩 (플레이어 & 적)
            handPresenter.Bind(model);
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
                if (!enabled && handPresenter != null)
                {
                    handPresenter.ResetPlayerVisualStates();
                }
            }
        }

        public void RefreshHandVisuals()
        {
            handPresenter?.RefreshVisuals();
        }

        private void ReturnTrayCardsToHand()
        {
            handPresenter?.ReturnTrayCardsToHand();
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
            Tutorial.TutorialManager.Instance?.NotifySpecialSkillClicked();
        }

        public void ShowBattleResult(bool isWin, Action onComplete)
        {
            if (clashResultText == null)
            {
                onComplete?.Invoke();
                return;
            }

            clashResultText.text = isWin ? "<color=#00FF00>VICTORY</color>" : "<color=#FF0000>DEFEAT</color>";
            clashResultText.gameObject.SetActive(true);
            clashResultText.transform.localScale = Vector3.zero;

            Sequence seq = DOTween.Sequence();
            seq.Append(clashResultText.transform.DOScale(1.5f, 0.5f).SetEase(Ease.OutBack));
            seq.AppendInterval(2.0f);
            seq.OnComplete(() => {
                onComplete?.Invoke();
            });
        }

        /// <summary>
        /// [New] 스페셜 스킬 발동 시 캐릭터가 취하는 고유 포즈 연출 (지정된 시간 대기 후 복귀)
        /// </summary>
        public IEnumerator PlaySpecialPose(BattleCharacterModel owner)
        {
            if (owner == null) yield break;

            var figureRoot = (owner == battleModel.player) ? playerFigure : enemyFigure;
            if (figureRoot == null) yield break;

            var figureImg = figureRoot.GetComponent<Image>();
            if (figureImg == null) yield break;

            // 1. 특수 스킬 포즈 스프라이트 로드 및 적용
            string poseName = owner.tableData.specialSkillSpriteName;
            if (!string.IsNullOrEmpty(poseName))
            {
                var poseSprite = Resources.Load<Sprite>($"Sprites/Characters/Battle/{poseName}");
                if (poseSprite != null)
                {
                    figureImg.sprite = poseSprite;
                }
            }

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

        public void OnClickSubmit() => handPresenter?.OnClickSubmit();

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
            if (img == null || model == null) return;
            string spriteName = GetDefaultSpriteName(model);
            var sprite = Resources.Load<Sprite>($"Sprites/Characters/Battle/{spriteName}");
            if (sprite != null) img.sprite = sprite;
        }

        private string GetDefaultSpriteName(BattleCharacterModel model)
        {
            var defaultSprites = model.tableData.DefaultSpriteNames;
            if (defaultSprites == null || defaultSprites.Length == 0) return string.Empty;
            int hpIdx = (int)model.hpState.Value;
            if (hpIdx < 0 || hpIdx >= defaultSprites.Length) hpIdx = 0;
            return defaultSprites[hpIdx];
        }

        public void PlayDamageAnimation(int winnerIndex, BattleModel model, Action onApplyResults, Action onComplete, float customDamage = -1f, ProductionCommand successCommand = null, Action onClashEnd = null)
        {
            combatPresenter?.PlayDamageAnimation(sequencer, winnerIndex, model, onApplyResults, onComplete, customDamage, successCommand, onClashEnd);
        }

        public void CleanupClashCards()
        {
            combatPresenter?.CleanupClashCards();
        }

        public IEnumerator PlayDiceRollResolveRoutine(BattleCharacterModel user, BattleCharacterModel opponent, int diceEye, float effectValue, Action onDone)
        {
            if (combatPresenter == null)
            {
                onDone?.Invoke();
                yield break;
            }
            yield return combatPresenter.PlayDiceRollResolveRoutine(battleModel, user, opponent, diceEye, effectValue, onDone, playerStatus, enemyStatus);
        }

        public void InitializeUIState()
        {
            if (inGameUIGroup != null)
            {
                inGameUIGroup.alpha = 0f;
                inGameUIGroup.blocksRaycasts = false;
            }
        }

        public void FadeInUI(float duration, Action onComplete)
        {
            if (inGameUIGroup != null)
            {
                inGameUIGroup.blocksRaycasts = true;
                inGameUIGroup.DOFade(1f, duration).OnComplete(() => onComplete?.Invoke());
            }
            else onComplete?.Invoke();
        }

        public void SlideOutHands(float duration, Action onComplete)
        {
            // UI 슬라이드 아웃 연출
            if (playerHandArea != null) playerHandArea.DOAnchorPosY(-slideOffset, duration).SetEase(Ease.InCubic);
            if (enemyHandArea != null) enemyHandArea.DOAnchorPosY(slideOffset, duration).SetEase(Ease.InCubic);
            DOVirtual.DelayedCall(duration, () => onComplete?.Invoke());
        }

        public void SlideInHands(float duration, Action onComplete)
        {
            // UI 슬라이드 인 연출
            if (playerHandArea != null) playerHandArea.DOAnchorPosY(0f, duration).SetEase(Ease.OutCubic);
            if (enemyHandArea != null) enemyHandArea.DOAnchorPosY(0f, duration).SetEase(Ease.OutCubic);
            DOVirtual.DelayedCall(duration, () => onComplete?.Invoke());
        }

        private Button GetMenuButton(int index)
        {
            // 상단 메뉴 버튼 접근 헬퍼 (0: Setting, 1: Help, 2: Close 등)
            var menuObj = GameObject.Find("MenuContainer") ?? GameObject.Find("TopMenu");
            if (menuObj != null)
            {
                var buttons = menuObj.GetComponentsInChildren<Button>();
                if (index >= 0 && index < buttons.Length) return buttons[index];
            }
            return null;
        }
    }
}