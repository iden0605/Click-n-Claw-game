using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Water-ball ricochet attack for the Megalotl (evolved form of the Axolotl).
///
/// Base behaviour (all tiers):
///   Same charge → travel → ricochet cycle as the Axolotl. The orb now DAZES
///   every enemy it hits in addition to slowing them. Rings are deep violet-blue
///   instead of the Axolotl's cyan, and the core is denser.
///
/// Upgrade 1 — Rattled (UpgradeLevel ≥ 4):
///   Daze duration is longer (stunDuration in the DazeOnHit tier effect).
///   Impact crackles with a sharper purple electricity burst.
///
/// Upgrade 2 — Feedback Loop (UpgradeLevel ≥ 5):
///   After dazing each enemy the orb pulses feedback arcs to every nearby
///   already-dazed enemy, refreshing their daze and dealing partial damage.
///   Each arc is a jittery lightning LineRenderer that fades in ~0.25 s.
///
/// Upgrade 3 — Mind Shatter (UpgradeLevel ≥ 6):
///   Every dazed enemy gets a MindShatterDebuff. When their daze ends, a dark
///   crystal particle burst erupts and a strong 5-second slow is applied.
/// </summary>
[RequireComponent(typeof(TroopBehavior), typeof(TroopInstance))]
public class MegalotlWaterBallAttack : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Animation State Names")]
    [SerializeField] private string idleStateName   = "MegalotlIdle";
    [SerializeField] private string attackStateName = "MegalotlAttack";

    [Header("Timing")]
    [SerializeField] private float chargeDuration  = 0.70f;
    [SerializeField] private float recoverDuration = 0.20f;

    [Header("Water Ball")]
    [SerializeField] private float ballMouthOffset = 0.22f;
    [SerializeField] private float ballRadius      = 0.18f;
    [SerializeField] private float ballTravelSpeed = 5.0f;
    [SerializeField] private float ricochetRadius  = 3.0f;
    [SerializeField] private int   baseBounces     = 5;

    [Header("Feedback Loop (Upgrade 2)")]
    [SerializeField] private float feedbackRadius  = 2.8f;
    [SerializeField] private float feedbackDamageFraction = 0.40f;

    [Header("Sorting")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int    sortingOrder     = 7;

    // ── Internal ──────────────────────────────────────────────────────────────

    private enum Phase { Idle, ChargingUp, Firing, Recovering }

    private TroopBehavior     _behavior;
    private TroopInstance     _instance;
    private Animator          _animator;
    private Phase             _phase      = Phase.Idle;
    private float             _phaseTimer = 0f;
    private float             _cooldown   = 0f;
    private EnemyMovement     _lockedTarget;
    private MegalotlWaterBall _chargeBall;
    private Transform         _ballAnchor;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _behavior = GetComponent<TroopBehavior>();
        _instance = GetComponent<TroopInstance>();
        _animator = GetComponent<Animator>();
    }

    void OnDisable()
    {
        if (_ballAnchor != null) { Destroy(_ballAnchor.gameObject); _ballAnchor = null; }
        _chargeBall = null;
        _phase      = Phase.Idle;
        _cooldown   = 0f;
    }

    void Update()
    {
        _cooldown -= Time.deltaTime;
        switch (_phase)
        {
            case Phase.Idle:       TickIdle();       break;
            case Phase.ChargingUp: TickCharging();   break;
            case Phase.Recovering: TickRecovering(); break;
        }
    }

    // ── Idle ──────────────────────────────────────────────────────────────────

    void TickIdle()
    {
        if (_cooldown > 0f || _behavior.CurrentTarget == null) return;
        BeginCharge();
    }

    // ── Charging ──────────────────────────────────────────────────────────────

    void BeginCharge()
    {
        _phase        = Phase.ChargingUp;
        _phaseTimer   = 0f;
        _lockedTarget = _behavior.CurrentTarget;

        PlayAnim(attackStateName);

        var anchorGo = new GameObject("MegalotlBallAnchor");
        anchorGo.transform.SetParent(transform, false);
        anchorGo.transform.localPosition = Vector3.up * ballMouthOffset;
        _ballAnchor = anchorGo.transform;

        var ballGo = new GameObject("MegalotlWaterBall_Charge");
        ballGo.transform.SetParent(_ballAnchor, false);
        ballGo.transform.localPosition = Vector3.zero;

        _chargeBall = ballGo.AddComponent<MegalotlWaterBall>();
        _chargeBall.Build(ballRadius, sortingLayerName, sortingOrder);
        _chargeBall.SetVisualScale(0f);
    }

    void TickCharging()
    {
        _phaseTimer += Time.deltaTime;
        if (_behavior.CurrentTarget != null)
            _lockedTarget = _behavior.CurrentTarget;

        float t = Mathf.Clamp01(_phaseTimer / chargeDuration);
        _chargeBall?.SetVisualScale(EaseOutBack(t));

        if (_phaseTimer >= chargeDuration)
            BeginFiring();
    }

    // ── Firing ────────────────────────────────────────────────────────────────

    void BeginFiring()
    {
        _phase = Phase.Firing;
        PlayAnim(idleStateName);

        if (_chargeBall == null || _lockedTarget == null)
        {
            CleanupAnchor();
            BeginRecovering();
            return;
        }

        _chargeBall.transform.SetParent(null, true);
        CleanupAnchor();

        float dazeDuration  = GetDazeDuration();
        bool  hasFeedback   = _instance.UpgradeLevel >= 5;
        bool  hasMindShatter = _instance.UpgradeLevel >= 6;

        _chargeBall.Launch(
            _lockedTarget, _instance,
            baseBounces, ricochetRadius, ballTravelSpeed,
            dazeDuration, hasFeedback, feedbackRadius, feedbackDamageFraction,
            hasMindShatter, sortingLayerName, sortingOrder);

        _chargeBall = null;
        BeginRecovering();
    }

    // ── Recovering ────────────────────────────────────────────────────────────

    void BeginRecovering()
    {
        _phase      = Phase.Recovering;
        _phaseTimer = 0f;
        _cooldown   = _instance.GetEffectiveAttackInterval();
    }

    void TickRecovering()
    {
        _phaseTimer += Time.deltaTime;
        if (_phaseTimer >= recoverDuration) _phase = Phase.Idle;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    float GetDazeDuration()
    {
        var cfg = _instance.GetEffectConfig(TroopEffectType.DazeOnHit);
        return cfg != null && cfg.stunDuration > 0f ? cfg.stunDuration : 1.0f;
    }

    void CleanupAnchor()
    {
        if (_ballAnchor != null) { Destroy(_ballAnchor.gameObject); _ballAnchor = null; }
    }

    void PlayAnim(string stateName)
    {
        if (_animator != null && _animator.runtimeAnimatorController != null)
            _animator.Play(stateName);
    }

    public int CurrentBounces => baseBounces;

    static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float f = t - 1f;
        return 1f + c3 * f * f * f + c1 * f * f;
    }
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// The Megalotl's water-ball projectile. Deep violet-blue Saturn rings surround
/// a dense violet particle core. On each hit the ball dazes the enemy, applies
/// a short slow, and (if unlocked) triggers feedback arcs or mind shatter.
/// </summary>
public class MegalotlWaterBall : MonoBehaviour
{
    // ── Ring constants ────────────────────────────────────────────────────────

    private const int RingSegments = 36;

    private static readonly float[] RingInclinations = { 20f,  55f,  82f  };
    private static readonly float[] RingRevSpeeds    = { 70f, -48f,  35f  };
    private static readonly float[] RingRadiusMult   = { 1.0f, 0.82f, 0.65f };
    private static readonly float[] RingWidths       = { 0.026f, 0.020f, 0.015f };
    private static readonly Color[] RingColors =
    {
        new Color(0.55f, 0.25f, 1.00f, 0.92f),  // deep violet
        new Color(0.35f, 0.55f, 1.00f, 0.80f),  // blue-violet
        new Color(0.78f, 0.48f, 1.00f, 0.65f),  // light purple
    };

    // ── Visual state ──────────────────────────────────────────────────────────

    private float          _visualScale = 0f;
    private float          _baseRadius;
    private float[]        _revAngles = new float[3];
    private LineRenderer[] _rings     = new LineRenderer[3];
    private ParticleSystem _corePs;

    // ── Travel state ──────────────────────────────────────────────────────────

    private bool          _launched;
    private TroopInstance _owner;
    private EnemyMovement _currentTarget;
    private int           _bouncesLeft;
    private float         _travelSpeed;
    private float         _ricochetRadius;
    private float         _dazeDuration;
    private bool          _hasFeedback;
    private float         _feedbackRadius;
    private float         _feedbackDamageFraction;
    private bool          _hasMindShatter;
    private string        _sortLayer;
    private int           _sortOrder;

    private readonly HashSet<EnemyMovement> _hit = new();

    private const float SlowDuration = 1.5f;
    private const float SlowFactor   = 0.45f;

    // ── Build ─────────────────────────────────────────────────────────────────

    public void Build(float baseRadius, string sortLayer, int sortOrder)
    {
        _baseRadius = baseRadius;
        _sortLayer  = sortLayer;
        _sortOrder  = sortOrder;
        BuildRings(sortLayer, sortOrder);
        BuildCoreParticles(sortLayer, sortOrder);
    }

    void BuildRings(string sortLayer, int sortOrder)
    {
        for (int i = 0; i < 3; i++)
        {
            var go = new GameObject($"Ring_{i}");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace     = true;
            lr.loop              = true;
            lr.positionCount     = RingSegments;
            lr.numCapVertices    = 0;
            lr.numCornerVertices = 0;
            lr.widthMultiplier   = RingWidths[i];
            lr.material          = new Material(Shader.Find("Sprites/Default"));
            lr.startColor        = RingColors[i];
            lr.endColor          = RingColors[i];
            lr.sortingLayerName  = sortLayer;
            lr.sortingOrder      = sortOrder + i;
            _rings[i] = lr;
        }
    }

    void BuildCoreParticles(string sortLayer, int sortOrder)
    {
        var go = new GameObject("MegalotlCore");
        go.transform.SetParent(transform, false);

        var ps   = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop            = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.25f, 0.50f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.02f, 0.10f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.030f, 0.075f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(0.50f, 0.20f, 1.00f, 0.95f),
                                   new Color(0.75f, 0.55f, 1.00f, 0.85f));
        main.gravityModifier = 0f;
        main.maxParticles    = 120;

        var emission = ps.emission;
        emission.rateOverTime = 95f;

        var shape = ps.shape;
        shape.enabled         = true;
        shape.shapeType       = ParticleSystemShapeType.Sphere;
        shape.radius          = 0.055f;
        shape.radiusThickness = 1f;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space   = ParticleSystemSimulationSpace.World;
        vel.x       = new ParticleSystem.MinMaxCurve(-0.12f,  0.12f);
        vel.y       = new ParticleSystem.MinMaxCurve(-0.12f,  0.12f);
        vel.z       = new ParticleSystem.MinMaxCurve(-0.01f,  0.01f);

        var colLife = ps.colorOverLifetime;
        colLife.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.85f, 0.65f, 1.00f), 0.00f),
                new GradientColorKey(new Color(0.55f, 0.25f, 1.00f), 0.50f),
                new GradientColorKey(new Color(0.30f, 0.10f, 0.80f), 1.00f),
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.00f, 0.00f),
                new GradientAlphaKey(0.90f, 0.10f),
                new GradientAlphaKey(0.55f, 0.60f),
                new GradientAlphaKey(0.00f, 1.00f),
            });
        colLife.color = new ParticleSystem.MinMaxGradient(grad);

        var sizeLife = ps.sizeOverLifetime;
        sizeLife.enabled = true;
        sizeLife.size    = new ParticleSystem.MinMaxCurve(1f,
                               new AnimationCurve(
                                   new Keyframe(0f,    0.25f),
                                   new Keyframe(0.30f, 1.00f),
                                   new Keyframe(1f,    0.00f)));

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material         = new Material(Shader.Find("Sprites/Default"));
        psr.sortingLayerName = sortLayer;
        psr.sortingOrder     = sortOrder + 3;

        ps.Play();
        _corePs = ps;
    }

    // ── Scale ─────────────────────────────────────────────────────────────────

    public void SetVisualScale(float t)
    {
        _visualScale = Mathf.Clamp(t, 0f, 1.1f);
        if (_corePs != null)
        {
            float c = Mathf.Clamp01(t);
            var shape = _corePs.shape;
            shape.radius = 0.055f * c;
            var main = _corePs.main;
            main.startSize = new ParticleSystem.MinMaxCurve(0.030f * c, 0.075f * c);
        }
    }

    // ── Launch ────────────────────────────────────────────────────────────────

    public void Launch(EnemyMovement firstTarget, TroopInstance owner,
                       int maxBounces, float ricochetRadius, float travelSpeed,
                       float dazeDuration, bool hasFeedback, float feedbackRadius,
                       float feedbackDamageFraction, bool hasMindShatter,
                       string sortLayer, int sortOrder)
    {
        _owner                  = owner;
        _currentTarget          = firstTarget;
        _bouncesLeft            = maxBounces;
        _ricochetRadius         = ricochetRadius;
        _travelSpeed            = travelSpeed;
        _dazeDuration           = dazeDuration;
        _hasFeedback            = hasFeedback;
        _feedbackRadius         = feedbackRadius;
        _feedbackDamageFraction = feedbackDamageFraction;
        _hasMindShatter         = hasMindShatter;
        _sortLayer              = sortLayer;
        _sortOrder              = sortOrder;
        _launched               = true;
        _visualScale            = 1f;

        if (firstTarget != null) _hit.Add(firstTarget);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        for (int i = 0; i < 3; i++)
            _revAngles[i] += RingRevSpeeds[i] * Time.deltaTime;
        DrawAllRings();
        if (_launched) TickTravel();
    }

    // ── Travel ────────────────────────────────────────────────────────────────

    void TickTravel()
    {
        if (_bouncesLeft <= 0 || _currentTarget == null) { Dissolve(); return; }

        Vector3 targetPos = _currentTarget.transform.position;
        float   step      = _travelSpeed * Time.deltaTime;
        float   dist      = Vector3.Distance(transform.position, targetPos);

        if (dist <= step)
        {
            transform.position = targetPos;
            HitTarget(_currentTarget);
        }
        else
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPos, step);
        }
    }

    void HitTarget(EnemyMovement target)
    {
        if (target != null)
        {
            _owner?.DealDamage(target, _owner.Data?.attackType ?? AttackType.Ranged,
                               transform.position);

            EnemyStatusEffects.ApplyFreeze(target.gameObject, SlowDuration, SlowFactor);
            EnemySlowIndicator.Apply(target.gameObject, SlowDuration);

            EnemyStatusEffects.ApplyDaze(target.gameObject, _dazeDuration);
            MegalotlDazedMarker.Mark(target.gameObject, _dazeDuration);

            if (_hasMindShatter)
                MindShatterDebuff.Apply(target.gameObject, _dazeDuration, _sortLayer, _sortOrder);

            if (_hasFeedback)
                SpawnFeedbackArcs(target);
        }

        SpawnHitRipple(transform.position);

        _bouncesLeft--;
        if (_bouncesLeft <= 0) { Dissolve(); return; }

        EnemyMovement next = FindNextTarget();
        if (next == null)     { Dissolve(); return; }

        _hit.Add(next);
        _currentTarget = next;
    }

    EnemyMovement FindNextTarget()
    {
        var   cols  = Physics2D.OverlapCircleAll(transform.position, _ricochetRadius);
        EnemyMovement best  = null;
        float         bestD = float.MaxValue;

        foreach (var col in cols)
        {
            if (!col.TryGetComponent<EnemyMovement>(out var em)) continue;
            if (_hit.Contains(em)) continue;
            float d = (em.transform.position - transform.position).sqrMagnitude;
            if (d < bestD) { bestD = d; best = em; }
        }
        return best;
    }

    // ── Feedback Loop ─────────────────────────────────────────────────────────

    void SpawnFeedbackArcs(EnemyMovement justHit)
    {
        var cols = Physics2D.OverlapCircleAll(transform.position, _feedbackRadius);
        foreach (var col in cols)
        {
            if (!col.TryGetComponent<EnemyMovement>(out var em)) continue;
            if (em == justHit) continue;

            var marker = em.GetComponent<MegalotlDazedMarker>();
            if (marker == null || !marker.IsActive) continue;

            // Partial damage to already-dazed enemy
            float dmg = (_owner != null ? _owner.CurrentAttack : 0f) * _feedbackDamageFraction;
            em.TakeDamage(dmg, AttackType.Splash, transform.position);

            // Refresh their daze
            EnemyStatusEffects.ApplyDaze(em.gameObject, _dazeDuration * 0.7f);
            MegalotlDazedMarker.Mark(em.gameObject, _dazeDuration * 0.7f);

            // Lightning arc VFX from orb to dazed enemy
            var arcGo = new GameObject("FeedbackArc");
            arcGo.transform.position = transform.position;
            arcGo.AddComponent<FeedbackArc>().Init(
                transform.position, em.transform.position, _sortLayer, _sortOrder + 5);
        }
    }

    // ── Ring drawing ──────────────────────────────────────────────────────────

    void DrawAllRings()
    {
        Vector3 centre = transform.position;
        float   scale  = Mathf.Max(0f, _visualScale);

        for (int i = 0; i < 3; i++)
        {
            float r = _baseRadius * RingRadiusMult[i] * scale;
            DrawRing(_rings[i], centre, r,
                     RingInclinations[i], _revAngles[i],
                     RingColors[i], scale, RingWidths[i]);
        }
    }

    void DrawRing(LineRenderer lr, Vector3 centre, float r,
                  float phiDeg, float thetaDeg, Color color,
                  float alphaMult, float baseWidth)
    {
        float phi   = phiDeg   * Mathf.Deg2Rad;
        float theta = thetaDeg * Mathf.Deg2Rad;

        float sinPhi = Mathf.Sin(phi), cosPhi = Mathf.Cos(phi);
        float sinTheta = Mathf.Sin(theta), cosTheta = Mathf.Cos(theta);

        var e1 = new Vector2(-sinTheta,            cosTheta);
        var e2 = new Vector2(-cosPhi * cosTheta,  -cosPhi * sinTheta);

        for (int i = 0; i < RingSegments; i++)
        {
            float a      = 2f * Mathf.PI * i / RingSegments;
            var   offset = r * (e1 * Mathf.Cos(a) + e2 * Mathf.Sin(a));
            lr.SetPosition(i, centre + new Vector3(offset.x, offset.y, 0f));
        }

        float clamped = Mathf.Clamp01(alphaMult);
        lr.widthMultiplier = baseWidth * Mathf.Max(0.001f, clamped);
        lr.startColor = new Color(color.r, color.g, color.b, color.a * clamped);
        lr.endColor   = new Color(color.r, color.g, color.b, color.a * clamped);
    }

    // ── Hit VFX ───────────────────────────────────────────────────────────────

    void SpawnHitRipple(Vector3 pos)
    {
        var go = new GameObject("MegalotlHit_VFX");
        go.transform.position = pos;

        var ps   = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop            = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.12f, 0.32f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.8f, 3.2f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.025f, 0.08f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(0.55f, 0.25f, 1.00f),
                                   new Color(0.85f, 0.65f, 1.00f));
        main.gravityModifier = 0.2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 18;
        main.stopAction      = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = 0.04f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.85f, 0.65f, 1.00f), 0.00f),
                new GradientColorKey(new Color(0.40f, 0.10f, 0.80f), 1.00f),
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1.00f, 0.00f),
                new GradientAlphaKey(0.00f, 1.00f),
            });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var sizeLife = ps.sizeOverLifetime;
        sizeLife.enabled = true;
        sizeLife.size    = new ParticleSystem.MinMaxCurve(1f,
                               new AnimationCurve(
                                   new Keyframe(0f, 1f),
                                   new Keyframe(1f, 0f)));

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material         = new Material(Shader.Find("Sprites/Default"));
        psr.sortingLayerName = _sortLayer;
        psr.sortingOrder     = _sortOrder + 5;

        ps.Play();

        var ringGo = new GameObject("MegalotlHit_Ring");
        ringGo.transform.position = pos;
        ringGo.AddComponent<WaterRippleRing>().Init(
            new Color(0.50f, 0.15f, 0.90f, 0.85f), _sortLayer, _sortOrder + 4);
    }

    // ── Dissolve ──────────────────────────────────────────────────────────────

    void Dissolve()
    {
        _launched      = false;
        _currentTarget = null;

        foreach (var lr in _rings)
            if (lr != null) lr.enabled = false;

        if (_corePs != null)
            _corePs.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        SpawnHitRipple(transform.position);
        Destroy(gameObject, 1.2f);
    }
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Lightweight marker added to enemies that the Megalotl has dazed.
/// Used by the Feedback Loop to identify valid chain targets.
/// Self-destructs when the daze window expires.
/// </summary>
public class MegalotlDazedMarker : MonoBehaviour
{
    private float _endTime;

    public static void Mark(GameObject target, float duration)
    {
        var m = target.GetComponent<MegalotlDazedMarker>()
                ?? target.AddComponent<MegalotlDazedMarker>();
        m._endTime = Mathf.Max(m._endTime, Time.time + duration);
    }

    public bool IsActive => Time.time < _endTime;

    void Update()
    {
        if (!IsActive) Destroy(this);
    }
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Applied to a dazed enemy by the Megalotl's Mind Shatter upgrade.
/// Waits for the daze window to expire, then triggers a dark crystal particle
/// burst and applies a strong 5-second slow — the enemy's resolve has crumbled.
/// </summary>
public class MindShatterDebuff : MonoBehaviour
{
    private float  _triggerTime;
    private bool   _fired;
    private string _sortLayer;
    private int    _sortOrder;

    private const float ShatterSlowFactor   = 0.45f;
    private const float ShatterSlowDuration = 5.0f;

    public static void Apply(GameObject enemy, float dazeDuration, string sortLayer, int sortOrder)
    {
        var d = enemy.GetComponent<MindShatterDebuff>()
                ?? enemy.AddComponent<MindShatterDebuff>();
        // Re-arm if daze was refreshed
        d._triggerTime = Mathf.Max(d._triggerTime, Time.time + dazeDuration);
        d._fired       = false;
        d._sortLayer   = sortLayer;
        d._sortOrder   = sortOrder;
    }

    void Update()
    {
        if (_fired) return;
        if (Time.time < _triggerTime) return;

        _fired = true;
        TriggerShatter();
        Destroy(this, 0.1f);
    }

    void TriggerShatter()
    {
        // Long lasting slow
        EnemyStatusEffects.ApplyFreeze(gameObject, ShatterSlowDuration, ShatterSlowFactor);
        EnemySlowIndicator.Apply(gameObject, ShatterSlowDuration);

        SpawnShatterVFX(transform.position);
    }

    void SpawnShatterVFX(Vector3 pos)
    {
        // Dark crystal shard burst
        var go = new GameObject("MindShatter_VFX");
        go.transform.position = pos;

        var ps   = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop            = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.35f, 0.70f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(1.0f, 3.8f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.04f, 0.13f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(0.35f, 0.05f, 0.70f, 1.00f),
                                   new Color(0.65f, 0.25f, 1.00f, 0.90f));
        main.gravityModifier = 0.20f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 22;
        main.stopAction      = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = 0.05f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.80f, 0.55f, 1.00f), 0.00f),
                new GradientColorKey(new Color(0.35f, 0.05f, 0.70f), 1.00f),
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1.00f, 0.00f),
                new GradientAlphaKey(0.00f, 1.00f),
            });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var sizeLife = ps.sizeOverLifetime;
        sizeLife.enabled = true;
        sizeLife.size    = new ParticleSystem.MinMaxCurve(1f,
                               new AnimationCurve(
                                   new Keyframe(0f, 1.0f),
                                   new Keyframe(1f, 0.0f)));

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material         = new Material(Shader.Find("Sprites/Default"));
        psr.sortingLayerName = _sortLayer;
        psr.sortingOrder     = _sortOrder + 8;

        ps.Play();

        // Expanding dark ring
        var ringGo = new GameObject("MindShatter_Ring");
        ringGo.transform.position = pos;
        ringGo.AddComponent<WaterRippleRing>().Init(
            new Color(0.40f, 0.05f, 0.75f, 0.90f), _sortLayer, _sortOrder + 7);
    }
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// A brief jittery lightning arc drawn between two world-space points.
/// Used by the Megalotl's Feedback Loop to visualise chains between dazed enemies.
/// Re-randomises its path every frame for a crackling electricity look.
/// Self-destructs after ~0.25 s.
/// </summary>
public class FeedbackArc : MonoBehaviour
{
    private LineRenderer _lr;
    private Vector3      _from;
    private Vector3      _to;
    private float        _timer;

    private const float Duration      = 0.25f;
    private const int   Segments      = 8;
    private const float JitterRadius  = 0.12f;
    private const float StartWidth    = 0.028f;

    public void Init(Vector3 from, Vector3 to, string sortLayer, int sortOrder)
    {
        _from = from;
        _to   = to;

        _lr = gameObject.AddComponent<LineRenderer>();
        _lr.useWorldSpace     = true;
        _lr.loop              = false;
        _lr.positionCount     = Segments + 1;
        _lr.numCapVertices    = 2;
        _lr.widthMultiplier   = StartWidth;
        _lr.material          = new Material(Shader.Find("Sprites/Default"));
        _lr.sortingLayerName  = sortLayer;
        _lr.sortingOrder      = sortOrder;

        DrawArc(1f);
    }

    void Update()
    {
        _timer += Time.deltaTime;
        float t = Mathf.Clamp01(_timer / Duration);

        float alpha = 1f - t;
        _lr.widthMultiplier = Mathf.Lerp(StartWidth, 0.004f, t);
        _lr.startColor = new Color(0.72f, 0.38f, 1.00f, alpha);
        _lr.endColor   = new Color(0.38f, 0.62f, 1.00f, alpha);

        DrawArc(alpha);

        if (t >= 1f) Destroy(gameObject);
    }

    void DrawArc(float jitterMult)
    {
        for (int i = 0; i <= Segments; i++)
        {
            float   s      = (float)i / Segments;
            Vector3 pt     = Vector3.Lerp(_from, _to, s);
            float   taper  = Mathf.Sin(s * Mathf.PI);
            float   jx     = Random.Range(-1f, 1f) * JitterRadius * taper * jitterMult;
            float   jy     = Random.Range(-1f, 1f) * JitterRadius * taper * jitterMult;
            _lr.SetPosition(i, pt + new Vector3(jx, jy, 0f));
        }
    }
}
