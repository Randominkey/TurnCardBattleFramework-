using UnityEngine;
using Core.Utils;
using Core.Table;
using Core.Common;

namespace Core.View
{
    /// <summary>
    /// 툴팁 시스템의 전역 관리자.
    /// 테이블의 ID를 TooltipWindow에 전달하여 TextIdHolder가 다국어를 처리하도록 합니다.
    /// </summary>
    public class TooltipManager : Singleton<TooltipManager>
    {
        private TooltipWindow tooltipInstance;

        /// <summary>
        /// 툴팁 시스템 초기화
        /// </summary>
        public void Setup(TooltipWindow prefab, Transform parent)
        {
            if (tooltipInstance != null) return;
            
            tooltipInstance = Instantiate(prefab, parent);
            tooltipInstance.Hide();
        }

        public void ShowSkill(int skillId)
        {
            if (tooltipInstance == null) return;

            var row = TableManager.Instance.GetTable<SkillTable>().Get(skillId);
            if (row == null) return;

            tooltipInstance.SetContent(
                GameConstants.STR_ID_HEADER_SKILL, 
                GameConstants.STR_ID_SUBHEADER_SPECIAL_SKILL, 
                row.nameStringId, 
                row.descStringId);
            
            tooltipInstance.Show();
        }

        public void ShowChain(int chainId)
        {
            if (tooltipInstance == null) return;

            var row = TableManager.Instance.GetTable<ChainTable>().Get(chainId);
            if (row == null) return;

            tooltipInstance.SetContent(
                GameConstants.STR_ID_HEADER_CHAIN, 
                GameConstants.STR_ID_SUBHEADER_CHAIN_SKILL, 
                row.nameStringId, 
                row.descStringId);
            
            tooltipInstance.Show();
        }

        public void Hide()
        {
            if (tooltipInstance != null) tooltipInstance.Hide();
        }
    }
}
