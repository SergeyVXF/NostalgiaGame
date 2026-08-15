using System.Collections.Generic;
using UnityEngine;

namespace MiniVanGame
{
    public partial class MiniVanPlayer
    {
        private const int FacePaintResolution = 512;
        private const int FacePaintHistoryLimit = 24;

        private MiniVanFacePaintStation facePaintStation;
        private Texture2D facePaintTexture;
        private Texture2D facePaintCommitted;
        private Color32[] facePaintScratch;
        private readonly Stack<Color32[]> facePaintUndo = new Stack<Color32[]>(FacePaintHistoryLimit);
        private readonly Stack<Color32[]> facePaintRedo = new Stack<Color32[]>(FacePaintHistoryLimit);
        private Renderer facePaintRenderer;
        private MeshFilter facePaintMeshFilter;
        private Material facePaintMaterial;
        private Material facePaintOriginalMaterial;
        private Color32 facePaintBaseColor = new Color32(210, 170, 140, 255);
        private bool facePaintStrokeActive;
        private Vector2 facePaintLastUv;
        private bool hasFacePaintLastUv;

        // Cached mesh data for CPU raycasts (no MeshCollider / ClosestPoint).
        private Vector3[] facePaintVerts;
        private int[] facePaintTris;
        private Vector2[] facePaintMeshUvs;

        public bool IsFacePainting()
        {
            return facePaintStation != null;
        }

        public void BeginFacePaintSession(MiniVanFacePaintStation station)
        {
            if (!IsOwner || station == null)
            {
                return;
            }

            EnsureFacePaintSurface();
            facePaintStation = station;
            facePaintStrokeActive = false;
            hasFacePaintLastUv = false;
            ConfigureLocalCamera(false);
        }

        public void EndFacePaintStroke()
        {
            facePaintStrokeActive = false;
            hasFacePaintLastUv = false;
        }

        public void EndFacePaintSession(bool confirm)
        {
            if (!IsOwner)
            {
                return;
            }

            if (!confirm)
            {
                RestoreFacePaintFromCommitted();
            }
            else
            {
                CommitFacePaint();
            }

            facePaintStation = null;
            facePaintStrokeActive = false;
            hasFacePaintLastUv = false;
            facePaintUndo.Clear();
            facePaintRedo.Clear();
            ConfigureLocalCamera(true);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>Paint by raycasting the capsule mesh directly (correct UVs, no physics colliders).</summary>
        public bool TryPaintFaceRay(Ray worldRay, bool erase, Color color, float brushSize01)
        {
            if (!IsOwner || facePaintStation == null || facePaintTexture == null)
            {
                return false;
            }

            if (!RaycastOwnMesh(worldRay, out Vector2 uv, out Vector3 localPoint))
            {
                return false;
            }

            if (!facePaintStrokeActive)
            {
                SyncFacePaintScratchFromTexture();
                PushFacePaintSnapshot();
                facePaintRedo.Clear();
                facePaintStrokeActive = true;
                hasFacePaintLastUv = false;
            }

            float radius = Mathf.Lerp(0.45f, 12f, Mathf.Clamp01(brushSize01));
            Color32 paint = erase ? facePaintBaseColor : (Color32)color;

            if (hasFacePaintLastUv)
            {
                StampFacePaintLine(facePaintLastUv, uv, radius, paint);
            }
            else
            {
                StampFacePaintCircle(uv, radius, paint);
            }

            facePaintLastUv = uv;
            hasFacePaintLastUv = true;
            facePaintTexture.Apply(false);
            return true;
        }

        public bool TryFillFaceRay(Ray worldRay, Color color)
        {
            if (!IsOwner || facePaintStation == null || facePaintTexture == null)
            {
                return false;
            }

            if (!RaycastOwnMesh(worldRay, out Vector2 uv, out Vector3 localPoint))
            {
                return false;
            }

            SyncFacePaintScratchFromTexture();
            PushFacePaintSnapshot();
            facePaintRedo.Clear();

            int cx = Mathf.RoundToInt(uv.x * (FacePaintResolution - 1));
            int cy = Mathf.RoundToInt(uv.y * (FacePaintResolution - 1));
            FloodFillFacePaint(cx, cy, (Color32)color);
            facePaintTexture.SetPixels32(facePaintScratch);
            facePaintTexture.Apply(false);
            facePaintStrokeActive = false;
            hasFacePaintLastUv = false;
            return true;
        }

        private void FloodFillFacePaint(int startX, int startY, Color32 fillColor)
        {
            if (facePaintScratch == null || facePaintScratch.Length != FacePaintResolution * FacePaintResolution)
            {
                facePaintScratch = facePaintTexture.GetPixels32();
            }

            int minY = 0;
            if (startX < 0 || startX >= FacePaintResolution || startY < minY || startY >= FacePaintResolution)
            {
                return;
            }

            Color32 target = facePaintScratch[startY * FacePaintResolution + startX];
            if (ColorsMatch(target, fillColor))
            {
                return;
            }

            Stack<Vector2Int> stack = new Stack<Vector2Int>(FacePaintResolution * 4);
            stack.Push(new Vector2Int(startX, startY));
            int safety = FacePaintResolution * FacePaintResolution;
            while (stack.Count > 0 && safety-- > 0)
            {
                Vector2Int p = stack.Pop();
                int x = p.x;
                int y = p.y;
                if (x < 0 || x >= FacePaintResolution || y < minY || y >= FacePaintResolution)
                {
                    continue;
                }

                int index = y * FacePaintResolution + x;
                if (!ColorsMatch(facePaintScratch[index], target))
                {
                    continue;
                }

                facePaintScratch[index] = fillColor;
                stack.Push(new Vector2Int(x + 1, y));
                stack.Push(new Vector2Int(x - 1, y));
                stack.Push(new Vector2Int(x, y + 1));
                stack.Push(new Vector2Int(x, y - 1));
            }
        }

        private static bool ColorsMatch(Color32 a, Color32 b)
        {
            const int tol = 10;
            return Mathf.Abs(a.r - b.r) <= tol &&
                   Mathf.Abs(a.g - b.g) <= tol &&
                   Mathf.Abs(a.b - b.b) <= tol;
        }

        public void UndoFacePaint()
        {
            if (!IsOwner || facePaintTexture == null || facePaintUndo.Count == 0)
            {
                return;
            }

            facePaintRedo.Push(CaptureFacePaintPixels());
            ApplyFacePaintPixels(facePaintUndo.Pop());
            facePaintStrokeActive = false;
            hasFacePaintLastUv = false;
        }

        public void RedoFacePaint()
        {
            if (!IsOwner || facePaintTexture == null || facePaintRedo.Count == 0)
            {
                return;
            }

            facePaintUndo.Push(CaptureFacePaintPixels());
            ApplyFacePaintPixels(facePaintRedo.Pop());
            facePaintStrokeActive = false;
            hasFacePaintLastUv = false;
        }

        private void EnsureFacePaintSurface()
        {
            DestroyFacePaintChild("FacePaintSurface");
            DestroyFacePaintChild("FacePaintHitProxy");

            facePaintRenderer = GetFacePaintMeshRenderer();
            facePaintMeshFilter = GetFacePaintMeshFilter();
            if (facePaintRenderer == null || facePaintMeshFilter == null || facePaintMeshFilter.sharedMesh == null)
            {
                Debug.LogWarning("MiniVanPlayer face paint needs a Head mesh with MeshRenderer+MeshFilter.");
                return;
            }

            CacheFacePaintMesh(facePaintMeshFilter.sharedMesh);

            if (facePaintTexture == null)
            {
                facePaintBaseColor = ResolveFacePaintBaseColor();
                facePaintTexture = new Texture2D(FacePaintResolution, FacePaintResolution, TextureFormat.RGBA32, false);
                facePaintTexture.name = "FacePaintRuntime";
                facePaintTexture.wrapMode = TextureWrapMode.Clamp;
                facePaintTexture.filterMode = FilterMode.Bilinear;
                ClearFacePaintTexture(facePaintBaseColor);
                facePaintCommitted = new Texture2D(FacePaintResolution, FacePaintResolution, TextureFormat.RGBA32, false);
                facePaintCommitted.SetPixels32(facePaintTexture.GetPixels32());
                facePaintCommitted.Apply(false);
            }

            if (facePaintMaterial == null)
            {
                facePaintOriginalMaterial = facePaintRenderer.sharedMaterial;
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                facePaintMaterial = new Material(shader);
                if (facePaintMaterial.HasProperty("_BaseColor"))
                {
                    facePaintMaterial.SetColor("_BaseColor", Color.white);
                }

                facePaintMaterial.color = Color.white;
                facePaintMaterial.mainTexture = facePaintTexture;
                if (facePaintMaterial.HasProperty("_BaseMap"))
                {
                    facePaintMaterial.SetTexture("_BaseMap", facePaintTexture);
                }

                facePaintRenderer.material = facePaintMaterial;
            }
            else
            {
                facePaintMaterial.mainTexture = facePaintTexture;
                if (facePaintMaterial.HasProperty("_BaseMap"))
                {
                    facePaintMaterial.SetTexture("_BaseMap", facePaintTexture);
                }

                facePaintRenderer.material = facePaintMaterial;
            }
        }

        private void CacheFacePaintMesh(Mesh mesh)
        {
            facePaintVerts = mesh.vertices;
            facePaintTris = mesh.triangles;
            facePaintMeshUvs = mesh.uv;
        }

        private bool RaycastOwnMesh(Ray worldRay, out Vector2 uv, out Vector3 localPoint)
        {
            uv = Vector2.zero;
            localPoint = Vector3.zero;
            if (facePaintVerts == null || facePaintTris == null || facePaintMeshUvs == null)
            {
                return false;
            }

            Transform meshTransform = facePaintRenderer != null ? facePaintRenderer.transform : transform;
            Vector3 origin = meshTransform.InverseTransformPoint(worldRay.origin);
            Vector3 direction = meshTransform.InverseTransformDirection(worldRay.direction).normalized;
            float bestT = float.MaxValue;
            bool hit = false;
            Vector2 bestUv = Vector2.zero;
            Vector3 bestPoint = Vector3.zero;

            for (int i = 0; i < facePaintTris.Length; i += 3)
            {
                Vector3 v0 = facePaintVerts[facePaintTris[i]];
                Vector3 v1 = facePaintVerts[facePaintTris[i + 1]];
                Vector3 v2 = facePaintVerts[facePaintTris[i + 2]];
                if (!IntersectTriangle(origin, direction, v0, v1, v2, out float t, out float u, out float v))
                {
                    continue;
                }

                if (t < 0.001f || t >= bestT)
                {
                    continue;
                }

                bestT = t;
                hit = true;
                bestPoint = origin + direction * t;
                Vector2 uv0 = facePaintMeshUvs[facePaintTris[i]];
                Vector2 uv1 = facePaintMeshUvs[facePaintTris[i + 1]];
                Vector2 uv2 = facePaintMeshUvs[facePaintTris[i + 2]];
                bestUv = uv0 * (1f - u - v) + uv1 * u + uv2 * v;
            }

            if (!hit)
            {
                return false;
            }

            uv = bestUv;
            localPoint = bestPoint;
            return true;
        }

        private static bool IntersectTriangle(
            Vector3 origin,
            Vector3 dir,
            Vector3 v0,
            Vector3 v1,
            Vector3 v2,
            out float t,
            out float u,
            out float v)
        {
            t = 0f;
            u = 0f;
            v = 0f;
            Vector3 e1 = v1 - v0;
            Vector3 e2 = v2 - v0;
            Vector3 p = Vector3.Cross(dir, e2);
            float det = Vector3.Dot(e1, p);
            if (det > -1e-6f && det < 1e-6f)
            {
                return false;
            }

            float invDet = 1f / det;
            Vector3 s = origin - v0;
            u = Vector3.Dot(s, p) * invDet;
            if (u < 0f || u > 1f)
            {
                return false;
            }

            Vector3 q = Vector3.Cross(s, e1);
            v = Vector3.Dot(dir, q) * invDet;
            if (v < 0f || u + v > 1f)
            {
                return false;
            }

            t = Vector3.Dot(e2, q) * invDet;
            return t > 1e-5f;
        }

        private Color32 ResolveFacePaintBaseColor()
        {
            Material source = facePaintRenderer != null ? facePaintRenderer.sharedMaterial : null;
            if (source == null)
            {
                return new Color32(210, 170, 140, 255);
            }

            Color c = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.color;
            return (Color32)c;
        }

        private void ClearFacePaintTexture(Color32 fill)
        {
            Color32[] pixels = new Color32[FacePaintResolution * FacePaintResolution];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = fill;
            }

            facePaintTexture.SetPixels32(pixels);
            facePaintTexture.Apply(false);
            facePaintScratch = pixels;
        }

        private void PushFacePaintSnapshot()
        {
            if (facePaintTexture == null)
            {
                return;
            }

            facePaintUndo.Push(CaptureFacePaintPixels());
            while (facePaintUndo.Count > FacePaintHistoryLimit)
            {
                Color32[][] kept = facePaintUndo.ToArray();
                facePaintUndo.Clear();
                int keep = Mathf.Min(FacePaintHistoryLimit, kept.Length);
                for (int i = keep - 1; i >= 0; i--)
                {
                    facePaintUndo.Push(kept[i]);
                }

                break;
            }
        }

        private Color32[] CaptureFacePaintPixels()
        {
            return facePaintTexture.GetPixels32();
        }

        private void ApplyFacePaintPixels(Color32[] pixels)
        {
            facePaintTexture.SetPixels32(pixels);
            facePaintTexture.Apply(false);
            facePaintScratch = (Color32[])pixels.Clone();
        }

        private void CommitFacePaint()
        {
            if (facePaintTexture == null || facePaintCommitted == null)
            {
                return;
            }

            facePaintCommitted.SetPixels32(facePaintTexture.GetPixels32());
            facePaintCommitted.Apply(false);
        }

        private void RestoreFacePaintFromCommitted()
        {
            if (facePaintTexture == null || facePaintCommitted == null)
            {
                return;
            }

            facePaintTexture.SetPixels32(facePaintCommitted.GetPixels32());
            facePaintTexture.Apply(false);
            SyncFacePaintScratchFromTexture();
        }

        private void StampFacePaintLine(Vector2 fromUv, Vector2 toUv, float radius, Color32 paint)
        {
            float dist = Vector2.Distance(fromUv, toUv);
            int steps = Mathf.Max(1, Mathf.CeilToInt(dist * FacePaintResolution / Mathf.Max(1f, radius * 0.35f)));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                StampFacePaintCircle(Vector2.Lerp(fromUv, toUv, t), radius, paint);
            }
        }

        private void StampFacePaintCircle(Vector2 uv, float radius, Color32 paint)
        {
            int cx = Mathf.RoundToInt(uv.x * (FacePaintResolution - 1));
            int cy = Mathf.RoundToInt(uv.y * (FacePaintResolution - 1));
            int r = Mathf.CeilToInt(radius);
            int r2 = r * r;

            if (facePaintScratch == null || facePaintScratch.Length != FacePaintResolution * FacePaintResolution)
            {
                facePaintScratch = facePaintTexture.GetPixels32();
            }

            for (int y = -r; y <= r; y++)
            {
                int py = cy + y;
                if (py < 0 || py >= FacePaintResolution)
                {
                    continue;
                }

                for (int x = -r; x <= r; x++)
                {
                    if (x * x + y * y > r2)
                    {
                        continue;
                    }

                    int px = cx + x;
                    if (px < 0 || px >= FacePaintResolution)
                    {
                        continue;
                    }

                    facePaintScratch[py * FacePaintResolution + px] = paint;
                }
            }

            facePaintTexture.SetPixels32(facePaintScratch);
        }

        private void SyncFacePaintScratchFromTexture()
        {
            facePaintScratch = facePaintTexture.GetPixels32();
        }

        private void DestroyFacePaintChild(string childName)
        {
            Transform child = transform.Find(childName);
            if (child == null)
            {
                return;
            }

            Object.DestroyImmediate(child.gameObject);
        }
    }
}
