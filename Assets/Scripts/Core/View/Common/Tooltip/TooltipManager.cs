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

        public void ShowEnvironment(Core.Battle.EnvironmentType envType)
        {
            if (tooltipInstance == null || envType == Core.Battle.EnvironmentType.None) return;

            string name = "";
            string desc = "";

            switch (envType)
            {
                case Core.Battle.EnvironmentType.IrysHope:
                    name = "아이리스의 희망";
                    desc = "최초에 패를 가득(8장) 채워주고, 매 턴 카드를 소모한 만큼 다시 드로우하여 상시 풀 핸드를 유지합니다.";
                    break;
                case Core.Battle.EnvironmentType.MumeiCivilization:
                    name = "무메이의 문명";
                    desc = "카드 제출로 획득하는 스킬 게이지 충전량이 2배로 증가합니다.";
                    break;
            }

            tooltipInstance.SetRawContent("ENVIRONMENT", "STAGE MODIFIER", name, desc);
            tooltipInstance.Show();
        }

        public void Hide()
        {
            if (tooltipInstance != null) tooltipInstance.Hide();
        }
    }
}
