using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BlobRobotSphereSetupTool
{
    private const string RobotSpherePrefabPath = "Assets/RobotSphere/Assets/Prefab/robotSphere.prefab";

    [MenuItem("Blob3D/Setup/Assign RobotSphere To Blobs")]
    public static void AssignRobotSphereToBlobs()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RobotSpherePrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[Blob3D] RobotSphere prefab not found at path: {RobotSpherePrefabPath}");
            return;
        }

        BlobManager manager = Object.FindAnyObjectByType<BlobManager>();
        if (manager == null)
        {
            Debug.LogError("[Blob3D] BlobManager not found in the open scene.");
            return;
        }

        int assignedCount = 0;
        assignedCount += AssignToBlob(manager.blobOne, prefab, "Blob One");
        assignedCount += AssignToBlob(manager.blobTwo, prefab, "Blob Two");

        if (assignedCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            Debug.Log($"[Blob3D] RobotSphere assigned to {assignedCount} blob controller(s). Save the scene to keep changes.");
        }
        else
        {
            Debug.LogWarning("[Blob3D] No blob controllers were updated.");
        }
    }

    [MenuItem("Blob3D/Setup/Assign RobotSphere To Blobs", true)]
    private static bool ValidateAssignRobotSphereToBlobs()
    {
        return !EditorApplication.isPlaying;
    }

    private static int AssignToBlob(BlobController blob, GameObject prefab, string label)
    {
        if (blob == null)
        {
            Debug.LogWarning($"[Blob3D] {label} reference is missing on BlobManager.");
            return 0;
        }

        Undo.RecordObject(blob, "Assign RobotSphere Prefab");
        blob.robotSpherePrefab = prefab;

        if (blob.avatarRoot == null)
            blob.avatarRoot = blob.transform;

        blob.hideBlobMeshWhenAvatarPresent = true;
        EditorUtility.SetDirty(blob);

        Debug.Log($"[Blob3D] {label} updated: RobotSphere prefab assigned.");
        return 1;
    }
}
