using System;
using System.Collections;
using System.Collections.Generic;
using BeatFramer.Configuration;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace BeatFramer.Rendering
{
    /// <summary>
    /// Zero-allocation dynamic mesh batcher. Combines all geometry sharing a blend mode
    /// (opaque, translucent, or additive) into a single GPU draw call with per-vertex colors.
    /// Works on every Unity shader (including Hidden/Internal-Colored) in Single Pass Instanced VR.
    /// </summary>
    internal sealed class DynamicBatchMesh
    {
        public readonly Mesh Mesh;
        private readonly List<Vector3> _verts = new List<Vector3>(4096);
        private readonly List<Color32> _colors = new List<Color32>(4096);
        private readonly List<int> _tris = new List<int>(6144);
        private static readonly Bounds LargeBounds = new Bounds(Vector3.zero, new Vector3(400f, 400f, 400f));

        public int VertexCount => _verts.Count;

        public DynamicBatchMesh(string name)
        {
            Mesh = new Mesh { name = name };
            Mesh.MarkDynamic();
        }

        public void Clear()
        {
            _verts.Clear();
            _colors.Clear();
            _tris.Clear();
        }

        public void AddShadedBox(Matrix4x4 m, float hx, float hy, float hz, Color32 baseColor, bool shadeFaces)
        {
            // 8 corners transformed once
            Vector3 c000 = m.MultiplyPoint3x4(new Vector3(-hx, -hy, -hz));
            Vector3 c010 = m.MultiplyPoint3x4(new Vector3(-hx,  hy, -hz));
            Vector3 c110 = m.MultiplyPoint3x4(new Vector3( hx,  hy, -hz));
            Vector3 c100 = m.MultiplyPoint3x4(new Vector3( hx, -hy, -hz));
            Vector3 c001 = m.MultiplyPoint3x4(new Vector3(-hx, -hy,  hz));
            Vector3 c011 = m.MultiplyPoint3x4(new Vector3(-hx,  hy,  hz));
            Vector3 c111 = m.MultiplyPoint3x4(new Vector3( hx,  hy,  hz));
            Vector3 c101 = m.MultiplyPoint3x4(new Vector3( hx, -hy,  hz));

            Color32 colFront  = shadeFaces ? ScaleColor(baseColor, 1.00f) : baseColor;
            Color32 colBack   = shadeFaces ? ScaleColor(baseColor, 0.50f) : baseColor;
            Color32 colTop    = shadeFaces ? ScaleColor(baseColor, 0.85f) : baseColor;
            Color32 colBottom = shadeFaces ? ScaleColor(baseColor, 0.42f) : baseColor;
            Color32 colSide   = shadeFaces ? ScaleColor(baseColor, 0.66f) : baseColor;

            // Front (-Z, facing player)
            AddQuad(c000, c010, c110, c100, colFront);
            // Back (+Z)
            AddQuad(c101, c111, c011, c001, colBack);
            // Top (+Y)
            AddQuad(c010, c011, c111, c110, colTop);
            // Bottom (-Y)
            AddQuad(c001, c000, c100, c101, colBottom);
            // Left (-X)
            AddQuad(c001, c011, c010, c000, colSide);
            // Right (+X)
            AddQuad(c100, c110, c111, c101, colSide);
        }

        public void AddBoxWorld(Vector3 center, Quaternion rot, Vector3 size, Color32 color, bool shadeFaces)
        {
            var m = Matrix4x4.TRS(center, rot, Vector3.one);
            AddShadedBox(m, size.x * 0.5f, size.y * 0.5f, size.z * 0.5f, color, shadeFaces);
        }

        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 col)
        {
            int idx = _verts.Count;
            _verts.Add(a); _verts.Add(b); _verts.Add(c); _verts.Add(d);
            _colors.Add(col); _colors.Add(col); _colors.Add(col); _colors.Add(col);
            _tris.Add(idx); _tris.Add(idx + 1); _tris.Add(idx + 2);
            _tris.Add(idx); _tris.Add(idx + 2); _tris.Add(idx + 3);
        }

        public void AddTri(Vector3 a, Vector3 b, Vector3 c, Color32 col)
        {
            int idx = _verts.Count;
            _verts.Add(a); _verts.Add(b); _verts.Add(c);
            _colors.Add(col); _colors.Add(col); _colors.Add(col);
            _tris.Add(idx); _tris.Add(idx + 1); _tris.Add(idx + 2);
        }

        public void AddArrow(Matrix4x4 m, Color32 col)
        {
            // Front face is -Z; Beat Saber noteTransform rotates local -Y toward cut direction
            const float z = -0.242f;
            Vector3 tip   = m.MultiplyPoint3x4(new Vector3( 0.00f, -0.155f, z));
            Vector3 left  = m.MultiplyPoint3x4(new Vector3(-0.155f,  0.075f, z));
            Vector3 right = m.MultiplyPoint3x4(new Vector3( 0.155f,  0.075f, z));
            AddTri(tip, left, right, col);
        }

        public void AddDot(Matrix4x4 m, float radius, float z, Color32 col)
        {
            // Crisp 8-sided circle on front face (-Z)
            Vector3 center = m.MultiplyPoint3x4(new Vector3(0f, 0f, z));
            int centerIdx = _verts.Count;
            _verts.Add(center);
            _colors.Add(col);

            const int segs = 8;
            for (int i = 0; i < segs; i++)
            {
                float ang = i * (Mathf.PI * 2f / segs);
                Vector3 p = m.MultiplyPoint3x4(new Vector3(Mathf.Cos(ang) * radius, Mathf.Sin(ang) * radius, z));
                _verts.Add(p);
                _colors.Add(col);
            }
            for (int i = 0; i < segs; i++)
            {
                int next = (i + 1) % segs;
                // Clockwise viewed from -Z
                _tris.Add(centerIdx);
                _tris.Add(centerIdx + 1 + next);
                _tris.Add(centerIdx + 1 + i);
            }
        }

        public void AddOctaBomb(Matrix4x4 m, float r, Color32 baseCol)
        {
            Vector3 top = m.MultiplyPoint3x4(new Vector3(0f,  r, 0f));
            Vector3 bot = m.MultiplyPoint3x4(new Vector3(0f, -r, 0f));
            Vector3 lft = m.MultiplyPoint3x4(new Vector3(-r, 0f, 0f));
            Vector3 rgt = m.MultiplyPoint3x4(new Vector3( r, 0f, 0f));
            Vector3 frt = m.MultiplyPoint3x4(new Vector3(0f, 0f, -r));
            Vector3 bck = m.MultiplyPoint3x4(new Vector3(0f, 0f,  r));

            Color32 hi = ScaleColor(baseCol, 1.0f);
            Color32 mid = ScaleColor(baseCol, 0.72f);
            Color32 lo = ScaleColor(baseCol, 0.45f);

            AddTri(top, frt, rgt, hi);
            AddTri(top, rgt, bck, mid);
            AddTri(top, bck, lft, mid);
            AddTri(top, lft, frt, hi);

            AddTri(bot, rgt, frt, lo);
            AddTri(bot, bck, rgt, lo);
            AddTri(bot, lft, bck, lo);
            AddTri(bot, frt, lft, lo);
        }

        public void AddRingFrame(Vector3 center, float angleDeg, float size, float thickness, Color32 col)
        {
            var m = Matrix4x4.TRS(center, Quaternion.Euler(0f, 0f, angleDeg), Vector3.one);
            float h = size * 0.5f;
            float t = thickness;

            void Bar(float cx, float cy, float ex, float ey)
            {
                Vector3 a = m.MultiplyPoint3x4(new Vector3(cx - ex, cy - ey, 0f));
                Vector3 b = m.MultiplyPoint3x4(new Vector3(cx - ex, cy + ey, 0f));
                Vector3 c = m.MultiplyPoint3x4(new Vector3(cx + ex, cy + ey, 0f));
                Vector3 d = m.MultiplyPoint3x4(new Vector3(cx + ex, cy - ey, 0f));
                AddQuad(a, b, c, d, col);
            }

            Bar(0f,  h, h + t, t);
            Bar(0f, -h, h + t, t);
            Bar(-h, 0f, t, h);
            Bar( h, 0f, t, h);
        }

        public void UploadAndDraw(Material mat)
        {
            if (_verts.Count == 0 || mat == null) return;
            Mesh.Clear(false);
            Mesh.SetVertices(_verts);
            Mesh.SetColors(_colors);
            Mesh.SetTriangles(_tris, 0, false);
            Mesh.bounds = LargeBounds;
            Graphics.DrawMesh(Mesh, Matrix4x4.identity, mat, 0, null, 0, null, false, false, false);
        }

        public void Dispose()
        {
            if (Mesh != null) UnityEngine.Object.Destroy(Mesh);
        }

        private static Color32 ScaleColor(Color32 c, float s)
        {
            return new Color32(
                (byte)Mathf.Clamp((int)(c.r * s), 0, 255),
                (byte)Mathf.Clamp((int)(c.g * s), 0, 255),
                (byte)Mathf.Clamp((int)(c.b * s), 0, 255),
                c.a);
        }
    }

    /// <summary>
    /// 100% mod/API/interface-compatible custom lightweight gameplay renderer.
    /// Keeps all Unity GameObjects, hitboxes, audio, Zenject/SiraUtil/BSML/BeatTogether mods,
    /// and HUD/Canvas UI intact, while replacing stock gameplay & environment rendering with
    /// 3 single-pass dynamic batched draw calls (opaque, translucent walls, additive lights/trails).
    /// </summary>
    internal class LiteRendererController : MonoBehaviour
    {
        private const int MaxLightIds = 32;
        private static readonly Color[] _lightColors = new Color[MaxLightIds];
        private static bool _hasAnyLightEvent;

        private bool _inGameplay;
        private bool _active;
        private bool _loggedFirstNote;

        private BeatmapObjectManager _bom;
        private SaberManager _saberManager;
        private ColorManager _colorManager;

        private readonly List<NoteController> _activeNotes = new List<NoteController>(64);
        private readonly List<ObstacleController> _activeObstacles = new List<ObstacleController>(32);
        private readonly List<Renderer> _suppressedRenderers = new List<Renderer>(512);

        private Material _opaqueMat;
        private Material _additiveMat;
        private Material _wallMat;

        private DynamicBatchMesh _opaqueBatch;
        private DynamicBatchMesh _wallBatch;
        private DynamicBatchMesh _additiveBatch;

        // Saber ribbon trail history (8 frames per saber)
        private const int TrailSamples = 8;
        private readonly Vector3[] _leftTopHist = new Vector3[TrailSamples];
        private readonly Vector3[] _leftBotHist = new Vector3[TrailSamples];
        private readonly Vector3[] _rightTopHist = new Vector3[TrailSamples];
        private readonly Vector3[] _rightBotHist = new Vector3[TrailSamples];
        private int _trailCount;

        private Color32 _leftColor = new Color32(235, 35, 45, 255);
        private Color32 _rightColor = new Color32(30, 135, 250, 255);
        private Color32 _wallColor = new Color32(235, 40, 50, 72);
        private Color32 _wallEdgeColor = new Color32(255, 90, 100, 150);

        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 BombCol = new Color32(68, 72, 82, 255);
        private static readonly Color32 HandleCol = new Color32(38, 42, 48, 255);
        private static readonly Color32 RunwayCol = new Color32(12, 14, 20, 255);

        public static void OnLightColorChanged(int lightId, Color color)
        {
            if (lightId >= 0 && lightId < MaxLightIds)
            {
                _lightColors[lightId] = color;
                _hasAnyLightEvent = true;
            }
        }

        private void Awake()
        {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            InitDefaultLights();
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            TeardownGameplay();
            _opaqueBatch?.Dispose();
            _wallBatch?.Dispose();
            _additiveBatch?.Dispose();
        }

        private static void InitDefaultLights()
        {
            for (int i = 0; i < MaxLightIds; i++)
            {
                _lightColors[i] = new Color(0.15f, 0.45f, 0.95f, 0.35f);
            }
            _lightColors[1] = new Color(0.10f, 0.45f, 0.95f, 0.45f); // Back lasers
            _lightColors[2] = new Color(0.20f, 0.65f, 1.00f, 0.40f); // Rings
            _lightColors[3] = new Color(0.95f, 0.15f, 0.30f, 0.40f); // Left lasers
            _lightColors[4] = new Color(0.15f, 0.55f, 1.00f, 0.40f); // Right lasers
            _lightColors[5] = new Color(0.30f, 0.70f, 1.00f, 0.45f); // Road/center lights
            _hasAnyLightEvent = false;
        }

        private void EnsureResources()
        {
            if (_opaqueMat != null) return;

            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Plugin.Log?.Error("[LiteRenderer] Could not find fallback shader!");
                return;
            }

            _opaqueMat = new Material(shader) { name = "BF_Opaque" };
            _opaqueMat.SetInt("_SrcBlend", (int)BlendMode.One);
            _opaqueMat.SetInt("_DstBlend", (int)BlendMode.Zero);
            _opaqueMat.SetInt("_Cull", (int)CullMode.Back);
            _opaqueMat.SetInt("_ZWrite", 1);

            _wallMat = new Material(shader) { name = "BF_Wall" };
            _wallMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            _wallMat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            _wallMat.SetInt("_Cull", (int)CullMode.Off);
            _wallMat.SetInt("_ZWrite", 0);

            _additiveMat = new Material(shader) { name = "BF_Additive" };
            _additiveMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            _additiveMat.SetInt("_DstBlend", (int)BlendMode.One);
            _additiveMat.SetInt("_Cull", (int)CullMode.Off);
            _additiveMat.SetInt("_ZWrite", 0);

            _opaqueBatch = new DynamicBatchMesh("BF_OpaqueBatch");
            _wallBatch = new DynamicBatchMesh("BF_WallBatch");
            _additiveBatch = new DynamicBatchMesh("BF_AdditiveBatch");

            Plugin.Log?.Info($"[LiteRenderer] Initialized 3-draw-call batcher with shader '{shader.name}'");
        }

        private void OnActiveSceneChanged(Scene prev, Scene next)
        {
            TeardownGameplay();
            bool isGameplay = next.name == "GameCore" || next.name.Contains("Gameplay");
            _inGameplay = isGameplay;

            if (isGameplay && PluginConfig.Instance != null && PluginConfig.Instance.IsLiteRenderer)
            {
                StartCoroutine(AttachToGameplayDeferred());
            }
        }

        private IEnumerator AttachToGameplayDeferred()
        {
            yield return null;
            yield return null;

            if (!_inGameplay || PluginConfig.Instance == null || !PluginConfig.Instance.IsLiteRenderer)
            {
                yield break;
            }

            EnsureResources();
            if (_opaqueMat == null) yield break;

            InitDefaultLights();

            _saberManager = FindFirstObjectByType<SaberManager>();
            _bom = FindBeatmapObjectManager();
            _colorManager = FindColorManager();

            if (_colorManager != null)
            {
                _leftColor = ToOpaqueColor32(_colorManager.ColorForType(ColorType.ColorA));
                _rightColor = ToOpaqueColor32(_colorManager.ColorForType(ColorType.ColorB));
                Color wc = _colorManager.obstaclesColor;
                _wallColor = new Color32(
                    (byte)Mathf.Clamp((int)(wc.r * 255f), 0, 255),
                    (byte)Mathf.Clamp((int)(wc.g * 255f), 0, 255),
                    (byte)Mathf.Clamp((int)(wc.b * 255f), 0, 255),
                    72);
                _wallEdgeColor = new Color32(
                    (byte)Mathf.Clamp((int)(wc.r * 255f), 80, 255),
                    (byte)Mathf.Clamp((int)(wc.g * 255f), 80, 255),
                    (byte)Mathf.Clamp((int)(wc.b * 255f), 80, 255),
                    155);
            }

            if (_bom != null)
            {
                _bom.noteWasSpawnedEvent += HandleNoteSpawned;
                _bom.noteWasDespawnedEvent += HandleNoteDespawned;
                _bom.obstacleWasSpawnedEvent += HandleObstacleSpawned;
                _bom.obstacleWasDespawnedEvent += HandleObstacleDespawned;
            }

            SuppressStockSceneRenderers();
            _trailCount = 0;
            _loggedFirstNote = false;
            _active = true;

            Plugin.Log?.Info($"[LiteRenderer] Activated in gameplay: BOM={_bom != null}, Sabers={_saberManager != null}, SuppressedRenderers={_suppressedRenderers.Count}");
        }

        private BeatmapObjectManager FindBeatmapObjectManager()
        {
            var spawner = FindFirstObjectByType<NoteCutCoreEffectsSpawner>();
            if (spawner != null)
            {
                var field = HarmonyLib.AccessTools.Field(typeof(NoteCutCoreEffectsSpawner), "_beatmapObjectManager");
                if (field != null) return field.GetValue(spawner) as BeatmapObjectManager;
            }
            return null;
        }

        private ColorManager FindColorManager()
        {
            var spawner = FindFirstObjectByType<NoteCutCoreEffectsSpawner>();
            if (spawner != null)
            {
                var field = HarmonyLib.AccessTools.Field(typeof(NoteCutCoreEffectsSpawner), "_colorManager");
                if (field != null) return field.GetValue(spawner) as ColorManager;
            }
            return null;
        }

        private void SuppressStockSceneRenderers()
        {
            _suppressedRenderers.Clear();
            var allRenderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            for (int i = 0; i < allRenderers.Length; i++)
            {
                var r = allRenderers[i];
                if (r == null || !r.enabled) continue;
                if (IsProtectedRenderer(r)) continue;
                r.enabled = false;
                _suppressedRenderers.Add(r);
            }
        }

        private static bool IsProtectedRenderer(Renderer r)
        {
            // Keep UI, HUD, score/combo, BSML panels, TextMeshPro, and Arc sliders visible
            if (r.gameObject.layer == 5) return true;
            if (r.GetComponentInParent<Canvas>() != null) return true;
            if (r.GetComponentInParent<SliderController>() != null) return true;
            string n = r.gameObject.name;
            if (n.IndexOf("Text", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("HUD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("UI", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Pause", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
            return false;
        }

        private static void SuppressHierarchyRenderers(GameObject go)
        {
            if (go == null) return;
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null) renderers[i].enabled = false;
            }
        }

        private void HandleNoteSpawned(NoteController note)
        {
            if (note == null) return;
            SuppressHierarchyRenderers(note.gameObject);
            if (!_activeNotes.Contains(note)) _activeNotes.Add(note);
        }

        private void HandleNoteDespawned(NoteController note)
        {
            _activeNotes.Remove(note);
        }

        private void HandleObstacleSpawned(ObstacleController obs)
        {
            if (obs == null) return;
            SuppressHierarchyRenderers(obs.gameObject);
            if (!_activeObstacles.Contains(obs)) _activeObstacles.Add(obs);
        }

        private void HandleObstacleDespawned(ObstacleController obs)
        {
            _activeObstacles.Remove(obs);
        }

        private void TeardownGameplay()
        {
            _active = false;
            if (_bom != null)
            {
                _bom.noteWasSpawnedEvent -= HandleNoteSpawned;
                _bom.noteWasDespawnedEvent -= HandleNoteDespawned;
                _bom.obstacleWasSpawnedEvent -= HandleObstacleSpawned;
                _bom.obstacleWasDespawnedEvent -= HandleObstacleDespawned;
                _bom = null;
            }
            _activeNotes.Clear();
            _activeObstacles.Clear();
            for (int i = 0; i < _suppressedRenderers.Count; i++)
            {
                if (_suppressedRenderers[i] != null) _suppressedRenderers[i].enabled = true;
            }
            _suppressedRenderers.Clear();
            _saberManager = null;
            _colorManager = null;
        }

        private void LateUpdate()
        {
            if (!_active || _opaqueMat == null) return;

            var cfg = PluginConfig.Instance;
            if (cfg == null || !cfg.IsLiteRenderer) return;

            _opaqueBatch.Clear();
            _wallBatch.Clear();
            _additiveBatch.Clear();

            if (cfg.LiteEnvironmentLights)
            {
                BuildLiteEnvironment(cfg.LiteAmbientGlow);
            }

            BuildNotesAndBombs();
            BuildObstacles();
            BuildSabers(cfg.LiteSaberTrails);

            // Entire gameplay world in 3 draw calls!
            _opaqueBatch.UploadAndDraw(_opaqueMat);
            _wallBatch.UploadAndDraw(_wallMat);
            _additiveBatch.UploadAndDraw(_additiveMat);
        }

        private void BuildNotesAndBombs()
        {
            for (int i = _activeNotes.Count - 1; i >= 0; i--)
            {
                var n = _activeNotes[i];
                if (n == null || !n.gameObject.activeInHierarchy)
                {
                    _activeNotes.RemoveAt(i);
                    continue;
                }
                if (n.hidden) continue;

                var tr = n.noteTransform;
                if (tr == null) continue;
                var data = n.noteData;
                if (data == null) continue;

                var m = tr.localToWorldMatrix;

                if (!_loggedFirstNote)
                {
                    _loggedFirstNote = true;
                    Plugin.Log?.Info($"[LiteRenderer] Drawing first note: type={data.gameplayType}, color={data.colorType}, pos={tr.position}");
                }

                if (data.gameplayType == NoteData.GameplayType.Bomb || data.colorType == ColorType.None)
                {
                    _opaqueBatch.AddOctaBomb(m, 0.19f, BombCol);
                    continue;
                }

                Color32 col = data.colorType == ColorType.ColorA ? _leftColor : _rightColor;

                if (data.gameplayType == NoteData.GameplayType.BurstSliderElement)
                {
                    // Chain link: thin horizontal slice with bright white dot
                    _opaqueBatch.AddShadedBox(m, 0.23f, 0.065f, 0.23f, col, shadeFaces: true);
                    _opaqueBatch.AddDot(m, 0.05f, -0.236f, White);
                }
                else
                {
                    // Standard note cube (or chain head)
                    _opaqueBatch.AddShadedBox(m, 0.23f, 0.23f, 0.23f, col, shadeFaces: true);
                    if (data.cutDirection == NoteCutDirection.Any)
                        _opaqueBatch.AddDot(m, 0.095f, -0.242f, White);
                    else
                        _opaqueBatch.AddArrow(m, White);
                }
            }
        }

        private void BuildObstacles()
        {
            for (int i = _activeObstacles.Count - 1; i >= 0; i--)
            {
                var o = _activeObstacles[i];
                if (o == null || !o.gameObject.activeInHierarchy)
                {
                    _activeObstacles.RemoveAt(i);
                    continue;
                }
                var b = o.bounds;
                var tr = o.transform;
                Vector3 center = tr.TransformPoint(b.center);
                float w = Mathf.Max(0.05f, o.width);
                float h = Mathf.Max(0.05f, o.height);
                float l = Mathf.Max(0.05f, o.length);
                var m = Matrix4x4.TRS(center, tr.rotation, Vector3.one);

                // Translucent wall body
                _wallBatch.AddShadedBox(m, w * 0.5f, h * 0.5f, l * 0.5f, _wallColor, shadeFaces: false);

                // Crisp front-face glowing frame so wall edges are easy to read when dodging
                float hx = w * 0.5f, hy = h * 0.5f, hz = l * 0.5f, e = 0.03f;
                Vector3 f0 = m.MultiplyPoint3x4(new Vector3(-hx, -hy, -hz));
                Vector3 f1 = m.MultiplyPoint3x4(new Vector3(-hx,  hy, -hz));
                Vector3 f2 = m.MultiplyPoint3x4(new Vector3( hx,  hy, -hz));
                Vector3 f3 = m.MultiplyPoint3x4(new Vector3( hx, -hy, -hz));
                Vector3 i0 = m.MultiplyPoint3x4(new Vector3(-hx + e, -hy + e, -hz));
                Vector3 i1 = m.MultiplyPoint3x4(new Vector3(-hx + e,  hy - e, -hz));
                Vector3 i2 = m.MultiplyPoint3x4(new Vector3( hx - e,  hy - e, -hz));
                Vector3 i3 = m.MultiplyPoint3x4(new Vector3( hx - e, -hy + e, -hz));
                _additiveBatch.AddQuad(f0, f1, i1, i0, _wallEdgeColor);
                _additiveBatch.AddQuad(f1, f2, i2, i1, _wallEdgeColor);
                _additiveBatch.AddQuad(f2, f3, i3, i2, _wallEdgeColor);
                _additiveBatch.AddQuad(f3, f0, i0, i3, _wallEdgeColor);
            }
        }

        private void BuildSabers(bool drawTrails)
        {
            if (_saberManager == null) return;
            BuildSingleSaber(_saberManager.leftSaber, _leftColor, _leftTopHist, _leftBotHist, drawTrails);
            BuildSingleSaber(_saberManager.rightSaber, _rightColor, _rightTopHist, _rightBotHist, drawTrails);
            if (_trailCount < TrailSamples) _trailCount++;
        }

        private void BuildSingleSaber(Saber saber, Color32 color, Vector3[] topHist, Vector3[] botHist, bool drawTrails)
        {
            if (saber == null || !saber.gameObject.activeInHierarchy) return;

            Vector3 bot = saber.saberBladeBottomPos;
            Vector3 top = saber.saberBladeTopPos;
            Vector3 dir = top - bot;
            float len = dir.magnitude;
            if (len < 0.01f) return;

            Vector3 fwd = dir / len;
            Quaternion rot = Quaternion.LookRotation(fwd);
            Vector3 mid = (bot + top) * 0.5f;

            // Colored outer blade + bright white core + dark handle
            _opaqueBatch.AddBoxWorld(mid, rot, new Vector3(0.034f, 0.034f, len), color, shadeFaces: false);
            _opaqueBatch.AddBoxWorld(mid, rot, new Vector3(0.018f, 0.018f, len * 0.99f), White, shadeFaces: false);
            _opaqueBatch.AddBoxWorld(bot - fwd * 0.08f, rot, new Vector3(0.030f, 0.030f, 0.16f), HandleCol, shadeFaces: true);

            if (drawTrails)
            {
                for (int i = TrailSamples - 1; i > 0; i--)
                {
                    topHist[i] = topHist[i - 1];
                    botHist[i] = botHist[i - 1];
                }
                topHist[0] = top;
                botHist[0] = bot;

                int segs = Mathf.Min(_trailCount - 1, TrailSamples - 1);
                for (int i = 0; i < segs; i++)
                {
                    byte alpha = (byte)(95 * (1f - (float)i / (segs + 1)));
                    var tc = new Color32(color.r, color.g, color.b, alpha);
                    _additiveBatch.AddQuad(botHist[i], topHist[i], topHist[i + 1], botHist[i + 1], tc);
                }
            }
        }

        private void BuildLiteEnvironment(float glowScale)
        {
            Color32 backLight  = ScaleLight(_lightColors[1], glowScale);
            Color32 ringLight  = ScaleLight(_lightColors[2], glowScale);
            Color32 leftLight  = ScaleLight(_lightColors[3], glowScale);
            Color32 rightLight = ScaleLight(_lightColors[4], glowScale);
            Color32 roadLight  = ScaleLight(_lightColors[5], glowScale);

            // 1. Dark runway platform
            _opaqueBatch.AddBoxWorld(new Vector3(0f, -0.05f, 18f), Quaternion.identity, new Vector3(3.1f, 0.08f, 42f), RunwayCol, shadeFaces: false);

            // 2. Glowing neon lane side rails (driven by road/center light)
            if (roadLight.a > 2)
            {
                _additiveBatch.AddBoxWorld(new Vector3(-1.58f, 0.01f, 18f), Quaternion.identity, new Vector3(0.06f, 0.04f, 42f), roadLight, shadeFaces: false);
                _additiveBatch.AddBoxWorld(new Vector3( 1.58f, 0.01f, 18f), Quaternion.identity, new Vector3(0.06f, 0.04f, 42f), roadLight, shadeFaces: false);
            }

            // 3. Concentric glowing tunnel rings (driven by ring light)
            if (ringLight.a > 2)
            {
                float rotBase = Time.time * 4f;
                for (int i = 0; i < 6; i++)
                {
                    float z = 14f + i * 9.5f;
                    _additiveBatch.AddRingFrame(new Vector3(0f, 1.8f, z), 45f + rotBase + i * 8f, 9.5f, 0.14f, ringLight);
                }
            }

            // 4. Left & Right diagonal laser beams
            if (leftLight.a > 2)
            {
                for (int i = 0; i < 3; i++)
                {
                    float z = 20f + i * 14f;
                    _additiveBatch.AddBoxWorld(new Vector3(-7.5f, 4.5f, z), Quaternion.Euler(0f, 0f, -24f - i * 6f), new Vector3(0.14f, 22f, 0.14f), leftLight, shadeFaces: false);
                }
            }
            if (rightLight.a > 2)
            {
                for (int i = 0; i < 3; i++)
                {
                    float z = 20f + i * 14f;
                    _additiveBatch.AddBoxWorld(new Vector3(7.5f, 4.5f, z), Quaternion.Euler(0f, 0f, 24f + i * 6f), new Vector3(0.14f, 22f, 0.14f), rightLight, shadeFaces: false);
                }
            }

            // 5. Back fan lasers
            if (backLight.a > 2)
            {
                for (int i = -2; i <= 2; i++)
                {
                    _additiveBatch.AddBoxWorld(new Vector3(i * 2.8f, 3.5f, 68f), Quaternion.Euler(0f, 0f, i * 12f), new Vector3(0.18f, 28f, 0.18f), backLight, shadeFaces: false);
                }
            }
        }

        private static Color32 ScaleLight(Color c, float glowScale)
        {
            float intensity = Mathf.Clamp01(c.a) * glowScale;
            if (!_hasAnyLightEvent) intensity = Mathf.Max(intensity, 0.30f * glowScale);
            return new Color32(
                (byte)Mathf.Clamp((int)(c.r * 255f), 0, 255),
                (byte)Mathf.Clamp((int)(c.g * 255f), 0, 255),
                (byte)Mathf.Clamp((int)(c.b * 255f), 0, 255),
                (byte)Mathf.Clamp((int)(intensity * 255f), 0, 255));
        }

        private static Color32 ToOpaqueColor32(Color c)
        {
            return new Color32(
                (byte)Mathf.Clamp((int)(c.r * 255f), 0, 255),
                (byte)Mathf.Clamp((int)(c.g * 255f), 0, 255),
                (byte)Mathf.Clamp((int)(c.b * 255f), 0, 255),
                255);
        }
    }
}
