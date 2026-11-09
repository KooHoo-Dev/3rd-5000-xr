// ─────────────────────────────────────────────
// SceneTimestep — 이 씬이 켜져 있는 동안만 물리 시간 간격을 헤드셋 주사율에 맞춘다
// 학습 포인트
//  - Time.fixedDeltaTime 은 씬이 아니라 전역 값이라, 바꾸면 반드시 되돌려야 다른 씬으로 새지 않습니다
//  - 기본 0.02초(50Hz)는 72Hz 화면과 어긋나 물체가 떨려 보입니다. 화면 주사율과 맞추면 매끄럽습니다
// 조작
//  - 없음
// ─────────────────────────────────────────────
using UnityEngine;

namespace StudyXR.Common
{
    public class SceneTimestep : MonoBehaviour
    {
        [SerializeField] private float fixedTimestep = 1f / 72f;

        private float previous;
        private float applied;

        public float Applied => applied;

        private void OnEnable()
        {
            previous = Time.fixedDeltaTime;
            applied = Mathf.Max(0.001f, fixedTimestep);
            Time.fixedDeltaTime = applied;
        }

        private void OnDisable()
        {
            Time.fixedDeltaTime = previous;
            applied = 0f;
        }
    }
}
