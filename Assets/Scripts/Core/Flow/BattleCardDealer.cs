using UnityEngine;
using Core.Table;
using Core.Battle;
using System.Collections.Generic;

namespace Core.Flow
{
    /// <summary>
    /// 배틀 중 카드 분배, 랜덤 드로우, 상태 이상 턴 관리 등 덱 및 핸드 라이프사이클을 전담하는 도메인 서비스 클래스
    /// </summary>
    public class BattleCardDealer
    {
        private readonly BattleModel battleModel;
        private readonly BattleChainActionHandler chainHandler;
        private readonly Core.Tutorial.TutorialBattleScriptController tutorialScriptController;
        private readonly int executeId;

        public SkillElement PlayerFirstRoundElement { get; set; } = SkillElement.Rock;

        public BattleCardDealer(
            BattleModel battleModel, 
            BattleChainActionHandler chainHandler, 
            Core.Tutorial.TutorialBattleScriptController tutorialScriptController, 
            int executeId)
        {
            this.battleModel = battleModel;
            this.chainHandler = chainHandler;
            this.tutorialScriptController = tutorialScriptController;
            this.executeId = executeId;
        }

        /// <summary>
        /// 라운드 시작 시 환경(아이리스 풀핸드 등) 및 기본 드로우 규칙에 따라 양측 카드 일괄 분배
        /// </summary>
        public void ProcessRoundStartDraw()
        {
            if (battleModel == null) return;

            var gameSet = TableManager.Instance.GetTable<GameSetTable>().Get(0);
            int drawCount = (battleModel.currentRound.Value == 1) ? 0 : (gameSet?.roundDrawCards ?? 1);
            int maxCards = gameSet?.maxCards ?? 8;

            if (battleModel.currentRound.Value == 1)
            {
                DrawInitialCards(battleModel.player);
                DrawInitialCards(battleModel.enemy);
            }
            else
            {
                // [New 261013] 아이리스의 희망 환경: 사용한 만큼 다시 드로우하여 상시 풀 핸드 유지
                if (battleModel.currentEnvironment.Value == EnvironmentType.IrysHope)
                {
                    while (battleModel.player.hand.Count < maxCards) DrawRandomCard(battleModel.player);
                    while (battleModel.enemy.hand.Count < maxCards) DrawRandomCard(battleModel.enemy);
                }
                else
                {
                    for (int i = 0; i < drawCount; i++)
                    {
                        if (battleModel.player.hand.Count < maxCards) DrawRandomCard(battleModel.player);
                        if (battleModel.enemy.hand.Count < maxCards) DrawRandomCard(battleModel.enemy);
                    }
                }
            }
        }

        /// <summary>
        /// 캐릭터의 초기 핸드 구성 (테이블 데이터 기준 또는 튜토리얼 스크립트 기준)
        /// </summary>
        public void DrawInitialCards(BattleCharacterModel model)
        {
            if (model == null || battleModel == null) return;

            // 튜토리얼 스크립트 활성화 및 1라운드인 경우 고정 핸드 세팅
            if (tutorialScriptController != null && tutorialScriptController.IsApplicable(executeId, 1))
            {
                if (model == battleModel.player)
                {
                    tutorialScriptController.SetupInitialHands(battleModel.player, battleModel.enemy);
                }
                return;
            }

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

            // [New 261013] 아이리스의 희망 환경: 최초 시작 시 손패를 최대치(8장)까지 가득 채움
            if (battleModel.currentEnvironment.Value == EnvironmentType.IrysHope)
            {
                while (model.hand.Count < maxCards)
                {
                    DrawRandomCard(model);
                }
            }
        }

        /// <summary>
        /// 캐릭터 1인에게 카드 1장 랜덤 드로우 (튜토리얼/RainTime Fool/일반 랜덤 및 드로우 체인 발동)
        /// </summary>
        public CardInstance DrawRandomCard(BattleCharacterModel model)
        {
            if (model == null || battleModel == null) return null;

            CardInstance newCard = null;
            int currentRound = battleModel.currentRound.Value;

            // 튜토리얼 지정 드로우 확인
            if (tutorialScriptController != null && tutorialScriptController.IsApplicable(executeId, currentRound))
            {
                bool isPlayer = (model == battleModel.player);
                newCard = tutorialScriptController.GetScriptedDrawCard(isPlayer, currentRound, PlayerFirstRoundElement);
            }

            if (newCard == null)
            {
                // [RainTime 체크]
                // 상대방이 RainTime을 활성화한 상태라면, 이 캐릭터가 드로우하는 모든 카드는 Fool 카드로 강제 전환!
                var opponent = (model == battleModel.player) ? battleModel.enemy : battleModel.player;
                if (opponent != null && opponent.isRainTimeActive.Value)
                {
                    var foolSkillRow = chainHandler?.GetFoolSkillRow(opponent);
                    newCard = new CardInstance(foolSkillRow);
                    Debug.Log($"[Chain: RainTime] {model.tableData.id} drew a forced Fool card due to opponent's RainTime!");
                }
                else
                {
                    int randomIdx = UnityEngine.Random.Range(0, model.tableData.SkillIds.Length);
                    int skillId = model.tableData.SkillIds[randomIdx];
                    var skillRow = TableManager.Instance.GetTable<SkillTable>().Get(skillId);
                    newCard = new CardInstance(skillRow);
                }
            }

            model.hand.Add(newCard);

            // [체인 타이밍 1] 라운드 플로우 - 드로우 체인 효과 발동
            chainHandler?.OnDraw(model);

            return newCard;
        }

        /// <summary>
        /// 핸드 내 카드의 Break 상태 이상 지속 턴 차감 (단일 캐릭터)
        /// </summary>
        public void UpdateStatusDurations(BattleCharacterModel charModel)
        {
            if (charModel == null) return;

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

        /// <summary>
        /// 플레이어와 적 양측의 Break 상태 이상 지속 턴 일괄 차감
        /// </summary>
        public void UpdateAllStatusDurations()
        {
            if (battleModel == null) return;
            UpdateStatusDurations(battleModel.player);
            UpdateStatusDurations(battleModel.enemy);
        }
    }
}

