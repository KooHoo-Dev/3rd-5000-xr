using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StudyXR.EditorTools
{
    /// <summary>
    /// 씬의 글자를 모두 scale 배로 키웁니다. 세션 YF 에 02 XR Interaction Lab 용으로 만들고("글자들이 작아서 잘 안 보임"),
    /// 세션 YH 에 03 Throw Range 도 같은 요청을 받아 공용(99. Common/Editor)으로 옮겼습니다.
    ///  - 월드 캔버스(안내판 · 입구 안내판 + 버튼 · 탈출 책상 안내) : 맨 바깥 캔버스의 localScale 을 scale 배 → 글자 · 버튼 · 배경판(Backdrop)이 함께 커짐
    ///  - 캔버스 밖 3D 글자(TextMeshPro — 02 Examples 이동 설정 테이블의 라벨 · 성능 표시 · 03 과녁 이름표 등) : 글꼴 · 자동 크기 범위 · 글 상자 · 여백을 scale 배.
    ///    왼쪽 · 오른쪽 정렬 글자는 정렬한 쪽 끝을 제자리에 둡니다(피벗 기준으로 키우면 'Move/Strafe' · 'Teleport/Turn' 처럼 서로 다가붙음).
    ///  - 커진 3D 라벨이 옆 라벨과 겹치면(버튼 간격 0.2 m 에 맞춘 원래 배치) 먼저 단어 단위로 줄을 바꿔 위로 늘리고, 그래도 겹치면 옆으로 조금 벌립니다.
    /// 원본(XRI_Examples 프리팹)은 고치지 않고 씬 사본의 인스턴스 값만 바꿉니다 — 프리팹 인스턴스 덮어쓰기로 기록.
    /// </summary>
    internal static class SceneTextEnlarger
    {
        private const float Gap = 0.012f;          // 옆 라벨과 띄울 거리 (m)
        private const float MinColumnGap = 0.05f;  // 가운데끼리 이보다 가까우면 같은 칸(위아래로 쌓인 라벨)으로 보고 건드리지 않음
        private const float MaxNudge = 0.05f;      // 옆으로 벌릴 때 한 라벨이 움직일 수 있는 최대 거리 (m)

        public static void EnlargeAll(Scene scene, float scale)
        {
            int canvases = 0;
            var labels = new List<TMP_Text>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                {
                    if (canvas.renderMode != RenderMode.WorldSpace) continue;
                    Transform parent = canvas.transform.parent;
                    if (parent != null && parent.GetComponentInParent<Canvas>(true) != null) continue;   // 안쪽 캔버스는 바깥과 함께 커짐
                    canvas.transform.localScale *= scale;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(canvas.transform);
                    canvases++;
                }

                foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (text.GetComponentInParent<Canvas>(true) != null) continue;   // 캔버스 안 글자는 위에서 함께 커짐
                    Enlarge3DText(text, scale);
                    labels.Add(text);
                }
            }

            int wrapped = 0;
            int nudged = 0;
            int left = Separate(labels, ref wrapped, ref nudged);
            foreach (TMP_Text text in labels)
            {
                SyncAnchoredPosition(text.rectTransform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(text);
                PrefabUtility.RecordPrefabInstancePropertyModifications(text.rectTransform);
            }
            Debug.Log($"[StudyXR] {scene.name} 글자 {scale} 배 : 월드 캔버스 {canvases} 개 · 3D 글자 {labels.Count} 개 (줄바꿈 {wrapped} · 벌림 {nudged})");
            if (left > 0) Debug.LogWarning($"[StudyXR] {scene.name} 글자 {scale} 배 뒤에도 겹치는 3D 라벨 쌍 {left} 개 — 캡처로 확인하세요");
        }

        private static void Enlarge3DText(TMP_Text text, float scale)
        {
            RectTransform rect = text.rectTransform;
            float oldWidth = rect.sizeDelta.x;
            text.fontSize *= scale;
            text.fontSizeMin *= scale;
            text.fontSizeMax *= scale;
            text.margin *= scale;
            rect.sizeDelta *= scale;

            // 정렬한 쪽 끝을 제자리에 : 왼쪽 끝 = -pivot.x · w, 오른쪽 끝 = (1 - pivot.x) · w
            float grow = rect.sizeDelta.x - oldWidth;
            float shift = 0f;
            if (text.horizontalAlignment == HorizontalAlignmentOptions.Left) shift = rect.pivot.x * grow;
            else if (text.horizontalAlignment == HorizontalAlignmentOptions.Right) shift = -(1f - rect.pivot.x) * grow;
            if (shift != 0f) rect.position += rect.TransformVector(new Vector3(shift, 0f, 0f));
        }

        // 부모가 RectTransform 이 아닌 3D 글자는 position 을 옮겨도 직렬화 값 m_AnchoredPosition(x · y)이 그대로라,
        // 프리팹 인스턴스 덮어쓰기에 옆 · 위 이동이 기록되지 않습니다(저장 뒤 다시 열면 제자리). 직렬화 값을 직접 맞춰 기록되게 합니다
        private static void SyncAnchoredPosition(RectTransform rect)
        {
            var serialized = new SerializedObject(rect);
            SerializedProperty anchored = serialized.FindProperty("m_AnchoredPosition");
            if (anchored == null) return;
            anchored.vector2Value = rect.anchoredPosition;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── 겹침 풀기 ──

        // 라벨의 글자 잉크 상자 : 기준 라벨(평면)의 오른쪽 · 위 축으로 잰 가운데와 반너비 (m)
        private readonly struct Ink
        {
            public Ink(Vector2 center, Vector2 half)
            {
                Center = center;
                Half = half;
            }

            public Vector2 Center { get; }
            public Vector2 Half { get; }
        }

        private static Ink Measure(TMP_Text text, Transform frame)
        {
            text.ForceMeshUpdate();
            Bounds b = text.textBounds;
            Vector3 c = text.transform.TransformPoint(b.center);
            Vector3 ex = text.transform.TransformVector(new Vector3(b.extents.x, 0f, 0f));
            Vector3 ey = text.transform.TransformVector(new Vector3(0f, b.extents.y, 0f));
            return new Ink(new Vector2(Vector3.Dot(c, frame.right), Vector3.Dot(c, frame.up)), new Vector2(ex.magnitude, ey.magnitude));
        }

        private static bool Visible(TMP_Text text) => text.gameObject.activeInHierarchy && text.enabled && !string.IsNullOrWhiteSpace(text.text);

        // 같은 평면(앞 방향이 같고 앞뒤 거리 2 cm 안)에 있는 라벨인지
        private static bool SamePlane(TMP_Text a, TMP_Text b)
        {
            Vector3 fa = a.transform.forward;
            return Vector3.Dot(fa, b.transform.forward) > 0.99f && Mathf.Abs(Vector3.Dot(b.transform.position - a.transform.position, fa)) < 0.02f;
        }

        // 나란히 놓인 두 라벨(다른 칸 · 같은 줄)이 Gap 보다 가까이 겹치는지. 겹치면 모자란 거리(m)를, 아니면 0 을 돌려줍니다
        private static float SideOverlap(TMP_Text a, TMP_Text b)
        {
            if (a == b || !Visible(a) || !Visible(b) || !SamePlane(a, b)) return 0f;
            Ink ia = Measure(a, a.transform);
            Ink ib = Measure(b, a.transform);
            float dx = Mathf.Abs(ia.Center.x - ib.Center.x);
            if (dx < MinColumnGap) return 0f;
            float overlapY = Mathf.Min(ia.Center.y + ia.Half.y, ib.Center.y + ib.Half.y) - Mathf.Max(ia.Center.y - ia.Half.y, ib.Center.y - ib.Half.y);
            if (overlapY < 0.4f * Mathf.Min(ia.Half.y, ib.Half.y) * 2f) return 0f;   // 위아래로 거의 안 겹치면 다른 줄
            return Mathf.Max(0f, ia.Half.x + ib.Half.x + Gap - dx);
        }

        // 줄을 바꿔 위로 늘린 라벨이, 예전 윗끝(oldTop)보다 위에 있던 라벨(위 줄 · 제목)에 닿는지
        private static bool TouchesAbove(TMP_Text a, float oldTop, List<TMP_Text> labels)
        {
            Ink ia = Measure(a, a.transform);
            foreach (TMP_Text b in labels)
            {
                if (b == a || !Visible(b) || !SamePlane(a, b)) continue;
                Ink ib = Measure(b, a.transform);
                if (ib.Center.y - ib.Half.y < oldTop - 0.002f) continue;   // 위에 있던 라벨만
                if (Mathf.Abs(ia.Center.x - ib.Center.x) < ia.Half.x + ib.Half.x && ia.Center.y + ia.Half.y > ib.Center.y - ib.Half.y - Gap * 0.5f) return true;
            }
            return false;
        }

        private static int Separate(List<TMP_Text> labels, ref int wrapped, ref int nudged)
        {
            // 1) 줄바꿈 : 옆과 겹치는 가운데 정렬 라벨의 글 상자를 '옆 라벨까지 거리'로 좁혀 단어 단위로 줄을 바꾸고, 아래 끝을 제자리에 둡니다(위로 늘어남)
            foreach (TMP_Text a in labels)
            {
                if (!Visible(a) || a.horizontalAlignment != HorizontalAlignmentOptions.Center) continue;
                float slot = float.MaxValue;
                foreach (TMP_Text b in labels)
                {
                    if (SideOverlap(a, b) <= 0f) continue;
                    slot = Mathf.Min(slot, Mathf.Abs(Measure(a, a.transform).Center.x - Measure(b, a.transform).Center.x) - Gap);
                }
                if (slot == float.MaxValue) continue;
                if (TryWrap(a, slot, labels)) wrapped++;
            }

            // 2) 벌리기 : 그래도 겹치는 쌍은 서로 반씩 옆으로 (라벨당 최대 MaxNudge)
            var moved = new Dictionary<TMP_Text, float>();
            for (int iteration = 0; iteration < 30; iteration++)
            {
                bool any = false;
                foreach (TMP_Text a in labels)
                {
                    foreach (TMP_Text b in labels)
                    {
                        float need = SideOverlap(a, b);
                        if (need <= 0f) continue;
                        any = true;
                        float side = Mathf.Sign(Measure(a, a.transform).Center.x - Measure(b, a.transform).Center.x);
                        Nudge(a, a.transform.right * side, need * 0.5f + 0.0005f, moved);
                        Nudge(b, -a.transform.right * side, need * 0.5f + 0.0005f, moved);
                    }
                }
                if (!any) break;
            }
            nudged = moved.Count;

            int left = 0;
            for (int i = 0; i < labels.Count; i++)
                for (int j = i + 1; j < labels.Count; j++)
                    if (SideOverlap(labels[i], labels[j]) > Gap * 0.5f) left++;
            return left;
        }

        private static bool TryWrap(TMP_Text text, float slotWidth, List<TMP_Text> labels)
        {
            RectTransform rect = text.rectTransform;
            Vector2 oldSize = rect.sizeDelta;
            Vector3 oldPosition = rect.position;
            TextWrappingModes oldWrap = text.textWrappingMode;
            Ink oldInk = Measure(text, text.transform);
            float oldTop = oldInk.Center.y + oldInk.Half.y;
            float oldBottom = text.textBounds.min.y;

            // 단어 하나보다 좁히면 단어 가운데서 끊기므로 가장 긴 단어 너비가 하한
            float unit = rect.TransformVector(Vector3.right).magnitude;   // 로컬 1 = 몇 m
            float widest = 0f;
            foreach (string word in text.text.Split(' ', '\n'))
                if (word.Length > 0) widest = Mathf.Max(widest, text.GetPreferredValues(word).x);
            float width = Mathf.Max(slotWidth / unit, widest + 0.01f);
            if (width >= oldSize.x) return false;

            // 가운데를 지키며 좁힘 (피벗이 가운데가 아니면 상자 가운데가 움직이므로 보정)
            rect.sizeDelta = new Vector2(width, oldSize.y);
            rect.position += rect.TransformVector(new Vector3((0.5f - rect.pivot.x) * (oldSize.x - width), 0f, 0f));
            text.textWrappingMode = TextWrappingModes.Normal;
            text.ForceMeshUpdate();
            float newBottom = text.textBounds.min.y;
            rect.position += rect.TransformVector(new Vector3(0f, oldBottom - newBottom, 0f));   // 아래 끝 제자리 → 위로 늘어남

            if (!TouchesAbove(text, oldTop, labels)) return true;
            // 위 줄 라벨(제목 등)에 닿으면 되돌리고 옆으로 벌리기에 맡김
            rect.sizeDelta = oldSize;
            rect.position = oldPosition;
            text.textWrappingMode = oldWrap;
            text.ForceMeshUpdate();
            return false;
        }

        private static void Nudge(TMP_Text text, Vector3 direction, float distance, Dictionary<TMP_Text, float> moved)
        {
            moved.TryGetValue(text, out float done);
            float step = Mathf.Min(distance, MaxNudge - done);
            if (step <= 0f) return;
            text.rectTransform.position += direction * step;
            moved[text] = done + step;
        }
    }
}
