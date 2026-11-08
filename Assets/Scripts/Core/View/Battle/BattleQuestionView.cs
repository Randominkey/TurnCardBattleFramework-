using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UniRx;
using DG.Tweening;
using Core.Battle;
using Core.Table;

namespace Core.View
{
    /// <summary>
    /// 질문(Question / NPC 다음 행동 힌트) 시스템 전용 UI 뷰.
    /// 1) 배틀 화면 좌측의 [질문] 진입 버튼
    /// 2) 대사 선택지 모달 (기본 4개 가변 버튼 + 외부 영역 클릭 시 닫기 Blocker)
    /// 3) 적 캐릭터 측 대답 말풍선 (카드 제출 후 클래시 애니메이션 진입 직전까지 유지)
    /// 인스펙터에 프리팹/오브젝트가 연결되어 있으면 우선 사용하며, 미연결 시 런타임에 안전하게 기본 UI를 자동 구성합니다.
    /// </summary>
    public class BattleQuestionView : MonoBehaviour
    {
        [Header("Entry Button")]
        [SerializeField] private Button openQuestionButton;
        [SerializeField] private TextMeshProUGUI openButtonLabel;

        [Header("Question Modal (Popup)")]
        [SerializeField] private GameObject questionModalRoot;
        [SerializeField] private Button outsideBlockerButton;
        [SerializeField] private RectTransform questionListContainer;
        [SerializeField] private Button questionButtonTemplate;

        [Header("NPC Answer Speech Bubble")]
        [SerializeField] private RectTransform answerBubbleRoot;
        [SerializeField] private TextMeshProUGUI answerBubbleText;
        [SerializeField] private TextIdHolder answerTextIdHolder;

        private BattleModel battleModel;
        private TMP_FontAsset fallbackFont;
        private readonly List<QuestionButtonSlot> questionSlots = new List<QuestionButtonSlot>();
        private CompositeDisposable bindDisposables = new CompositeDisposable();

        // [26.11.07 추가 기획] 거짓 대답 연출용 트윈, 코루틴 및 원래 좌표 캐시
        private Tween answerBubbleShakeTween;
        private Coroutine typingCoroutine;
        private Vector2 answerBubbleOriginalAnchoredPosition;
        private bool hasCachedBubblePosition = false;

        private class QuestionButtonSlot
        {
            public int index;
            public GameObject rootObject;
            public Button button;
            public Image backgroundImage;
            public TextMeshProUGUI textComponent;
            public TextIdHolder textIdHolder;
        }

        private static readonly Color ActiveButtonColor = new Color(0.96f, 0.62f, 0.18f, 0.98f);
        private static readonly Color ConsumedButtonColor = new Color(0.32f, 0.32f, 0.36f, 0.85f);
        private static readonly Color ActiveTextColor = new Color(0.12f, 0.08f, 0.04f, 1f);
        private static readonly Color ConsumedTextColor = new Color(0.62f, 0.62f, 0.65f, 0.85f);

        /// <summary>
        /// 런타임 초기화 및 인스펙터 미할당 시 기본 UI 계층 자동 생성
        /// </summary>
        public void Initialize(Transform uiParent, RectTransform playerFigure, RectTransform enemyFigure, TMP_FontAsset referenceFont)
        {
            this.fallbackFont = referenceFont;

            EnsureEntryButton(uiParent, playerFigure);
            EnsureAnswerBubble(uiParent, enemyFigure);
            EnsureQuestionModal(uiParent);

            if (!hasCachedBubblePosition && answerBubbleRoot != null)
            {
                answerBubbleOriginalAnchoredPosition = answerBubbleRoot.anchoredPosition;
                hasCachedBubblePosition = true;
            }

            SetQuestionModalVisible(false);
            SetAnswerBubbleVisible(false, false);
        }

        /// <summary>
        /// 새로운 배틀(또는 재시작) 시 BattleModel과 바인딩하여 4개 버튼을 모두 활성화 상태로 세팅합니다.
        /// </summary>
        public void Bind(BattleModel model)
        {
            bindDisposables.Clear();
            this.battleModel = model;

            SetQuestionModalVisible(false);
            SetAnswerBubbleVisible(false, false);

            if (model == null || model.question == null)
                return;

            RebuildQuestionButtons(model.question.Questions);
            RefreshQuestionButtonStates();

            // 대답 활성화 상태 구독 (클래시 진입 직전 ClearActiveAnswer 호출 시 말풍선 자동 종료)
            model.question.hasActiveAnswer
                .DistinctUntilChanged()
                .Subscribe(isActive =>
                {
                    if (!isActive)
                    {
                        SetAnswerBubbleVisible(false, true);
                    }
                })
                .AddTo(bindDisposables);

            // 대답 내용 갱신 구독
            model.question.latestAnswer
                .Skip(1)
                .Subscribe(result =>
                {
                    if (model.question.hasActiveAnswer.Value)
                    {
                        ShowAnswerBubble(result);
                    }
                })
                .AddTo(bindDisposables);
        }

        private void OnDestroy()
        {
            StopTyping();
            StopAnswerBubbleShake();
            bindDisposables.Dispose();
        }

        /// <summary>
        /// 질문 진입 버튼 클릭 시 대사 선택지 모달을 엽니다.
        /// </summary>
        public void OpenQuestionModal()
        {
            if (battleModel == null || battleModel.question == null) return;

            Core.Audio.SoundManager.Instance?.PlaySe("UI_Card_Select");
            RefreshQuestionButtonStates();
            SetQuestionModalVisible(true);
        }

        /// <summary>
        /// 대사 버튼 외 영역 클릭 시 소모 없이 선택지 모달을 닫고 이전 배틀 화면으로 복귀합니다.
        /// </summary>
        public void CloseQuestionModal()
        {
            SetQuestionModalVisible(false);
        }

        /// <summary>
        /// 카드 클래시 애니메이션 진입 직전에 호출되어 열려 있는 모달과 말풍선을 즉시 정리합니다.
        /// </summary>
        public void HideAllForClash()
        {
            SetQuestionModalVisible(false);
            StopTyping();
            StopAnswerBubbleShake();
            if (battleModel != null && battleModel.question != null)
            {
                battleModel.question.ClearActiveAnswer();
            }
            else
            {
                SetAnswerBubbleVisible(false, false);
            }
        }

        private void OnQuestionButtonClicked(int questionIndex)
        {
            if (battleModel == null || battleModel.question == null) return;

            // 비활성화(이미 소모된) 버튼은 클릭 시 어떤 반응도 일어나지 않음 (모달도 닫히지 않음)
            if (battleModel.question.IsQuestionUsed(questionIndex))
            {
                return;
            }

            if (battleModel.question.TryAskQuestion(questionIndex, out QuestionAnswerResult answerResult))
            {
                Core.Audio.SoundManager.Instance?.PlaySe("UI_Submit_Click");

                // 1. 소모된 버튼 상태 갱신 (1-1 비활성화)
                RefreshQuestionButtonStates();

                // 2. 선택지 모달 닫고 이전 배틀 화면으로 복귀
                SetQuestionModalVisible(false);

                // 3. 상대방 대답 말풍선 출력 (유저가 카드를 골라 클래시 페이즈에 돌입하기 직전까지 유지)
                ShowAnswerBubble(answerResult);
            }
        }

        private void RebuildQuestionButtons(IReadOnlyList<QuestionChoiceData> questions)
        {
            if (questionListContainer == null) return;

            // 필요한 슬롯 수만큼 생성 또는 재사용 (4개 기본, 5개 이상 가변 대응)
            while (questionSlots.Count < questions.Count)
            {
                int slotIdx = questionSlots.Count;
                var slot = CreateQuestionSlot(slotIdx);
                questionSlots.Add(slot);
            }

            for (int i = 0; i < questionSlots.Count; i++)
            {
                var slot = questionSlots[i];
                if (i < questions.Count)
                {
                    slot.rootObject.SetActive(true);
                    var qData = questions[i];
                    if (slot.textIdHolder != null && qData.stringId > 0)
                    {
                        slot.textIdHolder.SetId(qData.stringId);
                    }
                    else if (slot.textIdHolder != null)
                    {
                        slot.textIdHolder.SetRawText(qData.questionText);
                    }
                    else if (slot.textComponent != null)
                    {
                        slot.textComponent.text = qData.questionText;
                    }
                }
                else
                {
                    slot.rootObject.SetActive(false);
                }
            }
        }

        private void RefreshQuestionButtonStates()
        {
            if (battleModel == null || battleModel.question == null) return;

            for (int i = 0; i < questionSlots.Count; i++)
            {
                var slot = questionSlots[i];
                if (slot == null || !slot.rootObject.activeSelf) continue;

                bool isUsed = battleModel.question.IsQuestionUsed(slot.index);

                if (slot.backgroundImage != null)
                {
                    slot.backgroundImage.color = isUsed ? ConsumedButtonColor : ActiveButtonColor;
                    // 비활성화 버튼 클릭 시 뒤쪽의 Outside Blocker로 클릭이 관통되어 창이 닫히지 않도록 raycastTarget 유지
                    slot.backgroundImage.raycastTarget = true;
                }

                if (slot.textComponent != null)
                {
                    slot.textComponent.color = isUsed ? ConsumedTextColor : ActiveTextColor;
                }
            }
        }

        private void ShowAnswerBubble(QuestionAnswerResult result)
        {
            if (answerBubbleRoot == null) return;

            StopTyping();
            StopAnswerBubbleShake();
            answerBubbleRoot.DOKill();

            if (!hasCachedBubblePosition)
            {
                answerBubbleOriginalAnchoredPosition = answerBubbleRoot.anchoredPosition;
                hasCachedBubblePosition = true;
            }
            else
            {
                answerBubbleRoot.anchoredPosition = answerBubbleOriginalAnchoredPosition;
            }

            // 전체 대답 텍스트 해석
            string fullText = null;
            if (result.answerStringId > 0 && TableManager.Instance != null)
            {
                var stringRow = TableManager.Instance.GetTable<StringTable>()?.Get(result.answerStringId);
                if (stringRow != null)
                {
                    fullText = stringRow.Get(TableManager.Instance.currentLanguage.Value);
                }
            }
            if (string.IsNullOrEmpty(fullText))
            {
                fullText = result.answerText ?? "";
            }

            SetAnswerBubbleVisible(true, true);

            bool isLie = (result.answerType == QuestionAnswerType.Lie);

            // 1. 거짓 대답 시: 사라지기 전까지 계속 흔들림 (오차 누적 방지 리셋 포함)
            if (isLie)
            {
                StartContinuousShake();
            }

            // 2. 타이핑 연출 (거짓말일 경우 머뭇거림 감속/일시정지 포함)
            typingCoroutine = StartCoroutine(TypewriterRoutine(fullText, result.answerStringId, isLie));
        }

        private void StartContinuousShake()
        {
            StopAnswerBubbleShake();

            // 0.4초 주기로 미세하게 흔들린 후, 매 주기마다 원래 좌표로 리셋하여 오차/위치 이탈 누적 원천 차단
            Sequence shakeSeq = DOTween.Sequence();
            shakeSeq.Append(answerBubbleRoot.DOShakePosition(
                duration: 0.4f,
                strength: new Vector3(5f, 2.5f, 0f),
                vibrato: 12,
                randomness: 45,
                snapping: false,
                fadeOut: false
            ));
            shakeSeq.AppendCallback(() =>
            {
                if (answerBubbleRoot != null && hasCachedBubblePosition)
                {
                    answerBubbleRoot.anchoredPosition = answerBubbleOriginalAnchoredPosition;
                }
            });
            shakeSeq.SetLoops(-1);
            answerBubbleShakeTween = shakeSeq;
        }

        private void StopAnswerBubbleShake()
        {
            if (answerBubbleShakeTween != null)
            {
                answerBubbleShakeTween.Kill();
                answerBubbleShakeTween = null;
            }
            if (answerBubbleRoot != null && hasCachedBubblePosition)
            {
                answerBubbleRoot.anchoredPosition = answerBubbleOriginalAnchoredPosition;
            }
        }

        private void StopTyping()
        {
            if (typingCoroutine != null)
            {
                StopCoroutine(typingCoroutine);
                typingCoroutine = null;
            }
            if (answerBubbleText != null)
            {
                answerBubbleText.maxVisibleCharacters = int.MaxValue;
            }
        }

        private System.Collections.IEnumerator TypewriterRoutine(string rawText, int stringId, bool isLie)
        {
            if (answerBubbleText == null) yield break;

            var (cleanText, pauseIndex, pauseDuration) = ParseHesitationMarker(rawText, isLie);

            ApplyBubbleText(cleanText, stringId);
            answerBubbleText.ForceMeshUpdate();

            int totalVisible = answerBubbleText.textInfo.characterCount;
            answerBubbleText.maxVisibleCharacters = 0;

            for (int i = 0; i <= totalVisible; i++)
            {
                answerBubbleText.maxVisibleCharacters = i;

                if (isLie && pauseIndex > 0 && i == pauseIndex)
                {
                    // 머뭇거리는 타이밍: 타이핑 속도가 멈추거나 대폭 감속 (일시정지)
                    yield return new WaitForSeconds(pauseDuration);
                }
                else if (isLie && pauseIndex > 2 && i >= pauseIndex - 2 && i < pauseIndex)
                {
                    // 머뭇거리기 직전 1~2글자: 타이핑 속도 감속
                    yield return new WaitForSeconds(0.08f);
                }
                else
                {
                    // 일반 타이핑 간격
                    yield return new WaitForSeconds(isLie ? 0.035f : 0.025f);
                }
            }

            answerBubbleText.maxVisibleCharacters = int.MaxValue;
            typingCoroutine = null;
        }

        private (string cleanText, int pauseIndex, float pauseDuration) ParseHesitationMarker(string rawText, bool isLie)
        {
            if (string.IsNullOrEmpty(rawText) || !isLie)
            {
                return (rawText ?? "", -1, 0f);
            }

            // 1. 추후 StringTable에 적용될 수 있는 명시적 마커/태그 우선 지원 (<pause>, <wait>, ^, | 등)
            string[] explicitTags = new string[] { "<pause>", "<wait>", "^", "|" };
            foreach (var tag in explicitTags)
            {
                int tagIdx = rawText.IndexOf(tag, StringComparison.OrdinalIgnoreCase);
                if (tagIdx >= 0)
                {
                    string cleaned = rawText.Remove(tagIdx, tag.Length);
                    return (cleaned, tagIdx, 0.45f);
                }
            }

            // 2. 태그 미지정 시: 문장 전반부(1~12자 내) 자연 문장부호(말줄임표, 쉼표, 느낌표)를 머뭇거림 지점으로 자동 감지
            int ellipsisIdx = rawText.IndexOf("…", StringComparison.Ordinal);
            if (ellipsisIdx < 0) ellipsisIdx = rawText.IndexOf("...", StringComparison.Ordinal);
            int commaIdx = rawText.IndexOf(',', StringComparison.Ordinal);
            int exclamationIdx = rawText.IndexOf('!', StringComparison.Ordinal);

            int splitIdx = -1;
            if (ellipsisIdx >= 0 && ellipsisIdx <= 12)
            {
                splitIdx = rawText.IndexOf("…", StringComparison.Ordinal) >= 0 ? ellipsisIdx + 1 : ellipsisIdx + 3;
            }
            else if (commaIdx >= 0 && commaIdx <= 10)
            {
                splitIdx = commaIdx + 1;
            }
            else if (exclamationIdx >= 0 && exclamationIdx <= 10)
            {
                splitIdx = exclamationIdx + 1;
            }
            else
            {
                // 3. 평문 폴백: 첫 어절 공백 또는 앞 3글자 뒤
                int spaceIdx = rawText.IndexOf(' ', StringComparison.Ordinal);
                splitIdx = (spaceIdx > 0 && spaceIdx <= 6) ? spaceIdx : Mathf.Min(rawText.Length, 3);
            }

            return (rawText, splitIdx, 0.45f);
        }

        private void ApplyBubbleText(string text, int stringId)
        {
            if (answerTextIdHolder != null && stringId > 0)
            {
                answerTextIdHolder.SetId(stringId);
            }
            else if (answerTextIdHolder != null)
            {
                answerTextIdHolder.SetRawText(text);
            }
            else if (answerBubbleText != null)
            {
                answerBubbleText.text = text;
            }
        }

        private void SetQuestionModalVisible(bool visible)
        {
            if (questionModalRoot != null)
            {
                questionModalRoot.SetActive(visible);
            }
        }

        private void SetAnswerBubbleVisible(bool visible, bool animate)
        {
            if (answerBubbleRoot == null) return;

            StopTyping();
            answerBubbleRoot.DOKill();

            if (visible)
            {
                answerBubbleRoot.gameObject.SetActive(true);
                if (animate)
                {
                    answerBubbleRoot.localScale = Vector3.zero;
                    answerBubbleRoot.DOScale(1f, 0.25f).SetEase(Ease.OutBack);
                }
                else
                {
                    answerBubbleRoot.localScale = Vector3.one;
                }
            }
            else
            {
                StopAnswerBubbleShake();

                if (animate && answerBubbleRoot.gameObject.activeSelf)
                {
                    answerBubbleRoot.DOScale(0f, 0.18f).SetEase(Ease.InBack).OnComplete(() =>
                    {
                        if (answerBubbleRoot != null)
                        {
                            answerBubbleRoot.gameObject.SetActive(false);
                            answerBubbleRoot.localScale = Vector3.one;
                            StopAnswerBubbleShake();
                        }
                    });
                }
                else
                {
                    answerBubbleRoot.gameObject.SetActive(false);
                    answerBubbleRoot.localScale = Vector3.one;
                    StopAnswerBubbleShake();
                }
            }
        }

        #region Auto UI Construction (Fallback when not wired in Scene/Prefab)

        private void EnsureEntryButton(Transform uiParent, RectTransform playerFigure)
        {
            if (openQuestionButton != null)
            {
                openQuestionButton.onClick.RemoveListener(OpenQuestionModal);
                openQuestionButton.onClick.AddListener(OpenQuestionModal);
                return;
            }

            var btnGo = new GameObject("Button_Question");
            btnGo.transform.SetParent(uiParent, false);

            var rect = btnGo.AddComponent<RectTransform>();
            // 좌측 플레이어 스탠딩 옆/스킬 아이콘 인근 배치
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(145f, -55f);
            rect.sizeDelta = new Vector2(88f, 110f);

            var img = btnGo.AddComponent<Image>();
            img.color = ActiveButtonColor;

            var outline = btnGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.15f, 0.08f, 0.02f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);

            openQuestionButton = btnGo.AddComponent<Button>();
            openQuestionButton.targetGraphic = img;
            openQuestionButton.onClick.AddListener(OpenQuestionModal);

            var textGo = new GameObject("Text_Label");
            textGo.transform.SetParent(btnGo.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(6f, 6f);
            textRect.offsetMax = new Vector2(-6f, -6f);

            openButtonLabel = textGo.AddComponent<TextMeshProUGUI>();
            if (fallbackFont != null) openButtonLabel.font = fallbackFont;
            openButtonLabel.text = "질문";
            openButtonLabel.fontSize = 24f;
            openButtonLabel.fontStyle = FontStyles.Bold;
            openButtonLabel.alignment = TextAlignmentOptions.Center;
            openButtonLabel.color = ActiveTextColor;
            openButtonLabel.raycastTarget = false;
        }

        private void EnsureQuestionModal(Transform uiParent)
        {
            if (questionModalRoot != null)
            {
                if (outsideBlockerButton != null)
                {
                    outsideBlockerButton.onClick.RemoveListener(CloseQuestionModal);
                    outsideBlockerButton.onClick.AddListener(CloseQuestionModal);
                }
                return;
            }

            var modalGo = new GameObject("Modal_QuestionChoices");
            modalGo.transform.SetParent(uiParent, false);

            var modalRect = modalGo.AddComponent<RectTransform>();
            modalRect.anchorMin = Vector2.zero;
            modalRect.anchorMax = Vector2.one;
            modalRect.offsetMin = Vector2.zero;
            modalRect.offsetMax = Vector2.zero;

            // 서브 캔버스를 사용하는 카드(GameUI)보다 항상 위에 그려지고 외부 클릭을 완전 차단하도록 Popup 레이어 부여
            var modalCanvas = modalGo.AddComponent<Canvas>();
            modalCanvas.overrideSorting = true;
            modalCanvas.sortingLayerName = "Popup";
            modalCanvas.sortingOrder = 50;
            modalGo.AddComponent<GraphicRaycaster>();

            // 1. 대사 버튼 외 영역 클릭 차단 및 닫기용 전체화면 Blocker
            var blockerGo = new GameObject("OutsideBlocker");
            blockerGo.transform.SetParent(modalGo.transform, false);
            var blockerRect = blockerGo.AddComponent<RectTransform>();
            blockerRect.anchorMin = Vector2.zero;
            blockerRect.anchorMax = Vector2.one;
            blockerRect.offsetMin = Vector2.zero;
            blockerRect.offsetMax = Vector2.zero;

            var blockerImg = blockerGo.AddComponent<Image>();
            blockerImg.color = new Color(0f, 0f, 0f, 0.25f);
            blockerImg.raycastTarget = true;

            outsideBlockerButton = blockerGo.AddComponent<Button>();
            outsideBlockerButton.transition = Selectable.Transition.None;
            outsideBlockerButton.onClick.AddListener(CloseQuestionModal);

            // 2. 대사 선택지 리스트 컨테이너 (우측 중앙 정렬)
            var listGo = new GameObject("QuestionListContainer");
            listGo.transform.SetParent(modalGo.transform, false);
            questionListContainer = listGo.AddComponent<RectTransform>();
            questionListContainer.anchorMin = new Vector2(0.58f, 0.54f);
            questionListContainer.anchorMax = new Vector2(0.58f, 0.54f);
            questionListContainer.pivot = new Vector2(0.5f, 0.5f);
            questionListContainer.anchoredPosition = Vector2.zero;
            questionListContainer.sizeDelta = new Vector2(560f, 320f);

            var layout = listGo.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            questionModalRoot = modalGo;
        }

        private QuestionButtonSlot CreateQuestionSlot(int index)
        {
            GameObject btnGo;
            if (questionButtonTemplate != null)
            {
                btnGo = Instantiate(questionButtonTemplate.gameObject, questionListContainer);
                btnGo.SetActive(true);
            }
            else
            {
                btnGo = new GameObject($"Button_Question_{index}");
                btnGo.transform.SetParent(questionListContainer, false);

                var rect = btnGo.AddComponent<RectTransform>();
                rect.sizeDelta = new Vector2(540f, 62f);

                var img = btnGo.AddComponent<Image>();
                img.color = ActiveButtonColor;

                var outline = btnGo.AddComponent<Outline>();
                outline.effectColor = new Color(0.15f, 0.08f, 0.02f, 0.9f);
                outline.effectDistance = new Vector2(2f, -2f);

                var btn = btnGo.AddComponent<Button>();
                btn.targetGraphic = img;
                btn.transition = Selectable.Transition.None;

                var textGo = new GameObject("Text_Question");
                textGo.transform.SetParent(btnGo.transform, false);
                var textRect = textGo.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(18f, 6f);
                textRect.offsetMax = new Vector2(-18f, -6f);

                var tmp = textGo.AddComponent<TextMeshProUGUI>();
                if (fallbackFont != null) tmp.font = fallbackFont;
                tmp.fontSize = 21f;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = ActiveTextColor;
                tmp.raycastTarget = false;

                textGo.AddComponent<TextIdHolder>();
            }

            var buttonComp = btnGo.GetComponent<Button>();
            var bgImage = btnGo.GetComponent<Image>();
            var tmpComp = btnGo.GetComponentInChildren<TextMeshProUGUI>();
            var holderComp = btnGo.GetComponentInChildren<TextIdHolder>();

            int capturedIdx = index;
            if (buttonComp != null)
            {
                buttonComp.onClick.RemoveAllListeners();
                buttonComp.onClick.AddListener(() => OnQuestionButtonClicked(capturedIdx));
            }

            return new QuestionButtonSlot
            {
                index = capturedIdx,
                rootObject = btnGo,
                button = buttonComp,
                backgroundImage = bgImage,
                textComponent = tmpComp,
                textIdHolder = holderComp
            };
        }

        private void EnsureAnswerBubble(Transform uiParent, RectTransform enemyFigure)
        {
            if (answerBubbleRoot != null) return;

            var bubbleGo = new GameObject("SpeechBubble_EnemyAnswer");
            bubbleGo.transform.SetParent(uiParent, false);

            answerBubbleRoot = bubbleGo.AddComponent<RectTransform>();
            answerBubbleRoot.anchorMin = new Vector2(0.56f, 0.42f);
            answerBubbleRoot.anchorMax = new Vector2(0.56f, 0.42f);
            answerBubbleRoot.pivot = new Vector2(0.5f, 0.5f);
            answerBubbleRoot.anchoredPosition = Vector2.zero;
            answerBubbleRoot.sizeDelta = new Vector2(520f, 110f);

            var bgImg = bubbleGo.AddComponent<Image>();
            bgImg.color = ActiveButtonColor;
            // 말풍선이 떠 있는 동안 유저가 카드를 선택하는 데 방해되지 않도록 raycastTarget 해제
            bgImg.raycastTarget = false;

            var outline = bubbleGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.15f, 0.08f, 0.02f, 0.95f);
            outline.effectDistance = new Vector2(2.5f, -2.5f);

            var textGo = new GameObject("Text_Answer");
            textGo.transform.SetParent(bubbleGo.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(20f, 12f);
            textRect.offsetMax = new Vector2(-20f, -12f);

            answerBubbleText = textGo.AddComponent<TextMeshProUGUI>();
            if (fallbackFont != null) answerBubbleText.font = fallbackFont;
            answerBubbleText.fontSize = 22f;
            answerBubbleText.fontStyle = FontStyles.Bold;
            answerBubbleText.alignment = TextAlignmentOptions.Center;
            answerBubbleText.enableWordWrapping = true;
            answerBubbleText.color = ActiveTextColor;
            answerBubbleText.raycastTarget = false;

            answerTextIdHolder = textGo.AddComponent<TextIdHolder>();
        }

        #endregion
    }
}

