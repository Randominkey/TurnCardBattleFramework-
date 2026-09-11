using UnityEngine;
using UnityEngine.UI;
using Core.Table;

namespace Core.View
{
    /// <summary>
    /// 대화 로그의 개별 항목(메시지 그룹)을 관리하는 뷰 클래스
    /// </summary>
    public class SpeakLogItemView : MonoBehaviour
    {
        [SerializeField] private Image portraitImage;
        [SerializeField] private TextIdHolder speakerNameHolder;
        [SerializeField] private TextIdHolder messageHolder;

        /// <summary>
        /// 로그 데이터를 UI에 바인딩합니다.
        /// TextIdHolder를 통해 다국어 대응 및 자동 갱신을 지원합니다.
        /// </summary>
        public void SetLog(CharacterKeyName key, int nameId, int messageId, string hexCode = null)
        {
            if (speakerNameHolder != null)
            {
                if (!string.IsNullOrEmpty(hexCode))
                    speakerNameHolder.SetIdWithColor(nameId, hexCode);
                else
                    speakerNameHolder.SetId(nameId);
            }

            if (messageHolder != null) messageHolder.SetId(messageId);

            if (portraitImage != null)
            {
                var charTable = TableManager.Instance.GetTable<CharacterTable>();
                var row = charTable.rows.Find(r => r.keyName == key);
                
                if (row != null && !string.IsNullOrEmpty(row.characterFaceIconName))
                {
                    // 로그 전용 아이콘 경로 우선 시도, 없을 경우 Speak 경로 시도
                    var sprite = Resources.Load<Sprite>($"Sprites/Characters/LogIcons/{row.characterFaceIconName}");
                    if (sprite == null) sprite = Resources.Load<Sprite>($"Sprites/Characters/Speak/{row.characterFaceIconName}");
                    
                    portraitImage.sprite = sprite;
                    portraitImage.gameObject.SetActive(sprite != null);
                }
                else
                {
                    portraitImage.gameObject.SetActive(false);
                }
            }
        }
    }
}
