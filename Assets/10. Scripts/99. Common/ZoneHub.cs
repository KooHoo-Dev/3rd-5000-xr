// ─────────────────────────────────────────────
// ZoneHub — 한 씬을 여러 구역으로 나누고, 구역 안내판의 버튼으로 구역 사이를 페이드하며 옮겨 준다 (세션 YD)
// 학습 포인트
//  - 씬을 여러 개 만드는 대신 한 공간에 구역을 늘어놓으면, 로딩 없이 이어서 배울 수 있습니다
//  - VR 의 이동은 카메라가 아니라 XR Origin(몸)을 옮기는 것입니다. 텔레포트 요청을 TeleportationProvider 에 넣습니다
//  - 순간이동 전후에 페이드를 넣으면 멀미가 줄어듭니다 (ScreenFade.FadeOutIn — 완전히 어두울 때 옮김)
//  - 구역 안에서는 평소처럼 스틱 텔레포트로 걸어 다닙니다. 안내판 이동은 먼 구역 사이를 잇는 지름길입니다
//  - 바닥이 이어져 있어 걸어서 옆 구역에 들어가도, 가장 가까운 도착 자리를 보고 '지금 구역'을 바꿉니다
// 조작
//  - 안내판 버튼(레이 + 트리거) : 이전 구역 · 다음 구역 (시뮬레이터 Page Up · Page Down)
//  - 구역 이동에는 Quest 버튼(A · B · X · Y)을 쓰지 않습니다. 구역 안의 기능(02 · 03)이 그 버튼을 씁니다
// ─────────────────────────────────────────────
using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace StudyXR.Common
{
    public class ZoneHub : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private TeleportationProvider teleportation;
        [SerializeField] private ScreenFade screenFade;

        [Header("구역 (같은 순서)")]
        [SerializeField] private Transform[] arrivals;          // 도착 자리 · 방향 (구역 안내판 앞)
        [SerializeField] private string[] titles;
        [SerializeField] private string[] descriptions;
        [SerializeField] private TMP_Text[] boards;             // 구역마다 입구 안내판의 글자

        [Header("이동")]
        [SerializeField] private float fadeSeconds = 0.35f;

        [Header("걸어서 옮긴 경우")]
        [SerializeField] private bool trackByPosition = true;   // 머리와 가장 가까운 도착 자리의 구역을 지금 구역으로

        [Header("시뮬레이터 테스트 입력")]
        [SerializeField] private bool enableTestKeys = true;    // 에디터 시뮬레이터 대체 입력만 (안내판에는 적지 않음)

        private Transform head;
        private float trackPauseUntil;                          // 안내판 이동 직후에는 리그가 아직 옛 자리라 잠깐 쉼

        public int Current { get; private set; }
        public int Count => arrivals != null ? arrivals.Length : 0;
        public string CurrentTitle => titles != null && Current < titles.Length ? titles[Current] : "";

        // 구역이 바뀐 뒤 (새 구역 번호). 구역마다 켜고 끄는 것이 있으면 여기에 붙입니다
        public event Action<int> ZoneChanged;

        // 안내판 맨 아래 한 줄 — 구역 이동 방법 (ZoneBuildUtil 이 빌드 때 미리 보기 글에도 씀)
        public const string MoveHint = "구역 이동 : 아래 버튼을 레이 + " + QuestButtons.Trigger + "로 누르기";

        private void Start()
        {
            // 처음 자리는 빌더가 리그를 구역 0 에 두었으므로 옮기지 않습니다
            Current = 0;
            RefreshBoards();
        }

        private void Update()
        {
            if (enableTestKeys) ReadTestKeys();
            if (trackByPosition) TrackNearestZone();
        }

        // 시뮬레이터 대체 입력 : Page Down 다음 · Page Up 이전 (기기에서는 안내판 버튼만)
        private void ReadTestKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.pageDownKey.wasPressedThisFrame) Next();
            if (keyboard.pageUpKey.wasPressedThisFrame) Previous();
        }

        // 스틱 텔레포트로 걸어서 옆 구역에 들어간 경우 — 수평 거리로 가장 가까운 도착 자리의 구역
        private void TrackNearestZone()
        {
            if (Count < 2 || Time.time < trackPauseUntil) return;
            if (screenFade != null && screenFade.IsFading) return;
            if (head == null)
            {
                Camera cam = Camera.main;
                if (cam == null) return;
                head = cam.transform;
            }

            int nearest = Current;
            float best = float.MaxValue;
            for (int i = 0; i < Count; i++)
            {
                if (arrivals[i] == null) continue;
                Vector3 d = arrivals[i].position - head.position;
                float sq = d.x * d.x + d.z * d.z;
                if (sq < best) { best = sq; nearest = i; }
            }
            if (nearest != Current) SetCurrent(nearest);
        }

        public void Next()
        {
            if (Count == 0) return;
            GoTo((Current + 1) % Count);
        }

        public void Previous()
        {
            if (Count == 0) return;
            GoTo((Current - 1 + Count) % Count);
        }

        public void GoTo(int index)
        {
            if (index < 0 || index >= Count || arrivals[index] == null) return;
            if (screenFade != null && screenFade.IsFading) return;   // 페이드 도중에 또 누르면 무시

            if (screenFade == null || fadeSeconds <= 0f) Teleport(index);
            else screenFade.FadeOutIn(() => Teleport(index), fadeSeconds);
        }

        // XR Origin 을 도착 자리 · 방향으로 옮겨 달라고 요청합니다 (실제 이동은 Provider 의 다음 Update)
        private void Teleport(int index)
        {
            Transform destination = arrivals[index];
            if (teleportation != null)
            {
                var request = new TeleportRequest
                {
                    destinationPosition = destination.position,
                    destinationRotation = destination.rotation,
                    matchOrientation = MatchOrientation.TargetUpAndForward,
                    requestTime = Time.time,
                };
                teleportation.QueueTeleportRequest(request);
            }

            trackPauseUntil = Time.time + 0.5f;
            SetCurrent(index);
        }

        private void SetCurrent(int index)
        {
            Current = index;
            RefreshBoards();
            ZoneChanged?.Invoke(index);
        }

        // 안내판 : 이 구역의 제목 · 설명 + 전체 구역 목록(지금 있는 곳 표시)
        private void RefreshBoards()
        {
            if (boards == null) return;
            for (int i = 0; i < boards.Length; i++)
            {
                if (boards[i] == null) continue;
                var text = new System.Text.StringBuilder();
                text.Append($"<b>구역 {i + 1} / {Count} · {Title(i)}</b>\n");
                if (descriptions != null && i < descriptions.Length) text.Append(descriptions[i]).Append('\n');
                text.Append("<size=90%>");
                for (int k = 0; k < Count; k++)
                {
                    string mark = k == Current ? "▶ " : "   ";
                    text.Append(mark).Append(k + 1).Append(". ").Append(Title(k)).Append(k == Current ? "  (지금 여기)" : "").Append('\n');
                }
                text.Append("</size>");
                text.Append("<size=90%><color=#9FD0FF>").Append(MoveHint).Append("</color></size>");   // VR 에서 읽히도록 목록과 같은 크기
                boards[i].text = text.ToString();
            }
        }

        private string Title(int index)
        {
            return titles != null && index < titles.Length ? titles[index] : $"구역 {index + 1}";
        }
    }
}
