using UnityEngine;

namespace Core.Tutorial
{
    public static class TutorialProgress
    {
        private const string PrefKeyPrefix = "Tutorial_Completed_";

        public static bool IsCompleted(string scenarioKey)
        {
            if (string.IsNullOrEmpty(scenarioKey)) return false;
            return PlayerPrefs.GetInt(PrefKeyPrefix + scenarioKey, 0) == 1;
        }

        public static void SetCompleted(string scenarioKey, bool completed = true)
        {
            if (string.IsNullOrEmpty(scenarioKey)) return;
            PlayerPrefs.SetInt(PrefKeyPrefix + scenarioKey, completed ? 1 : 0);
            PlayerPrefs.Save();
            Debug.Log($"[TutorialProgress] '{scenarioKey}' completed set to: {completed}");
        }

        public static void ResetProgress(string scenarioKey)
        {
            if (string.IsNullOrEmpty(scenarioKey)) return;
            PlayerPrefs.DeleteKey(PrefKeyPrefix + scenarioKey);
            PlayerPrefs.Save();
            Debug.Log($"[TutorialProgress] Reset progress for: '{scenarioKey}'");
        }

        public static void ResetAll()
        {
            ResetProgress("Battle_Iroha_1002");
            ResetProgress("Tutorial_Global_Fever");
            PlayerPrefs.Save();
            Debug.Log("[TutorialProgress] Reset all tutorial progress completed (Iroha 1002, Global Fever).");
        }
    }
}
