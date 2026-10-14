using UnityEngine;
using UnityEngine.UI;
using UniRx;
using Core.Battle;
using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;

namespace Core.View
{
    /// <summary>
    /// BattleHandPresenter 생성에 필요한 View 의존성 및 콜백 묶음
    /// </summary>
    public class BattleHandContext
    {
        public MonoBehaviour viewOwner;
        public CardView playerCardPrefab;
        public CardView enemyCardPrefab;
        public HandContainer playerHandContainer;
        public HandContainer playerTrayContainer;
        public HandContainer enemyHandContainer;
        public Button submitButton;
        public AudioClip submitSfx;
        public AudioClip warningSfx;
        public Func<BattleModel> getBattleModel;
        public Func<BattleCombatPresenter> getCombatPresenter;
        public Action<List<CardInstance>> onSubmitCards;
        public Action onSlideOutHands;
    }

    /// <summary>
    /// 플레이어와 적의 카드 핸드 바인딩, 카드 선택/트레이 규칙(동일 속성, 피버 제한), 제출 버튼 상호작용을 전담하는 프리젠터
    /// </summary>
    public class BattleHandPresenter
    {
        private readonly BattleHandContext ctx;
        private readonly List<CardView> selectedCards = new List<CardView>();
        private readonly List<CardView> allPlayerCards = new List<CardView>();
        private readonly List<CardView> allEnemyCards = new List<CardView>();

        public List<CardView> SelectedCards => selectedCards;
        public List<CardView> AllPlayerCards => allPlayerCards;
        public List<CardView> AllEnemyCards => allEnemyCards;

        public BattleHandPresenter(BattleHandContext context)
        {
            this.ctx = context ?? throw new ArgumentNullException(nameof(context));

            if (ctx.submitButton != null)
            {
                ctx.submitButton.onClick.AddListener(OnClickSubmit);
                ctx.submitButton.gameObject.SetActive(false);
            }
        }

        public void Bind(BattleModel model)
        {
            if (model == null) return;

            // 플레이어 핸드 바인딩
            BindHand(model.player.hand, ctx.playerHandContainer, allPlayerCards, ctx.playerCardPrefab, OnCardClicked, false);
            // 적 핸드 바인딩 (클릭 이벤트 없음, 컴팩트 모드)
            BindHand(model.enemy.hand, ctx.enemyHandContainer, allEnemyCards, ctx.enemyCardPrefab, null, true);
        }

        public void RefreshVisuals()
        {
            foreach (var card in allPlayerCards)
            {
                if (card != null) card.RefreshVisuals();
            }
            foreach (var card in allEnemyCards)
            {
                if (card != null) card.RefreshVisuals();
            }
        }

        public void ResetPlayerVisualStates()
        {
            foreach (var card in allPlayerCards)
            {
                if (card != null) card.ResetVisualState();
            }
        }

        public void ReturnTrayCardsToHand()
        {
            if (selectedCards.Count == 0) return;

            var tempCards = selectedCards.ToList();
            foreach (var card in tempCards)
            {
                card.SetSelected(false);
                card.SetInteractionActive(true);
                ctx.playerTrayContainer.RemoveCard(card, false);
                ctx.playerHandContainer.AddCard(card);
            }
            selectedCards.Clear();
            UpdateSubmitButton();
        }

        public void OnClickSubmit()
        {
            if (selectedCards.Count == 0) return;

            if (ctx.submitSfx != null) Core.Audio.SoundManager.Instance.PlaySe(ctx.submitSfx);
            else Core.Audio.SoundManager.Instance.PlaySe("UI_Submit_Click");

            // UI 슬라이드 아웃 연출 콜백 호출
            ctx.onSlideOutHands?.Invoke();

            var combatPresenter = ctx.getCombatPresenter?.Invoke();
            combatPresenter?.ClearPlayerClashCards();
            foreach (var card in selectedCards)
            {
                combatPresenter?.AddPlayerClashCard(card);
                ctx.playerTrayContainer.RemoveCard(card, false); // 트레이에서 즉시 제거
                var button = card.GetComponent<Button>();
                if (button != null) button.interactable = false;
            }

            var dataList = selectedCards.Select(c => c.CardInstance).ToList();
            ctx.onSubmitCards?.Invoke(dataList);

            // [Tutorial] 제출 클릭 시 딤드와 대사창을 즉시 해제하여 연출을 온전한 밝기로 감상
            Tutorial.TutorialManager.Instance?.NotifySubmitClicked();

            selectedCards.Clear();
            UpdateSubmitButton();
        }

        public void OnCardClicked(CardView card)
        {
            if (card == null || card.CardInstance == null) return;

            if (card.CardInstance.IsBroken)
            {
                // Broken 상태면 흔들림 연출 등으로 피드백
                card.transform.DOShakePosition(0.3f, 10f);
                if (ctx.warningSfx != null) Core.Audio.SoundManager.Instance.PlaySe(ctx.warningSfx);
                return;
            }

            if (card.IsSelected)
            {
                // 선택 해제: 트레이에서 핸드로 복귀
                card.SetSelected(false); // 여기서 selectedCase가 꺼짐
                card.SetInteractionActive(true); // 핸드로 돌아오면 다시 상호작용 활성화
                selectedCards.Remove(card);

                ctx.playerTrayContainer.RemoveCard(card, false);
                ctx.playerHandContainer.AddCard(card);
            }
            else
            {
                var battleModel = ctx.getBattleModel?.Invoke();
                // 피버 모드일 때는 1장으로 제한
                int maxAllowed = (battleModel != null && battleModel.player.isFeverModeActive.Value) ? 1 : 5;

                // 속성 일치 체크 (다르면 기존 트레이 비우기)
                // - 일반 카드 및 Rain 카드: 동일 속성끼리 체인 가능 (1~5장)
                // - Fool 카드: Fool 카드끼리만 체인 가능 (1~5장)
                bool isDifferentElement = selectedCards.Count > 0 && selectedCards[0].CardInstance.EffectiveElement != card.CardInstance.EffectiveElement;

                if (isDifferentElement)
                {
                    var tempCards = selectedCards.ToList();
                    foreach (var c in tempCards)
                    {
                        c.SetSelected(false);
                        c.SetInteractionActive(true);
                        ctx.playerTrayContainer.RemoveCard(c, false);
                        ctx.playerHandContainer.AddCard(c);
                    }
                    selectedCards.Clear();
                }

                if (selectedCards.Count < maxAllowed)
                {
                    // 선택: 핸드에서 트레이로 이동
                    card.SetSelected(true); // 트레이에 있는 동안 유지
                    card.ResetVisualState(); // 호버된 상태의 위치를 0으로 강제 리셋
                    card.SetInteractionActive(false); // 트레이에 있을 때는 호버 연출(DOTween Pro) 차단
                    selectedCards.Add(card);

                    ctx.playerHandContainer.RemoveCard(card);
                    ctx.playerTrayContainer.AddCard(card);
                }
            }
            UpdateSubmitButton();
        }

        public void UpdateSubmitButton()
        {
            if (ctx.submitButton != null)
            {
                ctx.submitButton.gameObject.SetActive(selectedCards.Count > 0);
            }
            Tutorial.TutorialManager.Instance?.NotifyUserActionCompleted();
        }

        private void BindHand(ReactiveCollection<CardInstance> hand, HandContainer container, List<CardView> viewList, CardView prefab, Action<CardView> onClick, bool isCompact)
        {
            if (hand == null || container == null || prefab == null || ctx.viewOwner == null) return;

            hand.ObserveAdd().Subscribe(addEvent => {
                var card = CardViewPool.Get(prefab, container.transform);
                card.Setup(addEvent.Value, onClick, container);

#if UNITY_EDITOR
                bool shouldHideEnemy = Core.Utils.DevBootstrapper.Instance != null && Core.Utils.DevBootstrapper.Instance.HideEnemyHand;
                if (isCompact && shouldHideEnemy)
                {
                    card.SetCovered(true);
                }
#endif

                viewList.Add(card);
                container.AddCard(card);
            }).AddTo(ctx.viewOwner);

            hand.ObserveRemove().Subscribe(removeEvent => {
                var combatPresenter = ctx.getCombatPresenter?.Invoke();
                // 플레이어 카드의 경우 OnClickSubmit에서 이미 선점(Pre-capture)했을 수 있으므로 체크
                if (!isCompact && combatPresenter != null && combatPresenter.HasCapturedPlayerCard(removeEvent.Value))
                {
                    var capturedCard = combatPresenter.GetCapturedPlayerCard(removeEvent.Value, viewList);
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

                    var battleModel = ctx.getBattleModel?.Invoke();
                    // 격돌(Clash) 제출 목적의 카드인지 검사 (Rain/Fool 변환 등으로 파기된 카드는 화면에서 파괴)
                    bool isClashSubmission = (battleModel != null) &&
                        ((battleModel.playerSelectedCards != null && battleModel.playerSelectedCards.Contains(removeEvent.Value)) ||
                         (battleModel.enemySelectedCards != null && battleModel.enemySelectedCards.Contains(removeEvent.Value)));

                    if (isClashSubmission)
                    {
                        if (isCompact) combatPresenter?.AddEnemyClashCard(target);
                        else combatPresenter?.AddPlayerClashCard(target);
                    }
                    else
                    {
                        CardViewPool.Release(target, prefab);
                    }
                }
            }).AddTo(ctx.viewOwner);

            hand.ObserveReset().Subscribe(_ => {
                container.Clear();
                viewList.Clear();
                if (container == ctx.playerHandContainer)
                {
                    selectedCards.Clear();
                    UpdateSubmitButton();
                }
            }).AddTo(ctx.viewOwner);
        }
    }
}

