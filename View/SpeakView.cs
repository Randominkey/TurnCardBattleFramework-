using UnityEngine;
using UnityEngine.UI;
using Core.Table;
using System.Collections.Generic;
using DG.Tweening;

namespace Core.View
{
    public class SpeakView : MonoBehaviour
    {
        [Header("Background")]
        [SerializeField] private Image bgImage;
        [SerializeField] private CanvasGroup bgFadeGroup;
        [SerializeField] private CanvasGroup sceneFadeGroup; // 전체 화면 페이드

        [Header("Layers")]
        [SerializeField] private Transform imageLayer; // ShowImage용 레이어
        [SerializeField] private Transform effectLayer; // ShowEffect용 레이어

        [Header("Characters")]
        [SerializeField] private CharacterView[] characterSlots; // Position A, B, C, D, E 순서대로 할당

        [Header("Dialogue UI")]
        [SerializeField] private TextIdHolder speakerNameText;
        [SerializeField] private TextIdHolder dialogueText;
        [SerializeField] private GameObject dialoguePanel;
        [SerializeField] private GameObject nextCursor; // 다음 대기 아이콘

        [Header("UI Controls")]
        [SerializeField] private RectTransform topUiGroup; // 상단 버튼 그룹
        [SerializeField] private Button skipButton;
        [SerializeField] private Button autoButton;
        [SerializeField] private GameObject autoOnObject;
        [SerializeField] private GameObject autoOffObject;
        [SerializeField] private Button logButton;
        [SerializeField] private Button hideButton; // [New] 하이드 버튼
        [SerializeField] private SpeakLogView logView;
        [SerializeField] private Button advanceAreaButton;

        [Header("Juice Settings")]
        [SerializeField] private float typewriterSpeed = 30f;
        [SerializeField] private AudioClip bgmClip;
        [SerializeField] private AudioClip nextDialogueSfx;

        public Button SkipButton => skipButton;
        public Button AutoButton => autoButton;
        public Button LogButton => logButton;
        public Button HideButton => hideButton;
        public SpeakLogView LogView => logView;
        public Button AdvanceAreaButton => advanceAreaButton;

        private float typewriterMultiplier = 1f;
        private ActionSequencer sequencer = new ActionSequencer();
        private ActionSequencer subSequencer = new ActionSequencer(); // WaitAction=False일 때 병렬 실행용
        private float[] defaultXPositions;
        private bool isSceneTransitioning = false; // [New] 페이드 등 실제 트위닝 중인지 여부
        private bool isCurrentSequenceUnskippable = false; // [New] 현재 줄에 중요한 연출이 예약되어 있는지 여부

        public bool IsSequencerPlaying => sequencer.IsPlaying || subSequencer.IsPlaying;
        
        // [New] 유저 입력을 '완전 차단'해야 하는 상황인지 판단 (페이드 등)
        public bool IsInputBlocked => isSceneTransitioning || isCurrentSequenceUnskippable;

        private void InitDefaultPositions()
        {
            var gameSet = TableManager.Instance.GetTable<GameSetTable>().Get(0);
            if (gameSet == null) return;

            defaultXPositions = new float[5];
            defaultXPositions[0] = gameSet.positionAAxis * 3f;
            defaultXPositions[1] = gameSet.positionBAxis * 3f;
            defaultXPositions[2] = gameSet.positionCAxis * 3f;
            defaultXPositions[3] = gameSet.positionDAxis * 3f;
            defaultXPositions[4] = gameSet.positionEAxis * 3f;
        }

        public void ResetSlotPositions()
        {
            if (defaultXPositions == null) InitDefaultPositions();
            if (defaultXPositions == null) return;

            for (int i = 0; i < characterSlots.Length; i++)
            {
                if (i >= defaultXPositions.Length) break;
                
                var rt = characterSlots[i].GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.DOKill();
                    rt.anchoredPosition = new Vector2(defaultXPositions[i], rt.anchoredPosition.y);
                }
            }
        }

        public void SetAutoUi(bool isOn)
        {
            if (autoOnObject != null) autoOnObject.SetActive(isOn);
            if (autoOffObject != null) autoOffObject.SetActive(!isOn);
        }

        /// <summary>
        /// [New] Hide 모드에 따라 상/하단 UI의 가시성을 설정합니다.
        /// </summary>
        public void SetUiVisibility(bool visible)
        {
            if (topUiGroup != null) topUiGroup.gameObject.SetActive(visible);
            if (dialoguePanel != null) dialoguePanel.SetActive(visible);
        }

        public void SetTypewriterMultiplier(float multiplier)
        {
            typewriterMultiplier = multiplier;
        }

        private void Awake()
        {
            if (nextCursor != null) nextCursor.SetActive(false);
            if (sceneFadeGroup != null) sceneFadeGroup.alpha = 0f;
        }

        private void Start()
        {
            if (bgmClip != null)
            {
                Core.Audio.SoundManager.Instance.PlayBgm(bgmClip);
            }
        }

        public void StopSequencer()
        {
            Debug.Log("[SpeakView] StopSequencer called. Killing tweens and completing text.");
            sequencer.Stop();
            subSequencer.Stop();
            
            // [Fix] 중요 연출 중단 시 모든 페이드 강제 종료 및 플래그 초기화
            if (sceneFadeGroup != null) sceneFadeGroup.DOKill();
            isSceneTransitioning = false; 
            isCurrentSequenceUnskippable = false; 
            
            if (dialogueText != null && dialogueText.TextComponent != null)
            {
                // [핵심] 해당 컴포넌트를 타겟으로 하는 모든 트윈 중단
                DOTween.Kill(dialogueText.TextComponent);
                dialogueText.TextComponent.maxVisibleCharacters = 9999;
            }

            if (nextCursor != null)
            {
                if (dialogueText != null && dialogueText.TextComponent.text != "")
                {
                    nextCursor.SetActive(true);
                    nextCursor.transform.DOKill();
                    nextCursor.transform.localPosition = new Vector3(nextCursor.transform.localPosition.x, 40f, 0); // 초기 위치
                    nextCursor.transform.DOLocalMoveY(20, 0.5f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
                }
            }
        }

        public void ClearDialogue()
        {
            if (dialogueText != null) dialogueText.TextComponent.text = "";
            if (speakerNameText != null) speakerNameText.TextComponent.text = "";
            if (nextCursor != null) nextCursor.SetActive(false);
            isCurrentSequenceUnskippable = false;
        }

        public void SetBackground(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName)) return;
            var sprite = Resources.Load<Sprite>($"Sprites/Bg/{spriteName}");
            if (sprite != null) bgImage.sprite = sprite;
        }

        public void SetSpeaker(int nameId)
        {
            if (speakerNameText != null)
                speakerNameText.SetId(nameId);
        }

        public void SetSpeakerWithColor(int nameId, string hexCode)
        {
            if (speakerNameText != null)
                speakerNameText.SetIdWithColor(nameId, hexCode);
        }

        public void SetDialogue(int textId)
        {
            if (dialogueText != null)
            {
                dialogueText.SetId(textId); // ID 저장 (언어 변경 대비)
                
                if (textId == -1)
                {
                    dialogueText.TextComponent.text = "";
                    if (nextCursor != null) nextCursor.SetActive(false);
                    return;
                }

                string text = dialogueText.GetLocalizedText(textId);
                
                // 기존 타이핑 중단 및 커서 숨김
                DOTween.Kill(dialogueText.TextComponent);
                if (nextCursor != null)
                {
                    nextCursor.SetActive(false);
                    nextCursor.transform.DOKill();
                }

                sequencer.AddCommand(new TypewriterCommand(dialogueText.TextComponent, text, typewriterSpeed * typewriterMultiplier));
                
                if (nextDialogueSfx != null) Core.Audio.SoundManager.Instance.PlaySe(nextDialogueSfx);
                else Core.Audio.SoundManager.Instance.PlaySe("UI_Dialogue_Next");
            }
        }

        public void UpdateCharacters(SpeakTableRow row)
        {
            bool isDialogueHidden = row.scenarioTextId == -1;
            
            if (dialoguePanel != null)
            {
                dialoguePanel.SetActive(!isDialogueHidden);
            }

            for (int i = 0; i < characterSlots.Length; i++)
            {
                CharacterKeyName keyName = row.GetPosition(i);
                if (keyName == CharacterKeyName.None)
                {
                    characterSlots[i].gameObject.SetActive(false);
                }
                else
                {
                    characterSlots[i].gameObject.SetActive(true);
                    characterSlots[i].Setup(keyName);
                    
                    bool isSpeaker = (keyName == row.speakerKeyName);
                    characterSlots[i].SetHighlight(isSpeaker);
                    if (isSpeaker)
                    {
                        characterSlots[i].SetFace(row.speakerFaceName);
                    }
                }
            }
        }

        private bool TryDecodeInt(string value, out int result)
        {
            if (int.TryParse(value, out result)) return true;

            if (!string.IsNullOrEmpty(value))
            {
                char c = char.ToUpper(value[0]);
                if (c >= 'A' && c <= 'E')
                {
                    result = c - 'A';
                    return true;
                }
            }

            result = -1;
            return false;
        }

        private bool TryDecodeFloat(string value, out float result)
        {
            if (float.TryParse(value, out result)) return true;

            if (!string.IsNullOrEmpty(value))
            {
                char c = char.ToUpper(value[0]);
                if (c >= 'A' && c <= 'E')
                {
                    int index = c - 'A';
                    if (index >= 0 && index < characterSlots.Length)
                    {
                        result = characterSlots[index].GetComponent<RectTransform>().anchoredPosition.x;
                        return true;
                    }
                }
            }

            result = 0f;
            return false;
        }

        public void PlayAction(SpeakAction action, string[] values, bool waitAction, System.Action onComplete)
        {
            var targetSequencer = waitAction ? sequencer : subSequencer;

            // [New] 페이드 연출이 포함된 경우 줄 전체를 스킵 불가로 설정 (Hard Block)
            if (action == SpeakAction.SceneFadeIn || action == SpeakAction.SceneFadeOut)
            {
                isCurrentSequenceUnskippable = true;
            }

            if (action != SpeakAction.None)
            {
                switch (action)
                {
                    case SpeakAction.MoveCharacter:
                        if (values.Length >= 3 && TryDecodeInt(values[0], out int mPos) && mPos < characterSlots.Length)
                        {
                            var target = characterSlots[mPos].GetComponent<RectTransform>();
                            if (TryDecodeFloat(values[1], out float x))
                            {
                                float dur = float.Parse(values[2]) * 0.001f;
                                targetSequencer.AddCommand(new MoveCommand(target, new Vector2(x, target.anchoredPosition.y), dur));
                            }
                        }
                        break;

                    case SpeakAction.ShakeCharacter:
                        if (TryDecodeInt(values[0], out int sPos) && sPos < characterSlots.Length)
                        {
                            var target = characterSlots[sPos].GetComponent<RectTransform>();
                            float strength = (values.Length >= 2) ? float.Parse(values[1]) : 20f;
                            float dur = (values.Length >= 4) ? float.Parse(values[3]) * 0.001f : 0.5f;
                            targetSequencer.AddCommand(new ShakeCommand(target, dur, strength));
                        }
                        break;

                    case SpeakAction.ShakeBg:
                        if (bgImage != null)
                        {
                            float strength = (values.Length >= 1) ? float.Parse(values[0]) : 15f;
                            float dur = (values.Length >= 3) ? float.Parse(values[2]) * 0.001f : 0.5f;
                            targetSequencer.AddCommand(new ShakeCommand(bgImage.rectTransform, dur, strength));
                        }
                        break;

                    case SpeakAction.ShakeText:
                        if (dialogueText != null)
                        {
                            float strength = (values.Length >= 1) ? float.Parse(values[0]) : 10f;
                            float dur = (values.Length >= 3) ? float.Parse(values[2]) * 0.001f : 0.5f;
                            targetSequencer.AddCommand(new ShakeCommand(dialogueText.TextComponent.rectTransform, dur, strength));
                        }
                        break;

                    case SpeakAction.CharacterFadeIn:
                    case SpeakAction.CharacterFadeOut:
                        if (TryDecodeInt(values[0], out int fPos) && fPos < characterSlots.Length)
                        {
                            var cg = characterSlots[fPos].GetComponent<CanvasGroup>();
                            if (cg == null) cg = characterSlots[fPos].gameObject.AddComponent<CanvasGroup>();
                            
                            float targetAlpha = (action == SpeakAction.CharacterFadeIn) ? 1f : 0f;
                            float dur = (values.Length >= 2) ? float.Parse(values[1]) * 0.001f : 0.5f;
                            targetSequencer.AddCommand(new FadeCommand(cg, targetAlpha, dur));
                        }
                        break;

                    case SpeakAction.BgFadeIn:
                        if (bgFadeGroup != null)
                        {
                            float dur = (values.Length >= 1) ? float.Parse(values[0]) * 0.001f : 1f;
                            targetSequencer.AddCommand(new FadeCommand(bgFadeGroup, 1f, dur));
                        }
                        break;

                    case SpeakAction.SceneFadeIn:
                    case SpeakAction.SceneFadeOut:
                        if (sceneFadeGroup != null)
                        {
                            float targetAlpha = (action == SpeakAction.SceneFadeIn) ? 1f : 0f;
                            float dur = (values.Length >= 2) ? float.Parse(values[1]) * 0.001f : 1f;
                            
                            targetSequencer.AddCommand(new ActionCommand(() => isSceneTransitioning = true));
                            targetSequencer.AddCommand(new FadeCommand(sceneFadeGroup, targetAlpha, dur));
                            targetSequencer.AddCommand(new ActionCommand(() => isSceneTransitioning = false));
                        }
                        break;

                    case SpeakAction.TextScale:
                        if (dialogueText != null && values.Length >= 1)
                        {
                            float size = float.Parse(values[0]);
                            targetSequencer.AddCommand(new FontSizeCommand(dialogueText.TextComponent, size));
                        }
                        break;

                    case SpeakAction.TextSpeed:
                        if (values.Length >= 1 && float.TryParse(values[0], out float speedVal))
                        {
                            targetSequencer.AddCommand(new ActionCommand(() => { typewriterSpeed = speedVal; }));
                        }
                        break;

                    case SpeakAction.ShowImage:
                        if (values.Length >= 3 && imageLayer != null)
                        {
                            string imgName = values[0];
                            float x = float.Parse(values[1]);
                            float y = float.Parse(values[2]);
                            targetSequencer.AddCommand(new ActionCommand(() => {
                                var go = new GameObject($"Image_{imgName}");
                                go.transform.SetParent(imageLayer, false);
                                var img = go.AddComponent<Image>();
                                img.sprite = Resources.Load<Sprite>($"Sprites/SpeakImages/{imgName}");
                                img.SetNativeSize();
                                go.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);
                            }));
                        }
                        break;

                    case SpeakAction.DeleteImage:
                        if (imageLayer != null)
                        {
                            targetSequencer.AddCommand(new ActionCommand(() => {
                                foreach (Transform child in imageLayer) Destroy(child.gameObject);
                            }));
                        }
                        break;

                    case SpeakAction.ShowEffect:
                        if (values.Length >= 3 && effectLayer != null)
                        {
                            string fxName = values[0];
                            float x = float.Parse(values[1]);
                            float y = float.Parse(values[2]);
                            targetSequencer.AddCommand(new ActionCommand(() => {
                                var prefab = Resources.Load<GameObject>($"Prefabs/Effects/{fxName}");
                                if (prefab != null)
                                {
                                    var go = Instantiate(prefab, effectLayer);
                                    go.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);
                                }
                            }));
                        }
                        break;

                    case SpeakAction.Delay:
                        if (float.TryParse(values[0], out float delayTime))
                        {
                            targetSequencer.AddCommand(new DelayCommand(delayTime * 0.001f));
                        }
                        break;

                    default:
                        Debug.LogWarning($"[SpeakView] Action {action} is not implemented yet.");
                        break;
                }
            }

            if (!waitAction) subSequencer.Play();

            sequencer.Play(() => {
                isCurrentSequenceUnskippable = false; // 연출 완료 후 해제
                if (nextCursor != null && dialogueText.TextComponent.text != "")
                {
                    nextCursor.SetActive(true);
                    nextCursor.transform.DOKill();
                    nextCursor.transform.localPosition = new Vector3(nextCursor.transform.localPosition.x, 40f, 0); // 초기 위치
                    nextCursor.transform.DOLocalMoveY(20, 0.5f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
                }
                onComplete?.Invoke();
            });
        }
    }
}
