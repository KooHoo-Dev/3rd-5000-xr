// ─────────────────────────────────────────────
// PlayerHeight — 앉아서 · 서서 체험할 때의 눈높이 보정값을 기기에 저장한다 (런처에서 고름)
// 학습 포인트
//  - Quest 는 바닥 기준(Floor)으로 머리 높이를 잽니다. 앉아서 쓰면 씬이 서 있는 눈높이(약 1.6 m)로 만들어져 있어 모든 것이 높아 보입니다
//  - 그래서 '보정값'만큼 카메라의 부모(Camera Offset)를 올립니다. 머리 추적은 그대로이고 몸 전체가 떠오르는 효과입니다
//  - 값은 PlayerPrefs 에 저장해 앱을 다시 켜도, 씬을 바꿔도 유지됩니다
// 조작
//  - 런처의 '눈높이' 판에서 고릅니다 (PlayerHeightPanel)
// ─────────────────────────────────────────────
using System;
using UnityEngine;

namespace StudyXR.Common
{
    public static class PlayerHeight
    {
        public const float SeatedOffset = 0.40f;     // 앉은 눈높이(약 1.2 m) → 서 있는 눈높이(약 1.6 m)
        public const float StandingOffset = 0f;
        public const float StandingEyeHeight = 1.60f; // '지금 자세로 맞추기'의 목표 눈높이
        public const float Step = 0.05f;
        public const float MinOffset = -0.50f;
        public const float MaxOffset = 1.00f;

        private const string PrefsKey = "StudyXR.PlayerHeight.Offset";

        // 기본은 앉아서. 단, 에디터의 시뮬레이터 머리는 이미 서 있는 높이라 에디터에서는 서서가 기본입니다
        public static float DefaultOffset => Application.isEditor ? StandingOffset : SeatedOffset;

        public static event Action<float> Changed;

        private static bool loaded;
        private static float cached;

        public static float Offset
        {
            get
            {
                // 매 프레임 읽으므로 처음 한 번만 PlayerPrefs 에서 가져옵니다
                if (!loaded)
                {
                    cached = PlayerPrefs.GetFloat(PrefsKey, DefaultOffset);
                    loaded = true;
                }
                return cached;
            }
            set
            {
                float clamped = Mathf.Clamp(Mathf.Round(value * 100f) / 100f, MinOffset, MaxOffset);
                cached = clamped;
                loaded = true;
                PlayerPrefs.SetFloat(PrefsKey, clamped);
                PlayerPrefs.Save();
                Changed?.Invoke(clamped);
            }
        }

        public static bool IsSeated => Mathf.Abs(Offset - SeatedOffset) < 0.005f;
        public static bool IsStanding => Mathf.Abs(Offset - StandingOffset) < 0.005f;

        public static string ModeName => IsSeated ? "앉아서" : IsStanding ? "서서" : "직접 맞춤";
    }
}
