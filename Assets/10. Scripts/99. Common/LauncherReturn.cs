// ─────────────────────────────────────────────
// LauncherReturn — 런처에서 연 씬에서 왼손 메뉴 버튼을 누르면 런처로 돌아간다 (기기 테스트용)
// 학습 포인트
//  - RuntimeInitializeOnLoadMethod 로 씬마다 붙이지 않고 한 번만 만들어 DontDestroyOnLoad 로 유지합니다
//  - 입력은 장치 이름이 아니라 '용도(usage)'로 묶습니다. {MenuButton} 은 Quest 컨트롤러와 시뮬레이터 컨트롤러에 모두 있습니다
// 조작
//  - 왼손 메뉴(≡) : 런처로 돌아가기 (시뮬레이터 [ 후 M)
// ─────────────────────────────────────────────
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace StudyXR.Common
{
    public class LauncherReturn : MonoBehaviour
    {
        // 빌드 목록 0번 씬의 이름. DeviceBuildSetup 이 이 이름으로 런처 씬을 만듭니다
        public const string LauncherSceneName = "Launcher";

        private InputAction menuAction;

        // 첫 씬이 런처일 때만 만듭니다. 에디터에서 모듈 씬을 바로 Play 하면 생기지 않아 시뮬레이터 M 키와 엉키지 않습니다
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateIfStartedFromLauncher()
        {
            Scene first = SceneManager.GetActiveScene();
            if (first.buildIndex != 0 || first.name != LauncherSceneName) return;

            var go = new GameObject("Launcher Return");
            DontDestroyOnLoad(go);
            go.AddComponent<LauncherReturn>();
        }

        private void OnEnable()
        {
            menuAction = new InputAction("Return To Launcher", InputActionType.Button, "<XRController>{LeftHand}/{MenuButton}");
            menuAction.performed += OnMenuPressed;
            menuAction.Enable();
        }

        private void OnDisable()
        {
            if (menuAction == null) return;
            menuAction.performed -= OnMenuPressed;
            menuAction.Disable();
            menuAction.Dispose();
            menuAction = null;
        }

        private void OnMenuPressed(InputAction.CallbackContext context)
        {
            if (SceneManager.GetActiveScene().buildIndex == 0) return;
            SceneManager.LoadScene(0);
        }
    }
}
