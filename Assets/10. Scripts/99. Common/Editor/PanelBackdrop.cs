using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace StudyXR.EditorTools
{
    /// <summary>
    /// WorldPanel 뒤에 실제 게시판처럼 보이는 3D 배경판을 붙입니다(세션 YC — 반투명 판만 있으면 배경에 묻혀 글이 눈에 안 들어옴).
    /// 판 · 테 · 지붕은 패널(캔버스)의 자식이라 패널이 움직이면 같이 움직입니다.
    /// 기둥은 땅이 있어야 세울 수 있어, 씬을 저장할 때(SceneBuildUtil.SaveScene) 땅을 레이로 찾아 한 번에 세웁니다.
    /// </summary>
    public static class PanelBackdrop
    {
        private const string Folder = "Assets/00. Study/Common/Panel";
        private const float UnitsPerMeter = 1000f;

        private const float Margin = 0.035f;         // 글자 면 둘레로 보이는 테 폭 (m)
        private const float BoardDepth = 0.03f;      // 판 두께 (m)
        private const float MaxPostLength = 2.6f;    // 이보다 땅이 멀면 기둥을 세우지 않음 (벽걸이 · 공중 판)

        private static readonly List<Transform> pendingPosts = new List<Transform>();

        public static PanelStyle Style { get; set; } = PanelStyle.Wood;

        // 물 위에 선 판(04 호수) : 아래에 땅이 없으면 기둥을 이 높이 아래 0.6 m 까지 (물에 박힌 말뚝). null 이면 기둥 없음
        public static float? WaterLevel { get; set; }

        // CreateScene · CopyScene 이 부릅니다 — 씬마다 나무 게시판에서 시작, 빌더가 필요하면 바꿈
        public static void ResetForNewScene()
        {
            Style = PanelStyle.Wood;
            WaterLevel = null;
            pendingPosts.Clear();
        }

        public static Color FaceColor(PanelStyle style)
        {
            switch (style)
            {
                case PanelStyle.Wood: return new Color(0.085f, 0.115f, 0.10f, 1f);    // 칠판 녹흑색
                case PanelStyle.Metal: return new Color(0.075f, 0.095f, 0.125f, 1f);  // 짙은 회청색
                case PanelStyle.Frame: return new Color(0.05f, 0.06f, 0.075f, 0.94f);
                default: return new Color(0f, 0f, 0f, 0.6f);
            }
        }

        // 캔버스(1 m = 1000 단위) 뒤에 판 · 테를 만듭니다. 글자 면은 캔버스의 Image 가 그대로 맡습니다
        public static void Attach(RectTransform canvas, Vector2 sizeMeters)
        {
            PanelStyle style = Style;
            if (style == PanelStyle.None) return;

            var root = new GameObject("Backdrop").transform;
            root.SetParent(canvas, false);
            root.localScale = Vector3.one * UnitsPerMeter;   // 자식의 로컬 단위 = 미터
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;

            float w = sizeMeters.x;
            float h = sizeMeters.y;

            switch (style)
            {
                case PanelStyle.Wood:
                {
                    Material board = WoodMaterial("M_Panel_Wood_Dark", new Color(0.36f, 0.25f, 0.16f));
                    Material trim = WoodMaterial("M_Panel_Wood_Trim", new Color(0.55f, 0.39f, 0.24f));
                    Box("Board", root, new Vector3(0f, 0f, BoardDepth * 0.5f + 0.004f), new Vector3(w + 2f * Margin, h + 2f * Margin, BoardDepth), board);
                    Frame(root, w, h, 0.045f, 0.03f, trim);
                    // 지붕 판자 : 빗물막이처럼 위로 조금 튀어나온 두꺼운 판
                    Box("Cap", root, new Vector3(0f, h * 0.5f + Margin + 0.03f, 0.01f), new Vector3(w + 2f * Margin + 0.04f, 0.035f, 0.12f), trim)
                        .transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);
                    break;
                }
                case PanelStyle.Metal:
                {
                    Material board = MetalMaterial("M_Panel_Metal_Back", new Color(0.18f, 0.19f, 0.21f), 0.6f, 0.45f);
                    Material trim = MetalMaterial("M_Panel_Metal_Trim", new Color(0.62f, 0.64f, 0.67f), 0.9f, 0.65f);
                    Box("Board", root, new Vector3(0f, 0f, BoardDepth * 0.5f + 0.004f), new Vector3(w + 2f * Margin, h + 2f * Margin, BoardDepth), board);
                    Frame(root, w, h, 0.03f, 0.025f, trim);
                    break;
                }
                case PanelStyle.Frame:
                {
                    Material trim = MetalMaterial("M_Panel_Frame_Black", new Color(0.05f, 0.05f, 0.055f), 0.3f, 0.55f);
                    // 얇은 검은 뒷판 — 테와 글자 면 사이로 방(패스스루)이 비치지 않게
                    Box("Board", root, new Vector3(0f, 0f, 0.008f), new Vector3(w + 2f * Margin, h + 2f * Margin, 0.008f), trim);
                    Frame(root, w, h, 0.02f, 0.02f, trim);
                    break;
                }
            }

            if (style != PanelStyle.Frame) pendingPosts.Add(root);
        }

        // 실행 중에 움직이는 패널(결과판 등)은 기둥을 세우지 않습니다 — WorldPanel 이 돌려준 글자의 캔버스를 넘김
        public static void NoPosts(Transform canvas)
        {
            Transform root = canvas != null ? canvas.Find("Backdrop") : null;
            if (root != null) pendingPosts.Remove(root);
        }

        // 씬을 저장하기 직전에 부릅니다. 판 아래 두 모서리에서 땅을 찾아 기둥을 세웁니다
        public static void BuildPendingPosts()
        {
            RemoveCoveredCaps();
            if (pendingPosts.Count == 0) return;
            Physics.SyncTransforms();   // 막 놓은 프리팹의 콜라이더 위치를 반영 (편집 모드)

            foreach (Transform root in pendingPosts)
            {
                if (root == null) continue;
                AddPosts(root);
            }
            pendingPosts.Clear();
        }

        private static void AddPosts(Transform root)
        {
            Transform board = root.Find("Board");
            if (board == null) return;

            // 앞뒤로 기운 판(머리 위 안내판 등)은 기둥이 판 앞을 지나가므로 세우지 않음 — 떠 있는 안내판
            if (Vector3.Angle(root.up, Vector3.up) > 5f) return;

            // 바로 뒤 0.35 m 안에 벽이 있으면 벽걸이 — 기둥 없음
            Vector3 back = root.forward;
            if (Physics.Raycast(board.position, back, out _, 0.35f, ~0, QueryTriggerInteraction.Ignore)) return;

            // 나무인지는 판 머티리얼로 봅니다 (쌓은 판은 지붕 판자를 지워서 Cap 으로는 알 수 없음)
            Material boardMaterial = board.GetComponent<MeshRenderer>().sharedMaterial;
            bool wood = boardMaterial != null && boardMaterial.name.Contains("Wood");
            Material postMaterial = wood
                ? WoodMaterial("M_Panel_Wood_Trim", new Color(0.55f, 0.39f, 0.24f))
                : MetalMaterial("M_Panel_Metal_Trim", new Color(0.62f, 0.64f, 0.67f), 0.9f, 0.65f);

            Vector3 half = board.localScale * 0.5f;
            float postSize = wood ? 0.07f : 0.045f;
            float inset = wood ? postSize * 0.5f + 0.05f : postSize * 0.5f + 0.08f;

            // 두 기둥 모두 땅을 찾아야 세움 (한쪽만 있으면 어색함)
            var tops = new Vector3[2];
            var grounds = new Vector3[2];
            for (int i = 0; i < 2; i++)
            {
                float x = (i == 0 ? -1f : 1f) * (half.x - inset);
                // 판 뒤쪽 면, 아래 가장자리에서 조금 위 — 기둥이 판 뒤에 붙어 올라가 보이게
                tops[i] = root.TransformPoint(new Vector3(x, half.y * 0.6f, board.localPosition.z + half.z + postSize * 0.5f));
                Vector3 bottomEdge = root.TransformPoint(new Vector3(x, -half.y, board.localPosition.z + half.z + postSize * 0.5f));
                if (Physics.Raycast(bottomEdge, Vector3.down, out RaycastHit hit, MaxPostLength, ~0, QueryTriggerInteraction.Ignore))
                    grounds[i] = hit.point;
                else if (WaterLevel.HasValue && bottomEdge.y - WaterLevel.Value < MaxPostLength)
                    grounds[i] = new Vector3(bottomEdge.x, WaterLevel.Value - 0.55f, bottomEdge.z);   // 물에 박힌 말뚝
                else
                    return;
            }

            for (int i = 0; i < 2; i++)
            {
                Vector3 top = tops[i];
                Vector3 bottom = new Vector3(top.x, grounds[i].y - 0.05f, top.z);   // 땅에 5 cm 박음
                float length = top.y - bottom.y;
                if (length <= 0.05f) continue;

                GameObject post = wood
                    ? GameObject.CreatePrimitive(PrimitiveType.Cube)
                    : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = "Post " + (i + 1);
                Object.DestroyImmediate(post.GetComponent<Collider>());
                post.transform.SetParent(root, true);
                // 기둥은 언제나 땅에 수직 (패널이 뒤로 젖혀져 있어도)
                post.transform.rotation = Quaternion.Euler(0f, root.eulerAngles.y, 0f);
                post.transform.position = (top + bottom) * 0.5f;
                Vector3 worldScale = wood
                    ? new Vector3(postSize, length, postSize)
                    : new Vector3(postSize, length * 0.5f, postSize);   // 실린더 높이 2
                SetWorldScale(post.transform, worldScale);
                Setup(post.GetComponent<MeshRenderer>(), postMaterial);
            }
        }

        // 위아래로 붙여 쌓은 판 : 아래 판의 지붕 판자가 위 판에 파고들므로 지웁니다 (04 · 06 · 07 의 안내 + 채점/기록)
        private static void RemoveCoveredCaps()
        {
            var roots = new List<Transform>();
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name == "Backdrop" && t.Find("Board") != null) roots.Add(t);
            }

            foreach (Transform lower in roots)
            {
                Transform cap = lower.Find("Cap");
                if (cap == null) continue;
                Transform lowerBoard = lower.Find("Board");
                float lowerTop = lowerBoard.localPosition.y + lowerBoard.localScale.y * 0.5f;
                float lowerHalfW = lowerBoard.localScale.x * 0.5f;

                foreach (Transform upper in roots)
                {
                    if (upper == lower || Vector3.Dot(upper.forward, lower.forward) < 0.95f) continue;
                    Transform upperBoard = upper.Find("Board");
                    // 아래 판의 로컬(미터) 좌표로 위 판의 가운데 · 크기를 봄
                    Vector3 p = lower.InverseTransformPoint(upperBoard.position);
                    // 위 판의 반 크기도 아래 판 로컬 단위로 (세션 YH — 판을 1.3 배로 키운 씬에서 월드 단위와 섞이면 지붕을 못 지웠음)
                    Vector3 upperHalf = lower.InverseTransformVector(upperBoard.lossyScale * 0.5f);
                    upperHalf = new Vector3(Mathf.Abs(upperHalf.x), Mathf.Abs(upperHalf.y), Mathf.Abs(upperHalf.z));
                    float upperBottom = p.y - upperHalf.y;
                    bool sameDepth = Mathf.Abs(p.z) < 0.15f;
                    bool overlapX = Mathf.Abs(p.x) < lowerHalfW + upperHalf.x;
                    bool justAbove = upperBottom > lowerTop - 0.03f && upperBottom < lowerTop + 0.15f;
                    if (sameDepth && overlapX && justAbove)
                    {
                        Object.DestroyImmediate(cap.gameObject);
                        break;
                    }
                }
            }
        }

        // ───────────────────────────── 모양 ─────────────────────────────

        // 글자 면 둘레의 테 : 막대 4개 (위 · 아래는 전체 폭, 좌 · 우는 사이)
        private static void Frame(Transform root, float w, float h, float bar, float depth, Material material)
        {
            float outerW = w + 2f * Margin;
            float outerH = h + 2f * Margin;
            float z = -depth * 0.5f + 0.006f;   // 글자 면보다 6 mm 앞으로 — 면 가장자리를 덮음
            Box("Frame Top", root, new Vector3(0f, outerH * 0.5f - bar * 0.5f, z), new Vector3(outerW, bar, depth), material);
            Box("Frame Bottom", root, new Vector3(0f, -outerH * 0.5f + bar * 0.5f, z), new Vector3(outerW, bar, depth), material);
            Box("Frame Left", root, new Vector3(-outerW * 0.5f + bar * 0.5f, 0f, z), new Vector3(bar, outerH - 2f * bar, depth), material);
            Box("Frame Right", root, new Vector3(outerW * 0.5f - bar * 0.5f, 0f, z), new Vector3(bar, outerH - 2f * bar, depth), material);
        }

        private static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 size, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());   // 레이 · 손이 판에 걸리지 않게 (UI 레이캐스트는 캔버스가 맡음)
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            Setup(go.GetComponent<MeshRenderer>(), material);
            return go;
        }

        private static void Setup(MeshRenderer renderer, Material material)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        private static void SetWorldScale(Transform t, Vector3 worldScale)
        {
            Vector3 parentScale = t.parent != null ? t.parent.lossyScale : Vector3.one;
            t.localScale = new Vector3(worldScale.x / parentScale.x, worldScale.y / parentScale.y, worldScale.z / parentScale.z);
        }

        // ───────────────────────────── 머티리얼 · 텍스처 ─────────────────────────────

        private static Material WoodMaterial(string name, Color tint)
        {
            string path = Folder + "/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Texture2D grain = WoodGrainTexture();
            Material m = SceneBuildUtil.TexturedMaterial(path, grain, new Vector2(1f, 1f), tint, null, 0.22f);
            return m;
        }

        private static Material MetalMaterial(string name, Color color, float metallic, float smoothness)
        {
            return SceneBuildUtil.LitMaterial(Folder + "/" + name + ".mat", color, metallic, smoothness);
        }

        // 나뭇결 : 가로로 흐르는 결(사인) + 펄린 잡음으로 굽이 + 가는 결선. 흰색 바탕이라 머티리얼 색으로 물들입니다
        private static Texture2D WoodGrainTexture()
        {
            string path = Folder + "/T_Panel_Wood_Grain.png";
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            SceneBuildUtil.EnsureFolder(Folder);
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;
                    // 타일링되게 x 는 주기 함수만, y 잡음은 가로 방향으로 길게
                    float warp = Mathf.PerlinNoise(u * 3f, v * 12f) * 2.2f + Mathf.Sin(u * Mathf.PI * 2f) * 0.3f;
                    float ring = 0.5f + 0.5f * Mathf.Sin((v * 18f + warp) * Mathf.PI * 2f);
                    float fine = Mathf.PerlinNoise(u * 40f, v * 160f);
                    float shade = Mathf.Lerp(0.72f, 1f, ring) * Mathf.Lerp(0.88f, 1f, fine);
                    texture.SetPixel(x, y, new Color(shade, shade, shade, 1f));
                }
            }
            texture.Apply();
            File.WriteAllBytes(Path.GetFullPath(path), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // Image 가 있는 캔버스의 글자 면 색을 스타일에 맞춥니다
        public static void ApplyFace(Image face)
        {
            face.color = FaceColor(Style);
        }
    }
}
