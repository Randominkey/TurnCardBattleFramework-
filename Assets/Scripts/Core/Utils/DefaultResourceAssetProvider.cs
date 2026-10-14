using UnityEngine;
using UnityEngine.UI;
using Core.Table;

namespace Core.Utils
{
    /// <summary>
    /// 기본 Unity Resources 폴더 기반의 에셋 로더 구현체.
    /// 본 프로젝트 런타임에서 동작합니다.
    /// </summary>
    public class DefaultResourceAssetProvider : IAssetProvider
    {
        public Sprite LoadSprite(string path, Image targetImage = null, CharacterKeyName keyName = CharacterKeyName.None)
        {
            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite != null)
            {
                if (targetImage != null)
                {
                    targetImage.sprite = sprite;
                    targetImage.color = Color.white;
                    targetImage.gameObject.SetActive(true);
                }
                return sprite;
            }

            // 리소스 미비 시 Fallback 처리 (화이트 박스 방지를 위해 투명 처리)
            if (targetImage != null)
            {
                targetImage.sprite = null;
                targetImage.color = Color.clear;
                targetImage.gameObject.SetActive(false);
            }

            return null;
        }

        public AudioClip LoadAudio(string path)
        {
            return Resources.Load<AudioClip>(path);
        }

        public GameObject LoadPrefab(string path)
        {
            return Resources.Load<GameObject>(path);
        }

        public TextAsset LoadXmlData(string fileName)
        {
            return Resources.Load<TextAsset>($"XMLData/{fileName}");
        }
    }
}
