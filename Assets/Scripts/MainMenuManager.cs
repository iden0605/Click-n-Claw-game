using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Drives the main menu UI Document.
/// Assign this component (alongside a UIDocument) to a GameObject in the MainMenu scene.
/// Drag the corresponding sprites into each SerializeField slot in the Inspector.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class MainMenuManager : MonoBehaviour
{
    [Header("Background")]
    [SerializeField] private Sprite backgroundSprite;

    [Header("Centipede Decoration (hangs off Play button)")]
    [SerializeField] private Sprite centipedeSprite;

    [Header("Critter Icons (bottom row)")]
    [SerializeField] private Sprite frogSprite;
    [SerializeField] private Sprite antSprite;
    [SerializeField] private Sprite beetleSprite;
    [SerializeField] private Sprite mantisSprite;
    [SerializeField] private Sprite koiSprite;
    [SerializeField] private Sprite dragonFlySprite;
    [SerializeField] private Sprite axolotlSprite;
    [SerializeField] private Sprite aquaticWormSprite;

    [Header("Enemy Icons (water strip)")]
    [SerializeField] private Sprite plasticBagSprite;
    [SerializeField] private Sprite mosquitoSprite;
    [SerializeField] private Sprite strawSprite;
    [SerializeField] private Sprite plasticBottleSprite;
    [SerializeField] private Sprite tinCanSprite;
    [SerializeField] private Sprite alligatorSprite;
    [SerializeField] private Sprite cockroachSprite;
    [SerializeField] private Sprite babyMosquitoSprite;

    [Header("Corner Decorations")]
    [SerializeField] private Sprite raccoonSprite;
    [SerializeField] private Sprite waspSprite;

    void OnEnable()
    {
        AudioManager.Instance?.PlayMusic(AudioManager.Instance.bgmLobby);

        var root = GetComponent<UIDocument>().rootVisualElement;

        // ── Sprite assignments ─────────────────────────────────
        SetBg(root, "bg-image",           backgroundSprite);
        SetBg(root, "centipede-deco",     centipedeSprite);

        // Critter row
        SetBg(root, "deco-frog",          frogSprite);
        SetBg(root, "deco-ant",           antSprite);
        SetBg(root, "deco-beetle",        beetleSprite);
        SetBg(root, "deco-mantis",        mantisSprite);
        SetBg(root, "deco-koi",           koiSprite);
        SetBg(root, "deco-dragonfly",     dragonFlySprite);
        SetBg(root, "deco-axolotl",       axolotlSprite);
        SetBg(root, "deco-worm",          aquaticWormSprite);
        SetBg(root, "deco-centipede2",    centipedeSprite);

        // Water strip enemies
        SetBg(root, "deco-plastic-bag",   plasticBagSprite);
        SetBg(root, "deco-mosquito",      mosquitoSprite);
        SetBg(root, "deco-straw",         strawSprite);
        SetBg(root, "deco-plastic-bottle",plasticBottleSprite);
        SetBg(root, "deco-tin-can",       tinCanSprite);
        SetBg(root, "deco-alligator",     alligatorSprite);
        SetBg(root, "deco-cockroach",     cockroachSprite);
        SetBg(root, "deco-baby-mosquito", babyMosquitoSprite);
        SetBg(root, "deco-baby-mosquito-2", babyMosquitoSprite);

        // Corner ghosts
        SetBg(root, "deco-raccoon",       raccoonSprite);
        SetBg(root, "deco-wasp",          waspSprite);

        // ── Button callbacks ───────────────────────────────────
        root.Q<Button>("play-btn") .clicked += OnPlay;
        root.Q<Button>("exit-btn") .clicked += OnExit;
    }

    void OnDisable()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null) return;
        var root = doc.rootVisualElement;
        if (root == null) return;

        var playBtn = root.Q<Button>("play-btn");
        if (playBtn != null) playBtn.clicked -= OnPlay;

        var exitBtn = root.Q<Button>("exit-btn");
        if (exitBtn != null) exitBtn.clicked -= OnExit;
    }

    // ── Handlers ────────────────────────────────────────────────

    void OnPlay() => SceneManager.LoadScene("Main");

    void OnExit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ── Helper ──────────────────────────────────────────────────

    static void SetBg(VisualElement root, string elementName, Sprite sprite)
    {
        if (sprite == null) return;
        var el = root.Q(elementName);
        if (el != null)
            el.style.backgroundImage = new StyleBackground(sprite);
    }
}
