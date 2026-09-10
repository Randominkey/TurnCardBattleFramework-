using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Core.Table;

namespace Core.View
{
    /// <summary>
    /// 배틀 연출 프리팹들이 데이터를 전달받기 위한 인터페이스
    /// </summary>
    public interface IBattleProductionEffect
    {
        void Setup(object data);
    }

    public enum SpecialSkillPhase { Cast, Success }

    /// <summary>
    /// 스페셜 스킬 연출에 필요한 데이터를 묶어 전달하기 위한 구조체
    /// </summary>
    public struct SpecialSkillProductionData
    {
        public BattleCharacterTableRow characterRow;
        public SkillTableRow skillRow;
        public SpecialSkillPhase phase;
    }

    /// <summary>
    /// 스페셜 스킬 발동 시 캐릭터 컷인 이미지를 보여주는 컴포넌트
    /// </summary>
    public class SpecialSkillCutIn : MonoBehaviour, IBattleProductionEffect
    {
        [Header("UI References")]
        [SerializeField] private Image cutInImage;
        [SerializeField] private TextIdHolder skillNameText;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Animation Settings")]
        [SerializeField] private RectTransform mainContainer;
        [SerializeField] private float slideDuration = 0.5f;
        [SerializeField] private float fadeDuration = 0.3f;

        public void Setup(object data)
        {
            if (data is SpecialSkillProductionData skillData)
            {
                if (skillNameText != null && skillData.skillRow != null)
                {
                    skillNameText.SetId(skillData.skillRow.nameStringId);
                }

                if (cutInImage != null && skillData.skillRow != null)
                {
                    // [New] SkillTable의 필드를 우선 사용
                    string spriteName = (skillData.phase == SpecialSkillPhase.Cast) 
                        ? skillData.skillRow.castCutScene 
                        : skillData.skillRow.successCutScene;

                    // Fallback: SkillTable에 정의가 없으면 CharacterTable 시도
                    if (string.IsNullOrEmpty(spriteName) || spriteName == "-1")
                    {
                        spriteName = skillData.characterRow.specialSkillSpriteName;
                        if (skillData.phase == SpecialSkillPhase.Success && !string.IsNullOrEmpty(spriteName) && spriteName != "-1")
                        {
                            spriteName = $"{spriteName}_Success";
                        }
                    }

                    if (!string.IsNullOrEmpty(spriteName) && spriteName != "-1")
                    {
                        var sprite = Core.Utils.ResourceUtil.LoadSpriteWithFallback($"Sprites/Characters/Battle/{spriteName}", cutInImage, skillData.characterRow?.KeyName ?? CharacterKeyName.None);
                        
                        // 성공 스프라이트가 없으면 Cast용이나 기본 이름으로 Fallback 시도
                        if (sprite == null && skillData.phase == SpecialSkillPhase.Success)
                        {
                            string castSprite = skillData.skillRow.castCutScene;
                            if (string.IsNullOrEmpty(castSprite) || castSprite == "-1") castSprite = skillData.characterRow.specialSkillSpriteName;
                            sprite = Core.Utils.ResourceUtil.LoadSpriteWithFallback($"Sprites/Characters/Battle/{castSprite}", cutInImage, skillData.characterRow?.KeyName ?? CharacterKeyName.None);
                        }

                        if (sprite == null)
                        {
                            // [Fix] 프리팹에 구워져 있는 이전 캐릭터(마린 등)의 잔재 이미지를 지우고 정직한 Red 틴트 적용
                            var images = GetComponentsInChildren<Image>(true);
                            foreach (var img in images)
                            {
                                if (img == cutInImage || img.name.Contains("Illust") || img.name.Contains("Char") || img.name.Contains("CutIn") || img.name.Contains("Body"))
                                {
                                    img.sprite = null;
                                    img.color = Color.white;
                                    img.gameObject.SetActive(true);
                                }
                            }
                        }
                    }
                    else
                    {
                        cutInImage.gameObject.SetActive(false);
                    }
                }
            }

            PlayAnimation();
        }

        private void PlayAnimation()
        {
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (mainContainer != null) mainContainer.anchoredPosition = new Vector2(-1000f, 0f);

            Sequence seq = DOTween.Sequence();
            
            // 1. 등장 (슬라이드 + 페이드)
            if (canvasGroup != null) seq.Append(canvasGroup.DOFade(1f, fadeDuration));
            if (mainContainer != null) seq.Join(mainContainer.DOAnchorPos(Vector2.zero, slideDuration).SetEase(Ease.OutCubic));

            // 2. 유지 (BattleProductionManager에서 duration만큼 대기하므로 여기선 유지)
            
            // 3. 퇴장은 BattleProductionManager가 오브젝트를 파괴/비활성화할 때 처리됨.
        }
    }
}
