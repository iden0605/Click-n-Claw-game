using UnityEngine;

/// <summary>
/// Adds a permanent drop shadow and dark outline to any sprite-based unit.
///
/// Shadow: a flattened, semi-transparent copy of the sprite offset slightly
/// downward — simulates a short shadow cast by an overhead sun.
///
/// Outline: eight dark sprite copies arranged in a ring just outside the
/// sprite's silhouette — produces a crisp dark border.
///
/// Attach via TroopInstance.Initialize() and EnemyInstance.Initialize(),
/// or add directly to a prefab. Both methods work.
///
/// The shadow and outline automatically follow the main SpriteRenderer each
/// LateUpdate, so they stay in sync with animations, flipping, and dissolve.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteDepthEffect : MonoBehaviour
{
    // ── Configuration ─────────────────────────────────────────────────────────

    // Shadow
    private const float ShadowAlpha    = 0.22f;
    private const float ShadowOffsetX  = 0.02f;   // slight right offset (sun slightly left of zenith)
    private const float ShadowOffsetY  = -0.06f;  // just below unit
    private const float ShadowScaleY   = 0.18f;   // very flat — overhead sun
    private const int   ShadowOrderOffset = -2;

    // Outline
    private const float OutlineDistance   = 0.018f;
    private const float OutlineAlpha      = 0.55f;
    private const int   OutlineOrderOffset = -1;

    private static readonly Vector2[] OutlineOffsets = new Vector2[]
    {
        new Vector2(-1,  0), new Vector2( 1,  0),
        new Vector2( 0, -1), new Vector2( 0,  1),
        new Vector2(-1, -1), new Vector2( 1, -1),
        new Vector2(-1,  1), new Vector2( 1,  1),
    };

    // ── Runtime ───────────────────────────────────────────────────────────────

    private SpriteRenderer   _main;
    private SpriteRenderer   _shadow;
    private SpriteRenderer[] _outlines;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _main = GetComponent<SpriteRenderer>();
        BuildShadow();
        BuildOutlines();
    }

    void LateUpdate()
    {
        if (_main == null) return;
        SyncToMain();
    }

    // ── Setup ─────────────────────────────────────────────────────────────────

    void BuildShadow()
    {
        var go = new GameObject("_Shadow");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(ShadowOffsetX, ShadowOffsetY, 0f);
        go.transform.localScale    = new Vector3(1f, ShadowScaleY, 1f);

        _shadow = go.AddComponent<SpriteRenderer>();
        _shadow.sprite       = _main.sprite;
        _shadow.color        = new Color(0f, 0f, 0f, ShadowAlpha);
        _shadow.flipX        = _main.flipX;
        _shadow.flipY        = _main.flipY;
        _shadow.sortingLayerID    = _main.sortingLayerID;
        _shadow.sortingOrder      = _main.sortingOrder + ShadowOrderOffset;
        _shadow.material     = _main.material;
    }

    void BuildOutlines()
    {
        _outlines = new SpriteRenderer[OutlineOffsets.Length];
        for (int i = 0; i < OutlineOffsets.Length; i++)
        {
            var go = new GameObject($"_Outline{i}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(
                OutlineOffsets[i].x * OutlineDistance,
                OutlineOffsets[i].y * OutlineDistance,
                0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite          = _main.sprite;
            sr.color           = new Color(0.04f, 0.04f, 0.04f, OutlineAlpha);
            sr.flipX           = _main.flipX;
            sr.flipY           = _main.flipY;
            sr.sortingLayerID  = _main.sortingLayerID;
            sr.sortingOrder    = _main.sortingOrder + OutlineOrderOffset;
            sr.material        = _main.material;

            _outlines[i] = sr;
        }
    }

    // ── Sync ──────────────────────────────────────────────────────────────────

    void SyncToMain()
    {
        float mainAlpha = _main.color.a;

        // Shadow
        if (_shadow != null)
        {
            _shadow.sprite  = _main.sprite;
            _shadow.flipX   = _main.flipX;
            _shadow.flipY   = _main.flipY;
            _shadow.color   = new Color(0f, 0f, 0f, ShadowAlpha * mainAlpha);
            _shadow.sortingOrder = _main.sortingOrder + ShadowOrderOffset;
        }

        // Outlines
        if (_outlines != null)
        {
            float oa = OutlineAlpha * mainAlpha;
            var   oc = new Color(0.04f, 0.04f, 0.04f, oa);
            foreach (var sr in _outlines)
            {
                if (sr == null) continue;
                sr.sprite       = _main.sprite;
                sr.flipX        = _main.flipX;
                sr.flipY        = _main.flipY;
                sr.color        = oc;
                sr.sortingOrder = _main.sortingOrder + OutlineOrderOffset;
            }
        }
    }
}
