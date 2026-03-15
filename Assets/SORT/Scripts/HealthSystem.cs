using UnityEngine;
using System;

public class HealthSystem : MonoBehaviour
{
    [Header("Настройки здоровья")]
    public float maxHealth = 100f;
    public float currentHealth;
    
    public event Action OnDeath;
    public event Action<float> OnHealthChanged;

    private void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        OnHealthChanged?.Invoke(currentHealth / maxHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        OnDeath?.Invoke();
    }
} 