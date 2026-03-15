using UnityEngine;
using UnityEditor;

public class VehicleSetup : EditorWindow
{
    [MenuItem("Tools/Vehicle/Create Hover Vehicle")]
    public static void CreateHoverVehicle()
    {
        // Создаем базовую модель машины
        GameObject vehicle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        vehicle.name = "HoverVehicle";
        
        // Масштабируем для машины
        vehicle.transform.localScale = new Vector3(2f, 0.5f, 4f);
        
        // Добавляем Rigidbody
        Rigidbody rb = vehicle.GetComponent<Rigidbody>();
        if (rb == null)
            rb = vehicle.AddComponent<Rigidbody>();
        
        // Настраиваем физику
        rb.useGravity = false;
        rb.linearDamping = 0.5f;
        rb.angularDamping = 2f;
        
        // Создаем сиденье для игрока
        GameObject seat = new GameObject("PlayerSeat");
        seat.transform.SetParent(vehicle.transform);
        seat.transform.localPosition = new Vector3(0, 0.3f, 0);
        seat.transform.localRotation = Quaternion.identity;
        
        // Создаем триггер для взаимодействия
        GameObject trigger = new GameObject("InteractionTrigger");
        trigger.transform.SetParent(vehicle.transform);
        trigger.transform.localPosition = Vector3.zero;
        
        BoxCollider triggerCollider = trigger.AddComponent<BoxCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.size = new Vector3(3f, 2f, 3f);
        
        // Добавляем скрипты
        vehicle.AddComponent<VehicleController>();
        trigger.AddComponent<VehicleInteraction>();
        
        // Настраиваем VehicleController
        VehicleController vc = vehicle.GetComponent<VehicleController>();
        vc.playerSeat = seat.transform;
        
        // Настраиваем VehicleInteraction
        VehicleInteraction vi = trigger.GetComponent<VehicleInteraction>();
        vi.playerLayer = LayerMask.GetMask("Player");
        
        // Создаем UI для взаимодействия
        CreateInteractionUI();
        
        // Выбираем созданный объект
        Selection.activeGameObject = vehicle;
        
        EditorUtility.DisplayDialog("Hover Vehicle Created",
            "Машина без колёс создана!\n\n" +
            "1. Настройте параметры в VehicleController\n" +
            "2. Создайте анимации: VehicleEnter, VehicleExit, VehicleDriving\n" +
            "3. Замените куб на свою модель машины\n" +
            "4. Настройте PlayerSeat позицию\n" +
            "5. Убедитесь, что игрок на слое 'Player'",
            "OK");
    }
    
    private static void CreateInteractionUI()
    {
        // Создаем Canvas если его нет
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("VehicleInteractionCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }
        
        // Создаем UI элемент
        GameObject uiElement = new GameObject("VehicleInteractionUI");
        uiElement.transform.SetParent(canvas.transform, false);
        
        UnityEngine.UI.Text text = uiElement.AddComponent<UnityEngine.UI.Text>();
        text.text = "Нажмите E для входа в машину";
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 24;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        
        RectTransform rect = uiElement.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.1f);
        rect.anchorMax = new Vector2(0.5f, 0.1f);
        rect.sizeDelta = new Vector2(300, 50);
        rect.anchoredPosition = Vector2.zero;
        
        // Скрываем UI
        uiElement.SetActive(false);
        
        // Подключаем к VehicleInteraction
        VehicleInteraction vi = FindObjectOfType<VehicleInteraction>();
        if (vi != null)
        {
            vi.interactionUI = uiElement;
        }
    }
} 