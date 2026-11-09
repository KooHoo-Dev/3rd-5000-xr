using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.XR.CoreUtils.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager.UI;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace StudyXR.EditorTools
{
    /// <summary>
    /// 기획서 "개발 환경" 표대로 Player, XR, OpenXR, URP 설정을 맞추고 XRI 샘플을 가져옵니다.
    /// 에디터 메뉴 StudyXR/Setup/Apply Project Setup 또는 배치 모드
    /// -executeMethod StudyXR.EditorTools.XRProjectSetup.RunBatch 로 실행합니다.
    /// 여러 번 실행해도 결과가 같습니다.
    /// </summary>
    public static class XRProjectSetup
    {
        private const string OpenXRLoaderType = "UnityEngine.XR.OpenXR.OpenXRLoader";
        private const string SimulationLoaderType = "UnityEngine.XR.Simulation.SimulationLoader";
        private const string MetaFeatureSetId = "com.unity.openxr.featureset.meta";
        private const string XRSettingsFolder = "Assets/XR";
        private const string QuestRPAssetPath = "Assets/Settings/Mobile_RPAsset.asset";
        private const string QuestRendererPath = "Assets/Settings/Mobile_Renderer.asset";
        private const string XRIPackage = "com.unity.xr.interaction.toolkit";

        // 기기 테스트 목록 P4. 회사·제품 이름은 앱 이름과 persistentDataPath 폴더가 됩니다
        private const string CompanyName = "KooHooEdu";
        private const string ProductName = "study-xr";
        private const string AndroidPackageName = "com.KooHooEdu.studyxr";
        private const string SimulatorPrefabPath = "Assets/Samples/XR Interaction Toolkit/3.4.1/XR Interaction Simulator/XR Interaction Simulator.prefab";

        /// <summary>Quest 3S 컨트롤러(Touch Plus)와 Link용 Touch 프로필.</summary>
        private static readonly string[] ControllerProfileIds =
        {
            "com.unity.openxr.feature.input.oculustouch",
            "com.unity.openxr.feature.input.metaquestplus",
        };

        /// <summary>Meta Quest Support와 MR 모듈(패스스루, 평면, 룸 메시, 앵커, 레이캐스트)에 필요한 Android 기능.</summary>
        private static readonly string[] AndroidFeatureIds =
        {
            "com.unity.openxr.feature.metaquest",
            "com.unity.openxr.feature.arfoundation-meta-session",
            "com.unity.openxr.feature.arfoundation-meta-camera",
            "com.unity.openxr.feature.arfoundation-meta-plane",
            "com.unity.openxr.feature.arfoundation-meta-mesh",
            "com.unity.openxr.feature.arfoundation-meta-anchor",
            "com.unity.openxr.feature.arfoundation-meta-raycast",
        };

        /// <summary>
        /// Meta Quest 피처 그룹이 기본으로 켜지만 교안에서 쓰지 않는 기능.
        /// Colocation Discovery는 USE_COLOCATION_DISCOVERY_API 권한을 manifest에 추가합니다(모듈 2는 직접 IP 접속).
        /// </summary>
        private static readonly string[] AndroidDisabledFeatureIds =
        {
            "com.unity.openxr.feature.meta-colocation-discovery",
        };

        private static readonly string[] XRISamples = { "Starter Assets", "XR Interaction Simulator" };

        /// <summary>XRI 샘플은 Interaction Layer 31을 텔레포트용으로 예약합니다.</summary>
        private const int TeleportLayerIndex = 31;
        private const string TeleportLayerName = "Teleport";

        // 08 XRI Components 구역 5(레이어와 필터)가 쓰는 Interaction Layer 이름 (세션 YI). 기존 마스크 값은 바꾸지 않고 이름만 붙입니다
        public const int KeyRedLayerIndex = 1;
        public const string KeyRedLayerName = "Key Red";
        public const int KeyBlueLayerIndex = 2;
        public const string KeyBlueLayerName = "Key Blue";

        private static readonly BuildTargetGroup[] XRTargets = { BuildTargetGroup.Android, BuildTargetGroup.Standalone };

        [MenuItem("StudyXR/Setup/Apply Project Setup")]
        public static void Apply()
        {
            ConfigurePlayer();
            ConfigureXRLoaders();
            ConfigureOpenXRFeatures();
            ConfigureURP();
            ImportXRISamples();
            AssetDatabase.SaveAssets();

            foreach (var group in XRTargets)
                FixValidationIssues(group);

            AssetDatabase.SaveAssets();
            Debug.Log("[XRProjectSetup] 완료");
        }

        /// <summary>배치 모드 진입점. 예외가 나면 종료 코드 1로 끝냅니다.</summary>
        public static void RunBatch()
        {
            try
            {
                Apply();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Android 빌드 설정: 앱 이름·패키지 이름, IL2CPP, ARM64, Vulkan, ASTC, Internet Access = Require.</summary>
        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AndroidPackageName);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.colorSpace = ColorSpace.Linear;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            Debug.Log($"[XRProjectSetup] Player: {AndroidPackageName}, IL2CPP, ARM64, Vulkan, ASTC, Internet Access = Require");
        }

        /// <summary>Android(기기)와 Standalone(Quest Link) 모두 OpenXR 로더를 씁니다.</summary>
        private static void ConfigureXRLoaders()
        {
            var perTarget = GetOrCreatePerBuildTargetSettings();
            foreach (var group in XRTargets)
            {
                if (!perTarget.HasManagerSettingsForBuildTarget(group))
                    perTarget.CreateDefaultManagerSettingsForBuildTarget(group);

                var general = perTarget.SettingsForBuildTarget(group);
                general.InitManagerOnStart = true;
                XRPackageMetadataStore.AssignLoader(general.Manager, OpenXRLoaderType, group);
                EditorUtility.SetDirty(general);
                EditorUtility.SetDirty(general.Manager);
            }
            EditorUtility.SetDirty(perTarget);

            // 기본은 VR 모듈용 로더. MR 모듈에서 XR Simulation 으로 바꿔 두었더라도 되돌린다.
            UseStandaloneLoader(OpenXRLoaderType, SimulationLoaderType);
            Debug.Log("[XRProjectSetup] XR Plug-in Management: OpenXR (Android, Standalone)");
        }

        /// <summary>
        /// 에디터 Play(Standalone)용 로더를 VR 모듈용 OpenXR 로 둡니다.
        /// 헤드셋이 없으면 OpenXR 초기화가 조용히 실패하고 XR Interaction Simulator 가 동작합니다(로드맵 6장, 2026-10-29 확인).
        /// </summary>
        [MenuItem("StudyXR/Setup/Simulator Loader - VR (OpenXR)")]
        public static void UseVRLoader()
        {
            UseStandaloneLoader(OpenXRLoaderType, SimulationLoaderType);
            AssetDatabase.SaveAssets();
            Debug.Log("[XRProjectSetup] Standalone 로더: OpenXR (VR 모듈 00~08)");
        }

        /// <summary>
        /// 에디터 Play(Standalone)용 로더를 AR Foundation 의 XR Simulation 으로 둡니다. 평면·메시·레이캐스트를 에디터에서 시험할 수 있습니다.
        /// XR Simulation 은 카메라를 시뮬레이션 환경의 시작 위치로 옮기므로 VR 모듈에서는 쓰지 않습니다.
        /// </summary>
        [MenuItem("StudyXR/Setup/Simulator Loader - MR (XR Simulation)")]
        public static void UseMRLoader()
        {
            UseStandaloneLoader(SimulationLoaderType, OpenXRLoaderType);
            AssetDatabase.SaveAssets();
            Debug.Log("[XRProjectSetup] Standalone 로더: XR Simulation (MR 모듈 09~12)");
        }

        private static void UseStandaloneLoader(string loaderType, string otherLoaderType)
        {
            var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            if (settings == null || settings.Manager == null)
            {
                Debug.LogWarning("[XRProjectSetup] Standalone XR 설정이 없습니다. Apply Project Setup 을 먼저 실행하세요.");
                return;
            }

            XRPackageMetadataStore.RemoveLoader(settings.Manager, otherLoaderType, BuildTargetGroup.Standalone);
            XRPackageMetadataStore.AssignLoader(settings.Manager, loaderType, BuildTargetGroup.Standalone);
            EditorUtility.SetDirty(settings.Manager);
        }

        private static XRGeneralSettingsPerBuildTarget GetOrCreatePerBuildTargetSettings()
        {
            if (EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget existing) && existing != null)
                return existing;

            var guids = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget");
            if (guids.Length > 0)
            {
                existing = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(AssetDatabase.GUIDToAssetPath(guids[0]));
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, existing, true);
                return existing;
            }

            if (!AssetDatabase.IsValidFolder(XRSettingsFolder))
                AssetDatabase.CreateFolder("Assets", "XR");

            var created = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(created, $"{XRSettingsFolder}/XRGeneralSettingsPerBuildTarget.asset");
            AssetDatabase.SaveAssets();
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, created, true);
            return created;
        }

        /// <summary>Android에는 Meta Quest 피처 그룹과 MR 기능, 두 대상 모두에 컨트롤러 프로필을 켭니다.</summary>
        private static void ConfigureOpenXRFeatures()
        {
            OpenXRFeatureSetManager.InitializeFeatureSets();
            var metaSet = OpenXRFeatureSetManager.GetFeatureSetWithId(BuildTargetGroup.Android, MetaFeatureSetId);
            if (metaSet == null)
                throw new System.InvalidOperationException("Meta Quest 피처 그룹을 찾지 못했습니다. com.unity.xr.meta-openxr 설치를 확인하세요.");
            metaSet.isEnabled = true;
            OpenXRFeatureSetManager.SetFeaturesFromEnabledFeatureSets(BuildTargetGroup.Android);

            EnableFeatures(BuildTargetGroup.Android, AndroidFeatureIds.Concat(ControllerProfileIds).ToArray());
            EnableFeatures(BuildTargetGroup.Standalone, ControllerProfileIds);

            foreach (var feature in FeatureHelpers.GetFeaturesWithIdsForBuildTarget(BuildTargetGroup.Android, AndroidDisabledFeatureIds))
            {
                feature.enabled = false;
                EditorUtility.SetDirty(feature);
                Debug.Log($"[XRProjectSetup] OpenXR Android 끔: {feature.name}");
            }
        }

        private static void EnableFeatures(BuildTargetGroup group, string[] featureIds)
        {
            var features = FeatureHelpers.GetFeaturesWithIdsForBuildTarget(group, featureIds);
            foreach (var feature in features)
            {
                feature.enabled = true;
                EditorUtility.SetDirty(feature);
            }

            if (features.Length != featureIds.Length)
                Debug.LogWarning($"[XRProjectSetup] {group}: OpenXR 기능 {featureIds.Length}개 중 {features.Length}개만 찾았습니다");

            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
            if (settings != null)
                EditorUtility.SetDirty(settings);
            Debug.Log($"[XRProjectSetup] OpenXR {group}: {string.Join(", ", features.Select(f => f.name))}");
        }

        /// <summary>
        /// Mobile 품질 단계를 Quest용으로 씁니다: Forward, MSAA 4x, HDR 끔, Depth/Opaque Texture 끔, 후처리 끔.
        /// Quest Link 테스트가 기기와 같은 조건이 되도록 Standalone 기본 품질도 Mobile로 둡니다.
        /// </summary>
        private static void ConfigureURP()
        {
            var rpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(QuestRPAssetPath);
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(QuestRendererPath);
            if (rpAsset == null || renderer == null)
                throw new System.InvalidOperationException($"{QuestRPAssetPath} 또는 {QuestRendererPath}를 찾지 못했습니다.");

            rpAsset.msaaSampleCount = 4;
            rpAsset.supportsHDR = false;
            rpAsset.supportsCameraDepthTexture = false;
            rpAsset.supportsCameraOpaqueTexture = false;
            rpAsset.renderScale = 1f;
            EditorUtility.SetDirty(rpAsset);

            renderer.renderingMode = RenderingMode.Forward;
            renderer.postProcessData = null;
            EditorUtility.SetDirty(renderer);

            GraphicsSettings.defaultRenderPipeline = rpAsset;
            SetDefaultQualityForPlatforms(rpAsset, "Android", "Standalone");
            Debug.Log("[XRProjectSetup] URP: Mobile_RPAsset = Forward, MSAA 4x, HDR/Depth/Opaque/후처리 끔 (Android, Standalone 기본)");
        }

        private static void SetDefaultQualityForPlatforms(RenderPipelineAsset rpAsset, params string[] platforms)
        {
            var levelIndex = -1;
            for (var i = 0; i < QualitySettings.count; i++)
            {
                if (QualitySettings.GetRenderPipelineAssetAt(i) == rpAsset)
                {
                    levelIndex = i;
                    break;
                }
            }
            if (levelIndex < 0)
                throw new System.InvalidOperationException("Mobile_RPAsset을 쓰는 품질 단계가 없습니다.");

            var qualityAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset").FirstOrDefault();
            var so = new SerializedObject(qualityAsset);

            // 템플릿의 Mobile 단계는 Standalone을 제외하고 있으므로 제외 목록에서 뺍니다.
            var excluded = so.FindProperty("m_QualitySettings").GetArrayElementAtIndex(levelIndex).FindPropertyRelative("excludedTargetPlatforms");
            for (var i = excluded.arraySize - 1; i >= 0; i--)
            {
                if (platforms.Contains(excluded.GetArrayElementAtIndex(i).stringValue))
                    excluded.DeleteArrayElementAtIndex(i);
            }

            var map = so.FindProperty("m_PerPlatformDefaultQuality");
            for (var i = 0; i < map.arraySize; i++)
            {
                var entry = map.GetArrayElementAtIndex(i);
                if (platforms.Contains(entry.FindPropertyRelative("first").stringValue))
                    entry.FindPropertyRelative("second").intValue = levelIndex;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            QualitySettings.SetQualityLevel(levelIndex, false);
        }

        /// <summary>XRI Starter Assets(XR Origin, 입력 액션)와 기기 없이 테스트할 XR Interaction Simulator.</summary>
        private static void ImportXRISamples()
        {
            var samples = Sample.FindByPackage(XRIPackage, null).ToList();
            foreach (var name in XRISamples)
            {
                var sample = samples.FirstOrDefault(s => s.displayName == name);
                if (string.IsNullOrEmpty(sample.displayName))
                {
                    Debug.LogWarning($"[XRProjectSetup] XRI 샘플 '{name}'을(를) 찾지 못했습니다. 있는 샘플: {string.Join(", ", samples.Select(s => s.displayName))}");
                    continue;
                }
                if (!sample.isImported)
                    sample.Import(Sample.ImportOptions.OverridePreviousImports | Sample.ImportOptions.HideImportWindow);
                Debug.Log($"[XRProjectSetup] XRI 샘플: {name}");
            }
            AssetDatabase.Refresh();
            SetTeleportInteractionLayer();
            SetModuleInteractionLayers();
            EnableSimulatorInEditor();
        }

        /// <summary>
        /// 에디터 Play 때만 XR Interaction Simulator를 자동으로 만듭니다(XRI 설정의 "Use XR Interaction Simulator in scenes").
        /// 씬마다 시뮬레이터를 두지 않아도 되고, 기기 빌드에는 들어가지 않습니다.
        /// </summary>
        private static void EnableSimulatorInEditor()
        {
            var settingsGuids = AssetDatabase.FindAssets("t:XRDeviceSimulatorSettings");
            var simulator = AssetDatabase.LoadAssetAtPath<GameObject>(SimulatorPrefabPath);
            if (settingsGuids.Length == 0 || simulator == null)
            {
                Debug.LogWarning($"[XRProjectSetup] 시뮬레이터 설정 에셋 또는 프리팹이 없어 자동 생성을 켜지 못했습니다: {SimulatorPrefabPath}");
                return;
            }

            var settings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(settingsGuids[0]));
            var so = new SerializedObject(settings);
            so.FindProperty("m_AutomaticallyInstantiateSimulatorPrefab").boolValue = true;
            so.FindProperty("m_AutomaticallyInstantiateInEditorOnly").boolValue = true;
            so.FindProperty("m_UseClassic").boolValue = false;
            so.FindProperty("m_SimulatorPrefab").objectReferenceValue = simulator;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            Debug.Log("[XRProjectSetup] XR Interaction Simulator: 에디터 Play 때 자동 생성");
        }

        /// <summary>
        /// 모듈이 쓰는 Interaction Layer 이름을 붙입니다(08 XRI Components — Key Red 1 · Key Blue 2). 비어 있거나 같은 이름일 때만 씁니다.
        /// 이름만 붙이므로 다른 씬의 Interaction Layer Mask 값(비트)은 그대로입니다.
        /// </summary>
        [MenuItem("StudyXR/Setup/Interaction Layer Names")]
        public static void SetModuleInteractionLayers()
        {
            SetInteractionLayerName(KeyRedLayerIndex, KeyRedLayerName);
            SetInteractionLayerName(KeyBlueLayerIndex, KeyBlueLayerName);
        }

        private static void SetInteractionLayerName(int index, string layerName)
        {
            var guids = AssetDatabase.FindAssets("t:InteractionLayerSettings");
            if (guids.Length == 0)
            {
                Debug.LogWarning($"[XRProjectSetup] InteractionLayerSettings 에셋이 없어 {layerName} 레이어를 설정하지 못했습니다");
                return;
            }

            var layerSettings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
            var so = new SerializedObject(layerSettings);
            var names = so.FindProperty("m_LayerNames");
            if (names == null || names.arraySize <= index) return;

            var layer = names.GetArrayElementAtIndex(index);
            if (!string.IsNullOrEmpty(layer.stringValue) && layer.stringValue != layerName)
            {
                Debug.LogWarning($"[XRProjectSetup] Interaction Layer {index}이 '{layer.stringValue}'(으)로 쓰이고 있어 {layerName}(으)로 바꾸지 않았습니다");
                return;
            }

            layer.stringValue = layerName;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(layerSettings);
            AssetDatabase.SaveAssets();
            Debug.Log($"[XRProjectSetup] Interaction Layer {index} = {layerName}");
        }

        /// <summary>
        /// Starter Assets의 검증 규칙은 delayCall로 등록되어 배치 모드 -executeMethod에서는 잡히지 않으므로 직접 설정합니다.
        /// 31번이 다른 이름으로 쓰이고 있으면 건드리지 않습니다.
        /// </summary>
        private static void SetTeleportInteractionLayer()
        {
            var guids = AssetDatabase.FindAssets("t:InteractionLayerSettings");
            if (guids.Length == 0)
            {
                Debug.LogWarning("[XRProjectSetup] InteractionLayerSettings 에셋이 없어 Teleport 레이어를 설정하지 못했습니다");
                return;
            }

            var layerSettings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
            var so = new SerializedObject(layerSettings);
            var names = so.FindProperty("m_LayerNames");
            if (names == null || names.arraySize <= TeleportLayerIndex)
            {
                Debug.LogWarning("[XRProjectSetup] InteractionLayerSettings의 레이어 배열을 읽지 못했습니다");
                return;
            }

            var layer = names.GetArrayElementAtIndex(TeleportLayerIndex);
            if (!string.IsNullOrEmpty(layer.stringValue) && layer.stringValue != TeleportLayerName)
            {
                Debug.LogWarning($"[XRProjectSetup] Interaction Layer {TeleportLayerIndex}이 '{layer.stringValue}'(으)로 쓰이고 있어 Teleport로 바꾸지 않았습니다");
                return;
            }

            layer.stringValue = TeleportLayerName;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[XRProjectSetup] Interaction Layer {TeleportLayerIndex} = {TeleportLayerName}");
        }

        /// <summary>
        /// XR Plug-in Management의 Project Validation(OpenXR, AR Foundation, XRI 규칙)에서 자동 수정이 가능한 항목을 고칩니다.
        /// 남은 항목은 로그로 남깁니다.
        /// </summary>
        private static void FixValidationIssues(BuildTargetGroup group)
        {
            for (var pass = 0; pass < 3; pass++)
            {
                var openXRIssues = new List<OpenXRFeature.ValidationRule>();
                OpenXRProjectValidation.GetCurrentValidationIssues(openXRIssues, group);
                var coreIssues = GetCoreUtilsIssues(group);

                var fixable = openXRIssues.Count(r => r.fixIt != null && r.fixItAutomatic)
                              + coreIssues.Count(r => r.FixIt != null && r.FixItAutomatic);
                if (fixable == 0)
                    break;

                foreach (var rule in openXRIssues.Where(r => r.fixIt != null && r.fixItAutomatic))
                {
                    Debug.Log($"[XRProjectSetup] 수정 ({group}): {rule.message}");
                    rule.fixIt();
                }
                foreach (var rule in coreIssues.Where(r => r.FixIt != null && r.FixItAutomatic))
                {
                    Debug.Log($"[XRProjectSetup] 수정 ({group}): {rule.Message}");
                    rule.FixIt();
                }

                // OpenXR 규칙의 fixIt 중 일부(Latency Optimization 등)는 설정 에셋을 dirty로 표시하지 않아 저장되지 않습니다.
                var openXRSettings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
                if (openXRSettings != null)
                    EditorUtility.SetDirty(openXRSettings);
                AssetDatabase.SaveAssets();
            }

            var remainingOpenXR = new List<OpenXRFeature.ValidationRule>();
            OpenXRProjectValidation.GetCurrentValidationIssues(remainingOpenXR, group);
            foreach (var rule in remainingOpenXR)
                Debug.LogWarning($"[XRProjectSetup] 남은 항목 ({group}){(rule.error ? " [오류]" : "")}: {rule.message}");
            foreach (var rule in GetCoreUtilsIssues(group))
                Debug.LogWarning($"[XRProjectSetup] 남은 항목 ({group}){(rule.Error ? " [오류]" : "")}: {rule.Message}");
        }

        /// <summary>XR Core Utils BuildValidator의 현재 실패 규칙. 조회 메서드가 internal이라 리플렉션으로 부릅니다.</summary>
        private static List<BuildValidationRule> GetCoreUtilsIssues(BuildTargetGroup group)
        {
            var method = typeof(BuildValidator).GetMethod("GetCurrentValidationIssues", BindingFlags.Static | BindingFlags.NonPublic);
            var failures = new HashSet<BuildValidationRule>();
            if (method == null)
            {
                Debug.LogWarning("[XRProjectSetup] BuildValidator.GetCurrentValidationIssues를 찾지 못해 Core Utils 검증을 건너뜁니다");
                return failures.ToList();
            }
            method.Invoke(null, new object[] { failures, group });
            return failures.Where(r => !r.SceneOnlyValidation).ToList();
        }
    }
}
