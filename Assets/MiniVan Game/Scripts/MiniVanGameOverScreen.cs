using System.Collections;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MiniVanGame
{
    public class MiniVanGameOverScreen : MonoBehaviour
    {
        public static bool IsGameOverActive { get; private set; }
        public static MiniVanGameOverScreen Instance { get; private set; }

        [Header("Editable UI")]
        public CanvasGroup RootGroup;
        public Button ExitToMenuButton;

        [Header("Scene")]
        public string MenuSceneName = "MiniVan_Menu";
        public bool UnlockCursorWhenShown = true;
        public bool EnsureEventSystemWhenShown = true;

        [Header("Resources Fallback")]
        public string ResourcesPrefabPath = "UI/MiniVanGameOverScreen";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetRuntimeState()
        {
            IsGameOverActive = false;
            Instance = null;
        }

        public static void Show()
        {
            MiniVanGameOverScreen screen = EnsureInstance();
            if (screen != null)
            {
                screen.ShowScreen();
            }
        }

        private static MiniVanGameOverScreen EnsureInstance()
        {
            if (Instance != null)
            {
                return Instance;
            }

            Instance = FindFirstObjectByType<MiniVanGameOverScreen>(FindObjectsInactive.Include);
            if (Instance != null)
            {
                return Instance;
            }

            MiniVanGameOverScreen prefab = Resources.Load<MiniVanGameOverScreen>("UI/MiniVanGameOverScreen");
            if (prefab != null)
            {
                Instance = Instantiate(prefab);
                Instance.name = prefab.name;
                return Instance;
            }

            GameObject fallback = new GameObject("MiniVanGameOverScreen_Fallback");
            Instance = fallback.AddComponent<MiniVanGameOverScreen>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;

            if (ExitToMenuButton != null)
            {
                ExitToMenuButton.onClick.RemoveListener(ExitToMenu);
                ExitToMenuButton.onClick.AddListener(ExitToMenu);
            }

            HideImmediate();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            SceneManager.sceneLoaded -= HandleSceneLoaded;

            if (ExitToMenuButton != null)
            {
                ExitToMenuButton.onClick.RemoveListener(ExitToMenu);
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!IsGameOverActive && scene.name == (string.IsNullOrWhiteSpace(MenuSceneName) ? "MiniVan_Menu" : MenuSceneName))
            {
                HideImmediate();
            }
        }

        public void ShowScreen()
        {
            IsGameOverActive = true;
            gameObject.SetActive(true);

            if (RootGroup != null)
            {
                RootGroup.alpha = 1f;
                RootGroup.interactable = true;
                RootGroup.blocksRaycasts = true;
            }

            if (UnlockCursorWhenShown)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (EnsureEventSystemWhenShown)
            {
                EnsureEventSystem();
            }
        }

        public void HideImmediate()
        {
            IsGameOverActive = false;

            if (RootGroup != null)
            {
                RootGroup.alpha = 0f;
                RootGroup.interactable = false;
                RootGroup.blocksRaycasts = false;
            }

            gameObject.SetActive(false);
        }

        public void ExitToMenu()
        {
            IsGameOverActive = false;
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            HideCanvasOnly();
            StartCoroutine(ShutdownNetworkThenLoad());
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("MiniVan Game Over EventSystem");
            DontDestroyOnLoad(eventSystemObject);
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        private void HideCanvasOnly()
        {
            if (RootGroup == null)
            {
                return;
            }

            RootGroup.alpha = 0f;
            RootGroup.interactable = false;
            RootGroup.blocksRaycasts = false;
        }

        private IEnumerator ShutdownNetworkThenLoad()
        {
            Task lobbyCleanup = MiniVanNetworkBootstrap.LeaveCurrentLobbyAsync();
            NetworkManager network = NetworkManager.Singleton;
            if (network != null && network.IsListening)
            {
                network.Shutdown();
                float timeout = Time.realtimeSinceStartup + 2f;
                while (network != null && network.IsListening && Time.realtimeSinceStartup < timeout)
                {
                    yield return null;
                }
            }

            float cleanupTimeout = Time.realtimeSinceStartup + 4f;
            while (lobbyCleanup != null && !lobbyCleanup.IsCompleted && Time.realtimeSinceStartup < cleanupTimeout)
            {
                yield return null;
            }

            MiniVanLaunchState.PendingMode = MiniVanLaunchMode.None;
            MiniVanLaunchState.ActiveMode = MiniVanLaunchMode.None;
            MiniVanLaunchState.StatusMessage = "";
            MiniVanLaunchState.ClearLobby();

            string sceneName = string.IsNullOrWhiteSpace(MenuSceneName) ? "MiniVan_Menu" : MenuSceneName;
            SceneManager.LoadScene(sceneName);
        }
    }
}
