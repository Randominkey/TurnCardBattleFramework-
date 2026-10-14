using System.Collections.Generic;
using Core.Table;

namespace Core.Battle
{
    /// <summary>
    /// 배틀 중 생성되는 개별 카드의 실시간 인스턴스.
    /// 정적인 데이터(SkillTableRow) 외에 런타임에 변하는 상태(버프, 디버프 등)를 가집니다.
    /// </summary>
    public class CardInstance
    {
        // 원본 테이블 데이터 참조
        public SkillTableRow TableData { get; private set; }
        
        // [New] BreakCard 효과: 일정 라운드 동안 사용 불가
        public int BreakRemainingRound { get; set; }
        public bool IsBroken => BreakRemainingRound > 0;

        // [New 260921] 동적 속성 변환 (Rain, Fool 카드 변환 등 지원)
        public SkillElement? OverrideElement { get; private set; }
        public SkillElement EffectiveElement => OverrideElement ?? (TableData != null ? TableData.element : SkillElement.None);

        // [New 261011] Rain 효과는 속성이 아닌 상태 효과(디버프)로 적용됨 (카드의 묵/찌/빠 속성 유지)
        public bool IsRain { get; private set; }
        public bool HasRainEffect => IsRain;

        // [New 260921] 특수 속성 여부 (Fool은 가위바위보에 자동 패배)
        public bool IsFool => EffectiveElement == SkillElement.Fool;
        public bool IsSpecial => IsFool;
        // public bool IsJoker => EffectiveElement == SkillElement.Joker; // 기획 변경에 따라 임시 보류

        public CardInstance(SkillTableRow tableData)
        {
            this.TableData = tableData;
            this.OverrideElement = null;
            this.IsRain = false;
        }

        /// <summary>
        /// 카드에 Rain 효과를 부여합니다 (묵/찌/빠 속성은 유지되며, 제출 시 코보의 Rain 스택 증가).
        /// </summary>
        public void ApplyRainEffect()
        {
            IsRain = true;
        }

        public void RemoveRainEffect()
        {
            IsRain = false;
        }

        /// <summary>
        /// 하위 호환성 유지용 Rain 부여 메서드
        /// </summary>
        public void ConvertToRain(SkillTableRow rainTableData = null)
        {
            IsRain = true;
        }

        /// <summary>
        /// 카드의 속성을 Fool로 강제 변환합니다 (RainTime 등).
        /// </summary>
        public void ConvertToFool(SkillTableRow foolTableData = null)
        {
            OverrideElement = SkillElement.Fool;
            if (foolTableData != null)
            {
                TableData = foolTableData;
            }
        }

        /// <summary>
        /// 최종 데미지 계산
        /// ActionValues[0] 이 기본 데미지임.
        /// </summary>
        public float GetFinalDamage()
        {
            if (TableData.ActionValues == null || TableData.ActionValues.Length < 1) return 0;
            return UnityEngine.Mathf.Max(0, TableData.ActionValues[0]);
        }

        /// <summary>
        /// 최종 게이지 획득량 계산
        /// ActionValues[1] 이 기본 게이지임.
        /// </summary>
        public float GetFinalGauge()
        {
            if (TableData.ActionValues == null || TableData.ActionValues.Length < 2) return 0;
            return UnityEngine.Mathf.Max(0, TableData.ActionValues[1]);
        }
    }
}
