using System;
using System.IO;
using StudyXR.Common;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.UI;
using LitGUI = UnityEditor.Rendering.Universal.ShaderGUI.LitGUI;
using ParticleGUI = UnityEditor.Rendering.Universal.ShaderGUI.ParticleGUI;

namespace StudyXR.EditorTools
{
    /// <summary>
    /// 씬 빌더가 공통으로 쓰는 유틸리티. 씬 생성, 환경 설정, 에셋 get-or-create,
    /// 오브젝트 배치, SerializedObject 연결을 모아 둔다.
    /// </summary>
    public static class SceneBuildUtil
    {
        public const string KoreanFontPath = "Assets/99. External Assets/Font/Noto_Sans_KR/NotoSansKR-Regular SDF.asset";

        // XRI Starter Assets의 XR Origin. 샘플 폴더는 읽기 전용으로 참조만 합니다(규칙 1장).
        public const string XROriginPrefabPath = "Assets/Samples/XR Interaction Toolkit/3.4.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";

        // 밝은 바닥·하늘 위에서 읽히는 3D 라벨 기본 글자색.
        public static readonly Color LabelDark = new Color(0.10f, 0.10f, 0.12f);

        private const string LitShaderName = "Universal Render Pipeline/Lit";

        private static int createdAssets;
        private static int reusedAssets;

        // ───────────────────────────── 씬 ─────────────────────────────

        public static bool ConfirmOverwrite(string scenePath)
        {
            if (!File.Exists(scenePath)) return true;

            return EditorUtility.DisplayDialog(
                "씬 덮어쓰기",
                $"{scenePath}\n이미 있습니다. 덮어쓰면 씬에서 다듬은 내용이 사라집니다.",
                "덮어쓰기", "취소");
        }

        public static Scene CreateScene(string scenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return default;
            }

            string folder = Path.GetDirectoryName(scenePath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder))
            {
                EnsureFolder(folder);
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, scenePath);

            createdAssets = 0;
            reusedAssets = 0;
            PanelBackdrop.ResetForNewScene();

            return scene;
        }

        public static void SaveScene(Scene scene)
        {
            // 패널 배경판의 기둥은 땅(다른 오브젝트)이 다 놓인 뒤에 세웁니다
            PanelBackdrop.BuildPendingPosts();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"[StudyXR] 씬 생성: {scene.path} (새 에셋 {createdAssets}개, 재사용 {reusedAssets}개)");
        }

        // ───────────────────────────── 환경 ─────────────────────────────

        public static void ApplyEnvironment(EnvironmentPreset preset, Camera camera = null)
        {
            RenderSettings.fog = false;

            switch (preset)
            {
                case EnvironmentPreset.Default:
                    RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
                    RenderSettings.ambientMode = AmbientMode.Skybox;
                    RenderSettings.ambientIntensity = 1f;
                    RenderSettings.reflectionIntensity = 1f;
                    if (camera != null)
                    {
                        camera.clearFlags = CameraClearFlags.Skybox;
                    }
                    break;

                case EnvironmentPreset.DarkStage:
                    RenderSettings.skybox = null;
                    RenderSettings.ambientMode = AmbientMode.Flat;
                    RenderSettings.ambientLight = new Color(0.06f, 0.06f, 0.08f);
                    RenderSettings.reflectionIntensity = 0.2f;
                    if (camera != null)
                    {
                        camera.clearFlags = CameraClearFlags.SolidColor;
                        camera.backgroundColor = new Color(0.02f, 0.02f, 0.03f);
                    }
                    break;

                case EnvironmentPreset.Passthrough:
                    // MR: 카메라 배경을 알파 0인 검정으로 두어야 패스스루가 비칩니다(기획서 모듈 3).
                    // 실제 방이 배경이므로 스카이박스 없이 평평한 환경광만 둡니다.
                    RenderSettings.skybox = null;
                    RenderSettings.ambientMode = AmbientMode.Flat;
                    RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.48f);
                    RenderSettings.reflectionIntensity = 0.3f;
                    if (camera != null)
                    {
                        camera.clearFlags = CameraClearFlags.SolidColor;
                        camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                    }
                    break;
            }
        }

        // ───────────────────────────── 폴더와 에셋 ─────────────────────────────

        public static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0]; // "Assets"

            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        public static Material LitMaterial(string path, Color color, float metallic = 0f, float smoothness = 0.5f)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                reusedAssets++;
                return existing;
            }

            Shader shader = Shader.Find(LitShaderName);
            if (shader == null)
            {
                Debug.LogError($"[StudyXR] 셰이더를 찾을 수 없습니다: {LitShaderName}");
                return null;
            }

            var material = new Material(shader);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            ApplyUrpKeywords(material);

            return CreateAsset(material, path);
        }

        public static Material TexturedMaterial(string path, Texture2D baseMap, Vector2 tiling, Color tint, Texture2D normalMap = null, float smoothness = 0.3f)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                reusedAssets++;
                return existing;
            }

            Shader shader = Shader.Find(LitShaderName);
            if (shader == null)
            {
                Debug.LogError($"[StudyXR] 셰이더를 찾을 수 없습니다: {LitShaderName}");
                return null;
            }

            var material = new Material(shader);
            if (baseMap != null)
            {
                material.SetTexture("_BaseMap", baseMap);
            }
            material.SetTextureScale("_BaseMap", tiling);
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_Smoothness", smoothness);

            if (normalMap != null)
            {
                material.SetTexture("_BumpMap", normalMap);
                material.SetFloat("_BumpScale", 1f);
            }

            ApplyUrpKeywords(material);

            return CreateAsset(material, path);
        }

        public static Material EmissiveMaterial(string path, Color baseColor, Color emissionColor, float intensity = 3f)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                reusedAssets++;
                return existing;
            }

            Shader shader = Shader.Find(LitShaderName);
            if (shader == null)
            {
                Debug.LogError($"[StudyXR] 셰이더를 찾을 수 없습니다: {LitShaderName}");
                return null;
            }

            var material = new Material(shader);
            material.SetColor("_BaseColor", baseColor);
            material.SetColor("_EmissionColor", emissionColor * intensity);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            ApplyUrpKeywords(material);

            return CreateAsset(material, path);
        }

        public static Material TransparentMaterial(string path, Color colorWithAlpha, float smoothness = 0.9f)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                reusedAssets++;
                return existing;
            }

            Shader shader = Shader.Find(LitShaderName);
            if (shader == null)
            {
                Debug.LogError($"[StudyXR] 셰이더를 찾을 수 없습니다: {LitShaderName}");
                return null;
            }

            var material = new Material(shader);
            material.SetFloat("_Surface", 1f); // Transparent
            material.SetFloat("_Blend", 0f);   // Alpha
            material.SetColor("_BaseColor", colorWithAlpha);
            material.SetFloat("_Smoothness", smoothness);
            ApplyUrpKeywords(material);

            return CreateAsset(material, path);
        }

        // 교안이 직접 쓴 셰이더(예: StudyXR/Lake Shader/…)로 머티리얼을 만듭니다.
        // init은 **새로 만들 때만** 불립니다. 이미 있으면 재사용하고 속성을 다시 설정하지 않습니다(사용자가 바꾼 값 보호).
        // 셰이더의 [Toggle(키워드)] 속성은 SetFloat만으로는 키워드가 켜지지 않으므로 init에서 EnableKeyword도 함께 부릅니다.
        public static Material ShaderMaterial(string path, string shaderName, Action<Material> init = null)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                reusedAssets++;
                return existing;
            }

            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[StudyXR] 셰이더를 찾을 수 없습니다: {shaderName} (셰이더 파일에 컴파일 오류가 있으면 이름으로 찾을 수 없습니다 — verify_shader.py)");
                return null;
            }

            var material = new Material(shader);
            init?.Invoke(material);
            return CreateAsset(material, path);
        }

        public static Texture2D TempTexture(string path, TempPattern pattern, Color a, Color b, int size = 256, int cells = 8, FilterMode filter = FilterMode.Bilinear)
        {
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
            {
                reusedAssets++;
                return existing;
            }

            string folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder))
            {
                EnsureFolder(folder);
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            int cellPx = Mathf.Max(1, size / cells);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color pixel = PatternPixel(pattern, x, y, size, cellPx, a, b);
                    texture.SetPixel(x, y, pixel);
                }
            }

            texture.Apply();

            byte[] png = texture.EncodeToPNG();
            string fullPath = Path.GetFullPath(path);
            File.WriteAllBytes(fullPath, png);
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path);
            ApplyTempTextureImportSettings(path, pattern, filter);

            createdAssets++;
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Color PatternPixel(TempPattern pattern, int x, int y, int size, int cellPx, Color a, Color b)
        {
            switch (pattern)
            {
                case TempPattern.Checker:
                    return ((x / cellPx + y / cellPx) % 2 == 0) ? a : b;

                case TempPattern.Stripe:
                    return ((y / cellPx) % 2 == 0) ? a : b;

                case TempPattern.Grid:
                {
                    int lineW = Mathf.Max(1, size / 128);
                    bool onLine = (x % cellPx) < lineW || (y % cellPx) < lineW;
                    if (onLine) return a;
                    if (x < cellPx && y < cellPx) return new Color(1f, 0.25f, 0.25f);
                    return b;
                }

                case TempPattern.Solid:
                    return a;

                case TempPattern.BumpsNormal:
                    return BumpsNormalPixel(x, y, cellPx);

                default:
                    return a;
            }
        }

        private static float BumpHeight(int x, int y, int cellPx)
        {
            float u = ((x % cellPx + cellPx) % cellPx + 0.5f) / cellPx - 0.5f;
            float v = ((y % cellPx + cellPx) % cellPx + 0.5f) / cellPx - 0.5f;
            float d = Mathf.Sqrt(u * u + v * v) / 0.4f;
            return d < 1f ? Mathf.Cos(d * Mathf.PI * 0.5f) : 0f;
        }

        private static Color BumpsNormalPixel(int x, int y, int cellPx)
        {
            float hx1 = BumpHeight(x + 1, y, cellPx);
            float hx0 = BumpHeight(x - 1, y, cellPx);
            float hy1 = BumpHeight(x, y + 1, cellPx);
            float hy0 = BumpHeight(x, y - 1, cellPx);

            float dx = hx1 - hx0;
            float dy = hy1 - hy0;

            Vector3 n = new Vector3(-dx * 4f, -dy * 4f, 1f).normalized;
            return new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
        }

        private static void ApplyTempTextureImportSettings(string path, TempPattern pattern, FilterMode filter)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) return;

            importer.textureType = pattern == TempPattern.BumpsNormal
                ? TextureImporterType.NormalMap
                : TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = filter;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;

            importer.SaveAndReimport();
        }

        public static PhysicsMaterial PhysicsMat(string path, float dynamicFriction, float staticFriction, float bounciness, PhysicsMaterialCombine bounceCombine = PhysicsMaterialCombine.Average)
        {
            // Unity 6은 CreateAsset으로 '.physicsMaterial' 파일을 만들 수 없습니다(파일은 써지지만 PhysicsMaterial로 임포트되지 않음).
            // 코드로 만들 때는 '.asset' 확장자를 씁니다.
            if (!path.EndsWith(".asset"))
            {
                Debug.LogError($"[StudyXR] Physics Material 경로는 .asset 으로 끝나야 합니다: {path}");
                return null;
            }

            PhysicsMaterial existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (existing != null)
            {
                reusedAssets++;
                return existing;
            }

            string name = Path.GetFileNameWithoutExtension(path);
            var material = new PhysicsMaterial(name)
            {
                dynamicFriction = dynamicFriction,
                staticFriction = staticFriction,
                bounciness = bounciness,
                bounceCombine = bounceCombine,
                frictionCombine = PhysicsMaterialCombine.Average,
            };

            string folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder))
            {
                EnsureFolder(folder);
            }

            AssetDatabase.CreateAsset(material, path);
            createdAssets++;

            return material;
        }

        public static T CopyAsset<T>(string sourcePath, string destinationPath) where T : UnityEngine.Object
        {
            // 이미 복사본이 있으면 속성을 다시 설정하지 않고 그대로 돌려줍니다.
            // 사용자가 Render Pipeline Converter로 변환해 둔 결과를 빌더 재실행이 덮지 않기 위함입니다.
            T existing = AssetDatabase.LoadAssetAtPath<T>(destinationPath);
            if (existing != null)
            {
                reusedAssets++;
                return existing;
            }

            // 구매 에셋의 원본 파일은 고치지 않습니다. 변환 실습은 이 복사본에서 합니다.
            T source = AssetDatabase.LoadAssetAtPath<T>(sourcePath);
            if (source == null)
            {
                Debug.LogError($"[StudyXR] 원본 에셋을 찾을 수 없습니다: {sourcePath}");
                return null;
            }

            string folder = Path.GetDirectoryName(destinationPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder))
            {
                EnsureFolder(folder);
            }

            if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
            {
                Debug.LogError($"[StudyXR] 에셋 복사에 실패했습니다: {sourcePath} -> {destinationPath}");
                return null;
            }

            createdAssets++;
            return AssetDatabase.LoadAssetAtPath<T>(destinationPath);
        }

        // 정점 셰이더로 물결을 만들려면 정점이 촘촘해야 합니다. Unity 기본 Plane은 10 x 10 칸이라 모자랍니다.
        // XZ 평면, 가운데가 원점, 크기 size(m) 정사각형을 segments x segments 칸으로 나눈 메시를 에셋(.asset)으로 만듭니다.
        // 법선은 위(+y), 탄젠트는 +x(w = 1) — 노멀맵을 쓰는 셰이더가 탄젠트 공간을 만들 수 있게 넣어 둡니다.
        // bounds는 위아래로 boundsPadding만큼 키웁니다. 정점 셰이더가 높이를 바꿔도 시야 밖으로 잘못 잘리지 않게 하기 위함입니다.
        public static Mesh GridMesh(string path, float size, int segments, float boundsPadding = 0.5f)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                reusedAssets++;
                return existing;
            }

            int line = segments + 1;
            var vertices = new Vector3[line * line];
            var uvs = new Vector2[line * line];
            var normals = new Vector3[line * line];
            var tangents = new Vector4[line * line];
            for (int z = 0; z < line; z++)
            {
                for (int x = 0; x < line; x++)
                {
                    int i = z * line + x;
                    float u = (float)x / segments;
                    float v = (float)z / segments;
                    vertices[i] = new Vector3((u - 0.5f) * size, 0f, (v - 0.5f) * size);
                    uvs[i] = new Vector2(u, v);
                    normals[i] = Vector3.up;
                    tangents[i] = new Vector4(1f, 0f, 0f, 1f);
                }
            }

            var triangles = new int[segments * segments * 6];
            int t = 0;
            for (int z = 0; z < segments; z++)
            {
                for (int x = 0; x < segments; x++)
                {
                    int i = z * line + x;
                    // 위에서 볼 때 시계 방향 = Unity에서 앞면(위를 향함)
                    triangles[t++] = i;
                    triangles[t++] = i + line;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + line;
                    triangles[t++] = i + line + 1;
                }
            }

            var mesh = new Mesh { name = Path.GetFileNameWithoutExtension(path) };
            mesh.indexFormat = line * line > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.normals = normals;
            mesh.tangents = tangents;
            mesh.triangles = triangles;
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(size, boundsPadding * 2f, size));

            string folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder))
            {
                EnsureFolder(folder);
            }

            AssetDatabase.CreateAsset(mesh, path);
            createdAssets++;
            return mesh;
        }

        // 있는 머티리얼을 복제해 다른 값으로 저장합니다(get-or-create). init 은 처음 만들 때만 (세션 YC)
        public static Material CopyMaterial(Material source, string path, Action<Material> init = null)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                reusedAssets++;
                return existing;
            }

            string folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder)) EnsureFolder(folder);
            var copy = new Material(source) { name = Path.GetFileNameWithoutExtension(path) };
            init?.Invoke(copy);
            return CreateAsset(copy, path);
        }

        // 프리팹을 같은 경로에 다시 굽습니다 — GUID 가 유지되어 다른 에셋의 참조(네트워크 프리팹 목록 등)가 깨지지 않음 (세션 YC)
        public static GameObject RebuildPrefab(string path, Func<GameObject> buildTemplate)
        {
            GameObject template = buildTemplate();
            string folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder)) EnsureFolder(folder);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(template, path);
            UnityEngine.Object.DestroyImmediate(template);
            createdAssets++;
            return prefab;
        }

        public static GameObject GetOrCreatePrefab(string path, Func<GameObject> buildTemplate)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                reusedAssets++;
                return existing;
            }

            GameObject template = buildTemplate();

            string folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder))
            {
                EnsureFolder(folder);
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(template, path);
            UnityEngine.Object.DestroyImmediate(template);
            createdAssets++;

            return prefab;
        }

        // ScriptableObject 데이터 에셋(예: SO_Fish_Trout). 이미 있으면 재사용하고 init 을 다시 부르지 않습니다(세션 XI 추가).
        // 씬을 만든 뒤에 부르세요 — 먼저 만들면 새 씬을 여는 순간 메모리의 인스턴스가 내려가 씬에 null 로 저장됩니다.
        public static T GetOrCreateAsset<T>(string path, Action<T> init = null) where T : ScriptableObject
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                reusedAssets++;
                return existing;
            }

            string folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder))
            {
                EnsureFolder(folder);
            }

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            init?.Invoke(asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            createdAssets++;
            return asset;
        }

        private static Material CreateAsset(Material material, string path)
        {
            string folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder))
            {
                EnsureFolder(folder);
            }

            AssetDatabase.CreateAsset(material, path);
            createdAssets++;

            return material;
        }

        private static void ApplyUrpKeywords(Material material)
        {
            BaseShaderGUI.SetMaterialKeywords(material, LitGUI.SetMaterialKeywords);
        }

        // ───────────────────────────── 오브젝트 ─────────────────────────────

        public static Transform Group(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        public static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localEuler, Vector3 localScale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.transform.localScale = localScale;

            if (material != null)
            {
                go.GetComponent<Renderer>().sharedMaterial = material;
            }

            return go;
        }

        public static void RemoveCollider(GameObject go)
        {
            // GameObject.CreatePrimitive는 콜라이더를 자동으로 붙입니다.
            // 장식·라벨·표시용 도형은 물리에 끼면 안 되므로 지웁니다.
            Collider c = go.GetComponent<Collider>();
            if (c != null)
            {
                UnityEngine.Object.DestroyImmediate(c);
            }
        }

        public static Mesh BuiltinMesh(PrimitiveType type)
        {
            GameObject temp = GameObject.CreatePrimitive(type);
            Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.DestroyImmediate(temp);
            return mesh;
        }

        public static GameObject SpawnPrefab(GameObject prefab, Transform parent, Vector3 localPos, Vector3 localEuler)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            return go;
        }

        // FBX 모델은 루트에 임포터가 넣은 회전과 스케일이 들어 있습니다.
        // (예: 상용 Paper_Plane1.fbx 는 scale 100, 회전 X축 -90도 - Blender 의 Z-up 을 Unity 의 Y-up 으로 돌린 값)
        // SpawnPrefab 처럼 localRotation 을 덮어쓰면 모델이 옆으로 눕습니다.
        // 그래서 이 함수는 Transform 을 건드리지 않고 부모만 바꿉니다. 놓을 자리는 부모 Group 으로 정합니다.
        public static GameObject SpawnModel(GameObject modelPrefab, Transform parent)
        {
            if (modelPrefab == null)
            {
                Debug.LogError("[StudyXR] 모델 프리팹이 null 입니다. 경로를 확인하세요.");
                return null;
            }

            return (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, parent);
        }

        public static Light CreateLight(LightType type, string name, Transform parent, Vector3 position, Vector3 euler, Color color, float intensity, float range = 10f, float spotAngle = 30f, LightShadows shadows = LightShadows.Soft)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(euler);

            var light = go.AddComponent<Light>();
            light.type = type;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.spotAngle = spotAngle;
            light.shadows = shadows;
            light.lightmapBakeType = LightmapBakeType.Realtime;

            return light;
        }

        public static Light DefaultSun(Transform lights, float height = 5f)
        {
            // 교안 기본 태양광. 톤을 바꾸려면 여기만 고칩니다.
            return CreateLight(LightType.Directional, "Directional Light", lights,
                new Vector3(0f, height, 0f), new Vector3(50f, -30f, 0f), new Color(1f, 0.96f, 0.9f), 1f);
        }

        public static Quaternion LookRotation(Vector3 from, Vector3 to)
        {
            return Quaternion.LookRotation(to - from);
        }

        public static TextMeshPro Label3D(string text, Transform parent, Vector3 localPos, float fontSize = 4f, Color? color = null, string name = null)
        {
            var go = new GameObject(name ?? "Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.color = color ?? Color.white;
            tmp.rectTransform.sizeDelta = new Vector2(8f, 2f);

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
            if (font != null)
            {
                tmp.font = font;
            }
            else
            {
                Debug.LogWarning($"[StudyXR] 한글 폰트를 찾을 수 없어 기본 폰트를 씁니다: {KoreanFontPath}");
            }

            return tmp;
        }

        // ───────────────────────────── XR ─────────────────────────────

        // XRI Starter Assets의 XR Origin을 놓고 XR 카메라를 돌려줍니다.
        // 시뮬레이터는 씬에 두지 않습니다. XRProjectSetup이 켠 XRI 설정이 에디터 Play 때만 자동으로 만듭니다.
        // 놓는 위치는 바닥(카메라 높이 0)이고, 머리 높이는 XR Origin의 Camera Y Offset이 정합니다.
        // teleportOnly: 왼손 스틱의 연속 이동을 끄고 양손 모두 텔레포트만 쓰게 합니다(기획서 리스크 표의 멀미 대응).
        //   샘플 프리팹은 고치지 않고, 씬의 프리팹 인스턴스에 오버라이드로 남깁니다.
        public static Camera XRRig(Transform parent, Vector3 position, float yawDegrees = 0f, bool teleportOnly = true)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(XROriginPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[StudyXR] XR Origin 프리팹이 없습니다. XRI Starter Assets 샘플을 가져오세요: {XROriginPrefabPath}");
                return null;
            }

            var managerGo = new GameObject("XR Interaction Manager");
            managerGo.transform.SetParent(parent, false);
            managerGo.AddComponent<XRInteractionManager>();

            var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            rig.transform.position = position;
            rig.transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);

            if (teleportOnly)
            {
                // ControllerInputActionManager는 샘플 어셈블리의 타입이라 이름으로 찾습니다(샘플을 지워도 이 파일은 컴파일됨).
                foreach (var behaviour in rig.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour != null && behaviour.GetType().Name == "ControllerInputActionManager")
                    {
                        SetField(behaviour, "m_SmoothMotionEnabled", false);
                    }
                }
            }

            DisableJump(rig);
            DisableTunnelingVignette(rig);
            XRUIEventSystem(parent);

            var origin = rig.GetComponent<XROrigin>();
            if (origin == null || origin.Camera == null)
            {
                Debug.LogError("[StudyXR] XR Origin 프리팹에서 카메라를 찾지 못했습니다.");
                return null;
            }

            return origin.Camera;
        }

        // XRI 스타터 리그의 점프(오른손 A)를 끕니다(세션 YF). A 버튼은 씬 기능에 배정합니다(QuestButtons).
        // 리그를 직접 만들지 않는 씬(상용 리그 · Examples 리그)도 이 함수를 불러 끄면 됩니다.
        public static void DisableJump(GameObject rig)
        {
            if (rig == null) return;
            foreach (var behaviour in rig.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour != null && behaviour.GetType().Name == "JumpProvider") behaviour.gameObject.SetActive(false);
            }
        }

        // 이동할 때 시야 가장자리를 검게 좁히는 XRI 터널링 비네트(편의 모드)를 리그에서 뺍니다(세션 YG — 사용자 요청).
        // XRI Examples 리그는 LocomotionManager 의 '편의 모드'가 이 오브젝트를 켜고 끄므로,
        // 오브젝트만 끄면 다시 켜질 수 있습니다. 컨트롤러 · 렌더러까지 꺼서 켜져도 아무것도 그리지 않게 합니다.
        public static void DisableTunnelingVignette(GameObject rig)
        {
            if (rig == null) return;
            foreach (var behaviour in rig.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null) continue;
                string typeName = behaviour.GetType().Name;
                if (typeName == "TunnelingVignetteController")
                {
                    behaviour.enabled = false;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour);
                    foreach (var renderer in behaviour.GetComponentsInChildren<Renderer>(true))
                    {
                        renderer.enabled = false;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    }
                    behaviour.gameObject.SetActive(false);
                }
                else if (typeName == "LocomotionManager")
                {
                    SetField(behaviour, "m_EnableComfortMode", false);   // XRI Examples 리그의 편의 모드 기본값
                }
            }
        }

        // XRI 스타터 입력 액션 파일 (Starter Assets 샘플)
        public const string StarterInputActionsPath = "Assets/Samples/XR Interaction Toolkit/3.4.1/Starter Assets/XRI Default Input Actions.inputactions";

        // 리그의 Near-Far 인터랙터가 그립 대신 '트리거'로 잡게 합니다(세션 YH — 03 Throw Range 사용자 요청 "Grip 버튼이 아니라 Trigger를 이용해서 물체를 잡고 던질 수 있게").
        //  - 잡기(Select) ← 트리거(XRI {Left/Right} Interaction/Activate · Activate Value)
        //  - 활성화(Activate) ← 그립(… /Select · Select Value). 둘을 맞바꿔 트리거 한 번이 잡기와 활성화를 함께 일으키지 않게 합니다
        //  - UI 누르기는 원래 트리거라 그대로입니다(레이로 월드 버튼 누르기)
        // 샘플 프리팹 원본은 고치지 않고 씬의 프리팹 인스턴스 덮어쓰기로만 바꿉니다.
        // leftHand · rightHand 로 한 손만 바꿀 수 있습니다(04 밤 호수는 왼손만 — 릴을 왼손 트리거로 돌림, 오른손은 그립으로 낚싯대)
        public static void UseTriggerToGrab(GameObject rig, bool leftHand = true, bool rightHand = true)
        {
            if (rig == null) return;
            foreach (NearFarInteractor interactor in rig.GetComponentsInChildren<NearFarInteractor>(true))
            {
                bool isLeft = interactor.handedness == InteractorHandedness.Left;
                if (isLeft ? !leftHand : !rightHand) continue;
                string hand = isLeft ? "Left" : "Right";
                InputActionReference trigger = StarterAction($"XRI {hand} Interaction/Activate");
                InputActionReference triggerValue = StarterAction($"XRI {hand} Interaction/Activate Value");
                InputActionReference grip = StarterAction($"XRI {hand} Interaction/Select");
                InputActionReference gripValue = StarterAction($"XRI {hand} Interaction/Select Value");
                if (trigger == null || triggerValue == null || grip == null || gripValue == null) continue;

                interactor.selectInput.inputActionReferencePerformed = trigger;
                interactor.selectInput.inputActionReferenceValue = triggerValue;
                interactor.activateInput.inputActionReferencePerformed = grip;
                interactor.activateInput.inputActionReferenceValue = gripValue;
                EditorUtility.SetDirty(interactor);
                PrefabUtility.RecordPrefabInstancePropertyModifications(interactor);
            }
        }

        // 스타터 입력 액션의 참조를 "맵/액션" 이름으로 찾습니다. 같은 이름의 숨김 사본이 있을 수 있어 보이는 것을 먼저 고릅니다
        public static InputActionReference StarterAction(string mapSlashAction)
        {
            InputActionReference hidden = null;
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(StarterInputActionsPath))
            {
                var reference = asset as InputActionReference;
                if (reference == null || reference.name != mapSlashAction) continue;
                if ((reference.hideFlags & HideFlags.HideInHierarchy) == 0) return reference;
                hidden = reference;
            }
            if (hidden == null) Debug.LogError("[StudyXR] 액션 참조를 찾지 못했습니다: " + mapSlashAction);
            return hidden;
        }

        // 월드 공간 UI를 컨트롤러 레이로 누르려면 기본 InputModule 대신 XRUIInputModule이 필요합니다.
        public static EventSystem XRUIEventSystem(Transform parent)
        {
            var go = new GameObject("Event System");
            go.transform.SetParent(parent, false);
            var eventSystem = go.AddComponent<EventSystem>();
            go.AddComponent<XRUIInputModule>();
            return eventSystem;
        }

        // VR에서는 Screen Space Overlay가 보이지 않으므로 안내 문구·수치 표시는 월드 공간 패널로 둡니다.
        // sizeMeters는 미터 단위 패널 크기입니다. 캔버스는 1 m = 1000 단위로 만들어 글꼴 크기를 픽셀처럼 씁니다.
        public static TextMeshProUGUI WorldPanel(string text, string name, Transform parent, Vector3 localPos, Vector3 localEuler, Vector2 sizeMeters, float fontSize = 36f)
        {
            const float UnitsPerMeter = 1000f;

            var canvasGo = new GameObject(name);
            canvasGo.transform.SetParent(parent, false);
            canvasGo.transform.localPosition = localPos;
            canvasGo.transform.localRotation = Quaternion.Euler(localEuler);
            canvasGo.transform.localScale = Vector3.one / UnitsPerMeter;

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGo.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 2f;
            canvasGo.AddComponent<TrackedDeviceGraphicRaycaster>();

            var canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta = sizeMeters * UnitsPerMeter;

            // 글자 면 + 뒤의 3D 배경판(게시판 · 안내판 · 액자 — PanelBackdrop.Style, 세션 YC)
            var background = canvasGo.AddComponent<Image>();
            PanelBackdrop.ApplyFace(background);
            PanelBackdrop.Attach(canvasRect, sizeMeters);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(canvasGo.transform, false);

            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(24f, 24f);
            textRect.offsetMax = new Vector2(-24f, -24f);

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.alignment = TextAlignmentOptions.TopLeft;

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
            if (font != null)
            {
                tmp.font = font;
            }
            else
            {
                Debug.LogWarning($"[StudyXR] 한글 폰트를 찾을 수 없어 기본 폰트를 씁니다: {KoreanFontPath}");
            }

            return tmp;
        }

        // WorldPanel 위에 누를 수 있는 버튼을 붙입니다. 컨트롤러 레이로 누르면 onClick이 불립니다.
        // panelText는 WorldPanel이 돌려준 텍스트이고, 버튼은 그 부모(캔버스)의 자식이 됩니다.
        // anchoredPosition·sizePixels는 캔버스 단위(1 m = 1000)입니다. 캔버스 가운데가 (0, 0)입니다.
        // onClick 연결은 빌더가 UnityEditor.Events.UnityEventTools.AddVoidPersistentListener로 합니다.
        public static Button WorldButton(string label, string name, TMP_Text panelText, Vector2 anchoredPosition, Vector2 sizePixels, float fontSize = 32f)
        {
            if (panelText == null || panelText.transform.parent == null)
            {
                Debug.LogError("[StudyXR] WorldButton: WorldPanel이 돌려준 텍스트를 넘겨야 합니다.");
                return null;
            }

            var buttonGo = new GameObject(name);
            buttonGo.transform.SetParent(panelText.transform.parent, false);

            var rect = buttonGo.AddComponent<RectTransform>();
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizePixels;

            var image = buttonGo.AddComponent<Image>();
            image.color = new Color(0.20f, 0.45f, 0.85f, 1f);
            var button = buttonGo.AddComponent<Button>();
            button.targetGraphic = image;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(buttonGo.transform, false);
            var labelRect = labelGo.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
            if (font != null)
            {
                tmp.font = font;
            }

            return button;
        }

        // ───────────────────────────── v2 추가: 상용 씬·프리팹, 정적 표시, URP 변환 (기반 6.2) ─────────────────────────────

        // 상용 씬을 모듈 폴더에 사본으로 만들고 그 사본을 단독으로 열어 돌려줍니다. 원본은 고치지 않습니다.
        // 이미 사본이 있으면 ConfirmOverwrite 로 확인한 뒤 새로 복사합니다. 원본이 없으면 오류 로그 후 default(Scene).
        public static Scene CopyScene(string sourceScenePath, string destScenePath)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(sourceScenePath) == null)
            {
                Debug.LogError($"[StudyXR] 원본 씬을 찾을 수 없습니다: {sourceScenePath}");
                return default;
            }

            if (sourceScenePath == destScenePath)
            {
                Debug.LogError($"[StudyXR] 원본과 사본 경로가 같습니다: {sourceScenePath}");
                return default;
            }

            if (!ConfirmOverwrite(destScenePath)) return default;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return default;

            string folder = Path.GetDirectoryName(destScenePath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder))
            {
                EnsureFolder(folder);
            }

            // CopyAsset 은 대상이 있으면 실패하므로, 위에서 덮어쓰기를 확인받은 경우에만 지우고 다시 복사합니다
            if (File.Exists(destScenePath)) AssetDatabase.DeleteAsset(destScenePath);

            if (!AssetDatabase.CopyAsset(sourceScenePath, destScenePath))
            {
                Debug.LogError($"[StudyXR] 씬 복사에 실패했습니다: {sourceScenePath} -> {destScenePath}");
                return default;
            }

            createdAssets = 0;
            reusedAssets = 0;
            PanelBackdrop.ResetForNewScene();
            AssetDatabase.ImportAsset(destScenePath);
            return EditorSceneManager.OpenScene(destScenePath, OpenSceneMode.Single);
        }

        // 상용 프리팹을 연결을 유지한 채 놓습니다. 경로에 프리팹이 없으면 오류 로그 후 null — 빌드는 계속됩니다.
        // 프리팹 루트에 모델 임포터의 회전·스케일이 들어 있을 수 있어(SpawnModel 주석), 덮어쓰지 않고 곱해서 보존합니다.
        // 루트가 단위 회전·스케일인 프리팹(대부분의 Synty)은 localEuler·uniformScale 그대로입니다.
        public static GameObject SpawnPrefabAt(string prefabPath, Transform parent, Vector3 localPos, Vector3 localEuler, float uniformScale = 1f)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[StudyXR] 프리팹을 찾을 수 없습니다: {prefabPath}");
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            Transform t = go.transform;
            t.localPosition = localPos;
            t.localRotation = Quaternion.Euler(localEuler) * prefab.transform.localRotation;
            t.localScale = prefab.transform.localScale * uniformScale;
            return go;
        }

        // 움직이지 않는 배경에 Static 을 켭니다(라이트맵 기여 · 오클루전 · 배칭 · 반사 프로브).
        // Rigidbody 나 XRBaseInteractable 이 붙은 오브젝트와 그 자식은 움직일 수 있으므로 건너뜁니다.
        public static void MarkStatic(GameObject root)
        {
            if (root == null) return;

            const StaticEditorFlags flags = StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic
                | StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic;

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                GameObject go = t.gameObject;
                if (go.GetComponentInParent<Rigidbody>(true) != null) continue;
                if (go.GetComponentInParent<XRBaseInteractable>(true) != null) continue;
                GameObjectUtility.SetStaticEditorFlags(go, flags);
            }
        }

        // Built-in 셰이더(분홍으로 보이는 것)를 쓰는 머티리얼을 materialFolder 의 URP 사본으로 바꿔 끼우고, 바꾼 개수를 돌려줍니다.
        // 사본 이름은 M_URP_<원본 이름>이고 이미 있으면 재사용합니다(원본 이름이 같은 서로 다른 머티리얼은 같은 사본을 씁니다).
        public static int ReplaceBuiltinMaterials(GameObject root, string materialFolder)
        {
            if (root == null) return 0;

            int replaced = 0;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (source == null || !IsBuiltinShader(source.shader)) continue;

                    Material copy = UrpCopyOf(source, materialFolder, renderer is ParticleSystemRenderer);
                    if (copy == null) continue;
                    materials[i] = copy;
                    changed = true;
                    replaced++;
                }
                if (changed)
                {
                    renderer.sharedMaterials = materials;
                    EditorUtility.SetDirty(renderer);
                }
            }
            return replaced;
        }

        private static bool IsBuiltinShader(Shader shader)
        {
            if (shader == null) return true;
            string name = shader.name;
            return name == "Standard" || name.StartsWith("Legacy Shaders/") || name.StartsWith("Particles/") || name == "Hidden/InternalErrorShader";
        }

        private static Material UrpCopyOf(Material source, string materialFolder, bool onParticleRenderer)
        {
            string shaderName = source.shader != null ? source.shader.name : "";
            bool particle = onParticleRenderer || shaderName.StartsWith("Particles/") || shaderName.Contains("/Particles/");
            string path = $"{materialFolder}/M_URP_{source.name}.mat";

            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                reusedAssets++;
                return existing;
            }

            Shader urp = Shader.Find(particle ? "Universal Render Pipeline/Particles/Unlit" : LitShaderName);
            if (urp == null)
            {
                Debug.LogError($"[StudyXR] URP 셰이더를 찾을 수 없습니다: {(particle ? "Particles/Unlit" : LitShaderName)}");
                return null;
            }

            var copy = new Material(urp) { name = $"M_URP_{source.name}" };
            if (source.HasProperty("_MainTex"))
            {
                copy.SetTexture("_BaseMap", source.GetTexture("_MainTex"));
                copy.SetTextureScale("_BaseMap", source.GetTextureScale("_MainTex"));
                copy.SetTextureOffset("_BaseMap", source.GetTextureOffset("_MainTex"));
            }
            if (source.HasProperty("_Color")) copy.SetColor("_BaseColor", source.GetColor("_Color"));
            else if (source.HasProperty("_TintColor")) copy.SetColor("_BaseColor", source.GetColor("_TintColor"));

            if (particle)
            {
                // 파티클은 대부분 반투명 : Additive 계열 이름이면 가산, Multiply 면 곱, 나머지는 알파 블렌드
                copy.SetFloat("_Surface", 1f);
                copy.SetFloat("_Blend", shaderName.Contains("Additive") ? 2f : shaderName.Contains("Multiply") ? 3f : 0f);
                BaseShaderGUI.SetMaterialKeywords(copy, null, ParticleGUI.SetMaterialKeywords);
            }
            else
            {
                if (source.HasProperty("_Metallic")) copy.SetFloat("_Metallic", source.GetFloat("_Metallic"));
                if (source.HasProperty("_Glossiness")) copy.SetFloat("_Smoothness", source.GetFloat("_Glossiness"));
                if (source.HasProperty("_BumpMap") && source.GetTexture("_BumpMap") != null) copy.SetTexture("_BumpMap", source.GetTexture("_BumpMap"));
                if (source.HasProperty("_EmissionColor") && source.IsKeywordEnabled("_EMISSION"))
                {
                    copy.SetColor("_EmissionColor", source.GetColor("_EmissionColor"));
                    if (source.HasProperty("_EmissionMap")) copy.SetTexture("_EmissionMap", source.GetTexture("_EmissionMap"));
                    // new Material 의 기본값은 EmissiveIsBlack 이라 URP 가 _EMISSION 키워드를 켜지 않습니다 — 원본 플래그를 이어받습니다
                    MaterialGlobalIlluminationFlags flags = source.globalIlluminationFlags & ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                    copy.globalIlluminationFlags = flags == MaterialGlobalIlluminationFlags.None ? MaterialGlobalIlluminationFlags.BakedEmissive : flags;
                }

                // Standard 의 _Mode : 0 불투명 · 1 컷아웃 · 2 페이드 · 3 투명
                int mode = source.HasProperty("_Mode") ? Mathf.RoundToInt(source.GetFloat("_Mode")) : 0;
                if (mode == 1)
                {
                    copy.SetFloat("_AlphaClip", 1f);
                    if (source.HasProperty("_Cutoff")) copy.SetFloat("_Cutoff", source.GetFloat("_Cutoff"));
                }
                else if (mode >= 2)
                {
                    copy.SetFloat("_Surface", 1f);
                    copy.SetFloat("_Blend", mode == 3 ? 1f : 0f);
                }
                ApplyUrpKeywords(copy);
            }

            return CreateAsset(copy, path);
        }

        // 입자용 반투명 머티리얼 (URP Particles/Unlit). additive 면 가산 블렌드 — 물방울 · 불꽃 등 (세션 YC)
        public static Material ParticleMaterial(string path, Texture2D baseMap, Color color, bool additive = false)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                reusedAssets++;
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                Debug.LogError("[StudyXR] URP 셰이더를 찾을 수 없습니다: Particles/Unlit");
                return null;
            }

            string folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder)) EnsureFolder(folder);

            var material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            if (baseMap != null) material.SetTexture("_BaseMap", baseMap);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", additive ? 2f : 0f);
            BaseShaderGUI.SetMaterialKeywords(material, null, ParticleGUI.SetMaterialKeywords);
            return CreateAsset(material, path);
        }

        // 카메라 앞 0.12 m 에 검은 쿼드를 붙이고 ScreenFade 를 연결합니다. 알파 0 이면 ScreenFade 가 렌더러를 꺼 둡니다.
        public static ScreenFade AddScreenFade(Camera camera, string materialPath)
        {
            if (camera == null)
            {
                Debug.LogError("[StudyXR] AddScreenFade : 카메라가 null 입니다");
                return null;
            }

            // 알파 0 으로 만듭니다 — 이미 있는 에셋이 재사용돼 알파 1 이어도 아래에서 렌더러를 꺼 두므로 안전합니다
            Material material = TransparentMaterial(materialPath, new Color(0f, 0f, 0f, 0f), 0f);
            GameObject quad = Primitive(PrimitiveType.Quad, "Screen Fade", camera.transform, new Vector3(0f, 0f, 0.12f), Vector3.zero, Vector3.one, material);
            RemoveCollider(quad);

            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            // ScreenFade 는 Play 의 Awake 에서만 렌더러를 끄므로, 편집 모드에서 시야를 가리지 않게 저장 상태도 꺼 둡니다
            renderer.enabled = false;

            ScreenFade fade = quad.AddComponent<ScreenFade>();
            SetField(fade, "fadeRenderer", renderer);
            return fade;
        }

        // 월드 TMP 텍스트(한글 폰트)와 PerfHud 를 만들어 head · label 을 연결합니다. toggleAction 은 비워 둠(F2 만 동작).
        // 글자 크기 : 레이블을 0.01 배로 줄여 두었으므로 글꼴 크기 12 가 약 1.2 cm(0.6 m 거리에서 보기 편한 크기)입니다.
        public static PerfHud AddPerfHud(Transform parent, Camera head)
        {
            var go = new GameObject("Perf HUD");
            go.transform.SetParent(parent, false);

            TextMeshPro label = Label3D("FPS", go.transform, Vector3.zero, 12f, new Color(0.7f, 1f, 0.7f), "Perf Label");
            label.transform.localScale = Vector3.one * 0.01f;
            label.rectTransform.sizeDelta = new Vector2(20f, 6f);
            label.alignment = TextAlignmentOptions.Left;
            label.raycastTarget = false;

            PerfHud hud = go.AddComponent<PerfHud>();
            SetField(hud, "head", head != null ? head.transform : null);
            SetField(hud, "label", label);
            return hud;
        }

        // 야외 장면의 룩 : 선형 안개 · 해 그림자(Soft, 세기 0.8) · 환경광(스카이박스가 있으면 Skybox, 없으면 Trilight).
        // 안개와 그림자만 덧붙이는 용도라, 상용 씬 사본의 원래 설정을 지우지 않도록 스카이박스는 넘겼을 때만 바꾸고,
        // 넘기지 않았는데 씬에 이미 스카이박스가 있으면 환경광도 건드리지 않습니다.
        public static void ApplyOutdoorLook(Light sun, Color fogColor, float fogStart, float fogEnd, Material skybox = null)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;

            if (sun != null)
            {
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.8f;
            }

            if (skybox != null)
            {
                RenderSettings.skybox = skybox;
                RenderSettings.ambientMode = AmbientMode.Skybox;
            }
            else if (RenderSettings.skybox == null)
            {
                // 씬에도 스카이박스가 전혀 없을 때만 : 안개색에서 하늘 · 지평선 · 땅 세 색을 만들어 안개와 어울리게 합니다.
                // 씬에 스카이박스가 있으면 원래 환경광을 그대로 둡니다
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = Color.Lerp(fogColor, Color.white, 0.35f);
                RenderSettings.ambientEquatorColor = fogColor * 0.8f;
                RenderSettings.ambientGroundColor = fogColor * 0.4f;
            }
        }

        // ───────────────────────────── 연결 (SerializedObject) ─────────────────────────────

        public static void SetField(UnityEngine.Object target, string fieldName, UnityEngine.Object value)
        {
            SerializedProperty property = FindPropertyOrLog(target, fieldName);
            if (property == null) return;
            property.objectReferenceValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetField(UnityEngine.Object target, string fieldName, int value)
        {
            SerializedProperty property = FindPropertyOrLog(target, fieldName);
            if (property == null) return;
            property.intValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetField(UnityEngine.Object target, string fieldName, float value)
        {
            SerializedProperty property = FindPropertyOrLog(target, fieldName);
            if (property == null) return;
            property.floatValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetField(UnityEngine.Object target, string fieldName, bool value)
        {
            SerializedProperty property = FindPropertyOrLog(target, fieldName);
            if (property == null) return;
            property.boolValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetField(UnityEngine.Object target, string fieldName, string value)
        {
            SerializedProperty property = FindPropertyOrLog(target, fieldName);
            if (property == null) return;
            property.stringValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetField(UnityEngine.Object target, string fieldName, Color value)
        {
            SerializedProperty property = FindPropertyOrLog(target, fieldName);
            if (property == null) return;
            property.colorValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetField(UnityEngine.Object target, string fieldName, Vector2 value)
        {
            SerializedProperty property = FindPropertyOrLog(target, fieldName);
            if (property == null) return;
            property.vector2Value = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetField(UnityEngine.Object target, string fieldName, Vector3 value)
        {
            SerializedProperty property = FindPropertyOrLog(target, fieldName);
            if (property == null) return;
            property.vector3Value = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetEnum(UnityEngine.Object target, string fieldName, int value)
        {
            SerializedProperty property = FindPropertyOrLog(target, fieldName);
            if (property == null) return;
            property.intValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetArray(UnityEngine.Object target, string fieldName, UnityEngine.Object[] values)
        {
            SerializedProperty property = FindPropertyOrLog(target, fieldName);
            if (property == null) return;

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SerializedProperty FindPropertyOrLog(UnityEngine.Object target, string fieldName)
        {
            var serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError($"[StudyXR] 필드 없음: {target.GetType().Name}.{fieldName}");
            }
            return property;
        }
    }
}
