using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Displays a row of animated yellow arrows along the enemy path during the
/// very first pre-wave intermission to show the player where enemies will travel.
///
/// ── Scene setup ──
///   Add this component to any GameObject in the game scene (e.g. WaypointManager).
///   It reads waypoints from WaypointManager.Instance automatically.
///   It destroys itself (with a fade) the moment Wave 1 begins.
///
/// ── How the animation works ──
///   Arrows are placed at equal intervals along every path segment and given a
///   normalised 0-1 "path offset". Each frame each arrow's alpha is driven by
///   sin(Time * pulseSpeed – pathOffset * 2π), creating a continuous wave of
///   brightness that appears to travel from the spawn point toward the pond.
/// </summary>
public class PathArrowHighlight : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────────────────

    [Tooltip("World-unit gap between consecutive arrows along the path.")]
    [SerializeField] private float arrowSpacing  = 0.7f;

    [Tooltip("World-unit size of each arrow sprite.")]
    [SerializeField] private float arrowSize     = 0.30f;

    [Tooltip("Base yellow arrow colour. Alpha is overridden by the pulse animation.")]
    [SerializeField] private Color arrowColor    = new Color(1f, 0.88f, 0.1f, 1f);

    [Tooltip("How fast the brightness wave travels along the path (cycles per second).")]
    [SerializeField] private float pulseSpeed    = 2.2f;

    [Tooltip("How much alpha varies (0 = no variation, 1 = fully blinks).")]
    [SerializeField] private float pulseDepth    = 0.65f;

    [Tooltip("Seconds to fade out when Wave 1 begins.")]
    [SerializeField] private float fadeOutTime   = 0.6f;

    [Tooltip("Sorting order used by the arrow sprites.")]
    [SerializeField] private int   sortingOrder  = -1;

    // ── Static sprite cache ────────────────────────────────────────────────────

    private static Sprite _arrowSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCache() => _arrowSprite = null;

    // ── Per-instance ───────────────────────────────────────────────────────────

    private readonly List<SpriteRenderer> _arrows      = new();
    private          float[]              _pathOffsets;   // 0-1 normalised position along path
    private          bool                 _fadingOut;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    void Start()
    {
        // Only show during the very first intermission (before any wave has run).
        if (WaveManager.Instance != null && WaveManager.Instance.CurrentWaveIndex >= 0)
        {
            Destroy(gameObject);
            return;
        }

        EnsureSprite();
        BuildArrows();

        WaveManager.WaveStarted += OnWaveStarted;
    }

    void OnDestroy()
    {
        WaveManager.WaveStarted -= OnWaveStarted;
    }

    void Update()
    {
        if (_fadingOut || _arrows.Count == 0) return;

        float t = Time.time * pulseSpeed;
        for (int i = 0; i < _arrows.Count; i++)
        {
            // Phase offset = normalised path position mapped to one full 2π cycle.
            // The wave therefore always flows from path start toward path end.
            float phase = t - _pathOffsets[i] * Mathf.PI * 2f;
            float alpha = Mathf.Lerp(1f - pulseDepth, 1f, (Mathf.Sin(phase) + 1f) * 0.5f);

            var c = arrowColor;
            c.a = alpha;
            _arrows[i].color = c;
        }
    }

    // ── Event handlers ─────────────────────────────────────────────────────────

    void OnWaveStarted(int waveIndex)
    {
        if (waveIndex == 0)
            StartCoroutine(FadeAndDestroy());
    }

    // ── Arrow construction ─────────────────────────────────────────────────────

    void BuildArrows()
    {
        var wm = WaypointManager.Instance;
        if (wm == null || wm.waypoints == null || wm.waypoints.Length < 2) return;

        // Pre-compute each segment's length and the total path length.
        int    segCount    = wm.waypoints.Length - 1;
        var    segLengths  = new float[segCount];
        float  totalLength = 0f;

        for (int i = 0; i < segCount; i++)
        {
            segLengths[i] = Vector3.Distance(
                wm.waypoints[i].position, wm.waypoints[i + 1].position);
            totalLength += segLengths[i];
        }

        if (totalLength < 0.001f) return;

        // Walk the path in equal arrowSpacing steps and record placement data.
        var placements = new List<(Vector3 pos, float angleDeg, float normOffset)>();

        // Start half a spacing in so arrows don't sit exactly on the first waypoint.
        for (float dist = arrowSpacing * 0.5f;
             dist < totalLength - arrowSpacing * 0.25f;
             dist += arrowSpacing)
        {
            float remaining = dist;
            for (int seg = 0; seg < segCount; seg++)
            {
                if (remaining > segLengths[seg])
                {
                    remaining -= segLengths[seg];
                    continue;
                }

                Vector3 a   = wm.waypoints[seg].position;
                Vector3 b   = wm.waypoints[seg + 1].position;
                float   t   = remaining / segLengths[seg];
                Vector3 pos = Vector3.Lerp(a, b, t);
                pos.z = 0f;

                Vector2 dir   = ((Vector2)(b - a)).normalized;
                float   angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

                placements.Add((pos, angle, dist / totalLength));
                break;
            }
        }

        // Spawn one child GameObject per placement.
        _pathOffsets = new float[placements.Count];

        for (int i = 0; i < placements.Count; i++)
        {
            var (pos, angle, norm) = placements[i];
            _pathOffsets[i] = norm;

            var go = new GameObject($"PathArrow_{i}");
            go.transform.SetParent(transform, false);
            go.transform.position      = pos;
            go.transform.rotation      = Quaternion.Euler(0f, 0f, angle);
            go.transform.localScale    = Vector3.one * arrowSize;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = _arrowSprite;
            sr.color        = arrowColor;
            sr.sortingOrder = sortingOrder;

            _arrows.Add(sr);
        }
    }

    // ── Fade-out coroutine ─────────────────────────────────────────────────────

    IEnumerator FadeAndDestroy()
    {
        _fadingOut = true;
        float elapsed = 0f;

        // Capture each arrow's alpha at the moment fading begins.
        var startAlphas = new float[_arrows.Count];
        for (int i = 0; i < _arrows.Count; i++)
            startAlphas[i] = _arrows[i].color.a;

        while (elapsed < fadeOutTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeOutTime);

            for (int i = 0; i < _arrows.Count; i++)
            {
                var c = _arrows[i].color;
                c.a = Mathf.Lerp(startAlphas[i], 0f, t);
                _arrows[i].color = c;
            }

            yield return null;
        }

        Destroy(gameObject);
    }

    // ── Arrow sprite generation ────────────────────────────────────────────────

    static void EnsureSprite()
    {
        if (_arrowSprite != null) return;

        const int sz = 64;
        var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
        var px  = new Color32[sz * sz];

        // Filled right-pointing arrowhead triangle with a soft 1-px anti-aliased border.
        //   Tip:  (sz-6,  sz/2)
        //   Top:  (5,     5)
        //   Bot:  (5,     sz-5)
        var tip = new Vector2(sz - 6,  sz * 0.5f);
        var top = new Vector2(5,       5f);
        var bot = new Vector2(5,       sz - 5f);

        for (int y = 0; y < sz; y++)
        for (int x = 0; x < sz; x++)
        {
            var p = new Vector2(x, y);

            // Signed distances to each edge — positive = inside.
            float e0 = EdgeSign(p, tip, top);
            float e1 = EdgeSign(p, top, bot);
            float e2 = EdgeSign(p, bot, tip);

            float minEdge = Mathf.Min(e0, Mathf.Min(e1, e2));

            if (minEdge >= 0f)
            {
                // Fully inside — opaque white (tinted at runtime by SpriteRenderer.color)
                px[y * sz + x] = new Color32(255, 255, 255, 255);
            }
            else if (minEdge > -1.5f)
            {
                // Within 1.5 px of an edge — anti-aliased fringe
                byte a = (byte)(Mathf.Clamp01(1f + minEdge / 1.5f) * 255f);
                px[y * sz + x] = new Color32(255, 255, 255, a);
            }
            // else fully outside — stays transparent (Color32 default = 0,0,0,0)
        }

        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;
        tex.SetPixels32(px);
        tex.Apply(false);

        _arrowSprite = Sprite.Create(
            tex,
            new Rect(0, 0, sz, sz),
            new Vector2(0.5f, 0.5f),
            sz);   // PPU = sz → 1 world unit before child scale
    }

    // Returns positive when p is to the left of the directed edge a→b.
    static float EdgeSign(Vector2 p, Vector2 a, Vector2 b)
        => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
}
