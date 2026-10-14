using UnityEngine;
using UnityEngine.UI;
using Core.Table;
using System.Linq;

namespace Core.View
{
    public enum CharacterViewMode
    {
        Speak,
        Battle
    }

    public class CharacterView : MonoBehaviour
    {
        [SerializeField] private CharacterViewMode viewMode;
        [SerializeField] private Image bodyImage;
        [SerializeField] private Image faceImage;

        private CharacterKeyName currentKeyName = CharacterKeyName.None;
        private TableRow cachedRow;

        public void Setup(CharacterKeyName keyName)
        {
            if (keyName == CharacterKeyName.None)
            {
                gameObject.SetActive(false);
                currentKeyName = CharacterKeyName.None;
                return;
            }

            gameObject.SetActive(true);

            // [Optimization] 동일 캐릭터면 셋업 건너뜀 (이미지 로딩 최소화)
            if (currentKeyName == keyName && cachedRow != null)
            {
                return;
            }

            currentKeyName = keyName;
            if (viewMode == CharacterViewMode.Speak)
                SetupSpeak(keyName);
            else
                SetupBattle(keyName);
        }

        private void SetupSpeak(CharacterKeyName keyName)
        {
            var table = TableManager.Instance.GetTable<CharacterTable>();
            var row = table.rows.FirstOrDefault(r => r.keyName == keyName);
            cachedRow = row;

            if (row == null) 
            {
                Debug.LogWarning($"[CharacterView] Cannot find Speak data for {keyName}");
                return;
            }

            // [Refactor] 전신 이미지 방식 전환: faceImage 비활성화
            if (faceImage != null) faceImage.gameObject.SetActive(false);

            // 초기 설정 시 기본 표정(Default) 적용
            SetFace(CharacterFaceName.Default);
        }

        private void SetupBattle(CharacterKeyName keyName)
        {
            var table = TableManager.Instance.GetTable<BattleCharacterTable>();
            var row = table.rows.FirstOrDefault(r => r.KeyName == keyName || r.id == (int)keyName + 1001);
            cachedRow = row;

            if (row == null) return;

            if (row.DefaultSpriteNames.Length > 0)
            {
                Core.Utils.ResourceUtil.LoadSpriteWithFallback($"Sprites/Characters/Battle/{row.DefaultSpriteNames[0]}", bodyImage, keyName);
            }
        }

        public void SetFace(CharacterFaceName faceName)
        {
            if (viewMode == CharacterViewMode.Speak && cachedRow is CharacterTableRow speakRow)
            {
                // [Refactor] 전신 이미지 방식: bodyImage에 표정별 전신 이미지 직접 할당
                int index = (int)faceName; 
                string spriteName = (index < speakRow.FaceSpriteNames.Length && !string.IsNullOrEmpty(speakRow.FaceSpriteNames[index])) 
                                    ? speakRow.FaceSpriteNames[index] 
                                    : speakRow.bodySpriteName; // 없으면 기본 바디 사용

                var fullBodySprite = Resources.Load<Sprite>($"Sprites/Characters/Speak/{spriteName}");
                if (bodyImage != null)
                {
                    bodyImage.sprite = fullBodySprite;
                    bodyImage.gameObject.SetActive(fullBodySprite != null);
                }

                // faceImage는 계속 비활성화 상태 유지
                if (faceImage != null) faceImage.gameObject.SetActive(false);
            }
        }

        public void SetHighlight(bool isHighlight)
        {
            // 기획서: "Speaker를 제외한 모든 캐릭터 밝기 -50%"
            Color targetColor = isHighlight ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f);
            if (bodyImage != null) bodyImage.color = targetColor;
            // faceImage는 비활성화 상태이므로 무시하거나 함께 처리
            if (faceImage != null && faceImage.gameObject.activeSelf) faceImage.color = targetColor;
        }
    }
}
