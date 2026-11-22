using UnityEngine;
using UnityEngine.SceneManagement;

namespace CyberRunner.Core
{
    /// <summary>
    /// Entry-point scene loader for Bootstrap.unity.
    /// Ensures core persistent managers exist before transitioning to Gameplay (Phase 1)
    /// or MainMenu (Phase 6+).
    /// </summary>
    public sealed class BootstrapLoader : MonoBehaviour
    {
        [SerializeField] private string initialSceneName = "Gameplay";
        [SerializeField] private GameConfig defaultConfig;

        private void Start()
        {
            EnsureLifecycleManager();

            if (defaultConfig != null && AppLifecycleManager.Instance != null)
            {
                AppLifecycleManager.Instance.SetTargetFrameRate(defaultConfig.defaultTargetFps);
            }

            if (!string.IsNullOrEmpty(initialSceneName) && Application.CanStreamedLevelBeLoaded(initialSceneName))
            {
                SceneManager.LoadScene(initialSceneName, LoadSceneMode.Single);
            }
            else if (SceneManager.sceneCountInBuildSettings > 1)
            {
                SceneManager.LoadScene(1, LoadSceneMode.Single);
            }
        }

        private static void EnsureLifecycleManager()
        {
            if (AppLifecycleManager.Instance != null)
            {
                return;
            }

            var existing = FindFirstObjectByType<AppLifecycleManager>();
            if (existing != null)
            {
                return;
            }

            var managerObject = new GameObject("AppLifecycleManager");
            managerObject.AddComponent<AppLifecycleManager>();
        }
    }
}
