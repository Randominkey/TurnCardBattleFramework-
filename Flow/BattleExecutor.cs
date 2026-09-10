using UnityEngine;
using Core.Table;
using Core.Battle;
using System;
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
        private BattlePhase currentPhase;
        private BattleModel battleModel;

        private void Awake() => FlowController.Instance.RegisterExecutor(this);

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

            switch (nextPhase)
            {
                case BattlePhase.Setup: HandleSetup(); break;
                case BattlePhase.RoundStart: HandleRoundStart(); break;
                case BattlePhase.Command: HandleCommand(); break;
                case BattlePhase.Process: HandleProcess(); break;
                case BattlePhase.RoundEnd: HandleRoundEnd(); break;
                case BattlePhase.Result: HandleResult(); break;
            }
        }

        private void HandleSetup()
        {
            var battleTable = TableManager.Instance.GetTable<BattleTable>().Get(executeId);
            if (battleTable == null) return;

            // [New] 시작 시 UI 즉시 숨김
            if (battleView != null) battleView.InitializeUIState();

            // [New] 배틀 배경음 재생
            if (!string.IsNullOrEmpty(battleTable.bgmName) && Core.Audio.SoundManager.Instance != null)
            {
                Core.Audio.SoundManager.Instance.PlayBgm(battleTable.bgmName);
            }

            var pData = TableManager.Instance.GetTable<BattleCharacterTable>().Get(battleTable.storyPlayerCharacterId);
            var eData = TableManager.Instance.GetTable<BattleCharacterTable>().Get(battleTable.enemyCharacterId);

            battleModel = new BattleModel(battleTable, pData, eData);
            
            // [New] 피버 모드 BGM 관리
            Observable.Merge(
                battleModel.player.isFeverModeActive,
                battleModel.enemy.isFeverModeActive
            )
            .Subscribe(isFever => {
                if (Core.Audio.SoundManager.Instance == null) return;
                
                if (battleModel.player.isFeverModeActive.Value || battleModel.enemy.isFeverModeActive.Value)
                {
                    Core.Audio.SoundManager.Instance.PlayBgm("BGM_B_Fever");
                }
                else
                {
                    Core.Audio.SoundManager.Instance.PlayBgm(battleTable.bgmName);
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

            // [Tutorial] 튜토리얼 배틀 여부 체크
            if (executeId >= 1000 && executeId < 1100)
            {
                if (Core.Tutorial.TutorialManager.Instance != null)
                {
                    Core.Tutorial.TutorialManager.Instance.StartTutorial(battleView);
                }
            }

            // [New] 배틀 시작 연출 추가 (SO 기반)
            if (battleView != null && battleView.ProductionManager != null)
            {
                var sequencer = new ActionSequencer();
                sequencer.AddCommand(new ProductionCommand(battleView.ProductionManager, ProductionType.BattleStart));
                // [New] 배틀 스타트 후 UI 페이드 인
                sequencer.AddCommand(new ActionCommand(() => battleView.FadeInUI(1.0f, null)));
                sequencer.Play(() => ChangePhase(BattlePhase.RoundStart));
            }
            else
            {
                if (battleView != null) battleView.FadeInUI(0.5f, null);
                ChangePhase(BattlePhase.RoundStart);
            }
        }

        private void HandleRoundStart()
        {
            var gameSet = TableManager.Instance.GetTable<GameSetTable>().Get(0);
            int drawCount = (battleModel.currentRound.Value == 1) ? 0 : (gameSet?.roundDrawCards ?? 1);
            int maxCards = gameSet?.maxCards ?? 8;

            // --- 1단계: Card Draw ---
            if (battleModel.currentRound.Value == 1)
            {
                DrawInitialCards(battleModel.player);
                DrawInitialCards(battleModel.enemy);
            }
            else
            {
                for (int i = 0; i < drawCount; i++)
                {
                    if (battleModel.player.hand.Count < maxCards) DrawRandomCard(battleModel.player);
                    if (battleModel.enemy.hand.Count < maxCards) DrawRandomCard(battleModel.enemy);
                }
            }

            // --- 2단계: 상태 이상 및 특수 상태 갱신 ---
            UpdateStatusDurations(battleModel.player);
            UpdateStatusDurations(battleModel.enemy);

            if (battleView != null) battleView.RefreshHandVisuals();

            // --- 3단계: 피버 모드 관리 ---
            ManageFeverState(battleModel.player);
            ManageFeverState(battleModel.enemy);

            // --- 4단계: 특수 스킬 체크 ---
            if (!battleModel.player.isFeverModeActive.Value && !battleModel.enemy.isFeverModeActive.Value)
            {
                if (battleModel.enemy.IsGaugeFull() && !battleModel.enemy.isSpecialActive)
                {
                    ExecuteEnemySpecialSkill();
                }
            }
            
            // [New] 라운드 시작 전 UI 슬라이드 인 (타이밍 앞당김)
            if (battleView != null) battleView.SlideInHands(0.4f, null);

            // [New] 라운드 배너 연출을 전용 연출 시스템으로 교체
            if (battleView != null && battleView.ProductionManager != null)
            {
                var sequencer = new ActionSequencer();
                sequencer.AddCommand(new ProductionCommand(battleView.ProductionManager, ProductionType.RoundStart));
                sequencer.Play(() => ChangePhase(BattlePhase.Command));
            }
            else if (battleView != null)
            {
                battleView.ShowRoundBanner(battleModel.currentRound.Value, () => ChangePhase(BattlePhase.Command));
            }
            else
            {
                ChangePhase(BattlePhase.Command);
            }
        }

        private int GetMaxFeverGauge(BattleCharacterModel charModel)
        {
            var chainRow = TableManager.Instance.GetTable<ChainTable>().Get(charModel.tableData.chainId);
            if (chainRow != null && chainRow.ChainSetValues != null && chainRow.ChainSetValues.Length > 0)
                return (int)chainRow.ChainSetValues[0];
            var gameSet = TableManager.Instance.GetTable<GameSetTable>().Get(0);
            return gameSet?.maxFeverGauge ?? 100;
        }

        private int GetLimitFeverRound(BattleCharacterModel charModel)
        {
            var chainRow = TableManager.Instance.GetTable<ChainTable>().Get(charModel.tableData.chainId);
            if (chainRow != null && chainRow.ChainSetValues != null && chainRow.ChainSetValues.Length > 1)
                return (int)chainRow.ChainSetValues[1];
            var gameSet = TableManager.Instance.GetTable<GameSetTable>().Get(0);
            return gameSet?.limitFeverRound ?? 3;
        }

        private float GetFeverStackDamageCoeff(BattleCharacterModel charModel)
        {
            var chainRow = TableManager.Instance.GetTable<ChainTable>().Get(charModel.tableData.chainId);
            if (chainRow != null && chainRow.ChainSetValues != null && chainRow.ChainSetValues.Length > 2)
                return chainRow.ChainSetValues[2];
            var gameSet = TableManager.Instance.GetTable<GameSetTable>().Get(0);
            return (gameSet != null && gameSet.FeverStackDamage != null && gameSet.FeverStackDamage.Length > 0) ? gameSet.FeverStackDamage[0] : 5f;
        }

        private float GetFeverAutoFinisherDamage(BattleCharacterModel charModel)
        {
            var chainRow = TableManager.Instance.GetTable<ChainTable>().Get(charModel.tableData.chainId);
            if (chainRow != null && chainRow.ChainSetValues != null && chainRow.ChainSetValues.Length > 3)
                return chainRow.ChainSetValues[3];
            var gameSet = TableManager.Instance.GetTable<GameSetTable>().Get(0);
            return (gameSet != null && gameSet.FeverStackDamage != null && gameSet.FeverStackDamage.Length > 0) ? gameSet.FeverStackDamage.Last() : 0f;
        }

        private void ManageFeverState(BattleCharacterModel model)
        {
            int maxGauge = GetMaxFeverGauge(model);
            int limitRound = GetLimitFeverRound(model);

            if (!model.isFeverModeActive.Value)
            {
                if (model.chainGauge.Value >= maxGauge)
                {
                    model.ActivateFeverMode(limitRound);
                    if (battleView != null) battleView.RefreshHandVisuals();
                }
            }
            else
            {
                model.remainingFeverRound--;
                if (model.remainingFeverRound <= 0)
                {
                    model.EndFeverMode(true);
                }
            }
        }

        public void ExecuteSpecialSkill(BattleCharacterModel owner)
        {
            owner.UseSpecialGauge();
            
            var skillRow = TableManager.Instance.GetTable<SkillTable>().Get(owner.tableData.specialSkillId);
            if (skillRow == null) return;

            // [New] 스페셜 스킬 발동 연출 실행
            if (battleView != null && battleView.ProductionManager != null)
            {
                // Buff 타입은 발동 즉시 Success 연출, Require 타입은 Cast 연출 실행
                bool isBuff = (skillRow.element == SkillElement.Buff);
                ProductionType pType = isBuff ? ProductionType.SpecialSkillSuccess : ProductionType.SpecialSkillCast;
                SpecialSkillPhase phase = isBuff ? SpecialSkillPhase.Success : SpecialSkillPhase.Cast;

                var productionData = new SpecialSkillProductionData {
                    characterRow = owner.tableData,
                    skillRow = skillRow,
                    phase = phase
                };
                
                // 캐릭터 KeyName을 subKey로 전달하여 SO에서 캐릭터별 연출(하이라이트 등) 구분 가능
                battleView.ProductionManager.StartCoroutine(battleView.ProductionManager.PlayProduction(
                    pType, 
                    owner.tableData.KeyName, 
                    null, 
                    productionData));

                // [New] Buff 타입 스킬 발동 시 캐릭터 포즈 변경 연출 추가
                if (isBuff)
                {
                    battleView.StartCoroutine(battleView.PlaySpecialPose(owner));
                }
            }

            string action = skillRow.action.ToLower();
            var opponent = (owner == battleModel.player) ? battleModel.enemy : battleModel.player;

            if (action == "breakcard")
            {
                int breakCount = (skillRow.ActionValues.Length > 0) ? (int)skillRow.ActionValues[0] : 1;
                int duration = (skillRow.ActionValues.Length > 1) ? (int)skillRow.ActionValues[1] : 2;
                BreakOpponentCards(opponent, breakCount, duration);
                
                if (battleView != null) battleView.RefreshHandVisuals();
            }
            else if (action == "berserk")
            {
                Debug.Log($"[Special] {owner.tableData.id} Berserk activated with AttackMult={owner.attackMultiplier}, DefMult={owner.defenseMultiplier}");
            }

            Debug.Log($"[Special] {owner.tableData.id} executed Special Skill: {action}");
        }

        private void ExecuteEnemySpecialSkill() => ExecuteSpecialSkill(battleModel.enemy);

        private ProductionCommand GetSpecialSuccessCommand(BattleCharacterModel owner, SkillTableRow skillRow, int winnerIndex)
        {
            if (battleView != null && battleView.ProductionManager != null)
            {
                // [Fix] 스킬 타입 및 발동 조건에 따른 성공 컷인 반환
                bool isSuccess = false;
                
                if (skillRow.element == SkillElement.Require)
                {
                    string action = skillRow.action.ToLower();
                    if (action == "loseheal")
                    {
                        // LoseHeal은 격돌에서 패배했을 때 성공 연출 출력
                        bool isOwnerWinner = (owner == battleModel.player && winnerIndex == 1) || (owner == battleModel.enemy && winnerIndex == 2);
                        if (!isOwnerWinner && winnerIndex != 0) isSuccess = true;
                    }
                    else
                    {
                        // Berserk 포함 일반 Require 스킬은 격돌 승리 시 성공 연출
                        bool isOwnerWinner = (owner == battleModel.player && winnerIndex == 1) || (owner == battleModel.enemy && winnerIndex == 2);
                        if (isOwnerWinner) isSuccess = true;
                    }
                }

                if (!isSuccess) return null;

                var productionData = new SpecialSkillProductionData {
                    characterRow = owner.tableData,
                    skillRow = skillRow,
                    phase = SpecialSkillPhase.Success
                };
                
                return new ProductionCommand(
                    battleView.ProductionManager, 
                    ProductionType.SpecialSkillSuccess, 
                    owner.tableData.KeyName, 
                    null, 
                    productionData);
            }
            return null;
        }

        private void DrawInitialCards(BattleCharacterModel model)
        {
            var skillIds = model.tableData.SkillIds;
            var counts = model.tableData.InitialSkills;
            var gameSet = TableManager.Instance.GetTable<GameSetTable>().Get(0);
            int maxCards = gameSet?.maxCards ?? 8;

            for (int i = 0; i < skillIds.Length; i++)
            {
                if (i >= counts.Length) break;
                for (int j = 0; j < counts[i]; j++)
                {
                    if (model.hand.Count >= maxCards) break;
                    var skillRow = TableManager.Instance.GetTable<SkillTable>().Get(skillIds[i]);
                    model.hand.Add(new CardInstance(skillRow));
                }
            }
        }

        private void DrawRandomCard(BattleCharacterModel model)
        {
            int randomIdx = UnityEngine.Random.Range(0, model.tableData.SkillIds.Length);
            int skillId = model.tableData.SkillIds[randomIdx];
            var skillRow = TableManager.Instance.GetTable<SkillTable>().Get(skillId);
            var newCard = new CardInstance(skillRow);
            model.hand.Add(newCard);

            var opponent = (model == battleModel.player) ? battleModel.enemy : battleModel.player;
            if (opponent.drawDamageProb.Value > 0)
            {
                if (UnityEngine.Random.value < opponent.drawDamageProb.Value)
                {
                    int damage = opponent.drawDamageValue.Value;
                    model.TakeDamage(damage);
                    Debug.Log($"[Status] DrawDamage Triggered: {damage} damage taken by {model.tableData.id}");
                }
            }
        }

        private void HandleCommand() => Debug.Log("[Battle] Waiting for Player Command...");

        private void BreakOpponentCards(BattleCharacterModel target, int count, int duration)
        {
            if (target.hand.Count == 0) return;

            var availableCards = new List<CardInstance>(target.hand).FindAll(c => !c.IsBroken);
            if (availableCards.Count == 0) return;

            int actualBreakCount = Mathf.Min(availableCards.Count, count);
            for (int i = 0; i < actualBreakCount; i++)
            {
                int idx = UnityEngine.Random.Range(0, availableCards.Count);
                var card = availableCards[idx];
                card.BreakRemainingRound = duration;
                availableCards.RemoveAt(idx);
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

            var npcSelected = BattleLogic.CalculateNPCMove(battleModel);
            foreach (var card in npcSelected)
            {
                battleModel.enemySelectedCards.Add(card);
            }

            foreach (var card in selectedCards)
            {
                battleModel.playerSelectedCards.Add(card);
                // [Fix] 플레이어의 핸드 제거는 이미 View에서 Submit 시점에 되었을 수 있으나 
                // 일관성을 위해 모델 레벨의 핸드 제거는 HandleProcess로 미룸
            }

            ChangePhase(BattlePhase.Process);
        }

        private void HandleProcess()
        {
            // [Fix] 실제 카드 사용 시점에 핸드에서 제거 (증발 방지)
            foreach (var card in battleModel.playerSelectedCards) battleModel.player.hand.Remove(card);
            foreach (var card in battleModel.enemySelectedCards) battleModel.enemy.hand.Remove(card);

            int winner = 0;
            if (battleModel.playerChoseDraw && battleModel.enemyChoseDraw) winner = 0;
            else if (battleModel.playerChoseDraw) winner = 2;
            else if (battleModel.enemyChoseDraw) winner = 1;
            else if (battleModel.playerSelectedCards.Count > 0 && battleModel.enemySelectedCards.Count > 0)
            {
                winner = BattleLogic.DetermineWinner(battleModel.playerSelectedCards[0].TableData.element, 
                                                   battleModel.enemySelectedCards[0].TableData.element);
            }

            Debug.Log($"[Battle] Decision -> Winner: {winner} (P_Draw: {battleModel.playerChoseDraw}, E_Draw: {battleModel.enemyChoseDraw}, P_Cards: {battleModel.playerSelectedCards.Count}, E_Cards: {battleModel.enemySelectedCards.Count})");

            if (battleModel.playerChoseDraw) DrawRandomCard(battleModel.player);
            if (battleModel.enemyChoseDraw) DrawRandomCard(battleModel.enemy);

            bool isRoundEndGame = battleModel.currentRound.Value > battleModel.battleTable.limitRound;
            float pDamage = 0, eDamage = 0;
            if (winner == 1) eDamage = BattleLogic.CalculateDamage(battleModel, 1, isRoundEndGame);
            else if (winner == 2) pDamage = BattleLogic.CalculateDamage(battleModel, 2, isRoundEndGame);
            else if (winner == 0)
            {
                // [Berserk] 무승부 시 상대방에게 DrawDamage 부여
                if (battleModel.player.isSpecialActive && battleModel.player.drawDamageOnTie > 0)
                {
                    float baseDmg = BattleLogic.CalculateTotalBaseDamage(battleModel.playerSelectedCards);
                    if (baseDmg <= 0) baseDmg = 1f;
                    eDamage += baseDmg * battleModel.player.drawDamageOnTie * battleModel.player.attackMultiplier;
                    Debug.Log($"[Berserk] Player Berserk DrawDamage on Tie: {eDamage}");
                }
                if (battleModel.enemy.isSpecialActive && battleModel.enemy.drawDamageOnTie > 0)
                {
                    float baseDmg = BattleLogic.CalculateTotalBaseDamage(battleModel.enemySelectedCards);
                    if (baseDmg <= 0) baseDmg = 1f;
                    pDamage += baseDmg * battleModel.enemy.drawDamageOnTie * battleModel.enemy.attackMultiplier;
                    Debug.Log($"[Berserk] Enemy Berserk DrawDamage on Tie: {pDamage}");
                }
            }

            float pGauge = BattleLogic.CalculateTotalGauge(battleModel.playerSelectedCards);
            float eGauge = BattleLogic.CalculateTotalGauge(battleModel.enemySelectedCards);

            Action applyChain = null;
            if (winner == 1) applyChain = () => BattleLogic.ApplyChainEffects(battleModel, 1, battleModel.playerSelectedCards);
            else if (winner == 2) applyChain = () => BattleLogic.ApplyChainEffects(battleModel, 2, battleModel.enemySelectedCards);

            Action applyResults = () => {
                if (pDamage > 0)
                {
                    var pSkill = TableManager.Instance.GetTable<SkillTable>().Get(battleModel.player.tableData.specialSkillId);
                    if (battleModel.player.isSpecialActive && pSkill?.action == "LoseHeal" && winner == 2)
                    {
                        battleModel.player.Heal(pDamage);
                    }
                    else battleModel.player.TakeDamage(pDamage);
                }

                if (eDamage > 0)
                {
                    var eSkill = TableManager.Instance.GetTable<SkillTable>().Get(battleModel.enemy.tableData.specialSkillId);
                    if (battleModel.enemy.isSpecialActive && eSkill?.action == "LoseHeal" && winner == 1)
                    {
                        battleModel.enemy.Heal(eDamage);
                    }
                    else battleModel.enemy.TakeDamage(eDamage);
                }
                
                if (pGauge > 0) battleModel.player.AddGauge(pGauge);
                if (eGauge > 0) battleModel.enemy.AddGauge(eGauge);

                HandleFeverResults(winner);
                applyChain?.Invoke();
            };

            if (battleView != null)
            {
                // [New] 스페셜 스킬 성공 연출이 필요한지 사전에 판단
                ProductionCommand successCmd = null;

                // 플레이어가 특수 스킬 활성 상태이고, 조건(LoseHeal 등)을 만족하면 성공 컷인 생성
                if (battleModel.player.isSpecialActive)
                {
                    var pSkill = TableManager.Instance.GetTable<SkillTable>().Get(battleModel.player.tableData.specialSkillId);
                    if (pSkill != null) successCmd = GetSpecialSuccessCommand(battleModel.player, pSkill, winner);
                }

                // 적이 특수 스킬 활성 상태이고, 조건을 만족하면 성공 컷인 생성 (플레이어와 동시에 만족할 일은 현재 로직상 드묾)
                if (successCmd == null && battleModel.enemy.isSpecialActive)
                {
                    var eSkill = TableManager.Instance.GetTable<SkillTable>().Get(battleModel.enemy.tableData.specialSkillId);
                    if (eSkill != null) successCmd = GetSpecialSuccessCommand(battleModel.enemy, eSkill, winner);
                }

                float roundDamage = (winner == 1) ? eDamage : ((winner == 2) ? pDamage : 0f);
                battleView.PlayDamageAnimation(winner, battleModel, applyResults, () => {
                    CheckAutoFinisher(() => {
                        if (CheckBattleEnd()) ChangePhase(BattlePhase.Result);
                        else ChangePhase(BattlePhase.RoundEnd);
                    });
                }, roundDamage, successCmd);
            }
            else
            {
                applyResults();
                CheckAutoFinisher(() => {
                    if (CheckBattleEnd()) ChangePhase(BattlePhase.Result);
                    else ChangePhase(BattlePhase.RoundEnd);
                });
            }
        }

        private void HandleFeverResults(int winner)
        {
            if (battleModel.player.isFeverModeActive.Value)
            {
                if (winner == 1) battleModel.player.feverStack.Value++;
                else battleModel.player.EndFeverMode(false);
            }
            if (battleModel.enemy.isFeverModeActive.Value)
            {
                if (winner == 2) battleModel.enemy.feverStack.Value++;
                else battleModel.enemy.EndFeverMode(false);
            }
        }

        private void CheckAutoFinisher(Action onComplete)
        {
            int pLimit = GetLimitFeverRound(battleModel.player);
            int eLimit = GetLimitFeverRound(battleModel.enemy);

            if (battleModel.player.isFeverModeActive.Value && battleModel.player.feverStack.Value >= pLimit)
            {
                float bonusDmg = GetFeverAutoFinisherDamage(battleModel.player);
                battleModel.enemy.TakeDamage(bonusDmg);
                battleModel.player.EndFeverMode(true);
                onComplete?.Invoke();
            }
            else if (battleModel.enemy.isFeverModeActive.Value && battleModel.enemy.feverStack.Value >= eLimit)
            {
                float bonusDmg = GetFeverAutoFinisherDamage(battleModel.enemy);
                battleModel.player.TakeDamage(bonusDmg);
                battleModel.enemy.EndFeverMode(true);
                onComplete?.Invoke();
            }
            else onComplete?.Invoke();
        }

        private void HandleRoundEnd()
        {
            battleModel.player.ResetSpecialEffects();
            battleModel.enemy.ResetSpecialEffects();
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

        private void EndFeverModeWithDamage(BattleCharacterModel charModel, bool success, Action onComplete = null)
        {
            if (!charModel.isFeverModeActive.Value) { onComplete?.Invoke(); return; }

            int stack = charModel.feverStack.Value;
            var opponent = (charModel == battleModel.player) ? battleModel.enemy : battleModel.player;

            if (success && stack > 0)
            {
                float coeff = GetFeverStackDamageCoeff(charModel);
                float bonusDmg = stack * coeff;
                
                if (battleView != null)
                {
                    int winnerIndex = (charModel == battleModel.player) ? 1 : 2;
                    battleView.PlayDamageAnimation(winnerIndex, battleModel, () => {
                        opponent.TakeDamage(bonusDmg);
                        charModel.EndFeverMode(success);
                    }, () => {
                        if (CheckBattleEnd()) ChangePhase(BattlePhase.Result);
                        else onComplete?.Invoke();
                    }, bonusDmg);
                    return;
                }
                else opponent.TakeDamage(bonusDmg);
            }
            charModel.EndFeverMode(success);
            if (CheckBattleEnd()) ChangePhase(BattlePhase.Result);
            else onComplete?.Invoke();
        }

        public void ProcessPlayerEndFever()
        {
            EndFeverModeWithDamage(battleModel.player, true, () => {
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

        private void UpdateStatusDurations(BattleCharacterModel charModel)
        {
            var hand = charModel.hand;
            for (int i = 0; i < hand.Count; i++)
            {
                var card = hand[i];
                if (card.BreakRemainingRound > 0)
                {
                    if (card.BreakRemainingRound <= 1) card.BreakRemainingRound = 0;
                    else card.BreakRemainingRound--;
                }
            }
        }
    }
}
