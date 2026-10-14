using UnityEngine;
using Core.Table;
using Core.Battle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using Core.View;

namespace Core.Flow
{
    public enum BattlePhase
    {
        Setup,
        RoundStart,
        Command,
        Process,
        RoundEnd,
        Result
    }

    public class BattleExecutor : BaseFlowExecutor
    {
        [SerializeField] private Core.View.BattleView battleView;
        [SerializeField] private Core.Tutorial.TutorialBattleScriptController tutorialScriptController;
        private BattlePhase currentPhase;
        private BattleModel battleModel;
        private BattleChainActionHandler chainHandler;
        private BattleSpecialSkillHandler specialSkillHandler;
        private BattleCardDealer cardDealer;
        private BattleRoundProcessor roundProcessor;

        public BattleModel BattleModel => battleModel;
        public BattlePhase CurrentPhase => currentPhase;
        public BattleChainActionHandler ChainHandler => chainHandler;
        public BattleSpecialSkillHandler SpecialSkillHandler => specialSkillHandler;
        public BattleCardDealer CardDealer => cardDealer;
        public BattleRoundProcessor RoundProcessor => roundProcessor;

        private void Awake()
        {
            FlowController.Instance.RegisterExecutor(this);
            if (tutorialScriptController == null)
            {
                tutorialScriptController = GetComponent<Core.Tutorial.TutorialBattleScriptController>() 
                                        ?? FindObjectOfType<Core.Tutorial.TutorialBattleScriptController>();
            }
        }

        protected override void Execute() => ChangePhase(BattlePhase.Setup);

        private void ChangePhase(BattlePhase nextPhase)
        {
            currentPhase = nextPhase;
            Debug.Log($"[Battle] Phase Changed to: {nextPhase}");

            // [New] 인터랙션 가드: Command 페이즈가 아닐 때는 유저 입력을 차단
            if (battleView != null)
            {
                battleView.SetInteractionEnabled(nextPhase == BattlePhase.Command);
            }

            // [Tutorial] 튜토리얼 인터셉터 체크 (흐름 일시 보류 및 재개)
            int roundNum = battleModel != null ? battleModel.currentRound.Value : 1;
            if (Core.Tutorial.TutorialManager.Instance != null &&
                Core.Tutorial.TutorialManager.Instance.TryInterceptPhase(nextPhase, roundNum, () => DispatchPhase(nextPhase)))
            {
                return;
            }

            DispatchPhase(nextPhase);
        }

        private void DispatchPhase(BattlePhase phase)
        {
            switch (phase)
            {
                case BattlePhase.Setup: HandleSetup(); break;
                case BattlePhase.RoundStart: HandleRoundStart(); break;
                case BattlePhase.Command: HandleCommand(); break;
                case BattlePhase.Process: HandleProcess(); break;
                case BattlePhase.RoundEnd: HandleRoundEnd(); break;
                case BattlePhase.Result: HandleResult(); break;
            }
        }

        /// <summary>
        /// [New 261013] 스테이지 시작 시 환경 변화 요소 무작위 추첨
        /// </summary>
        private EnvironmentType DetermineEnvironment()
        {
#if UNITY_EDITOR
            if (Core.Utils.DevBootstrapper.Instance != null && Core.Utils.DevBootstrapper.Instance.EnvironmentOverride != EnvironmentType.None)
            {
                return Core.Utils.DevBootstrapper.Instance.EnvironmentOverride;
            }
#endif

            // 튜토리얼 스테이지(1001~1099)는 환경 요소를 비활성화하여 기본 룰 학습 보장
            if (Core.Common.GameConstants.IsTutorialStage(executeId))
            {
                return EnvironmentType.None;
            }

            // 현재 확정된 2종(아이리스의 희망, 무메이의 문명) 중 무작위 1개 추첨
            // (향후 TableManager 연동 시 테이블 데이터 기반 선택으로 확장)
            EnvironmentType[] availableEnvironments = new EnvironmentType[]
            {
                EnvironmentType.IrysHope,
                EnvironmentType.MumeiCivilization
            };

            int randomIndex = UnityEngine.Random.Range(0, availableEnvironments.Length);
            return availableEnvironments[randomIndex];
        }

        private void HandleSetup()
        {
            var battleTable = TableManager.Instance.GetTable<BattleTable>().Get(executeId);
            if (battleTable == null) return;

            // [New] 시작 시 UI 즉시 숨김
            if (battleView != null) battleView.InitializeUIState();

            // [New] 배틀 배경음 재생 (BattleTable.xml에 지정된 BgmName 우선 반영, 없으면 기본값)
            string stageBgmName = !string.IsNullOrEmpty(battleTable.bgmName) 
                ? battleTable.bgmName 
                : Core.Common.AudioConstants.BGM_BATTLE_DEFAULT;

            if (Core.Audio.SoundManager.Instance != null)
            {
                Core.Audio.SoundManager.Instance.PlayBgm(stageBgmName);
            }

            var pData = TableManager.Instance.GetTable<BattleCharacterTable>().Get(battleTable.storyPlayerCharacterId);
            var eData = TableManager.Instance.GetTable<BattleCharacterTable>().Get(battleTable.enemyCharacterId);

            battleModel = new BattleModel(battleTable, pData, eData);
            chainHandler = new BattleChainActionHandler(battleModel, battleView, this, () => {
                if (CheckBattleEnd()) ChangePhase(BattlePhase.Result);
            });
            specialSkillHandler = new BattleSpecialSkillHandler(battleModel, battleView, chainHandler);
            cardDealer = new BattleCardDealer(battleModel, chainHandler, tutorialScriptController, executeId);
            roundProcessor = new BattleRoundProcessor(battleModel, cardDealer, chainHandler, specialSkillHandler);
            
            // [New 261013] 환경 변화 요소 추첨 (확정된 2종 중 무작위 1개 선택)
            battleModel.currentEnvironment.Value = DetermineEnvironment();
            Debug.Log($"[Environment] Stage Environment initialized: {battleModel.currentEnvironment.Value}");
            Observable.Merge(
                battleModel.player.isFeverModeActive,
                battleModel.enemy.isFeverModeActive
            )
            .Subscribe(isFever => {
                if (Core.Audio.SoundManager.Instance == null) return;
                
                if (battleModel.player.isFeverModeActive.Value || battleModel.enemy.isFeverModeActive.Value)
                {
                    Core.Audio.SoundManager.Instance.PlayBgm(Core.Common.AudioConstants.BGM_FEVER);
                }
                else
                {
                    Core.Audio.SoundManager.Instance.PlayBgm(stageBgmName);
                }
            })
            .AddTo(this);

            // [Tutorial] 전역 피처 가이드: 플레이어 최초 피버 모드 진입 시 인터셉트 팝업
            battleModel.player.isFeverModeActive
                .Where(isFever => isFever)
                .Subscribe(_ => {
                    if (Core.Tutorial.TutorialManager.Instance != null)
                    {
                        Core.Tutorial.TutorialManager.Instance.TryInterceptCondition(
                            Core.Tutorial.TutorialTriggerPhase.FeverModeStart, 
                            null
                        );
                    }
                })
                .AddTo(this);

            if (battleView != null)
            {
                battleView.Bind(battleModel);
                battleView.OnSubmitCards = (selectedCards) => {
                    if (currentPhase == BattlePhase.Command)
                    {
                        ProcessPlayerTurn(selectedCards);
                    }
                };
                battleView.OnDrawCard = () => {
                    if (currentPhase == BattlePhase.Command)
                    {
                        ProcessPlayerDraw();
                    }
                };
            }

            // [New] 배틀 시작 연출 추가 (SO 기반)
            if (battleView != null && battleView.ProductionManager != null)
            {
                var sequencer = new ActionSequencer();
                sequencer.AddCommand(new ProductionCommand(battleView.ProductionManager, ProductionType.BattleStart));
                // [New] 배틀 스타트 후 UI 페이드 인
                sequencer.AddCommand(new ActionCommand(() => battleView.FadeInUI(1.0f, null)));
                sequencer.Play(() => {
                    // [New 261013] 환경 요소 배너 연출 후 RoundStart 진행
                    if (battleModel.currentEnvironment.Value != EnvironmentType.None)
                    {
                        battleView.ShowEnvironmentBanner(battleModel.currentEnvironment.Value, () => ChangePhase(BattlePhase.RoundStart));
                    }
                    else
                    {
                        ChangePhase(BattlePhase.RoundStart);
                    }
                });
            }
            else
            {
                if (battleView != null) battleView.FadeInUI(0.5f, null);
                if (battleView != null && battleModel.currentEnvironment.Value != EnvironmentType.None)
                {
                    battleView.ShowEnvironmentBanner(battleModel.currentEnvironment.Value, () => ChangePhase(BattlePhase.RoundStart));
                }
                else
                {
                    ChangePhase(BattlePhase.RoundStart);
                }
            }
        }

        private void HandleRoundStart()
        {
            // --- 1단계: Card Draw ---
            cardDealer.ProcessRoundStartDraw();

            // --- 2단계: 상태 이상 및 특수 상태 갱신 ---
            cardDealer.UpdateAllStatusDurations();

            if (battleView != null) battleView.RefreshHandVisuals();
            
            // --- 3단계: 특수 스킬 체크 ---
            if (!battleModel.player.isFeverModeActive.Value && !battleModel.enemy.isFeverModeActive.Value)
            {
                if (battleModel.enemy.IsGaugeFull() && !battleModel.enemy.isSpecialActive)
                {
                    ExecuteEnemySpecialSkill();
                }
            }
            
            // [New] 라운드 시작 전 UI 슬라이드 인 (타이밍 앞당김)
            if (battleView != null) battleView.SlideInHands(0.4f, null);

            // 라운드 시작 연출 (전용 연출 시스템 ProductionManager 활용)
            if (battleView != null && battleView.ProductionManager != null)
            {
                var sequencer = new ActionSequencer();
                sequencer.AddCommand(new ProductionCommand(battleView.ProductionManager, ProductionType.RoundStart, data: battleModel.currentRound.Value));
                sequencer.Play(() => ChangePhase(BattlePhase.Command));
            }
            else
            {
                ChangePhase(BattlePhase.Command);
            }
        }

        public void ExecuteSpecialSkill(BattleCharacterModel owner) => specialSkillHandler.ExecuteSpecialSkill(owner);
        private void ExecuteEnemySpecialSkill() => ExecuteSpecialSkill(battleModel.enemy);


        public void DrawInitialCards(BattleCharacterModel model) => cardDealer?.DrawInitialCards(model);
        public CardInstance DrawRandomCard(BattleCharacterModel model) => cardDealer?.DrawRandomCard(model);

        private void HandleCommand()
        {
            Debug.Log("[Battle] Waiting for Player Command...");

            // [Tutorial] 1라운드 시작 시 모든 UI 접근 권한이 열리기 직전에 튜토리얼 딤드 및 통제 시작
            if (Core.Common.GameConstants.IsTutorialStage(executeId) && battleModel != null && battleModel.currentRound.Value == 1)
            {
                if (Core.Tutorial.TutorialManager.Instance != null && !Core.Tutorial.TutorialManager.Instance.IsTutorialActive)
                {
                    Core.Tutorial.TutorialManager.Instance.StartTutorial(battleView);
                }
            }
        }


        private void ProcessPlayerDraw()
        {
            var gameSet = TableManager.Instance.GetTable<GameSetTable>().Get(0);
            int maxCards = gameSet?.maxCards ?? 8;

            if (battleModel.player.hand.Count >= maxCards)
            {
                if (battleView != null) battleView.ShowWarningMessage("FULL HAND");
                return;
            }

            // [Fix] 명시적 상태 초기화 (이전 잔류 데이터 방지)
            battleModel.playerSelectedCards.Clear();
            battleModel.enemySelectedCards.Clear();
            battleModel.playerChoseDraw = true;
            battleModel.enemyChoseDraw = false;

            var npcSelected = BattleLogic.CalculateNPCMove(battleModel);
            foreach (var card in npcSelected)
            {
                battleModel.enemySelectedCards.Add(card);
                // [Fix] 핸드에서 즉시 제거하지 않음 (HandleProcess에서 통합 처리)
            }

            ChangePhase(BattlePhase.Process);
        }

        private void ProcessPlayerTurn(List<CardInstance> selectedCards)
        {
            // [Fix] 명시적 상태 초기화
            battleModel.playerSelectedCards.Clear();
            battleModel.enemySelectedCards.Clear();
            battleModel.playerChoseDraw = false;
            battleModel.enemyChoseDraw = false;

            foreach (var card in selectedCards)
            {
                battleModel.playerSelectedCards.Add(card);
            }

            // 1라운드 플레이어 선택 속성 기록 (2R, 3R 드로우 계산용)
            if (battleModel.currentRound.Value == 1 && selectedCards.Count > 0 && cardDealer != null)
            {
                cardDealer.PlayerFirstRoundElement = selectedCards[0].TableData.element;
            }

            // 튜토리얼 스크립트 대응패 우선 확인
            List<CardInstance> npcSelected = null;
            if (tutorialScriptController != null && tutorialScriptController.IsApplicable(executeId, battleModel.currentRound.Value))
            {
                npcSelected = tutorialScriptController.GetScriptedNPCMove(battleModel);
            }

            if (npcSelected == null)
            {
                npcSelected = BattleLogic.CalculateNPCMove(battleModel);
            }

            foreach (var card in npcSelected)
            {
                battleModel.enemySelectedCards.Add(card);
            }

            ChangePhase(BattlePhase.Process);
        }

        private void HandleProcess()
        {
            var result = roundProcessor.ProcessRound();

            if (battleView != null)
            {
                battleView.PlayDamageAnimation(
                    result.Winner,
                    battleModel,
                    result.ApplyResults,
                    () => {
                        // [체인 타이밍 3: 라운드 플로우 - 가위바위보 종료]
                        chainHandler.OnRpsEnd(result.Winner, () => {
                            if (CheckBattleEnd()) ChangePhase(BattlePhase.Result);
                            else ChangePhase(BattlePhase.RoundEnd);
                        });
                    },
                    result.RoundDamage,
                    result.SuccessCommand,
                    result.ApplyAttackWinChain);
            }
            else
            {
                result.ApplyAttackWinChain();
                result.ApplyResults();
                chainHandler.OnRpsEnd(result.Winner, () => {
                    if (CheckBattleEnd()) ChangePhase(BattlePhase.Result);
                    else ChangePhase(BattlePhase.RoundEnd);
                });
            }
        }





        private void HandleRoundEnd()
        {
            battleModel.player.ResetSpecialEffects();
            battleModel.enemy.ResetSpecialEffects();

            // [New 260921] 특수 스킬 봉쇄 잔여 라운드 갱신 (1라운드 경과 시 자동 해제)
            battleModel.player.UpdateSkillBlockDuration();
            battleModel.enemy.UpdateSkillBlockDuration();

            battleModel.currentRound.Value++;
            battleModel.ResetRoundSelection();

            if (battleView != null) battleView.PlayRoundTransition(() => ChangePhase(BattlePhase.RoundStart));
            else ChangePhase(BattlePhase.RoundStart);
        }

        private void HandleResult()
        {
            if (battleView != null)
            {
                bool isWin = battleModel.enemy.hp.Value <= 0;
                battleView.ShowBattleResult(isWin, null);
            }
        }

        private bool CheckBattleEnd()
        {
            return battleModel.player.hp.Value <= 0 || battleModel.enemy.hp.Value <= 0;
        }

        public void ProcessPlayerEndFever()
        {
            chainHandler.OnManualFeverEnd(() => {
                // [Fix] 다음 라운드 AI 계산만 수행하고 핸드 제거는 하지 않음
                var npcSelected = BattleLogic.CalculateNPCMove(battleModel);
                battleModel.enemySelectedCards.Clear();
                foreach (var card in npcSelected)
                {
                    battleModel.enemySelectedCards.Add(card);
                }
                if (battleModel.enemy.IsGaugeFull() && !battleModel.enemy.isSpecialActive && !battleModel.enemy.isFeverModeActive.Value)
                    ExecuteEnemySpecialSkill();
                if (battleView != null) battleView.RefreshHandVisuals();
            });
        }

        public void UpdateStatusDurations(BattleCharacterModel charModel) => cardDealer?.UpdateStatusDurations(charModel);
    }
}
