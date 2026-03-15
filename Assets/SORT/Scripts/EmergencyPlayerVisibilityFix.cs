using UnityEngine;

public class EmergencyPlayerVisibilityFix : MonoBehaviour
{
    [ContextMenu("Force Show All Renderers and Reset Scale")]
    public void FixVisibility()
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            r.enabled = true;

        foreach (var t in GetComponentsInChildren<Transform>(true))
            t.localScale = Vector3.one;

        gameObject.SetActive(true);
    }
} 