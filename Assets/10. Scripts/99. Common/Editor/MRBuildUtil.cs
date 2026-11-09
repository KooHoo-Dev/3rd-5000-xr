using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;

namespace StudyXR.EditorTools
{
    /// <summary>
    /// MR 씬의 XR 리그(AR 세션 · 카메라 배경 · 평면 · 레이캐스트)와 AR 평면 프리팹, MR 렌더러 두 개를 만듭니다. 모듈 09~12 가 같이 씁니다(세션 XH).
    /// 확인 : 2026-10-31 XR Simulation 로더 Play — 평면 8개(바닥 5 · 벽 3), AR 레이캐스트, 중력 끈 리그 높이 1.36 그대로.
    /// </summary>
    public static class MRBuildUtil
    {
        public const string UnderwaterLayerName = "Aquarium";       // 포털(수족관 창) 안에서만 그릴 물체의 레이어 (모듈 05)
        public const int PortalStencilRef = 1;                      // 구멍 면이 스텐실에 쓰는 값

        private const string BaseRendererPath = "Assets/Settings/Mobile_Renderer.asset";
        private const string MRRendererPath = "Assets/Settings/MR_Renderer.asset";
        private const string PortalRendererPath = "Assets/Settings/Portal_Renderer.asset";

        // 리그에서 다른 코드가 연결에 쓰는 것들
        public sealed class MRRigParts
        {
            public Camera Camera { get; }
            public Transform Origin { get; }
            public ARSession Session { get; }
            public ARPlaneManager PlaneManager { get; }
            public ARRaycastManager RaycastManager { get; }
            public Transform LeftHand { get; }
            public Transform RightHand { get; }

            public MRRigParts(Camera camera, Transform origin, ARSession session, ARPlaneManager planeManager, ARRaycastManager raycastManager,
                Transform leftHand, Transform rightHand)
            {
                Camera = camera; Origin = origin; Session = session; PlaneManager = planeManager; RaycastManager = raycastManager;
                LeftHand = leftHand; RightHand = rightHand;
            }
        }

        // AR 평면 하나 : ARPlane + 메시(ARPlaneMeshVisualizer 가 채움) + 그리기 + 두께 0 MeshCollider
        public static GameObject PlanePrefab(string path, Material material)
        {
            return SceneBuildUtil.GetOrCreatePrefab(path, () =>
            {
                var go = new GameObject("AR Plane");
                go.AddComponent<ARPlane>();
                go.AddComponent<MeshFilter>();
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;   // 반투명 표시 면이 VR 대체 바닥에 그림자를 드리우지 않게
                renderer.receiveShadows = false;
                go.AddComponent<MeshCollider>();
                go.AddComponent<ARPlaneMeshVisualizer>();
                return go;
            });
        }

        // XRRig 위에 AR 을 더합니다. 평면 매니저는 꺼 둡니다(권한이 허용된 뒤 SpatialPermission 이 켬).
        // 리그의 중력을 끕니다 — MR 은 처음에 바닥 콜라이더가 없어(평면은 나중에 생김) 리그가 끝없이 떨어집니다(기기도 같음).
        // 카메라 배경은 부르는 쪽이 ApplyEnvironment(EnvironmentPreset.Passthrough, camera) 로 정합니다.
        // 카메라는 MR 렌더러(AR 배경 피처)를 씁니다 — 없으면 에디터 XR Simulation 의 방이 검게 보입니다(기기는 상관없음).
        public static MRRigParts BuildMRRig(Transform xrParent, GameObject planePrefab)
        {
            Camera camera = SceneBuildUtil.XRRig(xrParent, Vector3.zero, 0f);
            if (camera == null) return null;

            Transform origin = camera.transform;
            while (origin.parent != null && !origin.name.StartsWith("XR Origin")) origin = origin.parent;

            foreach (GravityProvider gravity in origin.GetComponentsInChildren<GravityProvider>(true)) gravity.enabled = false;

            var sessionGo = new GameObject("AR Session");
            sessionGo.transform.SetParent(xrParent, false);
            ARSession session = sessionGo.AddComponent<ARSession>();

            camera.gameObject.AddComponent<ARCameraManager>();
            camera.gameObject.AddComponent<ARCameraBackground>();
            UseRenderer(camera, MRRenderer());

            ARPlaneManager planes = origin.gameObject.AddComponent<ARPlaneManager>();
            planes.planePrefab = planePrefab;
            planes.enabled = false;
            ARRaycastManager raycast = origin.gameObject.AddComponent<ARRaycastManager>();

            EditorUtility.SetDirty(origin.gameObject);
            return new MRRigParts(camera, origin, session, planes, raycast, Find(origin, "Left Controller"), Find(origin, "Right Controller"));
        }

        // ───────────────────────────── 룸 메시 (모듈 11) ─────────────────────────────

        // 룸 메시 하나 : 메시(매니저가 채움) + 그리기(재질은 RoomMeshLoader 가 바꿈) + MeshCollider(매니저가 물리 메시를 채움)
        public static GameObject MeshPrefab(string path, Material material)
        {
            return SceneBuildUtil.GetOrCreatePrefab(path, () =>
            {
                var go = new GameObject("Room Mesh Chunk");
                go.AddComponent<MeshFilter>();
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                go.AddComponent<MeshCollider>();
                return go;
            });
        }

        // ARMeshManager 는 XR Origin 의 자식이어야 합니다(아니면 예외). 꺼 둔 채 — 권한 뒤 SpatialPermission 이 켬
        public static ARMeshManager AddMeshManager(MRRigParts parts, GameObject meshPrefab)
        {
            var go = new GameObject("Room Mesh");
            go.transform.SetParent(parts.Origin, false);
            ARMeshManager meshes = go.AddComponent<ARMeshManager>();
            meshes.meshPrefab = meshPrefab.GetComponent<MeshFilter>();
            meshes.normals = true;
            meshes.enabled = false;
            EditorUtility.SetDirty(go);
            return meshes;
        }

        // ───────────────────────────── 렌더러 ─────────────────────────────

        // 카메라가 파이프라인 에셋의 렌더러 목록 중 index 번째를 쓰게 합니다
        public static void UseRenderer(Camera camera, int index)
        {
            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.SetRenderer(index);
            EditorUtility.SetDirty(data);
        }

        // MR 렌더러 : 기본 렌더러 + AR 배경 피처. 렌더러 목록의 번호를 돌려줍니다
        public static int MRRenderer()
        {
            return EnsureRenderer(MRRendererPath, BaseRendererPath, data => AddFeature<ARBackgroundRendererFeature>(data, "AR Background"));
        }

        // 포털 렌더러 : MR 렌더러 + Underwater 레이어는 평소에 그리지 않고, 스텐실 값이 PortalStencilRef 인 곳에만 그림(모듈 10)
        public static int PortalRenderer()
        {
            int layer = EnsureLayer(UnderwaterLayerName);
            MRRenderer();
            return EnsureRenderer(PortalRendererPath, MRRendererPath, data =>
            {
                int everythingElse = ~(1 << layer);
                data.opaqueLayerMask = everythingElse;
                data.transparentLayerMask = everythingElse;
                AddStencilPass(data, "Underwater Opaque", RenderQueueType.Opaque, RenderPassEvent.AfterRenderingOpaques, layer);
                AddStencilPass(data, "Underwater Transparent", RenderQueueType.Transparent, RenderPassEvent.AfterRenderingTransparents, layer);
            });
        }

        // 렌더러 에셋이 없으면 source 를 복사해 configure 로 고치고, 모든 파이프라인 에셋(품질 단계마다)의 목록 끝에 넣습니다.
        // 목록 번호는 에셋마다 같아야 카메라의 렌더러 번호가 어느 품질에서나 맞습니다
        private static int EnsureRenderer(string path, string sourcePath, System.Action<UniversalRendererData> configure)
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (data == null)
            {
                AssetDatabase.CopyAsset(sourcePath, path);
                data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                // 복사본은 원본의 피처를 이미 가지고 있음 — configure 는 새로 더할 것만
                configure(data);
                data.SetDirty();
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
            }

            int index = -1;
            foreach (UniversalRenderPipelineAsset rp in PipelineAssets())
            {
                var so = new SerializedObject(rp);
                SerializedProperty list = so.FindProperty("m_RendererDataList");
                int found = -1;
                for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == data) found = i;
                if (found < 0)
                {
                    found = list.arraySize;
                    list.arraySize++;
                    list.GetArrayElementAtIndex(found).objectReferenceValue = data;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(rp);
                }
                if (index >= 0 && index != found)
                    Debug.LogError("[StudyXR] 파이프라인 에셋마다 " + data.name + " 번호가 다릅니다: " + index + " / " + found + " (" + rp.name + ")");
                index = found;
            }
            AssetDatabase.SaveAssets();
            return index;
        }

        private static IEnumerable<UniversalRenderPipelineAsset> PipelineAssets()
        {
            var seen = new HashSet<UniversalRenderPipelineAsset>();
            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset main && seen.Add(main)) yield return main;
            for (int i = 0; i < QualitySettings.count; i++)
                if (QualitySettings.GetRenderPipelineAssetAt(i) is UniversalRenderPipelineAsset rp && seen.Add(rp)) yield return rp;
        }

        // 렌더러 에디터의 'Add Renderer Feature' 와 같은 일 : 하위 에셋으로 넣고, 목록과 번호 지도(m_RendererFeatureMap)를 함께 늘립니다
        private static T AddFeature<T>(UniversalRendererData data, string name) where T : ScriptableRendererFeature
        {
            var feature = ScriptableObject.CreateInstance<T>();
            feature.name = name;
            AssetDatabase.AddObjectToAsset(feature, data);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);

            var so = new SerializedObject(data);
            SerializedProperty features = so.FindProperty("m_RendererFeatures");
            SerializedProperty map = so.FindProperty("m_RendererFeatureMap");
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();
            return feature;
        }

        // 레이어 하나만 스텐실 == PortalStencilRef 인 곳에 그리는 Render Objects
        private static void AddStencilPass(UniversalRendererData data, string name, RenderQueueType queue, RenderPassEvent passEvent, int layer)
        {
            RenderObjects feature = AddFeature<RenderObjects>(data, name);
            feature.settings.passTag = name;
            feature.settings.Event = passEvent;
            feature.settings.filterSettings.RenderQueueType = queue;
            feature.settings.filterSettings.LayerMask = 1 << layer;
            feature.settings.stencilSettings.overrideStencilState = true;
            feature.settings.stencilSettings.stencilReference = PortalStencilRef;
            feature.settings.stencilSettings.stencilCompareFunction = CompareFunction.Equal;
            feature.settings.stencilSettings.passOperation = StencilOp.Keep;
            feature.settings.stencilSettings.failOperation = StencilOp.Keep;
            feature.settings.stencilSettings.zFailOperation = StencilOp.Keep;
            EditorUtility.SetDirty(feature);
        }

        // 이름의 레이어가 없으면 비어 있는 첫 사용자 레이어(6번부터)에 만듭니다. 레이어 번호를 돌려줍니다
        public static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0) return existing;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            for (int i = 6; i < layers.arraySize; i++)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue)) continue;
                slot.stringValue = name;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                return i;
            }
            Debug.LogError("[StudyXR] 빈 레이어가 없어 " + name + " 을 만들지 못했습니다");
            return 0;
        }

        // 물체와 그 아래 모두를 한 레이어로
        public static void SetLayerRecursive(GameObject root, int layer)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }
    }
}
