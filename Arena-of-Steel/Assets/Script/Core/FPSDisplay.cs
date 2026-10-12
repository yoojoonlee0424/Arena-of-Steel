using UnityEngine;

public class FPSDisplay : MonoBehaviour
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private float _deltaTime = 0.0f;

    void Update()
    {
        _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
    }

    void OnGUI()
    {
        int w = Screen.width, h = Screen.height;

        GUIStyle style = new GUIStyle();

        Rect rect = new Rect(0, 10, w, h * 5 / 100);
        style.alignment = TextAnchor.UpperRight;
        style.fontSize = h * 2 / 50; 
        style.normal.textColor = new Color(0.0f, 1.0f, 0.0f, 1.0f);

        float msec = _deltaTime * 1000.0f;
        float fps = 1.0f / _deltaTime;
        string text = string.Format("{0:0.0} ms ({1:0.} fps)", msec, fps);

        GUI.Label(rect, text, style);
    }
#endif
}
