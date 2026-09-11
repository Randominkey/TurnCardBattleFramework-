using UnityEngine;
using Core.Table;
using Core.View;
using System.Collections.Generic;
using System.Linq;
using UniRx;

namespace Core.Flow
{
    public class SpeakExecutor : BaseFlowExecutor
    {
        [SerializeField] private SpeakView speakView;
        
        private List<SpeakTableRow> dialogueRows;
        private int currentIndex = 0;
        private bool isTextCompleted = false;
        private float lineStartTime = 0f;
        private float disableSkipDuration = 0.5f;
        private bool isAutoMode = false;
        private bool isHideMode = false;
        private bool isAdvancing = false; // [New] 중복 진행 방지 플래그
        private float autoNextTimer = 0f;
        private const float AUTO_NEXT_DELAY = 1.0f;

        private struct QueuedAction
        {
            public SpeakAction action;
            public string[] values;
        }
        private List<QueuedAction> keepActionQueue = new List<QueuedAction>();

        private void Awake() => FlowController.Instance.RegisterExecutor(this);

        protected override void Execute()
        {
            var table = TableManager.Instance.GetTable<SpeakTable>();
            dialogueRows = table.rows.Where(r => r.group == executeId).OrderBy(r => r.id).ToList();

            if (dialogueRows.Count == 0)
            {
                Debug.LogWarning($"[Speak] No dialogue rows found for group: {executeId}");
                FinishStep();
                return;
            }

            currentIndex = 0;
            isAdvancing = false;
            
            var gameSetTable = TableManager.Instance.GetTable<GameSetTable>();
            if (gameSetTable != null && gameSetTable.rows.Count > 0)
            {
                disableSkipDuration = gameSetTable.rows[0].disableTextSkipSecond;
            }

            if (speakView.SkipButton != null)
            {
                speakView.SkipButton.onClick.RemoveAllListeners();
                speakView.SkipButton.onClick.AddListener(SkipAll);
            }
            
            if (speakView.AutoButton != null)
            {
                speakView.AutoButton.onClick.RemoveAllListeners();
                speakView.AutoButton.onClick.AddListener(ToggleAutoMode);
            }

            if (speakView.LogButton != null && speakView.LogView != null)
            {
                speakView.LogButton.onClick.RemoveAllListeners();
                speakView.LogButton.onClick.AddListener(() => speakView.LogView.Open());
            }

            if (speakView.HideButton != null)
            {
                speakView.HideButton.onClick.RemoveAllListeners();
                speakView.HideButton.onClick.AddListener(() => ToggleHide());
            }

            if (speakView.AdvanceAreaButton != null)
            {
                speakView.AdvanceAreaButton.onClick.RemoveAllListeners();
                speakView.AdvanceAreaButton.onClick.AddListener(HandleClick);
            }

            speakView.SetAutoUi(isAutoMode);
            isHideMode = false;
            ShowCurrentRow();

            Observable.EveryUpdate()
                .Where(_ => !speakView.LogView.IsLogOpen)
                .Subscribe(_ => 
                {
                    if (Input.GetKeyDown(KeyCode.Escape))
                    {
                        if (isHideMode)
                        {
                            ToggleHide(false);
                            MenuController.Instance.SuppressMenuThisFrame();
                            return; 
                        }
                    }

                    if (MenuController.Instance.IsMenuOpen) return;

                    if (Input.GetKeyDown(KeyCode.Space))
                    {
                        HandleClick();
                    }

                    if (Input.GetKeyDown(KeyCode.S)) SkipAll();
                    if (Input.GetKeyDown(KeyCode.A)) ToggleAutoMode();

                    UpdateAutoMode();
                })
                .AddTo(this);
        }

        private void ToggleAutoMode()
        {
            if (isHideMode) return;
            isAutoMode = !isAutoMode;
            speakView.SetAutoUi(isAutoMode);
            speakView.SetTypewriterMultiplier(isAutoMode ? 5f : 1f);
            autoNextTimer = 0f;
        }

        private void ToggleHide(bool? forceState = null)
        {
            isHideMode = forceState ?? !isHideMode;
            speakView.SetUiVisibility(!isHideMode);
        }

        private void UpdateAutoMode()
        {
            if (!isAutoMode || isHideMode || isAdvancing) return;

            if (isTextCompleted)
            {
                autoNextTimer += Time.deltaTime;
                if (autoNextTimer >= AUTO_NEXT_DELAY)
                {
                    autoNextTimer = 0f;
                    HandleClick();
                }
            }
        }

        public void SkipAll()
        {
            if (isHideMode) return;
            currentIndex = dialogueRows.Count;
            speakView.StopSequencer();
            FinishStep();
        }

        private void HandleClick()
        {
            if (isHideMode)
            {
                ToggleHide(false);
                return;
            }

            // [Fix] 중요 연출 중이거나 이미 줄 변경 중이면 무시
            if (speakView.IsInputBlocked || isAdvancing) return;

            if (Time.time - lineStartTime < disableSkipDuration)
            {
                return;
            }

            if (speakView.IsSequencerPlaying)
            {
                if (dialogueRows[currentIndex].scenarioTextId != -1)
                {
                    Debug.Log("[Speak] Soft Skip: Forcing text completion");
                    isTextCompleted = true; 
                    speakView.StopSequencer();
                    OnTextCompleted();
                }
                return;
            }

            NextDialogue();
        }

        private void ShowCurrentRow()
        {
            if (currentIndex >= dialogueRows.Count)
            {
                FinishStep();
                return;
            }

            isAdvancing = true; // [New] 중복 실행 방지 시작
            var row = dialogueRows[currentIndex];
            speakView.ResetSlotPositions();
            
            isTextCompleted = false; 
            lineStartTime = Time.time;

            speakView.StopSequencer();

            speakView.SetBackground(row.bgImgName);
            speakView.UpdateCharacters(row);

            var charTable = TableManager.Instance.GetTable<CharacterTable>();
            var speakerRow = charTable.rows.FirstOrDefault(c => c.keyName == row.speakerKeyName);
            string hexCode = speakerRow?.characterHexCode;

            if (row.scenarioTextId != -1 && speakView.LogView != null)
            {
                speakView.LogView.AddLog(row.speakerKeyName, speakerRow?.nameStringId ?? 0, row.scenarioTextId, hexCode);
            }

            if (!string.IsNullOrEmpty(row.bgmName)) Core.Audio.SoundManager.Instance.PlayBgm(row.bgmName);
            if (!string.IsNullOrEmpty(row.seName)) Core.Audio.SoundManager.Instance.PlaySe(row.seName);
            if (!string.IsNullOrEmpty(row.voiceName)) Core.Audio.SoundManager.Instance.PlaySe($"Voice/{row.voiceName}");

            if (speakerRow != null)
            {
                if (!string.IsNullOrEmpty(hexCode))
                    speakView.SetSpeakerWithColor(speakerRow.nameStringId, hexCode);
                else
                    speakView.SetSpeaker(speakerRow.nameStringId);
            }

            speakView.SetDialogue(row.scenarioTextId);

            if (row.keepAction && row.actionName != SpeakAction.None)
            {
                keepActionQueue.Add(new QueuedAction { action = row.actionName, values = (string[])row.ActionValues.Clone() });
                speakView.PlayAction(SpeakAction.None, null, row.waitAction, OnSequenceCompleted);
            }
            else
            {
                speakView.PlayAction(row.actionName, row.ActionValues, row.waitAction, OnSequenceCompleted);
            }
            
            isAdvancing = false; // [New] 설정 완료 후 해제
        }

        private void OnSequenceCompleted()
        {
            OnTextCompleted();

            if (dialogueRows[currentIndex].scenarioTextId == -1)
            {
                NextDialogue();
            }
        }

        private void OnTextCompleted()
        {
            if (currentIndex < dialogueRows.Count && !isTextCompleted)
            {
                isTextCompleted = true;

                if (keepActionQueue.Count > 0)
                {
                    foreach (var queued in keepActionQueue)
                    {
                        speakView.PlayAction(queued.action, queued.values, true, null);
                    }
                    keepActionQueue.Clear();
                }
            }
        }

        private void NextDialogue()
        {
            if (isAdvancing) return; // [New] 중복 진입 방지
            currentIndex++;
            ShowCurrentRow();
        }
    }
}
