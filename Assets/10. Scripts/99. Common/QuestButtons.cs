// ─────────────────────────────────────────────
// QuestButtons — 씬 기능을 Meta Quest 컨트롤러 버튼(A · B · X · Y · 스틱 누르기)에 붙이는 공용 도우미 (세션 YF)
// 학습 포인트
//  - 버튼은 장치 이름이 아니라 '용도(usage)'로 묶습니다. {PrimaryButton} 은 오른손 A · 왼손 X, {SecondaryButton} 은 오른손 B · 왼손 Y 입니다
//  - 같은 InputAction 에 키보드 키를 두 번째 바인딩으로 붙이면 에디터 시뮬레이터에서도 같은 코드로 시험할 수 있습니다
//    (안내판에는 Quest 버튼 이름만 적고, 키보드 키는 적지 않습니다)
//  - 트리거 · 그립 · 스틱(기울이기)은 XRI 가 잡기 · UI · 텔레포트에 쓰므로 여기서 배정하지 않습니다. 왼손 메뉴(≡)는 런처로 돌아가기입니다
// 조작
//  - 없음 (씬 스크립트가 Create 로 만들어 씀)
// ─────────────────────────────────────────────
using UnityEngine.InputSystem;

namespace StudyXR.Common
{
    public static class QuestButtons
    {
        // 버튼 하나짜리 InputAction 을 만듭니다. keyboardFallback 은 "<Keyboard>/f2" 같은 시뮬레이터용 경로(없으면 null)
        // 만든 쪽이 OnEnable 에서 Enable(), OnDisable 에서 Disable() · Dispose() 합니다
        public static InputAction Create(QuestButton button, string actionName, string keyboardFallback = null)
        {
            var action = new InputAction(actionName, InputActionType.Button, Path(button));
            if (!string.IsNullOrEmpty(keyboardFallback)) action.AddBinding(keyboardFallback);
            return action;
        }

        public static string Path(QuestButton button)
        {
            switch (button)
            {
                case QuestButton.A: return "<XRController>{RightHand}/{PrimaryButton}";
                case QuestButton.B: return "<XRController>{RightHand}/{SecondaryButton}";
                case QuestButton.X: return "<XRController>{LeftHand}/{PrimaryButton}";
                case QuestButton.Y: return "<XRController>{LeftHand}/{SecondaryButton}";
                case QuestButton.RightStickClick: return "<XRController>{RightHand}/{Primary2DAxisClick}";
                default: return "<XRController>{LeftHand}/{Primary2DAxisClick}";
            }
        }

        // 안내판에 쓰는 이름. 예: "오른손 A"
        public static string Label(QuestButton button)
        {
            switch (button)
            {
                case QuestButton.A: return "오른손 A";
                case QuestButton.B: return "오른손 B";
                case QuestButton.X: return "왼손 X";
                case QuestButton.Y: return "왼손 Y";
                case QuestButton.RightStickClick: return "오른손 스틱 누르기";
                default: return "왼손 스틱 누르기";
            }
        }

        // 안내판에 함께 쓰는 XRI 기본 조작 이름
        public const string Trigger = "트리거";
        public const string Grip = "그립";
        public const string Stick = "스틱";
        public const string Menu = "왼손 메뉴(≡)";
    }
}
