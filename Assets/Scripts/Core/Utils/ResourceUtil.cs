using UnityEngine;
using UnityEngine.UI;
using Core.Table;

namespace Core.Utils
{
    public static class ResourceUtil
    {
        private static IAssetProvider currentProvider = new DefaultResourceAssetProvider();

        public static IAssetProvider AssetProvider
        {
            get => currentProvider ??= new DefaultResourceAssetProvider();
            set => currentProvider = value ?? new DefaultResourceAssetProvider();
        }

        /// <summary>
        /// 스프라이트를 로드하고, 파일이 없을 경우 Fallback 스프라이트 및 visual debug tint를 적용합니다.
        /// </summary>
        public static Sprite LoadSpriteWithFallback(string path, Image targetImage, CharacterKeyName keyName = CharacterKeyName.None)
        {
            return AssetProvider.LoadSprite(path, targetImage, keyName);
        }
    }
}
