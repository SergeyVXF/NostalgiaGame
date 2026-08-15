using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// Central state machine for the Flooded Dam / Boiler House obstacle.
    /// Flow: close dam → fuel boiler (15 coal, auto-starts generator) →
    /// pull both levers within 1s → pump drains water (only if dam closed).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MiniVanDamObstacleController : MonoBehaviour
    {
        public enum DamState
        {
            Flooded = 0,
            ValveInserted = 1,
            DamClosed = 2,
            BoilerFueled = 3,
            GeneratorOn = 4,
            PumpRunningButDamOpen = 5,
            PumpRunning = 6,
            Drained = 7
        }

        [Header("Required Coal")]
        [Min(1)] public int RequiredCoal = 2;

        [Header("Drain")]
        public float DrainDurationSeconds = 10f;

        [Header("Levers")]
        [Min(0.1f)] public float LeverWindowSeconds = 1f;

        [Header("References (auto-wired by builder)")]
        public MiniVanDamGate DamGate;
        public MiniVanDamBoilerFurnace Boiler;
        public MiniVanDamPumpGenerator Generator;
        public MiniVanDamFloodWaterZone FloodZone;
        public MiniVanDamLever[] Levers;

        public DamState State { get; private set; } = DamState.Flooded;

        public bool ValveInserted { get; private set; }
        public bool DamClosed { get; private set; }
        public int CoalLoaded { get; private set; }
        public bool BoilerFueled => CoalLoaded >= RequiredCoal;
        public bool LeversPulled { get; private set; }
        public bool GeneratorOn { get; private set; }
        public bool PumpRunning { get; private set; }
        public bool Drained { get; private set; }

        private int leversDownCount;
        private float firstLeverTime = -1f;
        private float drainProgress;

        private void Update()
        {
            UpdateLeverWindow();
            UpdateDrain();
        }

        public void NotifyValveInserted()
        {
            if (ValveInserted)
            {
                return;
            }

            ValveInserted = true;
            RecomputeState();
        }

        public void NotifyDamClosed()
        {
            if (DamClosed)
            {
                return;
            }

            DamClosed = true;
            RecomputeState();
        }

        public void NotifyCoalLoaded(int total)
        {
            CoalLoaded = Mathf.Max(CoalLoaded, total);
            if (BoilerFueled && !GeneratorOn)
            {
                NotifyGeneratorStarted();
            }
            else
            {
                RecomputeState();
            }
        }

        public void NotifyLeverPulled(MiniVanDamLever lever)
        {
            if (!GeneratorOn || LeversPulled)
            {
                return;
            }

            leversDownCount++;
            if (leversDownCount == 1)
            {
                firstLeverTime = Time.time;
            }
            else if (leversDownCount >= 2)
            {
                float elapsed = Time.time - firstLeverTime;
                if (elapsed <= LeverWindowSeconds)
                {
                    LeversPulled = true;
                    PumpRunning = true;
                }
                else
                {
                    ResetLevers();
                }
            }

            RecomputeState();
        }

        private void UpdateLeverWindow()
        {
            if (LeversPulled || leversDownCount != 1)
            {
                return;
            }

            if (Time.time - firstLeverTime > LeverWindowSeconds)
            {
                ResetLevers();
            }
        }

        private void ResetLevers()
        {
            leversDownCount = 0;
            firstLeverTime = -1f;
            if (Levers == null)
            {
                return;
            }

            for (int i = 0; i < Levers.Length; i++)
            {
                if (Levers[i] != null)
                {
                    Levers[i].ResetLever();
                }
            }
        }

        public bool CanStartGenerator()
        {
            return BoilerFueled && !GeneratorOn;
        }

        public void NotifyGeneratorStarted()
        {
            if (GeneratorOn)
            {
                return;
            }

            GeneratorOn = true;
            if (Generator != null)
            {
                Generator.RefreshVisual();
            }

            RecomputeState();
        }

        private void UpdateDrain()
        {
            if (Drained)
            {
                return;
            }

            if (!PumpRunning || !DamClosed)
            {
                return;
            }

            drainProgress += Time.deltaTime / Mathf.Max(0.1f, DrainDurationSeconds);
            if (FloodZone != null)
            {
                FloodZone.SetDrainProgress(Mathf.Clamp01(drainProgress));
            }

            if (drainProgress >= 1f)
            {
                Drained = true;
                if (FloodZone != null)
                {
                    FloodZone.SetDrained();
                }

                RecomputeState();
            }
        }

        private void RecomputeState()
        {
            if (Drained)
            {
                State = DamState.Drained;
                return;
            }

            if (PumpRunning && DamClosed)
            {
                State = DamState.PumpRunning;
                return;
            }

            if (PumpRunning && !DamClosed)
            {
                State = DamState.PumpRunningButDamOpen;
                return;
            }

            if (GeneratorOn)
            {
                State = DamState.GeneratorOn;
                return;
            }

            if (BoilerFueled)
            {
                State = DamState.BoilerFueled;
                return;
            }

            if (DamClosed)
            {
                State = DamState.DamClosed;
                return;
            }

            if (ValveInserted)
            {
                State = DamState.ValveInserted;
                return;
            }

            State = DamState.Flooded;
        }

        public string GetStatusHint()
        {
            switch (State)
            {
                case DamState.Flooded:
                    return "Find the valve to close the dam";
                case DamState.ValveInserted:
                    return "Turn the valve to close the dam";
                case DamState.DamClosed:
                    return "Fuel the boiler with coal (" + CoalLoaded + "/" + RequiredCoal + ")";
                case DamState.BoilerFueled:
                    return "Boiler heating - generator starting";
                case DamState.GeneratorOn:
                    return "Pull both levers within 1 second";
                case DamState.PumpRunningButDamOpen:
                    return "Pump running but dam is open - close the dam";
                case DamState.PumpRunning:
                    return "Pumping water...";
                case DamState.Drained:
                    return "Channel drained - drive through";
                default:
                    return string.Empty;
            }
        }
    }
}
