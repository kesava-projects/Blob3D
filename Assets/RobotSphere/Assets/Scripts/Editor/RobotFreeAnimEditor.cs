using System;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RobotFreeAnim))]
public class RobotFreeAnimEditor : Editor
{
    // ** Clean Old Prefs ** To remove with Next update
    static string notificationSeenPref = "Notification.Seen";
    // -----------------------------------------------------

    static bool s_styleInit;
    static GUIStyle s_style = new GUIStyle();
    static Color s_defaultBgColor;

    readonly Rect m_bannerRect = new Rect(0, 50, 200, 200);
    Texture2D m_testImage;

    void OnEnable()
    {
        if (!s_styleInit)
        {
            if (EditorPrefs.HasKey(notificationSeenPref))
                EditorPrefs.DeleteKey(notificationSeenPref);

            s_style.border = new RectOffset(10, 10, 10, 10);
            s_defaultBgColor = GUI.backgroundColor;
            s_styleInit = true;
        }

        m_testImage = Resources.Load<Texture2D>("Notifications/Banners/test");
    }

    public override void OnInspectorGUI()
    {
        if (m_testImage != null)
            GUI.DrawTexture(m_bannerRect, m_testImage);

        s_style.wordWrap = true;
        DrawText("*Upgrades*\n", 35, new Color(1f, 0.2373f, 0f, 1f));
        GUILayout.Space(10);
        DrawText("Get More! Do More! With expansion Packs.\n", 15, Color.white);
        GUILayout.Space(40);

        DrawButton("Animation Expansion 1", onButtonClick: () =>
        {
            Application.OpenURL("https://assetstore.unity.com/packages/3d/characters/robots/robot-sphere-basic-pack-147055");
        }, Color.red);
        GUILayout.Space(5);

        DrawButton("Texture Expansion (PBR) 1", onButtonClick: () =>
        {
            Application.OpenURL("https://assetstore.unity.com/packages/3d/characters/robots/robot-sphere-texture-pack-1-160553");
        }, Color.green);
        GUILayout.Space(20);

        DrawDefaultInspector();
    }

    void DrawText(string text, int font, Color color)
    {
        s_style.fontSize = font;
        s_style.normal.textColor = color;
        EditorGUILayout.LabelField(text, s_style);
    }

    void DrawButton(string name, Action onButtonClick, Color color)
    {
        GUI.backgroundColor = color;
        if (GUILayout.Button(name))
            onButtonClick();
        GUI.backgroundColor = s_defaultBgColor;
    }
}
