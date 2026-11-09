// ─────────────────────────────────────────────
// ScreenFade — 카메라 앞 쿼드의 알파를 바꿔 화면을 어둡게 했다 밝히는 페이드
// 학습 포인트
//  - 순간이동처럼 시야가 갑자기 바뀔 때 페이드를 넣으면 멀미가 줄어듭니다 (VR 편의 기법)
//  - 쿼드 머티리얼을 직접 바꾸면 에셋이 더러워지므로 MaterialPropertyBlock 으로 알파만 덮어씁니다
// 조작
//  - 없음 (다른 스크립트가 FadeOut · FadeIn · FadeOutIn 을 부름)
// ─────────────────────────────────────────────
using System;
using System.Collections;
using UnityEngine;

namespace StudyXR.Common
{
    public class ScreenFade : MonoBehaviour
    {
        [SerializeField] private Renderer fadeRenderer;
        [SerializeField] private float defaultDuration = 0.25f;
        [SerializeField] private Color color = Color.black;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private MaterialPropertyBlock block;
        private Coroutine running;
        private float alpha;

        public bool IsFading => running != null;

        private void Awake()
        {
            Apply(0f);
        }

        private void OnDisable()
        {
            // 페이드 도중 꺼져도 화면이 어두운 채로 남지 않게
            running = null;
            Apply(0f);
        }

        public void FadeOut(float duration = -1f)
        {
            Begin(1f, duration, null, false);
        }

        public void FadeIn(float duration = -1f)
        {
            Begin(0f, duration, null, false);
        }

        // 완전히 어두워진 순간에 midAction 을 한 번 부르고 다시 밝힙니다 (그 사이에 위치를 옮기면 이동이 보이지 않음)
        public void FadeOutIn(Action midAction, float duration = -1f)
        {
            Begin(1f, duration, midAction, true);
        }

        private void Begin(float target, float duration, Action midAction, bool thenBack)
        {
            if (running != null) StopCoroutine(running);
            float seconds = duration < 0f ? defaultDuration : duration;
            running = StartCoroutine(Run(target, seconds, midAction, thenBack));
        }

        private IEnumerator Run(float target, float seconds, Action midAction, bool thenBack)
        {
            yield return Blend(target, seconds);
            if (midAction != null) midAction();
            if (thenBack) yield return Blend(0f, seconds);
            running = null;
        }

        // 일시정지(timeScale 0) 중에도 페이드되도록 unscaledDeltaTime
        private IEnumerator Blend(float target, float seconds)
        {
            float start = alpha;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                Apply(Mathf.Lerp(start, target, t / seconds));
                yield return null;
            }
            Apply(target);
        }

        private void Apply(float value)
        {
            alpha = value;
            if (fadeRenderer == null) return;
            if (block == null) block = new MaterialPropertyBlock();

            // 투명이면 렌더러를 꺼서 쿼드를 그리는 비용을 아낍니다
            fadeRenderer.enabled = value > 0f;
            fadeRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, new Color(color.r, color.g, color.b, value));
            fadeRenderer.SetPropertyBlock(block);
        }
    }
}
