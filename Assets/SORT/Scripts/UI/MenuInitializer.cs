using UnityEngine;

public class MenuInitializer : MonoBehaviour
{
    [Header("Menu Type")]
    public bool isMainMenu = true;
    
    void Awake()
    {
        // Добавляем компонент MenuSetup
        MenuSetup menuSetup = gameObject.AddComponent<MenuSetup>();
        menuSetup.isMainMenu = isMainMenu;
        menuSetup.gameSceneName = "SampleScene";
        menuSetup.mainMenuSceneName = "MainMenu";
        menuSetup.menuKey = KeyCode.Escape;
    }
} 