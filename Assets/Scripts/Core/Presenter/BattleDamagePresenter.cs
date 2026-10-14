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
    /// BattleDamagePresenter 생성에 필요한 Context 묶음
    /// </summary>
    public class BattleDamageContext
    {
        public RectTransform playerFigure;
        public RectTransform enemyFigure;
        public CanvasGroup rootCanvasGroup;
        public Image flashOverlay;
        public TextMeshProUGUI clashResultText;
        public AudioClip playerAttackSfx;
        public AudioClip enemyAttackSfx;
        public AudioClip hitSfx;
        public Action<Image, BattleCharacterModel> updateFigureSprite;
        public Func<BattleCharacterModel, string> getDefaultSpriteName;
    }

    /// <summary>
    /// 캐릭터 공격 돌진, 피격 쉐이크/플래시 연출 및 주사위 연출을 전담하는 프리젠터
    /// </summary>
    public class BattleDamagePresenter
    {
        private readonly BattleDamageContext ctx;

        // Hit shake 상수
        private const float HitShakeDuration = 0.4f;
        private const float HitImpactDecay = 8f;
        private static readonly Vector2 HitFigureShakeStrength = new Vector2(64f, 28f);
        private const float HitShakeCycles = 5f;

        public BattleDamagePresenter(BattleDamageContext context)
        {
            this.ctx = context ?? throw new ArgumentNullException(nameof(context));
        }

        public void PlayDamageAnimation(
            ActionSequencer sequencer,
            BattleClashPresenter clashPresenter,
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
            bool canClash = clashPresenter != null && clashPresenter.HasAnyClashCards;

            if (canClash)
            {
                clashPresenter.PlayCardClashEffect(sequencer, model, winnerIndex, customDamage);
            }
            else
            {
                if (clashPresenter != null && clashPresenter.HasAnyClashCards)
                {
                    sequencer.AddCommand(new ActionCommand(() => {
                        foreach (var c in clashPresenter.PlayerClashViews) if (c != null) c.transform.DOScale(0, 0.2f);
                        foreach (var c in clashPresenter.EnemyClashViews) if (c != null) c.transform.DOScale(0, 0.2f);
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
                clashPresenter?.CleanupClashCards(); 
                onComplete?.Invoke();
            });
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

