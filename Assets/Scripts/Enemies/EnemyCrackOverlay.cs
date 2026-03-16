using UnityEngine;

/// <summary>
/// Renders crack textures on the enemy sprite at four health thresholds:
///   ≤ 80%  — hairline cracks
///   ≤ 60%  — visible cracks
///   ≤ 40%  — heavy cracks
///   ≤ 20%  — severe cracks + dark vignette
///
/// The overlay child SpriteRenderer is sized to exactly match the parent
/// sprite's world-space bounds so cracks cover the full sprite.
/// Crack textures have a fully transparent background — only the crack lines
/// themselves are opaque, so no clipping component is required.
///
/// Textures are generated once and cached statically across all instances.
/// Added dynamically by EnemyInstance — no prefab entry or inspector setup needed.
/// </summary>
public class EnemyCrackOverlay : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField] private int   textureSize  = 128;
    [SerializeField] private float crackAlpha   = 0.82f;
    [SerializeField] private int   orderOffset  = 1;   // sorting order above parent sprite

    // ── Static texture cache ──────────────────────────────────────────────────

    private static Sprite _spr1, _spr2, _spr3, _spr4;
    private static bool   _ready;

    // Wipe the static cache at the start of every play session so Sprites
    // destroyed when exiting Play mode are not reused the next run.
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState() { _spr1 = _spr2 = _spr3 = _spr4 = null; _ready = false; }

    // ── Per-instance ──────────────────────────────────────────────────────────

    private SpriteRenderer _overlaySr;
    private Sprite         _shown;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        EnsureTextures();

        var parentSr = GetComponent<SpriteRenderer>();

        var go = new GameObject("CrackOverlay");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;

        // Scale the overlay to exactly match the parent sprite's natural size.
        // sprite.bounds.size is in local/sprite space = world units when scale=1.
        // Because this GO is a child, localScale acts relative to parent scale,
        // so (sprite.bounds.size) correctly tracks the parent at any scale.
        if (parentSr != null && parentSr.sprite != null)
        {
            Vector2 sz = parentSr.sprite.bounds.size;
            go.transform.localScale = new Vector3(sz.x, sz.y, 1f);
        }

        _overlaySr = go.AddComponent<SpriteRenderer>();

        // Inherit the parent's sorting layer and sit one order above it.
        if (parentSr != null)
        {
            _overlaySr.sortingLayerID = parentSr.sortingLayerID;
            _overlaySr.sortingOrder   = parentSr.sortingOrder + orderOffset;
        }

        _overlaySr.enabled = false;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void OnHealthChanged(float fraction)
    {
        if (_overlaySr == null) return;

        Sprite next;
        if      (fraction > 0.80f) next = null;
        else if (fraction > 0.60f) next = _spr1;
        else if (fraction > 0.40f) next = _spr2;
        else if (fraction > 0.20f) next = _spr3;
        else                       next = _spr4;

        if (next == _shown) return;
        _shown             = next;
        _overlaySr.sprite  = next;
        _overlaySr.enabled = next != null;
    }

    // ── Texture generation ────────────────────────────────────────────────────

    void EnsureTextures()
    {
        if (_ready && _spr1 != null) return;
        _ready = true;
        _spr1  = MakeSprite(1);
        _spr2  = MakeSprite(2);
        _spr3  = MakeSprite(3);
        _spr4  = MakeSprite(4);
    }

    Sprite MakeSprite(int level)
    {
        var tex = BuildTexture(level);
        // PPU = textureSize  →  sprite natural size = 1×1 world unit before child scale
        return Sprite.Create(
            tex,
            new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f),
            textureSize);
    }

    Texture2D BuildTexture(int level)
    {
        var rng = new System.Random(level * 137 + 19);
        int sz  = textureSize;
        var px  = new Color32[sz * sz];

        int   impacts  = level == 1 ? 1 : level == 2 ? 2 : level == 3 ? 4 : 6;
        int   branches = level == 1 ? 2 : level == 2 ? 3 : level == 3 ? 4 : 5;
        float lenScale = level == 1 ? 0.18f : level == 2 ? 0.32f
                       : level == 3 ? 0.48f : 0.65f;

        // Level 4: circular dark vignette fading to transparent at the edges.
        // A hard rectangle would bleed outside the sprite; the circular gradient
        // stays roughly within the sprite silhouette without a SpriteMask.
        if (level == 4)
        {
            float cx = sz * 0.5f, cy = sz * 0.5f, r = sz * 0.44f;
            for (int y = 0; y < sz; y++)
            for (int x = 0; x < sz; x++)
            {
                float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                byte  v    = (byte)(Mathf.Clamp01(1f - dist / r) * 55f);
                if (v > 0) px[y * sz + x] = new Color32(0, 0, 0, v);
            }
        }

        for (int imp = 0; imp < impacts; imp++)
        {
            int cx = sz / 2 + (int)(rng.NextDouble() * sz * 0.5 - sz * 0.25);
            int cy = sz / 2 + (int)(rng.NextDouble() * sz * 0.5 - sz * 0.25);

            PaintDot(px, sz, cx, cy, 3, crackAlpha);

            for (int b = 0; b < branches; b++)
            {
                float angle = (float)(rng.NextDouble() * System.Math.PI * 2.0);
                float len   = (float)(sz * (lenScale * 0.6 + rng.NextDouble() * lenScale));
                DrawCrack(px, sz, cx, cy, angle, len, 3, crackAlpha, rng);
            }
        }

        // Circular crop — zero out pixels outside the inscribed circle and
        // softly fade the outer 15% so the edge isn't a hard ring.
        float cxM = sz * 0.5f, cyM = sz * 0.5f;
        float rOuter = sz * 0.38f;
        float rInner = rOuter * 0.85f;
        for (int y = 0; y < sz; y++)
        for (int x = 0; x < sz; x++)
        {
            float dist = Mathf.Sqrt((x - cxM) * (x - cxM) + (y - cyM) * (y - cyM));
            if (dist >= rOuter)
            {
                px[y * sz + x] = new Color32(0, 0, 0, 0);
            }
            else if (dist > rInner)
            {
                float t = 1f - (dist - rInner) / (rOuter - rInner);
                var   p = px[y * sz + x];
                p.a = (byte)(p.a * t);
                px[y * sz + x] = p;
            }
        }

        var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;
        tex.SetPixels32(px);
        tex.Apply(false);
        return tex;
    }

    // ── Drawing ───────────────────────────────────────────────────────────────

    static void DrawCrack(Color32[] px, int sz,
                          int startX, int startY,
                          float angle, float length,
                          int depth, float alpha,
                          System.Random rng)
    {
        if (depth <= 0 || length < 3f) return;

        float cx = startX, cy = startY, step = 1.2f;
        byte  a  = (byte)Mathf.Clamp(alpha * 255f, 0, 255);
        byte  ab = (byte)(a * 0.45f);

        for (float d = 0f; d < length; d += step)
        {
            angle += (float)(rng.NextDouble() * 0.22 - 0.11);
            cx    += Mathf.Cos(angle) * step;
            cy    += Mathf.Sin(angle) * step;

            int xi = Mathf.RoundToInt(cx);
            int yi = Mathf.RoundToInt(cy);
            if (xi < 1 || xi >= sz - 1 || yi < 1 || yi >= sz - 1) break;

            WritePixel(px, sz, xi,     yi,     new Color32(4, 2, 0, a));
            WritePixel(px, sz, xi + 1, yi,     new Color32(8, 4, 0, ab));
            WritePixel(px, sz, xi - 1, yi,     new Color32(8, 4, 0, ab));
            WritePixel(px, sz, xi,     yi + 1, new Color32(8, 4, 0, ab));
            WritePixel(px, sz, xi,     yi - 1, new Color32(8, 4, 0, ab));

            if (rng.NextDouble() < 0.055 && depth > 1)
            {
                float ba = angle + (float)(rng.NextDouble() * 1.4 - 0.7);
                DrawCrack(px, sz, xi, yi, ba, length * 0.46f,
                          depth - 1, alpha * 0.72f, rng);
            }
        }
    }

    static void PaintDot(Color32[] px, int sz, int cx, int cy, int r, float alpha)
    {
        byte a = (byte)(alpha * 255f);
        for (int dx = -r; dx <= r; dx++)
        for (int dy = -r; dy <= r; dy++)
            if (dx * dx + dy * dy <= r * r)
                WritePixel(px, sz, cx + dx, cy + dy, new Color32(4, 2, 0, a));
    }

    static void WritePixel(Color32[] px, int sz, int x, int y, Color32 col)
    {
        if ((uint)x >= (uint)sz || (uint)y >= (uint)sz) return;
        int i = y * sz + x;
        if (col.a > px[i].a) px[i] = col;
    }
}
