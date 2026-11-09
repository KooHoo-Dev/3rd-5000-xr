namespace StudyXR.EditorTools
{
    /// <summary>패널 뒤에 세우는 배경판의 모양. 씬 분위기에 맞춰 빌더가 고릅니다.</summary>
    public enum PanelStyle
    {
        None,       // 예전 모양 : 반투명 검은 판만
        Wood,       // 나무 게시판 : 어두운 칠판 면 + 나무 테 + 지붕 판자 + 땅에 박은 기둥 두 개
        Metal,      // 금속 안내판 : 짙은 회청색 면 + 금속 테 + 둥근 기둥 (실험실 · XRI 예제 · 런처)
        Frame       // 액자 : 얇은 검은 테만, 기둥 없음 (MR — 실제 방 안에 떠 있는 판)
    }
}
