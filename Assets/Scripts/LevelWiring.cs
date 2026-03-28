using UnityEngine;

/// <summary>
/// Wires scene-level connections at runtime.
/// Direct wiring: PressureButton_1 → Gate_1, PressureButton_2 → Gate_2.
/// Falls back to single button → single gate when only one of each exists.
/// </summary>
public class LevelWiring : MonoBehaviour
{
    void Start()
    {
        // Collect all buttons and gates by name
        var buttons = Object.FindObjectsByType<PressureButton>(FindObjectsSortMode.None);
        var gates   = Object.FindObjectsByType<Gate>(FindObjectsSortMode.None);

        if (buttons.Length == 0) { Debug.LogError("[Blob3D] LevelWiring: No PressureButtons found!"); return; }
        if (gates.Length   == 0) { Debug.LogError("[Blob3D] LevelWiring: No Gates found!");           return; }

        // Single button/gate fallback
        if (buttons.Length == 1 && gates.Length == 1)
        {
            Wire(buttons[0], gates[0]);
            Debug.Log("[Blob3D] LevelWiring: Single Button → Gate connected.");
            return;
        }

        // Direct wiring: Button_N → Gate_N
        int wired = 0;
        for (int n = 1; n <= Mathf.Max(buttons.Length, gates.Length); n++)
        {
            var btn  = FindByName<PressureButton>(buttons, $"PressureButton_{n}");
            var gate = FindByName<Gate>(gates, $"Gate_{n}");
            if (btn != null && gate != null) { Wire(btn, gate); wired++; }
        }

        if (wired > 0)
            Debug.Log($"[Blob3D] LevelWiring: {wired} Button → Gate pair(s) connected.");
        else
            Debug.LogWarning("[Blob3D] LevelWiring: Could not find named Button/Gate pairs to wire!");
    }

    static void Wire(PressureButton btn, Gate gate)
    {
        btn.onPressed .AddListener(gate.Open);
        btn.onReleased.AddListener(gate.Close);
    }

    static T FindByName<T>(T[] objects, string name) where T : Component
    {
        foreach (var obj in objects)
            if (obj.gameObject.name == name) return obj;
        return null;
    }
}
