using UnityEngine;

/// <summary>
/// Wires scene-level connections at runtime. Supports cross-wired dual gates:
///   PressureButton_1 → Gate_2  (blob1 opens the path for blob2)
///   PressureButton_2 → Gate_1  (blob2 opens the path for blob1)
/// Falls back to single button → single gate when only one of each exists.
/// Add this to the Managers GameObject.
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

        // Dual cross-wired: Button_1 → Gate_2, Button_2 → Gate_1
        PressureButton btn1 = FindByName<PressureButton>(buttons, "PressureButton_1");
        PressureButton btn2 = FindByName<PressureButton>(buttons, "PressureButton_2");
        Gate gate1 = FindByName<Gate>(gates, "Gate_1");
        Gate gate2 = FindByName<Gate>(gates, "Gate_2");

        int wired = 0;
        if (btn1 != null && gate2 != null) { Wire(btn1, gate2); wired++; }
        if (btn2 != null && gate1 != null) { Wire(btn2, gate1); wired++; }

        if (wired > 0)
            Debug.Log($"[Blob3D] LevelWiring: {wired} cross-wired Button → Gate pair(s) connected.");
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
