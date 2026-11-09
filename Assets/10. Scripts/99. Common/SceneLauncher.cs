// ─────────────────────────────────────────────
// SceneLauncher — 빌드에 들어간 씬을 버튼으로 띄워 기기에서 고르게 한다 (기기 테스트용)
// 학습 포인트
//  - 기기에는 에디터의 Project 창이 없으므로, 빌드 목록(Build Settings)의 씬을 코드로 읽어 버튼을 만듭니다
//  - 씬이 늘어도 런처 씬을 다시 만들 필요가 없습니다. 빌드 목록만 바꾸면 됩니다
// 조작
//  - 트리거 : 레이로 씬 버튼 · 이전 / 다음 쪽 버튼 누르기
//  - 왼손 메뉴(≡) : (다른 씬에서) 런처로 돌아오기
//  - 왼손 Y : 성능 표시 켜고 끄기 (PerfHud, 모든 씬 공통)
// ─────────────────────────────────────────────
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StudyXR.Common
{
    public class SceneLauncher : MonoBehaviour
    {
        [Header("표시")]
        [SerializeField] private RectTransform buttonArea;
        [SerializeField] private TMP_Text pageText;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;

        [Header("버튼 배치")]
        [SerializeField] private int columns = 2;
        [SerializeField] private int rowsPerPage = 8;
        [SerializeField] private Vector2 buttonSize = new Vector2(440f, 60f);
        [SerializeField] private Vector2 spacing = new Vector2(16f, 12f);
        [SerializeField] private float fontSize = 24f;

        private int page;

        private int SceneCount => SceneManager.sceneCountInBuildSettings - 1;   // 0번은 런처 자신
        private int PerPage => Mathf.Max(1, columns * rowsPerPage);
        private int PageCount => Mathf.Max(1, (SceneCount + PerPage - 1) / PerPage);

        private void Start()
        {
            if (previousButton != null) previousButton.onClick.AddListener(() => ShowPage(page - 1));
            if (nextButton != null) nextButton.onClick.AddListener(() => ShowPage(page + 1));
            ShowPage(0);
        }

        private void ShowPage(int newPage)
        {
            page = Mathf.Clamp(newPage, 0, PageCount - 1);
            if (buttonArea == null) return;

            // 페이지를 바꿀 때마다 버튼을 새로 만듭니다. 한 페이지가 16개라 매번 만들어도 가볍습니다
            for (int i = buttonArea.childCount - 1; i >= 0; i--)
            {
                Destroy(buttonArea.GetChild(i).gameObject);
            }

            int first = page * PerPage;
            for (int slot = 0; slot < PerPage && first + slot < SceneCount; slot++)
            {
                int buildIndex = first + slot + 1;
                string path = SceneUtility.GetScenePathByBuildIndex(buildIndex);
                CreateButton(slot, buildIndex, Label(path));
            }

            if (pageText != null) pageText.text = $"{page + 1} / {PageCount} 쪽 · 씬 {SceneCount}개";
            if (previousButton != null) previousButton.interactable = page > 0;
            if (nextButton != null) nextButton.interactable = page < PageCount - 1;
        }

        // "Assets/00. Study/00. XR Basics/Answers/01. Turn Direction Answer.unity" → "00 · 01. Turn Direction Answer"
        private static string Label(string path)
        {
            string scene = Path.GetFileNameWithoutExtension(path);
            string folder = Path.GetFileName(Path.GetDirectoryName(path));
            if (folder == "Answers") folder = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(path)));
            string module = folder != null && folder.Length >= 2 ? folder.Substring(0, 2) : "--";
            return $"{module} · {scene}";
        }

        private void CreateButton(int slot, int buildIndex, string label)
        {
            int column = slot % columns;
            int row = slot / columns;

            var go = new GameObject($"Scene {buildIndex}", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(buttonArea, false);

            // 버튼 영역의 왼쪽 위를 기준으로 칸을 채웁니다
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = buttonSize;
            rect.anchoredPosition = new Vector2(column * (buttonSize.x + spacing.x), -row * (buttonSize.y + spacing.y));

            var image = go.GetComponent<Image>();
            image.color = new Color(0.20f, 0.45f, 0.85f, 1f);

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => SceneManager.LoadScene(buildIndex));

            var textGo = new GameObject("Label", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 0f);
            textRect.offsetMax = new Vector2(-12f, 0f);

            // 폰트는 TMP 기본 폰트(한글 Noto Sans KR)를 그대로 씁니다
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Left;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
        }
    }
}
