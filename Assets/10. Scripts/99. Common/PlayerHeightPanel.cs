// ─────────────────────────────────────────────
// PlayerHeightPanel — 런처의 '눈높이' 판. 앉아서 · 서서를 고르고 5 cm 씩 미세 조절한다
// 학습 포인트
//  - 판은 값을 바꾸기만 하고, 실제로 높이를 올리는 일은 모든 씬에 있는 PlayerHeightApplier 가 합니다 (UI 와 동작 나누기)
//  - '지금 자세로 맞추기'는 지금 잰 눈높이와 목표 눈높이(1.6 m)의 차이를 보정값으로 씁니다. 키가 달라도 한 번에 맞습니다
// 조작
//  - 레이 + 트리거 : 앉아서 · 서서 · 낮게 · 높게 · 지금 자세로 맞추기
// ─────────────────────────────────────────────
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StudyXR.Common
{
    public class PlayerHeightPanel : MonoBehaviour
    {
        [Header("버튼")]
        [SerializeField] private Button seatedButton;
        [SerializeField] private Button standingButton;
        [SerializeField] private Button lowerButton;
        [SerializeField] private Button higherButton;
        [SerializeField] private Button fitButton;

        [Header("표시")]
        [SerializeField] private TMP_Text readout;
        [SerializeField] private Color normalColor = new Color(0.20f, 0.45f, 0.85f, 1f);
        [SerializeField] private Color selectedColor = new Color(0.95f, 0.60f, 0.15f, 1f);

        private float nextRefresh;

        private void Start()
        {
            if (seatedButton != null) seatedButton.onClick.AddListener(() => PlayerHeight.Offset = PlayerHeight.SeatedOffset);
            if (standingButton != null) standingButton.onClick.AddListener(() => PlayerHeight.Offset = PlayerHeight.StandingOffset);
            if (lowerButton != null) lowerButton.onClick.AddListener(() => PlayerHeight.Offset -= PlayerHeight.Step);
            if (higherButton != null) higherButton.onClick.AddListener(() => PlayerHeight.Offset += PlayerHeight.Step);
            if (fitButton != null) fitButton.onClick.AddListener(FitToCurrentPose);
            Refresh();
        }

        private void OnEnable()
        {
            PlayerHeight.Changed += OnChanged;
        }

        private void OnDisable()
        {
            PlayerHeight.Changed -= OnChanged;
        }

        private void OnChanged(float offset)
        {
            Refresh();
        }

        // 지금 자세(앉거나 서거나)의 실제 눈높이가 목표 눈높이가 되도록 보정값을 정합니다
        private void FitToCurrentPose()
        {
            float raw = PlayerHeightApplier.RawEyeHeight;
            if (float.IsNaN(raw)) return;
            PlayerHeight.Offset = PlayerHeight.StandingEyeHeight - raw;
        }

        // 머리가 움직이면 눈높이 숫자가 바뀌므로 0.2초마다 다시 씁니다
        private void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            Refresh();
        }

        private void Refresh()
        {
            nextRefresh = Time.unscaledTime + 0.2f;

            Paint(seatedButton, PlayerHeight.IsSeated);
            Paint(standingButton, PlayerHeight.IsStanding);

            if (readout == null) return;
            float offset = PlayerHeight.Offset;
            float eye = PlayerHeightApplier.EyeHeight;
            string eyeText = float.IsNaN(eye) ? "-" : $"{eye:0.00} m";
            readout.text = $"지금: <b>{PlayerHeight.ModeName}</b>  ·  보정 {offset:+0.00;-0.00;0.00} m\n눈높이 {eyeText} (서 있는 눈높이 약 {PlayerHeight.StandingEyeHeight:0.0} m)";
        }

        private void Paint(Button button, bool selected)
        {
            if (button == null || button.targetGraphic == null) return;
            button.targetGraphic.color = selected ? selectedColor : normalColor;
        }
    }
}
