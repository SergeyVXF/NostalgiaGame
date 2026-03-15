using UnityEngine;

public class AddMenuComponent : MonoBehaviour
{
    [Header("Menu Type")]
    public bool isMainMenu = true;
    
    void Start()
    {
        // Добавляем компонент SimpleMenuSetup
        SimpleMenuSetup menuSetup = gameObject.AddComponent<SimpleMenuSetup>();
        menuSetup.isMainMenu = isMainMenu;
        menuSetup.gameSceneName = "SampleScene";
        menuSetup.mainMenuSceneName = "MainMenu";
        menuSetup.menuKey = KeyCode.Escape;
        
        // Удаляем этот компонент после добавления
        Destroy(this);
    }
} 