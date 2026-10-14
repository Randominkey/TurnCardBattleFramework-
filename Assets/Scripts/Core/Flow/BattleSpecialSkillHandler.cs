using UnityEngine;
using Core.Table;
using Core.Battle;
using Core.View;
using Core.Flow.SpecialSkill;
using System.Collections;
using System.Collections.Generic;

namespace Core.Flow
{
    /// <summary>
    /// 캐릭터별 고유 특수 스킬(Special Skill) 시전 및 연출을 총괄하는 오케스트레이터.
    /// 구체적인 스킬 로직은 ISpecialSkillAction 전략 구현체들로 위임합니다.
    /// </summary>
    public class BattleSpecialSkillHandler
    {
        private readonly BattleModel battleModel;
        private readonly BattleView battleView;
        private readonly BattleChainActionHandler chainHandler;
        private readonly Dictionary<string, SpecialSkillActionBase> actions = new Dictionary<string, SpecialSkillActionBase>();

        public BattleSpecialSkillHandler(BattleModel battleModel, BattleView battleView, BattleChainActionHandler chainHandler)
        {
            this.battleModel = battleModel;
            this.battleView = battleView;
            this.chainHandler = chainHandler;

            RegisterActions();
        }

        private void RegisterActions()
        {
            RegisterAction(new BreakCardSkillAction());
            RegisterAction(new BerserkSkillAction());
            RegisterAction(new MixReloadSkillAction());
            RegisterAction(new AlignReloadSkillAction());
            RegisterAction(new RainDrawSkillAction());
            RegisterAction(new LoseHealSkillAction());
        }

        public void RegisterAction(SpecialSkillActionBase action)
        {
            if (action != null && !string.IsNullOrEmpty(action.ActionName))
            {
                actions[action.ActionName.ToLower()] = action;
            }
        }

        /// <summary>
        /// 특수 스킬을 발동합니다. 피버 모드이거나 RainTime 등으로 봉쇄된 상태에서는 발동이 거부됩니다.
        /// </summary>
        public void ExecuteSpecialSkill(BattleCharacterModel owner)
        {
            if (owner == null) return;

            // [New 260921] 피버 모드이거나 RainTime 등으로 스킬이 봉쇄된 경우 발동 불가
            if (!owner.CanUseSpecialSkill)
            {
                Debug.LogWarning($"[Special] {owner.tableData.id} cannot use special skill (Blocked: {owner.isSpecialSkillBlocked.Value}, Fever: {owner.isFeverModeActive.Value})!");
                return;
            }

            owner.UseSpecialGauge();
            
            var skillRow = TableManager.Instance.GetTable<SkillTable>()?.Get(owner.tableData.specialSkillId);
            if (skillRow == null) return;

            // 스페셜 스킬 발동 연출 실행
            PlaySpecialSkillProduction(owner, skillRow);

            // 전략 패턴 실행
            string actionKey = skillRow.action?.ToLower() ?? "";
            var context = new SpecialSkillContext(battleModel, battleView, chainHandler, owner, skillRow);

            if (actions.TryGetValue(actionKey, out var skillAction))
            {
                skillAction.Execute(context);
                Debug.Log($"[Special] {owner.tableData.id} executed Special Skill Action: {actionKey}");
            }
            else
            {
                Debug.LogWarning($"[Special] No ISpecialSkillAction registered for action: '{actionKey}'");
            }
        }

        private void PlaySpecialSkillProduction(BattleCharacterModel owner, SkillTableRow skillRow)
        {
            if (battleView == null || battleView.ProductionManager == null) return;

            // Buff 타입은 발동 즉시 Success 연출, Require 타입은 Cast 연출 실행
            bool isBuff = (skillRow.element == SkillElement.Buff);
            ProductionType pType = isBuff ? ProductionType.SpecialSkillSuccess : ProductionType.SpecialSkillCast;
            SpecialSkillPhase phase = isBuff ? SpecialSkillPhase.Success : SpecialSkillPhase.Cast;

            var productionData = new SpecialSkillProductionData {
                characterRow = owner.tableData,
                skillRow = skillRow,
                phase = phase
            };
            
            IEnumerator PlaySpecialSkillRoutine()
            {
                yield return battleView.ProductionManager.PlayProduction(pType, owner.tableData.KeyName, null, productionData);
                if (owner == battleModel.player)
                {
                    Core.Tutorial.TutorialManager.Instance?.NotifySpecialSkillCastFinished();
                }
            }
            battleView.ProductionManager.StartCoroutine(PlaySpecialSkillRoutine());

            // Buff 타입 스킬 발동 시 캐릭터 포즈 변경 연출 추가
            if (isBuff)
            {
                battleView.StartCoroutine(battleView.PlaySpecialPose(owner));
            }
        }

        /// <summary>
        /// 격돌 결과에 따라 스페셜 스킬 성공 컷인 연출 커맨드를 생성합니다.
        /// </summary>
        public ProductionCommand GetSpecialSuccessCommand(BattleCharacterModel owner, SkillTableRow skillRow, int winnerIndex)
        {
            if (battleView == null || battleView.ProductionManager == null || owner == null || skillRow == null) return null;

            string actionKey = skillRow.action?.ToLower() ?? "";
            var context = new SpecialSkillContext(battleModel, battleView, chainHandler, owner, skillRow);

            bool isSuccess = false;
            if (actions.TryGetValue(actionKey, out var skillAction))
            {
                isSuccess = skillAction.CheckSuccess(context, winnerIndex);
            }
            else
            {
                // Fallback: 일반 Require 스킬은 승리 시 성공
                if (skillRow.element == SkillElement.Require)
                {
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
    }
}
