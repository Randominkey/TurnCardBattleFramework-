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

        public CardInstance(SkillTableRow tableData)
        {
            this.TableData = tableData;
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
