using System;
using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// Per-transition route budget: switch a transition off completely or keep it
    /// between a minimum and a maximum number of uses on the generated route.
    /// </summary>
    [Serializable]
    public struct MiniVanPanelkaRouteTransitionBudget
    {
        [Tooltip("Выкл = маршрут вообще не использует этот переход.")]
        public bool Enabled;
        [Tooltip("Сколько таких переходов маршрут обязан поставить (если хватает этажей).")]
        [Min(0)] public int Min;
        [Tooltip("Больше этого числа переходов маршрут не поставит.")]
        [Min(0)] public int Max;

        public MiniVanPanelkaRouteTransitionBudget(bool enabled, int min, int max)
        {
            Enabled = enabled;
            Min = min;
            Max = max;
        }

        public static MiniVanPanelkaRouteTransitionBudget Unlimited
        {
            get { return new MiniVanPanelkaRouteTransitionBudget(true, 0, 99); }
        }

        public int AllowedMax
        {
            get { return Enabled ? Mathf.Max(0, Max) : 0; }
        }

        public int RequiredMin
        {
            get { return Mathf.Clamp(Min, 0, AllowedMax); }
        }

        public bool IsAllowed
        {
            get { return AllowedMax > 0; }
        }
    }
}
