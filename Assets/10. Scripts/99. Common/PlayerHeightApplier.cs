// ─────────────────────────────────────────────
// PlayerHeightApplier — 모든 씬의 XR Origin 에 눈높이 보정값(PlayerHeight.Offset)을 더한다
// 학습 포인트
//  - XR Origin 은 추적 모드(Floor · Device)에 따라 Camera Offset 의 높이를 스스로 정합니다(바닥 기준이면 0)
//  - 그 값을 '바탕 높이'로 기억하고 보정값을 더해 둡니다. 재중심 등으로 XR Origin 이 다시 쓰면 그 값을 새 바탕으로 삼습니다
//  - 텔레포트는 카메라의 높이(CameraInOriginSpaceHeight)를 그대로 두고 옮기므로 보정이 유지됩니다
//  - MR 씬(ARSession 이 있는 씬)은 실제 방과 겹쳐 보여야 하므로 보정하지 않습니다
//  - RuntimeInitializeOnLoadMethod 로 한 번만 만들고 DontDestroyOnLoad 로 씬이 바뀌어도 유지합니다
// 조작
//  - 없음 (런처의 눈높이 판에서 값을 바꾸면 바로 반영)
// ─────────────────────────────────────────────
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;

namespace StudyXR.Common
{
    public class PlayerHeightApplier : MonoBehaviour
    {
        private static PlayerHeightApplier instance;

        private XROrigin origin;
        private bool skipThisScene;
        private float baseHeight;
        private float lastWritten = float.NaN;
        private float applied;

        // 지금 눈높이(XR Origin 바닥에서 카메라까지, 보정 포함). 런처 판의 읽기 값입니다
        public static float EyeHeight
        {
            get
            {
                if (instance == null || instance.origin == null || instance.origin.Camera == null) return float.NaN;
                return instance.origin.CameraInOriginSpaceHeight;
            }
        }

        // 보정을 빼고 잰 실제 눈높이
        public static float RawEyeHeight => EyeHeight - (instance != null ? instance.applied : 0f);

        // 이 씬이 MR 이라 보정하지 않는지
        public static bool Skipped => instance != null && instance.skipThisScene;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (instance != null) return;
            var go = new GameObject("Player Height");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<PlayerHeightApplier>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            FindOrigin();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            FindOrigin();
        }

        private void FindOrigin()
        {
            origin = FindAnyObjectByType<XROrigin>();
            skipThisScene = FindAnyObjectByType<ARSession>() != null;
            lastWritten = float.NaN;
            applied = 0f;
        }

        // XR Origin 이 Camera Offset 을 정한 뒤(같은 프레임의 Update · 코루틴 뒤)에 더합니다
        private void LateUpdate()
        {
            if (origin == null || origin.CameraFloorOffsetObject == null) return;

            Transform offset = origin.CameraFloorOffsetObject.transform;
            Vector3 local = offset.localPosition;

            // 마지막으로 쓴 값과 다르면 XR Origin(또는 처음 상태)이 쓴 값 → 새 바탕 높이
            if (float.IsNaN(lastWritten) || !Mathf.Approximately(local.y, lastWritten)) baseHeight = local.y;

            applied = skipThisScene ? 0f : PlayerHeight.Offset;
            local.y = baseHeight + applied;
            offset.localPosition = local;
            lastWritten = local.y;
        }
    }
}
