using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Controls the Game Over overlay.
/// Hidden by default; shown automatically when PlayerHealthManager fires OnGameOver.
///
/// ── Scene setup ──
///   1. Add a GameObject "GameOverUI" to the scene.
///   2. Attach a UIDocument component → assign GameOverPanel.uxml, Sort Order 15.
///   3. Attach this script to the same GameObject.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class GameOverController : MonoBehaviour
{
    private VisualElement _root;
    private Button        _quitBtn;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void OnEnable()
    {
        var doc = GetComponent<UIDocument>();
        _root    = doc.rootVisualElement.Q("gameover-root");
        _quitBtn = doc.rootVisualElement.Q<Button>("quit-btn");

        // Hide panel until game over fires
        _root.style.display = DisplayStyle.None;

        if (_quitBtn != null)
            _quitBtn.clicked += OnQuit;

        PlayerHealthManager.OnGameOver += ShowGameOver;
    }

    void OnDisable()
    {
        if (_quitBtn != null)
            _quitBtn.clicked -= OnQuit;

        PlayerHealthManager.OnGameOver -= ShowGameOver;
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    void ShowGameOver()
    {
        _root.style.display = DisplayStyle.Flex;
        Time.timeScale = 0f;
        Debug.Log("[GameOverController] Showing game over screen.");
    }

    void OnQuit()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }
}
