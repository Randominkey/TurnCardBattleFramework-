using UnityEngine;
using TMPro;
using UniRx;
using Core.Table;

namespace Core.View
{
    /// <summary>
    /// 지정된 StringTable 또는 ScenarioTextTable ID를 바탕으로 텍스트를 자동으로 갱신해주는 컴포넌트
    /// </summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class TextIdHolder : MonoBehaviour
    {
        [SerializeField] private int stringId;
        private string colorCode;
        private TextMeshProUGUI textComponent;

        public TextMeshProUGUI TextComponent 
        {
            get 
            {
                if (textComponent == null) textComponent = GetComponent<TextMeshProUGUI>();
                return textComponent;
            }
        }

        private void Awake()
        {
            if (textComponent == null) textComponent = GetComponent<TextMeshProUGUI>();
            
            TableManager.Instance.currentLanguage
                .Subscribe(lang => UpdateText(lang))
                .AddTo(this);
        }

        public string GetLocalizedText(int id)
        {
            if (id <= 0) return "";

            var stringTable = TableManager.Instance.GetTable<StringTable>();
            var row = stringTable.Get(id);

            if (row == null)
            {
                var scenarioTable = TableManager.Instance.GetTable<ScenarioTextTable>();
                row = scenarioTable.Get(id);
            }

            return row != null ? row.Get(TableManager.Instance.currentLanguage.Value) : $"ID:{id}";
        }

        private void UpdateText(Language lang)
        {
            if (TextComponent == null) return;

            if (stringId <= 0) return;

            string localizedText = GetLocalizedText(stringId);
            
            if (!string.IsNullOrEmpty(colorCode))
            {
                textComponent.text = $"<color=#{colorCode}>{localizedText}</color>";
            }
            else
            {
                textComponent.text = localizedText;
            }
        }

        public void SetId(int newId)
        {
            stringId = newId;
            colorCode = null; // 색상 초기화
            UpdateText(TableManager.Instance.currentLanguage.Value);
        }

        public void SetIdWithColor(int newId, string hexCode)
        {
            stringId = newId;
            colorCode = hexCode;
            UpdateText(TableManager.Instance.currentLanguage.Value);
        }
    }
}
