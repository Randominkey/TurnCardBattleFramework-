using System.Collections.Generic;
using UniRx;
using Core.Table;
using UnityEngine;

namespace Core.Battle
{
    /// <summary>
    /// 전투 중인 캐릭터의 실시간 상태 데이터
    /// </summary>
    public class BattleCharacterModel
    {
        public BattleCharacterTableRow tableData { get; private set; }
        
        public ReactiveProperty<float> hp { get; } = new ReactiveProperty<float>();
        public ReactiveProperty<float> specialGauge { get; } = new ReactiveProperty<float>();
        public ReactiveProperty<float> chainGauge { get; } = new ReactiveProperty<float>();
        public ReactiveProperty<BattleCharacterHPState> hpState { get; } = new ReactiveProperty<BattleCharacterHPState>();

        // 수정: 단순 테이블 행 리스트가 아닌 인스턴스 리스트로 변경
        public ReactiveCollection<CardInstance> hand { get; } = new ReactiveCollection<CardInstance>();

        // [New] 드로우 시 데미지(DrawDamage) 관련 상태 데이터
        public ReactiveProperty<float> drawDamageProb { get; } = new ReactiveProperty<float>(0f);
        public ReactiveProperty<int> drawDamageValue { get; } = new ReactiveProperty<int>(0);

        /// <summary>
        /// 이번 라운드에 스페셜 스킬을 활성화했는지 여부
        /// </summary>
        public bool isSpecialActive { get; set; }

        // [New] 스페셜 스킬 효과 배율 (Berserk 등 대응)
        public float attackMultiplier { get; set; } = 1.0f;
        public float defenseMultiplier { get; set; } = 1.0f;
        public float drawDamageOnTie { get; set; } = 0f;

        /// <summary>
        /// 체인 게이지로 인한 특수 피버 모드 활성화 여부
        /// </summary>
        public ReactiveProperty<bool> isFeverModeActive { get; } = new ReactiveProperty<bool>(false);

        public ReactiveProperty<int> feverStack { get; } = new ReactiveProperty<int>(0);
        public int remainingFeverRound { get; set; }

        public BattleCharacterModel(BattleCharacterTableRow data)
        {
            tableData = data;
            hp.Value = data.maxHp;
            specialGauge.Value = 0;
            chainGauge.Value = 0;
            hpState.Value = BattleCharacterHPState.FullCondition;
            drawDamageProb.Value = 0f;
            drawDamageValue.Value = 1;
            isSpecialActive = false;
            attackMultiplier = 1.0f;
            defenseMultiplier = 1.0f;
            drawDamageOnTie = 0f;
            isFeverModeActive.Value = false;
            feverStack.Value = 0;
            remainingFeverRound = 0;
        }

        public void UpdateDrawDamage(float probInc, int damageInc)
        {
            drawDamageProb.Value = Mathf.Min(1.0f, drawDamageProb.Value + probInc);
            drawDamageValue.Value += damageInc;
            Debug.Log($"[DrawDamage] {tableData.id} Status Updated: Prob {drawDamageProb.Value}, Damage {drawDamageValue.Value}");
        }

        public void ResetDrawDamage()
        {
            drawDamageProb.Value = 0f;
            drawDamageValue.Value = 1;
        }

        public void TakeDamage(float amount)
        {
            float finalDamage = amount * defenseMultiplier;
            hp.Value = Mathf.Max(0, hp.Value - finalDamage);
            UpdateHpState();
            if (finalDamage > 0) Debug.Log($"[Battle] {tableData.id} took {finalDamage} damage (Original: {amount}, Multiplier: {defenseMultiplier})");
        }

        public void Heal(float amount)
        {
            hp.Value = Mathf.Min(tableData.maxHp, hp.Value + amount);
            UpdateHpState();
        }

        public void AddGauge(float amount)
        {
            specialGauge.Value = Mathf.Min(tableData.specialSkillGauge, specialGauge.Value + amount);
        }

        public void AddChainGauge(float amount)
        {
            // [Fix] 마린 전용 제한 제거. 대신 체인 액션이 Fever인 경우에만 게이지 추가.
            var chainRow = TableManager.Instance.GetTable<ChainTable>().Get(tableData.chainId);
            bool isFeverType = chainRow != null && chainRow.action.Equals("Fever", System.StringComparison.OrdinalIgnoreCase);
            
            if (!isFeverType) return;

            // [New] ChainSetValues 우선 참조, 부재 시 GameSetTable Fallback
            float maxGauge = 100f;
            if (chainRow != null && chainRow.ChainSetValues != null && chainRow.ChainSetValues.Length > 0)
            {
                maxGauge = chainRow.ChainSetValues[0];
            }
            else
            {
                var gameSet = TableManager.Instance.GetTable<GameSetTable>().Get(0);
                maxGauge = gameSet?.maxFeverGauge ?? 100f;
            }

            chainGauge.Value = Mathf.Min(maxGauge, chainGauge.Value + amount);
            Debug.Log($"[ChainGauge] {tableData.id} Gained {amount} Gauge. Total: {chainGauge.Value} / {maxGauge}");
            
            // 라운드 시작 단계에서 활성화 여부를 체크하도록 로직 이관 (여기서는 게지만 증가)
        }

        public void ActivateFeverMode(int duration)
        {
            isFeverModeActive.Value = true;
            feverStack.Value = 0;
            remainingFeverRound = duration;
            chainGauge.Value = 0; // 활성화 시 게이지 소모
            Debug.Log($"[ChainGauge] {tableData.id} FEVER MODE ACTIVATED! Duration: {duration}");
        }

        public void EndFeverMode(bool success)
        {
            isFeverModeActive.Value = false;
            chainGauge.Value = 0;
            remainingFeverRound = 0;
            if (!success) feverStack.Value = 0;
            Debug.Log($"[ChainGauge] {tableData.id} FEVER MODE ENDED. Success: {success}, Final Stack: {feverStack.Value}");
        }

        /// <summary>
        /// 게이지를 소모하여 스페셜 스킬을 활성화합니다.
        /// </summary>
        public void UseSpecialGauge()
        {
            specialGauge.Value = 0;
            isSpecialActive = true;
            
            var skillRow = TableManager.Instance.GetTable<SkillTable>().Get(tableData.specialSkillId);
            if (skillRow != null)
            {
                if (skillRow.action.Equals("Berserk", System.StringComparison.OrdinalIgnoreCase))
                {
                    // ActionValues: [0] DamageMultiple, [1] DrawDamage, [2] HitMultiple
                    float damageMultiple = (skillRow.ActionValues != null && skillRow.ActionValues.Length > 0) ? skillRow.ActionValues[0] : 2.0f;
                    float drawDamageVal = (skillRow.ActionValues != null && skillRow.ActionValues.Length > 1) ? skillRow.ActionValues[1] : 1.0f;
                    float hitMultiple = (skillRow.ActionValues != null && skillRow.ActionValues.Length > 2) ? skillRow.ActionValues[2] : 2.0f;

                    attackMultiplier = damageMultiple;
                    drawDamageOnTie = drawDamageVal;
                    defenseMultiplier = hitMultiple;

                    Debug.Log($"[Berserk] {tableData.id} Activated Berserk! AttackMult: {attackMultiplier}, DrawDmgOnTie: {drawDamageOnTie}, DefMult: {defenseMultiplier}");
                }
            }

            Debug.Log($"[Battle] {tableData.id} used Special Skill: {skillRow?.action ?? "Unknown"}!");
        }

        public bool IsGaugeFull()
        {
            return specialGauge.Value >= tableData.specialSkillGauge;
        }

        /// <summary>
        /// 피격 시 사용할 스프라이트 인덱스 결정
        /// 스페셜 스킬 활성화 시 1번(회복 연출), 평상시 0번
        /// </summary>
        public int GetHitSpriteIndex()
        {
            // [Fix] 액션명이 LoseHeal인 경우에만 1번(회복) 스프라이트 사용
            var skillRow = TableManager.Instance.GetTable<SkillTable>().Get(tableData.specialSkillId);
            bool isLoseHeal = skillRow != null && skillRow.action.Equals("LoseHeal", System.StringComparison.OrdinalIgnoreCase);
            
            return (isLoseHeal && isSpecialActive) ? 1 : 0;
        }

        private void UpdateHpState()
        {
            float ratio = hp.Value / tableData.maxHp;
            if (ratio > 0.5f) hpState.Value = BattleCharacterHPState.FullCondition;
            else if (ratio > 0.2f) hpState.Value = BattleCharacterHPState.HalfCondition;
            else hpState.Value = BattleCharacterHPState.AboutToDie;
        }

        public void ResetSpecialEffects()
        {
            isSpecialActive = false;
            attackMultiplier = 1.0f;
            defenseMultiplier = 1.0f;
            drawDamageOnTie = 0f;
        }
    }

    /// <summary>
    /// 전투 한 판의 전체 상태 데이터 (MVC의 Model)
    /// </summary>
    public class BattleModel
    {
        public BattleTableRow battleTable { get; private set; }
        
        // 현재 라운드
        public ReactiveProperty<int> currentRound { get; } = new ReactiveProperty<int>(1);
        
        // 플레이어와 적의 데이터
        public BattleCharacterModel player { get; private set; }
        public BattleCharacterModel enemy { get; private set; }

        // 수정: 이번 라운드에 선택된 카드 인스턴스들
        public List<CardInstance> playerSelectedCards { get; } = new List<CardInstance>();
        public List<CardInstance> enemySelectedCards { get; } = new List<CardInstance>();

        // 이번 라운드 드로우 선택 여부
        public bool playerChoseDraw { get; set; }
        public bool enemyChoseDraw { get; set; }

        public BattleModel(BattleTableRow battleTable, BattleCharacterTableRow pData, BattleCharacterTableRow eData)
        {
            this.battleTable = battleTable;
            player = new BattleCharacterModel(pData);
            enemy = new BattleCharacterModel(eData);
        }

        public void ResetRoundSelection()
        {
            playerSelectedCards.Clear();
            enemySelectedCards.Clear();
            playerChoseDraw = false;
            enemyChoseDraw = false;
        }
    }
}
