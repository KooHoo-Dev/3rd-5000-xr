using System.Collections.Generic;
using System.IO;
using StudyXR.Common;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.UI;

namespace StudyXR.EditorTools
{
    /// <summary>
    /// 기기(Meta Quest 3) 테스트 빌드용 메뉴. 런처 씬을 만들고, 빌드 목록을 런처 + 모든 모듈 씬으로 채우고, APK 를 만듭니다.
    /// 기기 테스트 목록(docs/기기 테스트 목록.md) P5·P6.
    /// </summary>
    public static class DeviceBuildSetup
    {
        internal const string LauncherFolder = "Assets/00. Study/Launcher";
        internal const string LauncherScenePath = LauncherFolder + "/" + LauncherReturn.LauncherSceneName + ".unity";
        private const string StudyFolder = "Assets/00. Study";
        private const string ApkPath = "Builds/StudyXR.apk";

        // 템플릿 기본 패키지 이름. 같은 템플릿으로 만든 다른 앱과 기기에서 서로 덮어쓰므로 이 이름으로는 빌드하지 않습니다
        private const string TemplatePackageName = "com.UnityTechnologies.com.unity.template.urpblank";

        // 패널 캔버스 단위(1 m = 1000). 런처 패널 1.0 x 0.85 m
        private static readonly Vector2 PanelSize = new Vector2(1.0f, 0.85f);

        [MenuItem("StudyXR/Build/Launcher")]
        public static void BuildLauncherScene()
        {
            if (!SceneBuildUtil.ConfirmOverwrite(LauncherScenePath)) return;
            var scene = SceneBuildUtil.CreateScene(LauncherScenePath);
            PanelBackdrop.Style = PanelStyle.Metal;
            if (!scene.IsValid()) return;

            var xr = SceneBuildUtil.Group("XR");
            Camera xrCamera = SceneBuildUtil.XRRig(xr, Vector3.zero, 0f);

            var lights = SceneBuildUtil.Group("Lights");
            SceneBuildUtil.DefaultSun(lights);

            var stage = SceneBuildUtil.Group("Stage");
            Material floor = SceneBuildUtil.LitMaterial(LauncherFolder + "/Materials/M_Launcher_Floor.mat", new Color(0.35f, 0.38f, 0.42f), 0f, 0.2f);
            SceneBuildUtil.Primitive(PrimitiveType.Plane, "Floor", stage, Vector3.zero, Vector3.zero, Vector3.one, floor);

            var ui = SceneBuildUtil.Group("UI");
            TextMeshProUGUI title = SceneBuildUtil.WorldPanel(
                "StudyXR 기기 테스트 런처\n레이 + 트리거 : 씬 열기 · 왼손 메뉴(≡) : 돌아오기 · 왼손 Y : 성능 표시",
                "Launcher Panel", ui, new Vector3(0f, 1.45f, 1.3f), Vector3.zero, PanelSize, 28f);
            Transform canvas = title.transform.parent;

            // 버튼 영역: 제목 두 줄 아래부터 2열 x 8행 (버튼 440 x 60, 간격 16 x 12)
            var areaGo = new GameObject("Buttons", typeof(RectTransform));
            areaGo.transform.SetParent(canvas, false);
            var area = (RectTransform)areaGo.transform;
            area.pivot = new Vector2(0f, 1f);
            area.anchoredPosition = new Vector2(-448f, 300f);
            area.sizeDelta = new Vector2(896f, 8 * 72f);

            Button previous = SceneBuildUtil.WorldButton("이전", "Previous Button", title, new Vector2(-340f, -360f), new Vector2(200f, 60f), 26f);
            Button next = SceneBuildUtil.WorldButton("다음", "Next Button", title, new Vector2(340f, -360f), new Vector2(200f, 60f), 26f);
            TextMeshProUGUI pageText = CreatePageText(canvas);

            var launcher = canvas.gameObject.AddComponent<SceneLauncher>();
            SceneBuildUtil.SetField(launcher, "buttonArea", area);
            SceneBuildUtil.SetField(launcher, "pageText", pageText);
            SceneBuildUtil.SetField(launcher, "previousButton", previous);
            SceneBuildUtil.SetField(launcher, "nextButton", next);

            BuildHeightPanel(ui);
            SceneBuildUtil.AddPerfHud(ui, xrCamera);                         // 왼손 Y : 성능 표시 (모든 씬 공통, 세션 YF)

            SceneBuildUtil.ApplyEnvironment(EnvironmentPreset.Default, xrCamera);
            SceneBuildUtil.SaveScene(scene);
        }

        // 왼쪽 옆의 '눈높이' 판(0.62 x 0.62 m). 사용자를 향하도록 -42도 돌립니다. 값은 PlayerHeight 가 기기에 저장하고 모든 씬에 반영합니다
        private static void BuildHeightPanel(Transform ui)
        {
            TextMeshProUGUI title = SceneBuildUtil.WorldPanel(
                "<b>눈높이 맞추기</b>\n앉아서 하면 '앉아서'(기본), 서서 하면 '서서'\n고른 높이는 모든 씬에 적용됩니다",
                "Height Panel", ui, new Vector3(-0.95f, 1.3f, 1.05f), new Vector3(0f, -42f, 0f), new Vector2(0.62f, 0.62f), 24f);
            Transform canvas = title.transform.parent;

            var size = new Vector2(250f, 70f);
            Button seated = SceneBuildUtil.WorldButton("앉아서", "Seated Button", title, new Vector2(-135f, 110f), size, 28f);
            Button standing = SceneBuildUtil.WorldButton("서서", "Standing Button", title, new Vector2(135f, 110f), size, 28f);
            Button lower = SceneBuildUtil.WorldButton("낮게 (-5 cm)", "Lower Button", title, new Vector2(-135f, 20f), size, 26f);
            Button higher = SceneBuildUtil.WorldButton("높게 (+5 cm)", "Higher Button", title, new Vector2(135f, 20f), size, 26f);
            Button fit = SceneBuildUtil.WorldButton("지금 자세로 맞추기", "Fit Button", title, new Vector2(0f, -70f), new Vector2(520f, 70f), 26f);

            TextMeshProUGUI readout = CreatePageText(canvas);
            readout.name = "Height Readout";
            readout.text = "Play를 누르면 지금 눈높이가 보입니다";
            readout.fontSize = 24f;
            readout.textWrappingMode = TextWrappingModes.Normal;
            var readoutRect = (RectTransform)readout.transform;
            readoutRect.anchoredPosition = new Vector2(0f, -205f);
            readoutRect.sizeDelta = new Vector2(570f, 120f);

            var panel = canvas.gameObject.AddComponent<PlayerHeightPanel>();
            SceneBuildUtil.SetField(panel, "seatedButton", seated);
            SceneBuildUtil.SetField(panel, "standingButton", standing);
            SceneBuildUtil.SetField(panel, "lowerButton", lower);
            SceneBuildUtil.SetField(panel, "higherButton", higher);
            SceneBuildUtil.SetField(panel, "fitButton", fit);
            SceneBuildUtil.SetField(panel, "readout", readout);
        }

        private static TextMeshProUGUI CreatePageText(Transform canvas)
        {
            var go = new GameObject("Page Text", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = new Vector2(0f, -360f);
            rect.sizeDelta = new Vector2(400f, 60f);

            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = "Play를 누르면 씬 목록이 보입니다";
            text.fontSize = 24f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SceneBuildUtil.KoreanFontPath);
            if (font != null) text.font = font;
            return text;
        }

        private static EditorBuildSettingsScene SceneEntry(string path)
        {
            return new EditorBuildSettingsScene(new GUID(AssetDatabase.AssetPathToGUID(path)), true);
        }

        /// <summary>빌드 목록 = 런처(0번) + Assets/00. Study 아래 모든 씬(경로 순 → 모듈 번호 순, 문제 다음 Answers). 이름에 (Editor)가 든 씬은 뺍니다.</summary>
        [MenuItem("StudyXR/Setup/Device Build Scenes")]
        public static void SetDeviceBuildScenes()
        {
            if (!File.Exists(LauncherScenePath))
            {
                Debug.LogError($"[StudyXR] 런처 씬이 없습니다. 먼저 StudyXR/Build/Launcher 를 실행하세요: {LauncherScenePath}");
                return;
            }

            var paths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { StudyFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                // 이름에 (Editor)가 든 씬은 데스크톱 비교용이라 기기에 넣지 않습니다(v2 모듈 00)
                if (path != LauncherScenePath && !path.Contains("(Editor)")) paths.Add(path);
            }
            paths.Sort(System.StringComparer.Ordinal);

            // 씬을 다시 만드는 빌더(02 CopyScene 등)는 씬 GUID 가 바뀝니다. 경로만 주면 예전 GUID 가 남는 일이 있어(세션 YH),
            // 지금 GUID 로 항목을 만들고 목록을 한 번 비운 뒤 넣습니다
            var scenes = new List<EditorBuildSettingsScene> { SceneEntry(LauncherScenePath) };
            foreach (string path in paths)
            {
                scenes.Add(SceneEntry(path));
            }
            EditorBuildSettings.scenes = new EditorBuildSettingsScene[0];
            EditorBuildSettings.scenes = scenes.ToArray();

            // 설정은 메모리에만 바뀌고 프로젝트를 저장할 때 파일(ProjectSettings/EditorBuildSettings.asset)에 써집니다. 바로 저장합니다
            AssetDatabase.SaveAssets();
            Debug.Log($"[StudyXR] 빌드 목록: 런처 + 씬 {paths.Count}개\n{string.Join("\n", paths)}");
        }

        [MenuItem("StudyXR/Setup/Build Android APK")]
        public static void BuildApk()
        {
            BuildApkInternal();
        }

        /// <summary>배치 모드 진입점: -executeMethod StudyXR.EditorTools.DeviceBuildSetup.RunBuildBatch</summary>
        public static void RunBuildBatch()
        {
            EditorApplication.Exit(BuildApkInternal() ? 0 : 1);
        }

        private static bool BuildApkInternal()
        {
            string packageName = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            if (packageName == TemplatePackageName)
            {
                Debug.LogError($"[StudyXR] Android 패키지 이름이 템플릿 기본값입니다({packageName}). Project Settings > Player 에서 정한 뒤 다시 빌드하세요 (기기 테스트 목록 P4).");
                return false;
            }
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.LogError("[StudyXR] 빌드 대상이 Android 가 아닙니다. StudyXR/Setup/Apply Project Setup 을 먼저 실행하세요.");
                return false;
            }

            SetDeviceBuildScenes();
            var enabled = new List<string>();
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            {
                if (s.enabled) enabled.Add(s.path);
            }
            if (enabled.Count == 0 || enabled[0] != LauncherScenePath) return false;

            Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = enabled.ToArray(),
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            });

            bool ok = report.summary.result == BuildResult.Succeeded;
            Debug.Log($"[StudyXR] APK 빌드 {(ok ? "성공" : "실패")}: {ApkPath} · {report.summary.totalSize / (1024 * 1024)} MB · 씬 {enabled.Count}개 · 오류 {report.summary.totalErrors}");
            return ok;
        }
    }
}
