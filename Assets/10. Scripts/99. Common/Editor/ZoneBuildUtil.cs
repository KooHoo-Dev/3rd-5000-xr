using System.Collections.Generic;
using StudyXR.Common;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace StudyXR.EditorTools
{
    /// <summary>
    /// 한 씬을 여러 구역으로 나눌 때 쓰는 빌더 도우미 (세션 YD — 강의 시간이 짧아 비슷한 씬들을 한 공간의 구역으로 합침).
    /// 쓰는 법 : hub = CreateHub(ui, camera) → 구역마다 AddZone(...) → 마지막에 Finish(hub).
    /// 구역마다 입구 안내판(제목 · 설명 · 전체 목록 · 이동 방법 '레이 + 트리거' + 이전 / 다음 버튼)과 도착 자리(Arrival)를 만들고 ZoneHub 에 연결합니다.
    /// </summary>
    public static class ZoneBuildUtil
    {
        public const string FadeMaterialPath = "Assets/00. Study/Common/M_Zone_Fade.mat";

        private class Pending
        {
            public readonly List<Object> arrivals = new List<Object>();
            public readonly List<Object> boards = new List<Object>();
            public readonly List<string> titles = new List<string>();
            public readonly List<string> descriptions = new List<string>();
        }

        private static readonly Dictionary<ZoneHub, Pending> pending = new Dictionary<ZoneHub, Pending>();

        // 리그의 TeleportationProvider 와 화면 페이드를 찾아(없으면 만들어) 연결한 ZoneHub
        public static ZoneHub CreateHub(Transform parent, Camera head)
        {
            Transform node = SceneBuildUtil.Group("Zone Hub", parent);
            ZoneHub hub = node.gameObject.AddComponent<ZoneHub>();

            XROrigin origin = head != null ? head.GetComponentInParent<XROrigin>() : null;
            TeleportationProvider provider = origin != null ? origin.GetComponentInChildren<TeleportationProvider>(true) : null;
            if (provider == null) Debug.LogError("[StudyXR] ZoneHub : XR Origin 에서 TeleportationProvider 를 찾지 못했습니다");

            ScreenFade fade = head != null ? head.GetComponentInChildren<ScreenFade>(true) : null;
            if (fade == null && head != null) fade = SceneBuildUtil.AddScreenFade(head, FadeMaterialPath);

            SceneBuildUtil.SetField(hub, "teleportation", provider);
            SceneBuildUtil.SetField(hub, "screenFade", fade);
            pending[hub] = new Pending();
            return hub;
        }

        // 구역 하나 : arrival = 도착 자리(월드), yaw = 도착해서 보는 방향, board* = 입구 안내판 자리(도착 자리 기준 로컬, 기본 왼쪽 앞)
        // boardScale = 안내판 전체(판 · 글자 · 버튼) 배율. 1.3 이면 글자가 30% 크게 보입니다 (세션 YF — 02 글자 키우기)
        // 돌려주는 값은 구역 루트 — 구역의 소품은 여기에 자식으로 두면 계층이 정리됩니다
        public static Transform AddZone(ZoneHub hub, Transform zonesParent, string title, string description,
            Vector3 arrival, float yaw, Vector3? boardLocalPos = null, float boardLocalYaw = -30f, float boardScale = 1f)
        {
            if (!pending.TryGetValue(hub, out Pending p))
            {
                Debug.LogError("[StudyXR] ZoneBuildUtil.AddZone : CreateHub 로 만든 허브가 아닙니다");
                return null;
            }

            int index = p.arrivals.Count;
            Transform zone = SceneBuildUtil.Group($"Zone {index + 1} · {title}", zonesParent);
            zone.SetPositionAndRotation(arrival, Quaternion.Euler(0f, yaw, 0f));

            Transform arrivalNode = SceneBuildUtil.Group("Arrival", zone);
            arrivalNode.localPosition = Vector3.zero;
            arrivalNode.localRotation = Quaternion.identity;

            Vector3 boardPos = boardLocalPos ?? new Vector3(-0.95f, 1.45f, 1.1f);
            // 빌드 직후 미리 보기 글 — Play 하면 ZoneHub 가 전체 구역 목록으로 바꿔 씁니다. 키보드 키 이름은 적지 않습니다
            string preview = $"<b>{title}</b>\n{description}\n<size=90%><color=#9FD0FF>{ZoneHub.MoveHint}</color></size>";
            TextMeshProUGUI board = SceneBuildUtil.WorldPanel(preview, "Zone Board", zone, boardPos, new Vector3(0f, boardLocalYaw, 0f), new Vector2(0.72f, 0.52f), 22f);
            Button previous = SceneBuildUtil.WorldButton("◀ 이전 구역", "Previous Zone", board, new Vector2(-170f, -210f), new Vector2(300f, 66f), 26f);
            Button next = SceneBuildUtil.WorldButton("다음 구역 ▶", "Next Zone", board, new Vector2(170f, -210f), new Vector2(300f, 66f), 26f);
            board.rectTransform.offsetMin = new Vector2(24f, 90f);   // 아래 90 단위는 버튼 자리
            if (boardScale > 0f && !Mathf.Approximately(boardScale, 1f))
                board.transform.parent.localScale *= boardScale;      // 캔버스(0.001) 배율 — 배경판 · 버튼이 함께 커짐
            if (previous != null) UnityEventTools.AddVoidPersistentListener(previous.onClick, hub.Previous);
            if (next != null) UnityEventTools.AddVoidPersistentListener(next.onClick, hub.Next);

            p.arrivals.Add(arrivalNode);
            p.boards.Add(board);
            p.titles.Add(title);
            p.descriptions.Add(description);
            return zone;
        }

        // 모은 구역을 ZoneHub 배열에 씁니다 (모든 AddZone 뒤에 한 번)
        public static void Finish(ZoneHub hub)
        {
            if (!pending.TryGetValue(hub, out Pending p)) return;
            SceneBuildUtil.SetArray(hub, "arrivals", p.arrivals.ToArray());
            SceneBuildUtil.SetArray(hub, "boards", p.boards.ToArray());
            SetStrings(hub, "titles", p.titles);
            SetStrings(hub, "descriptions", p.descriptions);
            pending.Remove(hub);
        }

        private static void SetStrings(Object target, string field, List<string> values)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[StudyXR] {target.GetType().Name}.{field} 필드를 찾지 못했습니다");
                return;
            }
            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) property.GetArrayElementAtIndex(i).stringValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
