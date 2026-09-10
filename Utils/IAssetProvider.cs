using UnityEngine;
using UnityEngine.UI;
using Core.Table;

namespace Core.Utils
{
    /// <summary>
    /// 게임 내 리소스(스프라이트, 오디오, 프리팹, 테이블 데이터) 로딩을 추상화하는 인터페이스.
    /// 본 프로젝트(Resources)와 포트폴리오/샘플 씬(DemoData/Addressables)의 에셋 제공 로직을 분리하기 위해 사용됩니다.
    /// </summary>
    public interface IAssetProvider
    {
        Sprite LoadSprite(string path, Image targetImage = null, CharacterKeyName keyName = CharacterKeyName.None);
        AudioClip LoadAudio(string path);
        GameObject LoadPrefab(string path);
        TextAsset LoadXmlData(string fileName);
    }
}
