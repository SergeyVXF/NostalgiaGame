using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
namespace Invector
{
    [InitializeOnLoad]
    public class vInvectorIcon
    {
        static Texture2D texturePanel;
        static List<int> markedObjects;
        static vInvectorIcon()
        {
            EditorApplication.hierarchyWindowItemOnGUI += ThirdPersonControllerIcon;
            EditorApplication.hierarchyWindowItemOnGUI += ThirPersonCameraIcon;
        }
        static void ThirPersonCameraIcon(int instanceId, Rect selectionRect)
        {
            GameObject go = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            if (go == null) return;

            var tpCamera = go.GetComponent<vCamera.vThirdPersonCamera>();
            if (tpCamera != null) DrawIcon("tp_camera", selectionRect);
        }

        static void ThirdPersonControllerIcon(int instanceId, Rect selectionRect)
        {
            GameObject go = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            if (go == null) return;

            var controller = go.GetComponent<Invector.vCharacterController.vThirdPersonController>();
            if (controller != null) DrawIcon("icon_v2", selectionRect);
        }


        private static void DrawIcon(string texName, Rect rect)
        {
            Rect r = new Rect(rect.x + rect.width - 16f, rect.y, 16f, 16f);
            GUI.DrawTexture(r, GetTex(texName));
        }

        private static Texture2D GetTex(string name)
        {
            // Сначала попробуем загрузить из Resources
            var icon = Resources.Load(name) as Texture2D;
            if (icon != null) return icon;
            
            // Если не найдено в Resources, попробуем загрузить из Textures
            var texturePath = "Assets/Invector-3rdPersonController/Basic Locomotion/Textures/" + name + ".png";
            icon = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (icon != null) return icon;
            
            // Если ничего не найдено, вернем null
            return null;
        }
    }
}