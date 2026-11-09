// ─────────────────────────────────────────────
// PerfHud — 머리 앞 시야 왼쪽 아래에 fps · 프레임 시간 · 주사율을 작게 띄우는 성능 표시
// 학습 포인트
//  - 기기 성능은 에디터가 아니라 헤드셋에서 봐야 하므로, 월드 텍스트를 머리 기준 고정 위치에 둡니다
//  - fps 는 매 프레임 값이 아니라 0.5초 동안의 평균(평활)으로 보여 줘야 숫자가 읽힙니다
// 조작
//  - 왼손 Y : 켜고 끄기 (모든 씬 공통, 기본 숨김. 시뮬레이터는 F2 도 됨 — QuestButtons)
//  - toggleAction : 켜고 끄기 (빌더가 따로 연결했을 때)
// ─────────────────────────────────────────────
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace StudyXR.Common
{
    public class PerfHud : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private Transform head;
        [SerializeField] private TMP_Text label;
        [SerializeField] private InputActionReference toggleAction;

        [Header("표시")]
        [SerializeField] private bool visibleOnStart = false;
        [SerializeField] private float distance = 0.6f;
        [SerializeField] private Vector2 viewOffset = new Vector2(-0.18f, -0.12f);

        [Header("Quest 버튼")]
        [SerializeField] private bool useQuestButton = true;    // 왼손 Y (시뮬레이터 F2)

        private const float SampleSeconds = 0.5f;

        private readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();
        private float sampleTime;
        private int sampleFrames;
        private float smoothedFps;
        private bool visible;
        private InputAction questToggle;

        public float SmoothedFps => smoothedFps;
        public bool Visible => visible;

        private void Awake()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            SetVisible(visibleOnStart);
        }

        private void OnEnable()
        {
            if (useQuestButton)
            {
                questToggle = QuestButtons.Create(QuestButton.Y, "Perf HUD Toggle", "<Keyboard>/f2");
                questToggle.performed += OnTogglePressed;
                questToggle.Enable();
            }

            if (toggleAction == null || toggleAction.action == null) return;
            toggleAction.action.performed += OnTogglePressed;
            // 기본 XRI 입력 액션은 InputActionManager 가 켜 두지만, 따로 만든 액션이어도 동작하게 합니다
            if (!toggleAction.action.enabled) toggleAction.action.Enable();
        }

        private void OnDisable()
        {
            if (questToggle != null)
            {
                questToggle.performed -= OnTogglePressed;
                questToggle.Disable();
                questToggle.Dispose();
                questToggle = null;
            }

            if (toggleAction != null && toggleAction.action != null) toggleAction.action.performed -= OnTogglePressed;
        }

        public void SetVisible(bool value)
        {
            visible = value;
            if (label != null) label.enabled = value;
        }

        private void OnTogglePressed(InputAction.CallbackContext context)
        {
            SetVisible(!visible);
        }

        private void Update()
        {
            // 숨겨져 있어도 평균은 이어서 계산해 두면 켠 순간 바로 값이 나옵니다
            sampleTime += Time.unscaledDeltaTime;
            sampleFrames++;
            if (sampleTime < SampleSeconds) return;

            smoothedFps = sampleFrames / sampleTime;
            float frameMs = sampleTime / sampleFrames * 1000f;
            sampleTime = 0f;
            sampleFrames = 0;
            if (visible && label != null) label.text = $"FPS {smoothedFps:0.0}\n{frameMs:0.0} ms\n{RefreshRate():0} Hz";
        }

        private void LateUpdate()
        {
            // 머리가 움직인 뒤에 따라가야 떨리지 않으므로 LateUpdate
            if (!visible || head == null || label == null) return;
            Vector3 local = new Vector3(viewOffset.x, viewOffset.y, distance);
            label.transform.SetPositionAndRotation(head.position + head.rotation * local, head.rotation);
        }

        // 헤드셋이 있으면 XR 디스플레이의 주사율, 없으면 모니터 주사율
        private float RefreshRate()
        {
            SubsystemManager.GetSubsystems(displays);
            foreach (XRDisplaySubsystem display in displays)
            {
                if (display.running && display.TryGetDisplayRefreshRate(out float rate)) return rate;
            }
            return (float)Screen.currentResolution.refreshRateRatio.value;
        }
    }
}
