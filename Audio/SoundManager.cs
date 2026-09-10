using UnityEngine;
using Core.Utils;
using System.Collections.Generic;

namespace Core.Audio
{
    /// <summary>
    /// 게임 전체의 오디오(BGM, SE)를 관리하는 싱글톤 클래스
    /// </summary>
    public class SoundManager : Singleton<SoundManager>
    {
        [Header("Audio Sources")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource seSource;

        [Header("Settings")]
        [SerializeField] private float defaultBgmVolume = 0.5f;
        [SerializeField] private float defaultSeVolume = 0.7f;

        private Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>();

        private const string MASTER_VOL_KEY = "MasterVolume";
        private const string BGM_VOL_KEY = "BgmVolume";
        private const string SE_VOL_KEY = "SeVolume";
        private const string VOICE_VOL_KEY = "VoiceVolume";

        private const string MASTER_MUTE_KEY = "MasterMute";
        private const string BGM_MUTE_KEY = "BgmMute";
        private const string SE_MUTE_KEY = "SeMute";
        private const string VOICE_MUTE_KEY = "VoiceMute";

        public float MasterVolume
        {
            get => PlayerPrefs.GetFloat(MASTER_VOL_KEY, 1.0f);
            set { PlayerPrefs.SetFloat(MASTER_VOL_KEY, value); UpdateAllVolumes(); }
        }

        public float BgmVolume
        {
            get => PlayerPrefs.GetFloat(BGM_VOL_KEY, 0.5f);
            set { PlayerPrefs.SetFloat(BGM_VOL_KEY, value); UpdateAllVolumes(); }
        }

        public float SeVolume
        {
            get => PlayerPrefs.GetFloat(SE_VOL_KEY, 0.7f);
            set { PlayerPrefs.SetFloat(SE_VOL_KEY, value); UpdateAllVolumes(); }
        }

        public float VoiceVolume
        {
            get => PlayerPrefs.GetFloat(VOICE_VOL_KEY, 0.8f);
            set { PlayerPrefs.SetFloat(VOICE_VOL_KEY, value); UpdateAllVolumes(); }
        }

        public bool IsMasterMuted
        {
            get => PlayerPrefs.GetInt(MASTER_MUTE_KEY, 0) == 1;
            set { PlayerPrefs.SetInt(MASTER_MUTE_KEY, value ? 1 : 0); UpdateAllVolumes(); }
        }

        public bool IsBgmMuted
        {
            get => PlayerPrefs.GetInt(BGM_MUTE_KEY, 0) == 1;
            set { PlayerPrefs.SetInt(BGM_MUTE_KEY, value ? 1 : 0); UpdateAllVolumes(); }
        }

        public bool IsSeMuted
        {
            get => PlayerPrefs.GetInt(SE_MUTE_KEY, 0) == 1;
            set { PlayerPrefs.SetInt(SE_MUTE_KEY, value ? 1 : 0); UpdateAllVolumes(); }
        }

        public bool IsVoiceMuted
        {
            get => PlayerPrefs.GetInt(VOICE_MUTE_KEY, 0) == 1;
            set { PlayerPrefs.SetInt(VOICE_MUTE_KEY, value ? 1 : 0); UpdateAllVolumes(); }
        }

        private float EffectiveMasterVolume => IsMasterMuted ? 0f : MasterVolume;
        private float EffectiveBgmVolume => IsBgmMuted ? 0f : BgmVolume;
        private float EffectiveSeVolume => IsSeMuted ? 0f : SeVolume;
        private float EffectiveVoiceVolume => IsVoiceMuted ? 0f : VoiceVolume;

        private void UpdateAllVolumes()
        {
            if (bgmSource != null) bgmSource.volume = EffectiveBgmVolume * EffectiveMasterVolume;
        }

        public override void Awake()
        {
            base.Awake();
            
            // AudioSource가 없으면 자동으로 생성
            if (bgmSource == null)
            {
                bgmSource = gameObject.AddComponent<AudioSource>();
                bgmSource.loop = true;
                bgmSource.playOnAwake = false;
            }

            if (seSource == null)
            {
                seSource = gameObject.AddComponent<AudioSource>();
                seSource.loop = false;
                seSource.playOnAwake = false;
            }

            UpdateAllVolumes();
        }

        public void SaveSettings()
        {
            PlayerPrefs.Save();
            Debug.Log("[Sound] Settings Saved.");
        }

        /// <summary>
        /// 배경음악 재생 (클립 직접 전달)
        /// </summary>
        public void PlayBgm(AudioClip clip, float volume = -1)
        {
            if (clip == null) return;
            if (bgmSource.clip == clip && bgmSource.isPlaying) return;

            bgmSource.clip = clip;
            bgmSource.volume = (volume >= 0 ? volume : EffectiveBgmVolume) * EffectiveMasterVolume;
            bgmSource.Play();
            Debug.Log($"[Sound] BGM Play (Direct): {clip.name}");
        }

        /// <summary>
        /// 배경음악 재생 (이름으로 로드)
        /// </summary>
        public void PlayBgm(string clipName, float volume = -1)
        {
            if (string.IsNullOrEmpty(clipName) || clipName == "-1") return;
            var clip = LoadClip($"Sounds/BackGround Musics/{clipName}");
            PlayBgm(clip, volume);
        }

        public void StopBgm()
        {
            if (bgmSource != null) bgmSource.Stop();
        }

        /// <summary>
        /// 효과음 단발성 재생 (클립 직접 전달)
        /// </summary>
        public void PlaySe(AudioClip clip, float volume = -1)
        {
            if (clip == null) return;
            float vol = (volume >= 0 ? volume : EffectiveSeVolume) * EffectiveMasterVolume;
            seSource.PlayOneShot(clip, vol);
        }

        /// <summary>
        /// 효과음 단발성 재생 (이름으로 로드)
        /// </summary>
        public void PlaySe(string clipName, float volume = -1)
        {
            if (string.IsNullOrEmpty(clipName) || clipName == "-1") return;
            var clip = LoadClip($"Sounds/SE/{clipName}");
            PlaySe(clip, volume);
        }

        private AudioClip LoadClip(string path)
        {
            if (clipCache.TryGetValue(path, out AudioClip clip))
            {
                return clip;
            }

            clip = Resources.Load<AudioClip>(path);
            if (clip != null)
            {
                clipCache.Add(path, clip);
            }
            else
            {
                Debug.LogWarning($"[Sound] Cannot find AudioClip at: Resources/{path}");
            }
            return clip;
        }
    }
}
