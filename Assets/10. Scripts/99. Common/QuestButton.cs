// ─────────────────────────────────────────────
// QuestButton — 씬 기능에 배정할 수 있는 Meta Quest 컨트롤러 버튼 (QuestButtons 와 함께 씀, 세션 YF)
// 학습 포인트
//  - 트리거 · 그립 · 스틱 기울이기는 XRI 가 쓰므로 목록에 없습니다
// 조작
//  - 없음
// ─────────────────────────────────────────────
namespace StudyXR.Common
{
    public enum QuestButton
    {
        A,                  // 오른손 아래 버튼
        B,                  // 오른손 위 버튼
        X,                  // 왼손 아래 버튼
        Y,                  // 왼손 위 버튼
        RightStickClick,    // 오른손 스틱 누르기
        LeftStickClick,     // 왼손 스틱 누르기
    }
}
