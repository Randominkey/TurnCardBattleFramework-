using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Core.Utils
{
    /// <summary>
    /// 토글(Toggle)의 상태에 따라 오브젝트들의 활성화/비활성화 상태를 동기화하는 유틸리티입니다.
    /// 해당 스크립트가 붙은 오브젝트의 Toggle 컴포넌트를 자동으로 감지하여 리스너를 등록합니다.
    /// </summary>
    [RequireComponent(typeof(Toggle))]
    public class ToggleActiveController : MonoBehaviour
    {
        [Header("Direct Sync (Enable when Toggle is On)")]
        [Tooltip("토글이 켜질 때 같이 켜질 오브젝트들입니다.")]
        [SerializeField] private List<GameObject> directSyncObjects = new List<GameObject>();

        [Header("Inverted Sync (Disable when Toggle is On)")]
        [Tooltip("토글이 켜질 때 반대로 꺼질 오브젝트들입니다.")]
        [SerializeField] private List<GameObject> invertedSyncObjects = new List<GameObject>();

        private Toggle _toggle;
        private bool? _lastState = null; // [New] 중복 방지용 상태 저장

        private void Awake()
        {
            _toggle = GetComponent<Toggle>();
            _toggle.onValueChanged.AddListener(SyncActiveStates);
        }

        private void Start()
        {
            // 초기 상태 동기화
            SyncActiveStates(_toggle.isOn);
        }

        /// <summary>
        /// 토글의 현재 체크 상태에 맞춰 등록된 오브젝트들의 활성 상태를 변경합니다.
        /// </summary>
        /// <param name="isOn">토글의 현재 체크 상태</param>
        public void SyncActiveStates(bool isOn)
        {
            // [New] 상태가 이전과 같다면 실행하지 않음 (Snap-back 방지)
            if (_lastState.HasValue && _lastState.Value == isOn) return;
            _lastState = isOn;

            // 직접 동기화 (Toggle ON -> Object ON)
            foreach (var obj in directSyncObjects)
            {
                if (obj != null) obj.SetActive(isOn);
            }

            // 반전 동기화 (Toggle ON -> Object OFF)
            foreach (var obj in invertedSyncObjects)
            {
                if (obj != null) obj.SetActive(!isOn);
            }
        }
    }
}
