using UnityEngine;

/// <summary>
/// Wires scene-level connections at runtime so the SceneBuilder does not
/// need to rely on UnityEventTools / serialized persistent listeners.
/// Add this to the Managers GameObject.
/// </summary>
public class LevelWiring : MonoBehaviour
{
    void Start()
    {
        var button = Object.FindAnyObjectByType<PressureButton>();
        var gate   = Object.FindAnyObjectByType<Gate>();

        if (button == null) { Debug.LogError("[Blob3D] LevelWiring: PressureButton not found!"); return; }
        if (gate   == null) { Debug.LogError("[Blob3D] LevelWiring: Gate not found!");            return; }

        button.onPressed .AddListener(gate.Open);
        button.onReleased.AddListener(gate.Close);

        Debug.Log("[Blob3D] LevelWiring: Button → Gate connected.");
    }
}
