using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core.Battle;
using Core.Table;
using DG.Tweening;

namespace Core.View
{
    /// <summary>
    /// BattleCombatPresenter 생성에 필요한 View 의존성 및 콜백 묶음
    /// </summary>
    public class BattleCombatContext
    {
        public RectTransform playerFigure;
        public RectTransform enemyFigure;
        public ClashLayoutGroup playerClashLayout;
        public ClashLayoutGroup enemyClashLayout;
        public RectTransform cardClashPoint;
        public RectTransform clashHighOrderRoot;
        public TextMeshProUGUI clashResultText;
        public Image flashOverlay;
        public BattleProductionManager productionManager;
        public BattleProductionData productionData;
        public CanvasGroup rootCanvasGroup;
        public Transform viewTransform;
        public AudioClip playerAttackSfx;
        public AudioClip enemyAttackSfx;
        public AudioClip hitSfx;
        public Action<Image, BattleCharacterModel> updateFigureSprite;
        public Func<BattleCharacterModel, string> getDefaultSpriteName;
    }

    /// <summary>
    /// 가위바위보 카드 격돌(Clash) 및 캐릭터 공격/피격 전투 연출을 전담하는 프리젠터
    /// </summary>
    public class BattleCombatPresenter
    {
        private readonly BattleCombatContext ctx;

        // Hit shake 상수
        private const float HitShakeDuration = 0.4f;
        private const float HitImpactDecay = 8f;
        private static readonly Vector2 HitFigureShakeStrength = new Vector2(64f, 28f);
        private const float HitShakeCycles = 5f;

        public List<CardView> PlayerClashViews { get; } = new List<CardView>();
        public List<CardView> EnemyClashViews { get; } = new List<CardView>();

        public BattleCombatPresenter(BattleCombatContext context)
        {
            this.ctx = context ?? throw new ArgumentNullException(nameof(context));
        }

        public void AddPlayerClashCard(CardView card)
        {
            if (card != null && !PlayerClashViews.Contains(card))
            {
                PlayerClashViews.Add(card);
            }
        }

        public void AddEnemyClashCard(CardView card)
        {
            if (card != null && !EnemyClashViews.Contains(card))
            {
                EnemyClashViews.Add(card);
            }
        }

        public void ClearPlayerClashCards()
        {
            PlayerClashViews.Clear();
        }

        public bool HasCapturedPlayerCard(CardInstance instance)
        {
            return PlayerClashViews.Any(c => c.CardInstance == instance);
        }

        public CardView GetCapturedPlayerCard(CardInstance instance, List<CardView> viewList)
        {
            return PlayerClashViews.FirstOrDefault(c => c.CardInstance == instance && viewList.Contains(c));
        }

        public void PlayDamageAnimation(
            ActionSequencer sequencer,
            int winnerIndex, 
            BattleModel model, 
            Action onApplyResults, 
            Action onComplete, 
            float customDamage = -1f, 
            ProductionCommand successCommand = null, 
            Action onClashEnd = null)
        {
            if (sequencer == null) return;

            // 1. 카드 충돌 연출 결정
            bool canClash = PlayerClashViews.Count > 0 || EnemyClashViews.Count > 0;

            if (canClash)
            {
                PlayCardClashEffect(sequencer, model, winnerIndex, customDamage);
            }
            else
            {
                if (PlayerClashViews.Count > 0 || EnemyClashViews.Count > 0)
                {
                    sequencer.AddCommand(new ActionCommand(() => {
                        foreach (var c in PlayerClashViews) if (c != null) c.transform.DOScale(0, 0.2f);
                        foreach (var c in EnemyClashViews) if (c != null) c.transform.DOScale(0, 0.2f);
                    }));
                    sequencer.AddCommand(new DelayCommand(0.2f));
                }
            }

            // [체인 타이밍 2] 가위바위보 승패판정 연출 완료 후 / 공격-피격 애니메이션 전 체인 효과 발동
            if (onClashEnd != null)
            {
                sequencer.AddCommand(new ActionCommand(() => onClashEnd.Invoke()));
                sequencer.AddCommand(new DelayCommand(0.25f)); // 게이지 증가/체인 연출 시각 인지 버퍼
            }

            // 2. 승자 결과에 따른 연출 분기
            if (winnerIndex == 0) // 무승부
            {
                if (!canClash) sequencer.AddCommand(new DelayCommand(0.4f));
                sequencer.AddCommand(new ActionCommand(() => onApplyResults?.Invoke()));
            }
            else
            {
                bool isPlayerWinner = (winnerIndex == 1);
                var winnerModel = isPlayerWinner ? model.player : model.enemy;
                var loserModel = isPlayerWinner ? model.enemy : model.player;
                
                var winnerFig = isPlayerWinner ? ctx.playerFigure : ctx.enemyFigure;
                var loserFig = isPlayerWinner ? ctx.enemyFigure : ctx.playerFigure;

                var winnerImg = winnerFig != null ? winnerFig.GetComponent<Image>() : null;
                var loserImg = loserFig != null ? loserFig.GetComponent<Image>() : null;

                string attackSpriteName = GetSafeSpriteName(winnerModel.tableData.AttackSpriteNames, 0);
                string hitSpriteName = GetSafeSpriteName(loserModel.tableData.HitSpriteNames, loserModel.GetHitSpriteIndex());
                string winnerDefaultName = ctx.getDefaultSpriteName?.Invoke(winnerModel) ?? "";
                string loserDefaultName = ctx.getDefaultSpriteName?.Invoke(loserModel) ?? "";

                var attackSprite = Core.Utils.ResourceUtil.LoadSpriteWithFallback($"Sprites/Characters/Battle/{attackSpriteName}", null, winnerModel.tableData.KeyName);
                var hitSprite = Core.Utils.ResourceUtil.LoadSpriteWithFallback($"Sprites/Characters/Battle/{hitSpriteName}", null, loserModel.tableData.KeyName);
                var winnerDefault = Core.Utils.ResourceUtil.LoadSpriteWithFallback($"Sprites/Characters/Battle/{winnerDefaultName}", null, winnerModel.tableData.KeyName);
                var loserDefault = Core.Utils.ResourceUtil.LoadSpriteWithFallback($"Sprites/Characters/Battle/{loserDefaultName}", null, loserModel.tableData.KeyName);

                // 공격 시작: 사운드 및 연출
                var attackSfx = isPlayerWinner ? ctx.playerAttackSfx : ctx.enemyAttackSfx;
                if (attackSfx != null) sequencer.AddCommand(new SoundCommand(attackSfx));

                if (successCommand != null)
                {
                    sequencer.AddCommand(successCommand);
                }

                // 후부키 주사위 체인 사전 연출
                bool isDiceRollChain = false;
                var chainRow = TableManager.Instance.GetTable<ChainTable>()?.Get(winnerModel.tableData.chainId);
                if (chainRow != null && chainRow.action.Equals("DiceRoll", StringComparison.OrdinalIgnoreCase))
                {
                    var winnerCards = isPlayerWinner ? model.playerSelectedCards : model.enemySelectedCards;
                    if (winnerCards != null && winnerCards.Count >= 2) isDiceRollChain = true;
                }

                if (isDiceRollChain || winnerModel.lastDiceRoll.Value > 0)
                {
                    AddDiceRollPreProduction(sequencer, winnerModel);
                }

                if (winnerImg != null) sequencer.AddCommand(new SpriteCommand(winnerImg, attackSprite));
                sequencer.AddCommand(new ActionCommand(() => onApplyResults?.Invoke()));

                if (winnerFig != null)
                {
                    sequencer.AddCommand(new MoveCommand(winnerFig, winnerFig.anchoredPosition + (isPlayerWinner ? Vector2.right : Vector2.left) * 50f, 0.1f));
                }
                
                // 피격 연출 (캐릭터 흔들기 + 화면 전체 흔들기 + 화면 플래시)
                if (ctx.hitSfx != null) sequencer.AddCommand(new SoundCommand(ctx.hitSfx));
                else sequencer.AddCommand(new SoundCommand("Battle_Hit"));

                if (loserImg != null) sequencer.AddCommand(new SpriteCommand(loserImg, hitSprite));
                sequencer.AddCommand(new ActionCommand(() => PlayHitShake(winnerFig, loserFig)));
                sequencer.AddCommand(new DelayCommand(HitShakeDuration));
                
                if (ctx.flashOverlay != null)
                {
                    sequencer.AddCommand(new FlashCommand(ctx.flashOverlay, new Color(1, 0, 0, 0.3f), 0.15f));
                }

                sequencer.AddCommand(new DelayCommand(0.5f));
                
                // 원상복귀
                sequencer.AddCommand(new ActionCommand(() => {
                    if (winnerImg != null) ctx.updateFigureSprite?.Invoke(winnerImg, winnerModel);
                    if (loserImg != null) ctx.updateFigureSprite?.Invoke(loserImg, loserModel);
                }));
                if (winnerFig != null)
                {
                    sequencer.AddCommand(new MoveCommand(winnerFig, winnerFig.anchoredPosition, 0.1f));
                }
            }

            // 3. 연출 실행 및 최종 정리
            sequencer.Play(() => {
                CleanupClashCards(); 
                onComplete?.Invoke();
            });
        }

        public void PlayCardClashEffect(ActionSequencer sequencer, BattleModel model, int winnerIndex, float customDamage = -1f)
        {
            if (sequencer == null) return;

            float moveDur = ctx.playerClashLayout != null ? ctx.playerClashLayout.MoveDuration : 0.4f;

            // 1. 모든 카드 활성화 및 고오더 루트 이동
            foreach (var card in PlayerClashViews)
            {
                if (card == null) continue;
                if (ctx.clashHighOrderRoot != null) card.transform.SetParent(ctx.clashHighOrderRoot, true);
                else if (ctx.viewTransform != null) card.transform.SetParent(ctx.viewTransform, true);

                card.gameObject.SetActive(true);
                card.ResetVisualState();
                card.SetSelected(false);
            }
            if (ctx.playerClashLayout != null) ctx.playerClashLayout.ArrangeCards(PlayerClashViews, sequencer);

            foreach (var card in EnemyClashViews)
            {
                if (card == null) continue;
                if (ctx.clashHighOrderRoot != null) card.transform.SetParent(ctx.clashHighOrderRoot, true);
                else if (ctx.viewTransform != null) card.transform.SetParent(ctx.viewTransform, true);

                card.gameObject.SetActive(true);
                card.ResetVisualState();

#if UNITY_EDITOR
                if (card.IsCovered)
                {
                    card.SetCovered(false);
                }
#endif
            }
            if (ctx.enemyClashLayout != null) ctx.enemyClashLayout.ArrangeCards(EnemyClashViews, sequencer);
            
            sequencer.AddCommand(new DelayCommand(moveDur + 0.1f));

            // 2. 결과 연출 (SO 데이터 기반)
            if (ctx.productionManager != null)
            {
                float totalDmg = (customDamage >= 0) ? customDamage : 0;
                if (customDamage < 0)
                {
                    totalDmg = BattleLogic.CalculateDamage(model, winnerIndex);
                }

                bool isPlayerWinner = (winnerIndex == 1);
                int chainCount = isPlayerWinner ? model.playerSelectedCards.Count : model.enemySelectedCards.Count;

                var data = new { winnerIndex, totalDmg, chainCount };
                
                ProductionType resultType = ProductionType.ClashDraw;
                if (winnerIndex == 1) resultType = ProductionType.ClashWin;
                else if (winnerIndex == 2) resultType = ProductionType.ClashLose;

                sequencer.AddCommand(new ProductionCommand(ctx.productionManager, resultType, CharacterKeyName.None, null, data));
            }
            else
            {
                sequencer.AddCommand(new ActionCommand(() => {
                    if (ctx.clashResultText != null)
                    {
                        if (winnerIndex == 0)
                        {
                            ctx.clashResultText.text = "DRAW";
                        }
                        else
                        {
                            bool isPlayerWinner = (winnerIndex == 1);
                            var winnerCards = isPlayerWinner ? model.playerSelectedCards : model.enemySelectedCards;
                            
                            float totalDmg = (customDamage >= 0) ? customDamage : 0;
                            if (customDamage < 0)
                            {
                                totalDmg = BattleLogic.CalculateDamage(model, winnerIndex);
                            }

                            if (winnerCards.Count == 0 && totalDmg > 0)
                            {
                                ctx.clashResultText.text = $"FEVER BONUS\nTOTAL DMG: {totalDmg}";
                            }
                            else
                            {
                                ctx.clashResultText.text = $"{winnerCards.Count} CHAIN\nTOTAL DMG: {totalDmg}";
                            }
                        }
                        
                        ctx.clashResultText.gameObject.SetActive(true);
                        ctx.clashResultText.transform.localScale = Vector3.zero;
                        ctx.clashResultText.transform.DOScale(1.2f, 0.2f).SetEase(Ease.OutBack);
                    }
                }));
                sequencer.AddCommand(new DelayCommand(0.8f));
            }

            // 3. 격돌 효과 (흔들기)
            float shakeDur = ctx.productionData != null ? ctx.productionData.clashShakeDuration : 0.4f;
            sequencer.AddCommand(new ActionCommand(() => {
                foreach (var c in PlayerClashViews) if (c != null) c.transform.DOShakePosition(shakeDur, 30f);
                foreach (var c in EnemyClashViews) if (c != null) c.transform.DOShakePosition(shakeDur, 30f);
            }));
            sequencer.AddCommand(new DelayCommand(shakeDur + 0.1f));
            
            // 4. 퇴장 연출
            float exitDur = ctx.productionData != null ? ctx.productionData.clashExitDuration : 0.2f;
            sequencer.AddCommand(new ActionCommand(() => {
                foreach (var c in PlayerClashViews) if (c != null) c.transform.DOScale(0, exitDur);
                foreach (var c in EnemyClashViews) if (c != null) c.transform.DOScale(0, exitDur);
                if (ctx.clashResultText != null) ctx.clashResultText.gameObject.SetActive(false);
            }));
            
            sequencer.AddCommand(new DelayCommand(exitDur));
        }

        public void CleanupClashCards()
        {
            foreach (var card in PlayerClashViews) if (card != null) UnityEngine.Object.Destroy(card.gameObject);
            foreach (var card in EnemyClashViews) if (card != null) UnityEngine.Object.Destroy(card.gameObject);
            PlayerClashViews.Clear();
            EnemyClashViews.Clear();
        }

        private void AddDiceRollPreProduction(ActionSequencer seq, BattleCharacterModel winnerModel)
        {
            seq.AddCommand(new ActionCommand(() => {
                if (ctx.clashResultText != null)
                {
                    ctx.clashResultText.gameObject.SetActive(true);
                    ctx.clashResultText.text = $"🎲 <color=#FFFF00>ROLLING DICE...</color>";
                }
                Core.Audio.SoundManager.Instance?.PlaySe("UI_Submit_Click");
            }));
            seq.AddCommand(new DelayCommand(0.35f));
            seq.AddCommand(new ActionCommand(() => {
                int diceEye = winnerModel.lastDiceRoll.Value;
                float effectValue = winnerModel.lastDiceRollValue.Value;
                if (ctx.clashResultText != null)
                {
                    ctx.clashResultText.text = $"🎲 <color=#FFFF00>DICE: {diceEye}!</color>\n<size=70%>대미지/회복 +{(int)effectValue}</size>";
                }
                Core.Audio.SoundManager.Instance?.PlaySe("Battle_Hit");
            }));
            seq.AddCommand(new DelayCommand(0.85f));
            seq.AddCommand(new ActionCommand(() => {
                if (ctx.clashResultText != null) ctx.clashResultText.gameObject.SetActive(false);
            }));
        }

        public IEnumerator PlayDiceRollResolveRoutine(BattleModel battleModel, BattleCharacterModel user, BattleCharacterModel opponent, int diceEye, float effectValue, Action onDone, BattleStatusView playerStatus, BattleStatusView enemyStatus)
        {
            if (ctx.clashResultText != null)
            {
                ctx.clashResultText.gameObject.SetActive(true);
                ctx.clashResultText.text = $"<color=#00FFFF>🎲 [DICE ROLL CHAIN]</color>\n피해 {(int)effectValue} / 회복 {(int)effectValue}";
            }

            bool isUserPlayer = (battleModel != null && user == battleModel.player);
            var userStatus = isUserPlayer ? playerStatus : enemyStatus;
            var oppStatus = isUserPlayer ? enemyStatus : playerStatus;

            if (userStatus != null) userStatus.ShowFloatingCustomText($"[DICE HEAL] +{(int)effectValue}", Color.green);
            if (oppStatus != null) oppStatus.ShowFloatingCustomText($"[DICE HIT] -{(int)effectValue}", Color.red);

            // 실제 체인 대미지/회복 로직 정산
            BattleLogic.ProcessDiceRollResult(user, opponent, diceEye, effectValue);

            // 피격 플래시 피드백
            if (ctx.flashOverlay != null)
            {
                ctx.flashOverlay.DOColor(new Color(1f, 0.9f, 0.2f, 0.35f), 0.15f).OnComplete(() => {
                    ctx.flashOverlay.DOColor(Color.clear, 0.2f);
                });
            }

            yield return new WaitForSeconds(0.9f);

            if (ctx.clashResultText != null) ctx.clashResultText.gameObject.SetActive(false);

            onDone?.Invoke();
        }

        private void PlayHitShake(RectTransform attacker, RectTransform defender)
        {
            if (attacker == null && defender == null) return;

            Vector2 attackerStart = attacker != null ? attacker.anchoredPosition : Vector2.zero;
            Vector2 defenderStart = defender != null ? defender.anchoredPosition : Vector2.zero;

            var backgrounds = new List<RectTransform>();
            if (ctx.rootCanvasGroup != null)
            {
                Transform gameplayRoot = ctx.rootCanvasGroup.transform;
                Transform canvasRoot = gameplayRoot.parent;
                RectTransform sceneBackground = canvasRoot != null
                    ? canvasRoot.Find("Background") as RectTransform : null;
                if (sceneBackground != null) backgrounds.Add(sceneBackground);
                foreach (RectTransform child in gameplayRoot.GetComponentsInChildren<RectTransform>(true))
                {
                    if (child.name == "BackgroundRoot" && !backgrounds.Contains(child))
                        backgrounds.Add(child);
                }
            }
            Vector2[] backgroundStarts = backgrounds.Select(bg => bg.anchoredPosition).ToArray();

            DOVirtual.Float(0f, 1f, HitShakeDuration, progress => {
                float envelope = Mathf.Exp(-HitImpactDecay * progress) * (1f - progress);
                float phase = progress * HitShakeCycles * Mathf.PI * 2f;
                Vector2 offset = new Vector2(
                    Mathf.Sin(phase) * HitFigureShakeStrength.x,
                    Mathf.Sin(phase * 1.4f) * HitFigureShakeStrength.y) * envelope;
                for (int i = 0; i < backgrounds.Count; i++)
                    if (backgrounds[i] != null) backgrounds[i].anchoredPosition = backgroundStarts[i] + offset;
                if (attacker != null) attacker.anchoredPosition = attackerStart + offset;
                if (defender != null) defender.anchoredPosition = defenderStart + offset * 1.25f;
            })
                .SetEase(Ease.Linear)
                .SetLink(defender != null ? defender.gameObject : attacker.gameObject,
                    LinkBehaviour.KillOnDisable)
                .OnKill(() => {
                    for (int i = 0; i < backgrounds.Count; i++)
                        if (backgrounds[i] != null) backgrounds[i].anchoredPosition = backgroundStarts[i];
                    if (attacker != null) attacker.anchoredPosition = attackerStart;
                    if (defender != null) defender.anchoredPosition = defenderStart;
                });
        }

        private string GetSafeSpriteName(string[] names, int index)
        {
            if (names == null || names.Length == 0) return "";
            if (index < 0 || index >= names.Length) return names[0];
            return names[index];
        }
    }
}
