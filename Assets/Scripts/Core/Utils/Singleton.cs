using UnityEngine;

namespace Core.Utils
{
    public class Singleton<T> : MonoBehaviour where T : Component
    {
        private static T instance;
        private static bool isQuitting = false;

        public static bool HasInstance => instance != null;

        public static T Instance
        {
            get
            {
                if (isQuitting) return null;

                if (instance == null)
                {
                    instance = FindObjectOfType<T>();

                    if (instance == null)
                    {
                        GameObject obj = new GameObject();
                        obj.name = typeof(T).Name;
                        instance = obj.AddComponent<T>();
                    }
                }

                return instance;
            }
        }

        public virtual void Awake()
        {
            if (instance == null)
            {
                instance = this as T;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        protected virtual void OnApplicationQuit()
        {
            isQuitting = true;
        }

        protected virtual void OnDestroy()
        {
            // 앱이 종료 중이 아닐 때만 인스턴스 참조를 해제
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
