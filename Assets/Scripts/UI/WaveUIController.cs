using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Updates the wave counter, animated heart HP display, and Next Wave button in the HUD.
///
/// ── Scene setup ──
///   1. Add a GameObject "WaveUI" to the scene.
///   2. Attach a UIDocument component → assign WaveUI.uxml, Sort Order 5.
///   3. Attach this script to the same GameObject.
///   4. Assign the 5 heart sprites (frames 0–4) to Heart Sprites in the Inspector.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class WaveUIController : MonoBehaviour
{
    [Header("Heart Animation")]
    [Tooltip("Sprite frames in order: 0, 1, 2, 3, 4")]
    [SerializeField] private Sprite[] heartSprites;

    [Tooltip("Frames per second for the heart animation.")]
    [SerializeField] private float frameRate = 8f;

    // ── Private state ──────────────────────────────────────────────────────────
    private Label         _waveLabel;
    private Label         _healthLabel;
    private VisualElement _heartIcon;
    private Button        _nextWaveBtn;

    private float _animTime   = 0f;
    private bool  _pingPongDir = true; // true = forward, false = backward

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        _waveLabel   = root.Q<Label>("wave-label");
        _healthLabel = root.Q<Label>("health-label");
        _heartIcon   = root.Q("heart-icon");
        _nextWaveBtn = root.Q<Button>("next-wave-btn");

        if (_nextWaveBtn != null)
            _nextWaveBtn.clicked += OnNextWaveClicked;

        PlayerHealthManager.OnHealthChanged += RefreshHealth;

        RefreshHealth();
    }

    void OnDisable()
    {
        if (_nextWaveBtn != null)
            _nextWaveBtn.clicked -= OnNextWaveClicked;

        PlayerHealthManager.OnHealthChanged -= RefreshHealth;
    }

    void Update()
    {
        UpdateWaveLabel();
        UpdateNextWaveButton();
        UpdateHeartAnimation();
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    void UpdateWaveLabel()
    {
        if (WaveManager.Instance == null || _waveLabel == null) return;
        int idx = WaveManager.Instance.CurrentWaveIndex;
        _waveLabel.text = idx < 0 ? "WAVE —" : $"WAVE {idx + 1}";
    }

    void UpdateNextWaveButton()
    {
        if (WaveManager.Instance == null || _nextWaveBtn == null) return;

        bool canStart = !WaveManager.Instance.IsWaveActive
                        && (PlayerHealthManager.Instance == null || !PlayerHealthManager.Instance.IsGameOver);

        _nextWaveBtn.SetEnabled(canStart);

        if (canStart)
            _nextWaveBtn.RemoveFromClassList("hud-next-btn--disabled");
        else
            _nextWaveBtn.AddToClassList("hud-next-btn--disabled");
    }

    void UpdateHeartAnimation()
    {
        if (_heartIcon == null || heartSprites == null || heartSprites.Length == 0) return;

        int frameCount = heartSprites.Length;

        // Advance time
        _animTime += Time.deltaTime * frameRate;

        // Ping-pong: go 0→(n-1) then back (n-1)→0
        int pingPongLength = frameCount * 2 - 2; // e.g. 5 frames → 8 steps (0,1,2,3,4,3,2,1)
        int step = Mathf.FloorToInt(_animTime) % Mathf.Max(1, pingPongLength);

        int frame = step < frameCount ? step : pingPongLength - step;
        frame = Mathf.Clamp(frame, 0, frameCount - 1);

        var sprite = heartSprites[frame];
        if (sprite != null)
            _heartIcon.style.backgroundImage = new StyleBackground(sprite);
    }

    void RefreshHealth()
    {
        if (_healthLabel == null) return;

        _healthLabel.text = PlayerHealthManager.Instance != null
            ? PlayerHealthManager.Instance.CurrentHealth.ToString()
            : "—";
    }

    void OnNextWaveClicked()
    {
        WaveManager.Instance?.StartNextWave();
    }
}
