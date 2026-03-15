using UnityEngine;

public class KrapivaLayerSetup : MonoBehaviour
{
    private void Awake()
    {
        // Убедимся, что слой Krapiva существует
        int krapivaLayer = LayerMask.NameToLayer("Krapiva");
        if (krapivaLayer == -1)
        {
            Debug.LogError("Слой 'Krapiva' не найден! Пожалуйста, создайте его в настройках слоев Unity.");
            return;
        }

        // Получаем маску слоя игрока
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer == -1)
        {
            Debug.LogError("Слой 'Player' не найден! Пожалуйста, создайте его в настройках слоев Unity.");
            return;
        }

        // Настраиваем игнорирование коллизий между крапивой и игроком
        Physics.IgnoreLayerCollision(krapivaLayer, playerLayer, true);

        // Находим все объекты с тегом Weapon и настраиваем их коллизии
        GameObject[] weapons = GameObject.FindGameObjectsWithTag("Weapon");
        foreach (GameObject weapon in weapons)
        {
            // Убедимся, что у оружия есть коллайдер
            Collider weaponCollider = weapon.GetComponent<Collider>();
            if (weaponCollider != null)
            {
                // Отключаем игнорирование коллизий между крапивой и оружием
                Physics.IgnoreLayerCollision(krapivaLayer, weapon.layer, false);
            }
        }
    }
} 