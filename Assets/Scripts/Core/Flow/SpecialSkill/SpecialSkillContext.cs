using Core.Battle;
using Core.Table;
using Core.View;

namespace Core.Flow.SpecialSkill
{
    /// <summary>
    /// 스페셜 스킬 실행에 필요한 배틀 모델, 뷰, 체인 핸들러 및 시전자/상대방 정보를 담는 컨텍스트 DTO
    /// </summary>
    public class SpecialSkillContext
    {
        public BattleModel BattleModel { get; }
        public BattleView BattleView { get; }
        public BattleChainActionHandler ChainHandler { get; }
        public BattleCharacterModel User { get; }
        public BattleCharacterModel Opponent { get; }
        public SkillTableRow SkillRow { get; }

        public SpecialSkillContext(
            BattleModel battleModel, 
            BattleView battleView, 
            BattleChainActionHandler chainHandler, 
            BattleCharacterModel user, 
            SkillTableRow skillRow)
        {
            BattleModel = battleModel;
            BattleView = battleView;
            ChainHandler = chainHandler;
            User = user;
            Opponent = (user == battleModel?.player) ? battleModel?.enemy : battleModel?.player;
            SkillRow = skillRow;
        }
    }
}
