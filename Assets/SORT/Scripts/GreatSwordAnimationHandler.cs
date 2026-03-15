using UnityEngine;

public class GreatSwordAnimationHandler : MonoBehaviour
{
    private void OnGreatSwordCastingStart()
    {
        Debug.Log("GreatSwordAnimationHandler: Анимация GreatSwordCasting началась");
        // Вызываем метод завершения, так как мы изменили логику
        OnGreatSwordCastingComplete();
    }

    private void OnGreatSwordCastingComplete()
    {
        Debug.Log("GreatSwordAnimationHandler: Анимация GreatSwordCasting завершена");
        FinalQuestDeathHandler.OnGreatSwordCastingComplete();
    }
} 