using UnityEngine;
public class Enemy : MonoBehaviour
{
    public System.Action OnDeath;
    [Header("Задержка перед исчезновением после смерти (сек)")]
    public float deathDelay = 2f;
    private bool isDead = false;

    public void Die()
    {
        if (isDead) return;
        isDead = true;
        Debug.Log("[Enemy] Die вызван");
        OnDeath?.Invoke();
        // Отключаем визуал, физику и т.д. если нужно
        // Например: GetComponent<Collider>().enabled = false;
        // Можно добавить анимацию смерти здесь
        StartCoroutine(DelayedDestroy());
    }

    private System.Collections.IEnumerator DelayedDestroy()
    {
        yield return new WaitForSeconds(deathDelay);
        Destroy(gameObject);
    }
} 