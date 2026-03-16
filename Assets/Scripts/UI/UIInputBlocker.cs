using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Utility that reports whether the current pointer position sits over a
/// visible UI Toolkit element in any active UIDocument in the scene.
///
/// Use at the top of any Update() that processes world-space mouse input so
/// that clicks on sidebars, selection panels, and HUD elements don't leak
/// through to the game objects underneath.
/// </summary>
public static class UIInputBlocker
{
    /// <summary>
    /// Returns true if the mouse cursor is currently over a non-background
    /// VisualElement in any active UIDocument. Safe to call every frame.
    /// </summary>
    public static bool IsPointerOverUI()
    {
        foreach (var doc in Object.FindObjectsOfType<UIDocument>(false))
        {
            if (!doc.isActiveAndEnabled) continue;
            var root = doc.rootVisualElement;
            if (root == null) continue;

            // Convert screen coordinates (origin bottom-left, pixels) to panel
            // coordinates (origin top-left, panel pixels). This matches the
            // conversion already used throughout the project.
            var sp = Input.mousePosition;
            float px = (sp.x / Screen.width)  * root.resolvedStyle.width;
            float py = ((Screen.height - sp.y) / Screen.height) * root.resolvedStyle.height;

            // panel.Pick() returns the deepest VisualElement whose hit area
            // contains the point (respects pickingMode). Hitting only the root
            // means the pointer is over an empty / transparent area — that should
            // not block world input. A deeper hit means a real panel or button.
            var picked = root.panel?.Pick(new Vector2(px, py));
            if (picked != null && picked != root)
                return true;
        }
        return false;
    }
}
