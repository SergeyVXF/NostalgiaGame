using UnityEngine;

namespace MiniVanGame
{
    [DisallowMultipleComponent]
    public sealed class MiniVanFpsCounter : MonoBehaviour
    {
        private const float RefreshSeconds = 0.25f;
        private readonly Rect rect = new Rect(0f, 0f, 112f, 30f);
        private GUIStyle style;
        private string label = "FPS: --";
        private float accumulatedTime;
        private int accumulatedFrames;
        public int CurrentFps { get; private set; }

        private void Update()
        {
            accumulatedTime += Time.unscaledDeltaTime;
            accumulatedFrames++;
            if (accumulatedTime < RefreshSeconds)
            {
                return;
            }

            CurrentFps = Mathf.RoundToInt(accumulatedFrames / Mathf.Max(0.001f, accumulatedTime));
            label = "FPS: " + CurrentFps;
            accumulatedTime = 0f;
            accumulatedFrames = 0;
        }

        private void OnGUI()
        {
            EnsureStyle();
            Rect drawRect = rect;
            drawRect.x = (Screen.width - drawRect.width) * 0.5f;
            drawRect.y = 12f;
            GUI.Box(drawRect, label, style);
        }

        private void EnsureStyle()
        {
            if (style != null)
            {
                return;
            }

            style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = new Color(0.35f, 1f, 0.48f);
        }
    }
}
