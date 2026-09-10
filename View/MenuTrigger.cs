using UnityEngine;
using Core.Flow;

namespace Core.View
{
    /// <summary>
    /// 씬 내의 버튼에서 MenuController.Instance에 접근하기 위한 프록시 컴포넌트.
    /// 씬 재시작 시 Missing Reference 문제를 방지합니다.
    /// </summary>
    public class MenuTrigger : MonoBehaviour
    {
        public void ToggleMenu()
        {
            MenuController.Instance.ToggleMenu();
        }

        public void PauseGame()
        {
            MenuController.Instance.PauseGame();
        }

        public void ResumeGame()
        {
            MenuController.Instance.ResumeGame();
        }
    }
}
