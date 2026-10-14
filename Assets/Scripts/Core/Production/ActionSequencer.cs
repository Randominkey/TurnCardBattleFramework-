using UnityEngine;
using DG.Tweening;
using System;
using System.Collections.Generic;
using UnityEngine.UI;

namespace Core.View
{
    /// <summary>
    /// 모든 연출 명령의 공통 인터페이스
    /// </summary>
    public interface IActionCommand
    {
        // 연출이 끝나면 onComplete를 호출해야 다음 연출로 넘어갑니다.
        void Execute(Action onComplete);
    }

    public class ActionSequencer
    {
        private Queue<IActionCommand> commandQueue = new Queue<IActionCommand>();
        private bool isPlaying = false;
        private Action currentOnAllComplete;

        public bool IsPlaying => isPlaying;

        public void AddCommand(IActionCommand command) => commandQueue.Enqueue(command);

        public void Stop()
        {
            Debug.Log("[Sequencer] Stop called. Clearing queue and isPlaying=false.");
            commandQueue.Clear();
            isPlaying = false;
            currentOnAllComplete = null;
        }

        public void Play(Action onAllComplete = null)
        {
            if (isPlaying)
            {
                Debug.Log("[Sequencer] Play called while already playing. Stopping previous and restarting.");
                Stop();
            }

            if (commandQueue.Count == 0)
            {
                Debug.Log("[Sequencer] Play called with empty queue. Invoking callback immediately.");
                onAllComplete?.Invoke();
                return;
            }

            Debug.Log($"[Sequencer] Starting sequence with {commandQueue.Count} commands.");
            isPlaying = true;
            currentOnAllComplete = onAllComplete;
            ExecuteNext();
        }

        private void ExecuteNext()
        {
            if (commandQueue.Count == 0 || !isPlaying)
            {
                Debug.Log($"[Sequencer] Sequence finished. (QueueCount: {commandQueue.Count}, isPlaying: {isPlaying})");
                var callback = currentOnAllComplete;
                isPlaying = false;
                currentOnAllComplete = null;
                callback?.Invoke();
                return;
            }

            var command = commandQueue.Dequeue();
            Debug.Log($"[Sequencer] Executing command: {command.GetType().Name}");
            command.Execute(() => {
                if (isPlaying)
                {
                    ExecuteNext();
                }
                else
                {
                    Debug.Log("[Sequencer] Command finished but sequencer was stopped. Not advancing.");
                }
            });
        }
    }

    // --- 확장 가능한 커맨드들 ---

    /// <summary>
    /// 이미지 스프라이트를 교체하는 커맨드
    /// </summary>
    public class SpriteCommand : IActionCommand
    {
        private Image target;
        private Sprite newSprite;

        public SpriteCommand(Image target, Sprite sprite)
        {
            this.target = target;
            this.newSprite = sprite;
        }

        public void Execute(Action onComplete)
        {
            if (target != null) target.sprite = newSprite;
            onComplete?.Invoke();
        }
    }

    /// <summary>
    /// UI 요소 이동 커맨드
    /// </summary>
    public class MoveCommand : IActionCommand
    {
        private RectTransform target;
        private Vector2 destination;
        private float duration;
        private Ease ease;

        public MoveCommand(RectTransform target, Vector2 dest, float dur, Ease ease = Ease.OutQuad)
        {
            this.target = target;
            this.destination = dest;
            this.duration = dur;
            this.ease = ease;
        }

        public void Execute(Action onComplete)
        {
            target.DOAnchorPos(destination, duration).SetEase(ease).OnComplete(() => onComplete?.Invoke());
        }
    }

    /// <summary>
    /// 단순 대기 커맨드
    /// </summary>
    public class DelayCommand : IActionCommand
    {
        private float delayTime;
        public DelayCommand(float delay) => delayTime = delay;

        public void Execute(Action onComplete)
        {
            DOVirtual.DelayedCall(delayTime, () => onComplete?.Invoke());
        }
    }

    /// <summary>
    /// 유니티 기본 애니메이터 연동용 커맨드
    /// </summary>
    public class AnimatorCommand : IActionCommand
    {
        private Animator animator;
        private string stateName;
        private float waitTime; // 애니메이션 길이를 알 수 없을 때를 대비한 수동 대기 시간

        public AnimatorCommand(Animator anim, string state, float wait)
        {
            animator = anim;
            stateName = state;
            waitTime = wait;
        }

        public void Execute(Action onComplete)
        {
            animator.Play(stateName);
            // 단순화를 위해 일정 시간 후 완료 호출 (실제로는 StateMachineBehaviour 등으로 감지 가능)
            DOVirtual.DelayedCall(waitTime, () => onComplete?.Invoke());
        }
    }

    /// <summary>
    /// 사운드 재생 커맨드
    /// </summary>
    public class SoundCommand : IActionCommand
    {
        private string soundName;
        private AudioClip clip;
        private bool isBgm;
        private float volume;

        public SoundCommand(string name, bool bgm = false, float vol = -1)
        {
            this.soundName = name;
            this.isBgm = bgm;
            this.volume = vol;
        }

        public SoundCommand(AudioClip clip, float vol = -1)
        {
            this.clip = clip;
            this.isBgm = false;
            this.volume = vol;
        }

        public void Execute(Action onComplete)
        {
            if (Core.Audio.SoundManager.Instance != null)
            {
                if (clip != null)
                {
                    Core.Audio.SoundManager.Instance.PlaySe(clip, volume);
                }
                else if (!string.IsNullOrEmpty(soundName))
                {
                    if (isBgm) Core.Audio.SoundManager.Instance.PlayBgm(soundName, volume);
                    else Core.Audio.SoundManager.Instance.PlaySe(soundName, volume);
                }
            }
            onComplete?.Invoke();
        }
    }

    /// <summary>
    /// 오브젝트 흔들기 연출 커맨드
    /// </summary>
    public class ShakeCommand : IActionCommand
    {
        private RectTransform target;
        private float duration;
        private float strength;

        public ShakeCommand(RectTransform target, float duration, float strength = 10f)
        {
            this.target = target;
            this.duration = duration;
            this.strength = strength;
        }

        public void Execute(Action onComplete)
        {
            target.DOShakeAnchorPos(duration, strength).OnComplete(() => onComplete?.Invoke());
        }
    }

    /// <summary>
    /// 투명도 연출 커맨드 (CanvasGroup 필요)
    /// </summary>
    public class FadeCommand : IActionCommand
    {
        private CanvasGroup target;
        private float endValue;
        private float duration;

        public FadeCommand(CanvasGroup target, float endValue, float duration)
        {
            this.target = target;
            this.endValue = endValue;
            this.duration = duration;
        }

        public void Execute(Action onComplete)
        {
            target.DOFade(endValue, duration).OnComplete(() => onComplete?.Invoke());
        }
    }

    /// <summary>
    /// 화면 전체나 특정 영역을 짧게 번쩍이게 하는 커맨드
    /// </summary>
    public class FlashCommand : IActionCommand
    {
        private Image target;
        private Color flashColor;
        private float duration;

        public FlashCommand(Image target, Color color, float duration = 0.1f)
        {
            this.target = target;
            this.flashColor = color;
            this.duration = duration;
        }

        public void Execute(Action onComplete)
        {
            if (target == null)
            {
                onComplete?.Invoke();
                return;
            }

            Color originalColor = target.color;
            target.color = flashColor;
            target.gameObject.SetActive(true);

            target.DOFade(0, duration).OnComplete(() => {
                target.gameObject.SetActive(false);
                target.color = originalColor;
                onComplete?.Invoke();
            });
        }
    }

    /// <summary>
    /// 임의의 액션을 실행하는 커맨드
    /// </summary>
    public class ActionCommand : IActionCommand
    {
        private Action action;
        public ActionCommand(Action action) => this.action = action;

        public void Execute(Action onComplete)
        {
            action?.Invoke();
            onComplete?.Invoke();
        }
    }

    /// <summary>
    /// 텍스트 폰트 크기 변경 커맨드
    /// </summary>
    public class FontSizeCommand : IActionCommand
    {
        private TMPro.TextMeshProUGUI target;
        private float endSize;
        private float duration;

        public FontSizeCommand(TMPro.TextMeshProUGUI target, float size, float duration = 0)
        {
            this.target = target;
            this.endSize = size;
            this.duration = duration;
        }

        public void Execute(Action onComplete)
        {
            if (target == null)
            {
                onComplete?.Invoke();
                return;
            }

            if (duration <= 0)
            {
                target.fontSize = endSize;
                onComplete?.Invoke();
            }
            else
            {
                DOTween.To(() => target.fontSize, x => target.fontSize = x, endSize, duration)
                    .OnComplete(() => onComplete?.Invoke());
            }
        }
    }

    /// <summary>
    /// 트랜스폼 스케일 변경 커맨드
    /// </summary>
    public class ScaleCommand : IActionCommand
    {
        private Transform target;
        private Vector3 endScale;
        private float duration;
        private Ease ease;

        public ScaleCommand(Transform target, Vector3 scale, float duration, Ease ease = Ease.OutQuad)
        {
            this.target = target;
            this.endScale = scale;
            this.duration = duration;
            this.ease = ease;
        }

        public void Execute(Action onComplete)
        {
            target.DOScale(endScale, duration).SetEase(ease).OnComplete(() => onComplete?.Invoke());
        }
    }

    /// <summary>
    /// 텍스트 타이핑 연출 커맨드 (TextMeshProUGUI 필요)
    /// </summary>
    public class TypewriterCommand : IActionCommand
    {
        private TMPro.TextMeshProUGUI target;
        private string fullText;
        private float speed; // 초당 글자 수

        public TypewriterCommand(TMPro.TextMeshProUGUI target, string text, float speed = 20f)
        {
            this.target = target;
            this.fullText = text;
            this.speed = speed;
        }

        public void Execute(Action onComplete)
        {
            if (target == null)
            {
                onComplete?.Invoke();
                return;
            }

            // 기존에 해당 타겟에서 돌고 있을지 모르는 트윈을 먼저 정리
            DOTween.Kill(target);

            target.text = fullText;
            target.maxVisibleCharacters = 0;
            
            float duration = fullText.Length / speed;
            if (duration <= 0) duration = 0.1f;

            Debug.Log($"[Typewriter] Starting: '{fullText.Substring(0, Mathf.Min(10, fullText.Length))}...' Duration: {duration}s");
            
            DOTween.To(() => target.maxVisibleCharacters, 
                       x => target.maxVisibleCharacters = x, 
                       fullText.Length, 
                       duration)
                   .SetEase(Ease.Linear)
                   .SetTarget(target) // [중요] 타겟을 명시하여 Kill이 작동하게 함
                   .OnComplete(() => {
                       Debug.Log("[Typewriter] Naturally Completed.");
                       onComplete?.Invoke();
                   });
        }
    }
}
