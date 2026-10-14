using UnityEngine;
using UnityEngine.Events;

namespace Core.Util
{
    /// <summary>
    /// UI 오브젝트가 활성화(Enable)되거나 비활성화(Disable)되는 시점에 
    /// 인스펙터에서 지정한 이벤트를 실행할 수 있게 해주는 유틸리티 스크립트입니다.
    /// 주로 호버 상태 리셋, DOTween 초기화 등에 사용됩니다.
    /// </summary>
    public class UIEnableTrigger : MonoBehaviour
    {
        [Header("Events")]
        [Tooltip("오브젝트가 활성화될 때 실행할 이벤트")]
        public UnityEvent onEnableEvent;

        [Tooltip("오브젝트가 비활성화될 때 실행할 이벤트")]
        public UnityEvent onDisableEvent;

        private void OnEnable()
        {
            onEnableEvent?.Invoke();
        }

        private void OnDisable()
        {
            onDisableEvent?.Invoke();
        }
    }
}
