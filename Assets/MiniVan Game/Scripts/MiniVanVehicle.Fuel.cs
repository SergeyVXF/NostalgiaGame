using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    public partial class MiniVanVehicle
    {
        [Header("Fuel")]
        public float FuelCapacityLiters = 100f;
        public float IdleFuelLitersPerMinute = 2f;
        public float ThrottleFuelLitersPerMinute = 4f;
        public float EngineLoadFuelLitersPerMinute = 3f;
        public float HighRpmFuelLitersPerMinute = 2f;
        public float WheelSlipFuelLitersPerMinute = 3f;
        public float LuggingFuelLitersPerMinute = 2.5f;
        public float FuelConsumptionMultiplier = 14f;

        public readonly NetworkVariable<float> FuelLiters = new NetworkVariable<float>(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Transform fuelGaugeNeedle;
        private TextMesh fuelGaugeCounter;
        private Renderer fuelGaugeFace;
        private Transform speedometerNeedle;
        private TextMesh speedometerCounter;
        private int displayedSpeedKph = -1;
        private TextMesh parkingBrakeIndicator;
        private TextMesh carBatteryIndicator;
        private Renderer carBatteryIndicatorFace;
        private Renderer carBatteryIndicatorTopLeft;
        private Renderer carBatteryIndicatorTopRight;
        private Transform engineTemperatureMercury;
        private Renderer engineTemperatureMercuryRenderer;
        private Renderer engineTemperatureBulbRenderer;
        private Renderer engineTemperatureFace;
        private TextMesh engineTemperatureCounter;
        private readonly List<Renderer> engineTemperatureIconRenderers = new List<Renderer>(8);
        private readonly List<TextMesh> engineTemperatureIconTexts = new List<TextMesh>(4);
        private Transform rpmFillColumn;
        private Renderer rpmFillRenderer;
        private Transform rpmSweetZone;
        private Renderer rpmSweetZoneRenderer;
        private Renderer rpmLedRenderer;
        private TextMesh rpmCounter;
        private TextMesh furnaceCounter;
        private GameObject furnaceFire;
        private MiniVanFuelFurnace fuelFurnace;
        private Material gaugeNormalMaterial;
        private Material gaugeEmptyMaterial;
        private Material gaugeDeadMaterial;
        private Material engineTemperatureNormalMaterial;
        private Material engineTemperatureHotMaterial;
        private Material engineTemperatureMercuryMaterial;
        private Material engineTemperatureMercuryHotMaterial;
        private Material rpmFillMaterial;
        private Material rpmSweetZoneMaterial;
        private Material rpmLedMaterial;
        private static readonly Color DashboardDeadColor = new Color(0.14f, 0.14f, 0.145f, 1f);
        private static readonly Color DashboardDeadTextColor = new Color(0.22f, 0.22f, 0.23f, 1f);
        private static readonly Color DashboardLiveTextColor = new Color(0.9f, 0.85f, 0.65f, 1f);
        private float fuelBurnPulseUntil;
        private const float EngineTemperatureThermometerX = 0.105f;
        private const float EngineTemperatureMercuryMinHeight = 0.020f;
        private const float EngineTemperatureMercuryMaxHeight = 0.335f;
        private const float EngineTemperatureMercuryDefaultZ = -0.091f;
        private const float RpmTrackBottomY = -0.16f;
        private const float RpmTrackTopY = 0.18f;
        private const float RpmFillMinHeight = 0.018f;
        private const float RpmFillMaxHeight = 0.34f;
        private float engineTemperatureMercuryBaseX = EngineTemperatureThermometerX;
        private float engineTemperatureMercuryBaseZ = EngineTemperatureMercuryDefaultZ;
        private float engineTemperatureMercuryBottomY = -0.145f;

        public MiniVanFuelFurnace FuelFurnace => fuelFurnace;

        [ContextMenu("Rebuild Fuel Visuals")]
        public void RebuildFuelVisuals()
        {
            RemoveFuelVisual("Fuel Gauge");
            RemoveFuelVisual("Speedometer");
            RemoveFuelVisual("Parking Brake Indicator");
            RemoveFuelVisual("Car Battery Indicator");
            RemoveFuelVisual("Engine Temperature Gauge");
            RemoveFuelVisual("RPM Gauge");
            RemoveFuelVisual("Fuel Furnace");
            BuildFuelGauge();
            BuildSpeedometer();
            BuildParkingBrakeIndicator();
            BuildCarBatteryIndicator();
            BuildEngineTemperatureGauge();
            BuildRpmGauge();
            BuildFuelFurnace();
            UpdateFuelSystemVisuals();
        }

        public void EnsureDashboardIndicatorPrefabVisuals()
        {
            if (transform.Find("Parking Brake Indicator") == null)
            {
                BuildParkingBrakeIndicator();
            }

            if (transform.Find("Car Battery Indicator") == null)
            {
                BuildCarBatteryIndicator();
            }

            if (transform.Find("Engine Temperature Gauge") == null)
            {
                BuildEngineTemperatureGauge();
            }

            if (transform.Find("RPM Gauge") == null)
            {
                BuildRpmGauge();
            }
            else
            {
                CacheRpmGauge(transform.Find("RPM Gauge"));
            }

            UpdateFuelSystemVisuals();
        }

        private void RemoveFuelVisual(string childName)
        {
            Transform child = transform.Find(childName);
            if (child == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }

        private void UpdateFuelConsumption()
        {
            if (!IsServer || !EngineOn.Value || body == null)
            {
                return;
            }

            Vector3 planarVelocity = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            ServerConsumeFuelForStep(planarVelocity.magnitude, Time.fixedDeltaTime);
        }

        public void ServerConsumeFuelForStep(float planarSpeedMetersPerSecond, float deltaTime)
        {
            if (!IsServer || !EngineOn.Value)
            {
                return;
            }

            float speedKph = Mathf.Max(0f, planarSpeedMetersPerSecond) * 3.6f;
            MiniVanGear gear = (MiniVanGear)CurrentGear.Value;
            float litersPerMinute = CalculateFuelBurnLitersPerMinute(
                speedKph,
                throttleInput,
                EngineRpm.Value,
                EngineLoad.Value,
                WheelSlip.Value,
                gear);
            float consumed = litersPerMinute * Mathf.Max(0f, deltaTime) / 60f;

            if (consumed <= 0f)
            {
                return;
            }

            FuelLiters.Value = Mathf.Max(0f, FuelLiters.Value - consumed);
            if (FuelLiters.Value <= 0.001f)
            {
                FuelLiters.Value = 0f;
                EngineOn.Value = false;
                EngineLoad.Value = 0f;
                throttleInput = 0f;
            }
        }

        public float CalculateFuelBurnLitersPerMinute(float speedKph, float throttle, float rpm,
            float engineLoad, float wheelSlip, MiniVanGear gear)
        {
            float resolvedThrottle = Mathf.Clamp01(throttle);
            float resolvedLoad = Mathf.Clamp01(engineLoad);
            float resolvedSlip = Mathf.Clamp01(wheelSlip);
            float rpm01 = Mathf.InverseLerp(IdleRpm, Mathf.Max(IdleRpm + 1f, RedlineRpm), rpm);
            float highRpm = rpm01 * rpm01;

            float minimumUsefulSpeed = GetMinimumUsefulSpeedKph(gear);
            float lugging = minimumUsefulSpeed > 0.01f && resolvedThrottle > 0.1f
                ? Mathf.InverseLerp(minimumUsefulSpeed, 0f, Mathf.Max(0f, speedKph))
                : 0f;

            float workingFuel = resolvedThrottle * Mathf.Max(0f, ThrottleFuelLitersPerMinute) +
                                resolvedLoad * Mathf.Max(0f, EngineLoadFuelLitersPerMinute) +
                                highRpm * Mathf.Max(0f, HighRpmFuelLitersPerMinute) +
                                resolvedSlip * Mathf.Max(0f, WheelSlipFuelLitersPerMinute) +
                                lugging * Mathf.Max(0f, LuggingFuelLitersPerMinute);

            float baseRate = Mathf.Max(0f, IdleFuelLitersPerMinute) + workingFuel * GetFuelGearMultiplier(gear);
            return baseRate * Mathf.Max(0f, FuelConsumptionMultiplier);
        }

        private static float GetFuelGearMultiplier(MiniVanGear gear)
        {
            switch (gear)
            {
                case MiniVanGear.Reverse:
                    return 1.15f;
                case MiniVanGear.First:
                    return 1.20f;
                case MiniVanGear.Second:
                    return 1.05f;
                case MiniVanGear.Third:
                    return 0.90f;
                case MiniVanGear.Fourth:
                    return 0.80f;
                case MiniVanGear.Fifth:
                    return 0.72f;
                default:
                    return 1f;
            }
        }

        public bool ServerTryAddFuel(MiniVanInventoryItem item, out float addedLiters)
        {
            addedLiters = 0f;
            if (!IsServer)
            {
                return false;
            }

            float value = MiniVanFuelRules.GetFuelLiters(item);
            float capacity = Mathf.Max(1f, FuelCapacityLiters);
            float room = capacity - FuelLiters.Value;
            if (value <= 0f || room <= 0.001f)
            {
                return false;
            }

            addedLiters = Mathf.Min(value, room);
            FuelLiters.Value = Mathf.Clamp(FuelLiters.Value + value, 0f, capacity);
            FuelBurnFeedbackClientRpc();
            return true;
        }

        [ClientRpc]
        private void FuelBurnFeedbackClientRpc()
        {
            fuelBurnPulseUntil = Time.time + 1.4f;
        }

        private void EnsureFuelSystemVisuals()
        {
            Transform existingGauge = transform.Find("Fuel Gauge");
            if (existingGauge == null)
            {
                BuildFuelGauge();
            }
            else
            {
                CacheFuelGauge(existingGauge);
            }

            Transform existingSpeedometer = transform.Find("Speedometer");
            if (existingSpeedometer == null)
            {
                BuildSpeedometer();
            }
            else
            {
                CacheSpeedometer(existingSpeedometer);
            }

            Transform existingParkingBrakeIndicator = transform.Find("Parking Brake Indicator");
            if (existingParkingBrakeIndicator == null)
            {
                BuildParkingBrakeIndicator();
            }
            else
            {
                CacheParkingBrakeIndicator(existingParkingBrakeIndicator);
            }

            Transform existingCarBatteryIndicator = transform.Find("Car Battery Indicator");
            if (existingCarBatteryIndicator == null)
            {
                BuildCarBatteryIndicator();
            }
            else
            {
                CacheCarBatteryIndicator(existingCarBatteryIndicator);
            }

            Transform existingEngineTemperatureGauge = transform.Find("Engine Temperature Gauge");
            if (existingEngineTemperatureGauge == null)
            {
                BuildEngineTemperatureGauge();
            }
            else
            {
                CacheEngineTemperatureGauge(existingEngineTemperatureGauge);
            }

            Transform existingRpmGauge = transform.Find("RPM Gauge");
            if (existingRpmGauge == null)
            {
                BuildRpmGauge();
            }
            else
            {
                CacheRpmGauge(existingRpmGauge);
            }

            Transform existingFurnace = transform.Find("Fuel Furnace");
            if (existingFurnace == null)
            {
                BuildFuelFurnace();
            }
            else
            {
                CacheFuelFurnace(existingFurnace);
            }

            UpdateFuelSystemVisuals();
        }

        private void BuildFuelGauge()
        {
            GameObject root = new GameObject("Fuel Gauge");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(-1.512f, 1.2781f, 3.2119f);
            root.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
            root.transform.localScale = Vector3.one * 0.4f;

            Material metal = GetFuelMaterial("GaugeMetal", "Fuel Gauge Metal", new Color(0.055f, 0.06f, 0.065f));
            Material edge = GetFuelMaterial("GaugeEdge", "Fuel Gauge Edge", new Color(0.11f, 0.105f, 0.095f));
            gaugeNormalMaterial = GetFuelMaterial("GaugeFace", "Fuel Gauge Face", new Color(0.035f, 0.038f, 0.04f));
            gaugeEmptyMaterial = GetFuelMaterial("GaugeEmpty", "Fuel Gauge Empty", new Color(0.34f, 0.018f, 0.015f));
            Material cream = GetFuelMaterial("GaugeMarks", "Fuel Gauge Marks", new Color(0.86f, 0.82f, 0.62f), true);
            Material red = GetFuelMaterial("GaugeRed", "Fuel Gauge Red", new Color(0.78f, 0.04f, 0.025f), true);
            Material amber = GetFuelMaterial("GaugeAmber", "Fuel Gauge Amber", new Color(1f, 0.58f, 0.06f), true);
            Material green = GetFuelMaterial("GaugeGreen", "Fuel Gauge Green", new Color(0.24f, 0.82f, 0.16f), true);

            CreateFuelCube(root.transform, "Bolted Back Plate", Vector3.zero, new Vector3(0.78f, 0.70f, 0.09f), metal);
            CreateFuelCylinder(root.transform, "Outer Bezel", new Vector3(0f, 0.015f, -0.075f),
                new Vector3(0.64f, 0.045f, 0.64f), edge, Quaternion.Euler(90f, 0f, 0f));
            GameObject face = CreateFuelCylinder(root.transform, "Dial Face", new Vector3(0f, 0.015f, -0.125f),
                new Vector3(0.54f, 0.025f, 0.54f), gaugeNormalMaterial, Quaternion.Euler(90f, 0f, 0f));
            fuelGaugeFace = face.GetComponent<Renderer>();

            Vector2[] boltPositions =
            {
                new Vector2(-0.31f, -0.27f), new Vector2(0.31f, -0.27f),
                new Vector2(-0.31f, 0.27f), new Vector2(0.31f, 0.27f)
            };
            for (int i = 0; i < boltPositions.Length; i++)
            {
                CreateFuelCylinder(root.transform, "Bezel Bolt " + i,
                    new Vector3(boltPositions[i].x, boltPositions[i].y, -0.075f),
                    new Vector3(0.052f, 0.025f, 0.052f), edge, Quaternion.Euler(90f, 0f, 0f));
            }

            for (int i = 0; i <= 12; i++)
            {
                float t = i / 12f;
                float angle = Mathf.Lerp(150f, 30f, t) * Mathf.Deg2Rad;
                Vector3 p = new Vector3(Mathf.Cos(angle) * 0.225f, Mathf.Sin(angle) * 0.225f - 0.015f, -0.16f);
                Material tickMaterial = t < 0.25f ? red : t < 0.72f ? amber : green;
                GameObject tick = CreateFuelCube(root.transform, "Tick " + i, p,
                    new Vector3(0.016f, i % 3 == 0 ? 0.068f : 0.050f, 0.014f), tickMaterial);
                tick.transform.localRotation = Quaternion.Euler(0f, 0f, 90f - angle * Mathf.Rad2Deg);
            }

            CreateFuelText(root.transform, "Zero", "0", new Vector3(-0.21f, -0.09f, -0.18f), 0.014f, cream);
            CreateFuelText(root.transform, "Half", "50", new Vector3(0f, 0.12f, -0.18f), 0.012f, cream);
            CreateFuelText(root.transform, "Full", "100", new Vector3(0.20f, -0.09f, -0.18f), 0.011f, cream);
            CreateFuelText(root.transform, "Fuel Label", "FUEL", new Vector3(0f, -0.005f, -0.18f), 0.011f, cream);

            GameObject pivot = new GameObject("Needle Pivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(0f, -0.09f, -0.16f);
            fuelGaugeNeedle = pivot.transform;
            CreateFuelCube(pivot.transform, "Needle", new Vector3(0f, 0.105f, 0f),
                new Vector3(0.025f, 0.22f, 0.018f), cream);

            CreateFuelCube(root.transform, "Counter Recess", new Vector3(0f, -0.245f, -0.155f),
                new Vector3(0.32f, 0.105f, 0.025f), edge);

            fuelGaugeCounter = CreateFuelText(root.transform, "Liter Counter", "100 L",
                new Vector3(0f, -0.245f, -0.18f), 0.014f, cream);
            CreateFuelCylinder(root.transform, "Fuel Warning Socket", new Vector3(0.30f, 0.19f, -0.08f),
                new Vector3(0.075f, 0.025f, 0.075f), edge, Quaternion.Euler(90f, 0f, 0f));
            CreateFuelText(root.transform, "Empty Lamp", "F", new Vector3(0.30f, 0.19f, -0.18f), 0.014f, red);
        }

        private void BuildSpeedometer()
        {
            GameObject root = new GameObject("Speedometer");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(-1.92f, 1.2781f, 3.2119f);
            root.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
            root.transform.localScale = Vector3.one * 0.4f;

            Material metal = GetFuelMaterial("GaugeMetal", "Speedometer Metal", new Color(0.055f, 0.06f, 0.065f));
            Material edge = GetFuelMaterial("GaugeEdge", "Speedometer Edge", new Color(0.11f, 0.105f, 0.095f));
            Material faceMaterial = GetFuelMaterial("GaugeFace", "Speedometer Face", new Color(0.035f, 0.038f, 0.04f));
            Material cream = GetFuelMaterial("GaugeMarks", "Speedometer Marks", new Color(0.86f, 0.82f, 0.62f), true);
            Material red = GetFuelMaterial("GaugeRed", "Speedometer Red", new Color(0.78f, 0.04f, 0.025f), true);

            CreateFuelCube(root.transform, "Bolted Back Plate", Vector3.zero, new Vector3(0.78f, 0.70f, 0.09f), metal);
            CreateFuelCylinder(root.transform, "Outer Bezel", new Vector3(0f, 0.015f, -0.075f),
                new Vector3(0.64f, 0.045f, 0.64f), edge, Quaternion.Euler(90f, 0f, 0f));
            CreateFuelCylinder(root.transform, "Dial Face", new Vector3(0f, 0.015f, -0.125f),
                new Vector3(0.54f, 0.025f, 0.54f), faceMaterial, Quaternion.Euler(90f, 0f, 0f));

            Vector2[] boltPositions =
            {
                new Vector2(-0.31f, -0.27f), new Vector2(0.31f, -0.27f),
                new Vector2(-0.31f, 0.27f), new Vector2(0.31f, 0.27f)
            };
            for (int i = 0; i < boltPositions.Length; i++)
            {
                CreateFuelCylinder(root.transform, "Bezel Bolt " + i,
                    new Vector3(boltPositions[i].x, boltPositions[i].y, -0.075f),
                    new Vector3(0.052f, 0.025f, 0.052f), edge, Quaternion.Euler(90f, 0f, 0f));
            }

            for (int i = 0; i <= 20; i++)
            {
                float t = i / 20f;
                float angleDegrees = Mathf.Lerp(220f, -40f, t);
                float angle = angleDegrees * Mathf.Deg2Rad;
                bool major = i % 2 == 0;
                Vector3 position = new Vector3(
                    Mathf.Cos(angle) * 0.225f,
                    Mathf.Sin(angle) * 0.225f - 0.015f,
                    -0.16f);
                GameObject tick = CreateFuelCube(root.transform, "Tick " + (i * 10), position,
                    new Vector3(major ? 0.014f : 0.010f, major ? 0.060f : 0.040f, 0.014f),
                    i >= 18 ? red : cream);
                tick.transform.localRotation = Quaternion.Euler(0f, 0f, 90f - angleDegrees);

                bool showLabel = i <= 2 || i % 2 == 0;
                if (showLabel)
                {
                    Vector3 labelPosition = new Vector3(
                        Mathf.Cos(angle) * 0.158f,
                        Mathf.Sin(angle) * 0.158f - 0.016f,
                        -0.18f);
                    CreateFuelText(root.transform, "Speed Label " + (i * 10), (i * 10).ToString(),
                        labelPosition, i >= 10 ? 0.0084f : 0.0096f, cream);
                }
            }

            CreateFuelText(root.transform, "Speed Unit", "KM/H", new Vector3(0f, -0.025f, -0.18f), 0.008f, cream);

            GameObject pivot = new GameObject("Needle Pivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(0f, -0.09f, -0.16f);
            speedometerNeedle = pivot.transform;
            CreateFuelCube(pivot.transform, "Needle", new Vector3(0f, 0.105f, 0f),
                new Vector3(0.020f, 0.22f, 0.018f), cream);

            CreateFuelCube(root.transform, "Counter Recess", new Vector3(0f, -0.245f, -0.155f),
                new Vector3(0.35f, 0.105f, 0.025f), edge);
            speedometerCounter = CreateFuelText(root.transform, "Speed Counter", "0 km/h",
                new Vector3(0f, -0.245f, -0.18f), 0.0105f, cream);
        }

        private void BuildParkingBrakeIndicator()
        {
            GameObject root = new GameObject("Parking Brake Indicator");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(-1.268f, 1.2781f, 3.2119f);
            root.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
            root.transform.localScale = Vector3.one * 0.4f;

            Material metal = GetFuelMaterial("GaugeMetal", "Parking Brake Metal", new Color(0.055f, 0.06f, 0.065f));
            Material edge = GetFuelMaterial("GaugeEdge", "Parking Brake Edge", new Color(0.11f, 0.105f, 0.095f));
            Material face = GetFuelMaterial("GaugeFace", "Parking Brake Face", new Color(0.018f, 0.020f, 0.021f));
            Material red = GetFuelMaterial("GaugeRed", "Parking Brake Red", new Color(0.78f, 0.04f, 0.025f), true);

            CreateFuelCube(root.transform, "Back Plate", Vector3.zero, new Vector3(0.34f, 0.30f, 0.09f), metal);
            CreateFuelCylinder(root.transform, "Outer Bezel", new Vector3(0f, 0f, -0.075f),
                new Vector3(0.145f, 0.035f, 0.145f), edge, Quaternion.Euler(90f, 0f, 0f));
            CreateFuelCylinder(root.transform, "Lamp Face", new Vector3(0f, 0f, -0.115f),
                new Vector3(0.115f, 0.022f, 0.115f), face, Quaternion.Euler(90f, 0f, 0f));

            parkingBrakeIndicator = CreateFuelText(root.transform, "Brake Icon", "(!)",
                new Vector3(0f, 0f, -0.18f), 0.024f, red);
        }

        private void BuildCarBatteryIndicator()
        {
            GameObject root = new GameObject("Car Battery Indicator");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(-1.024f, 1.2781f, 3.2119f);
            root.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
            root.transform.localScale = Vector3.one * 0.4f;

            Material metal = GetFuelMaterial("GaugeMetal", "Car Battery Metal", new Color(0.055f, 0.06f, 0.065f));
            Material edge = GetFuelMaterial("GaugeEdge", "Car Battery Edge", new Color(0.11f, 0.105f, 0.095f));
            Material face = GetFuelMaterial("GaugeFace", "Car Battery Face", new Color(0.018f, 0.020f, 0.021f));
            Material green = GetFuelMaterial("GaugeGreen", "Car Battery Green", new Color(0.08f, 1f, 0.18f), true);
            Material black = GetFuelMaterial("GaugeTextBlack", "Car Battery Icon Black", Color.black);

            CreateFuelCube(root.transform, "Back Plate", Vector3.zero, new Vector3(0.38f, 0.30f, 0.09f), metal);
            GameObject lampFace = CreateFuelCube(root.transform, "Lamp Face", new Vector3(0f, 0f, -0.12f),
                new Vector3(0.28f, 0.18f, 0.028f), face);
            carBatteryIndicatorFace = lampFace.GetComponent<Renderer>();
            carBatteryIndicatorTopLeft = CreateFuelCube(root.transform, "Battery Top Left", new Vector3(-0.075f, 0.105f, -0.15f),
                new Vector3(0.052f, 0.036f, 0.025f), face).GetComponent<Renderer>();
            carBatteryIndicatorTopRight = CreateFuelCube(root.transform, "Battery Top Right", new Vector3(0.075f, 0.105f, -0.15f),
                new Vector3(0.052f, 0.036f, 0.025f), face).GetComponent<Renderer>();
            carBatteryIndicator = CreateFuelText(root.transform, "Battery Icon", "-  +",
                new Vector3(0f, -0.005f, -0.18f), 0.020f, black);
        }

        private void BuildEngineTemperatureGauge()
        {
            GameObject root = new GameObject("Engine Temperature Gauge");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(-0.77f, 1.2781f, 3.2119f);
            root.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
            root.transform.localScale = Vector3.one * 0.4f;

            Material metal = GetFuelMaterial("GaugeMetal", "Engine Temperature Metal", new Color(0.055f, 0.06f, 0.065f));
            Material edge = GetFuelMaterial("GaugeEdge", "Engine Temperature Edge", new Color(0.11f, 0.105f, 0.095f));
            engineTemperatureNormalMaterial = GetFuelMaterial("GaugeFace", "Engine Temperature Face", new Color(0.018f, 0.020f, 0.021f));
            engineTemperatureHotMaterial = GetFuelMaterial("GaugeRed", "Engine Temperature Hot Face", new Color(0.78f, 0.04f, 0.025f), true);
            Material cream = GetFuelMaterial("GaugeMarks", "Engine Temperature Marks", new Color(0.86f, 0.82f, 0.62f), true);
            // RedEmmisive = true red. Do NOT use GaugeRed here: that asset has white base color.
            engineTemperatureMercuryMaterial = GetFuelMaterial("RedEmmisive", "Engine Temperature Mercury", new Color(0.9f, 0.04f, 0.025f), true);
            engineTemperatureMercuryHotMaterial = CreateFuelMaterial("Engine Temperature Mercury Hot", Color.white, true);
            Material tempIcon = GetFuelMaterial("EngineTempIcon", "Engine Temperature Icon", new Color(0.9f, 0.04f, 0.025f), true);

            CreateFuelCube(root.transform, "Back Plate", Vector3.zero, new Vector3(0.56f, 0.54f, 0.09f), metal);
            GameObject face = CreateFuelCube(root.transform, "Gauge Face", new Vector3(0f, 0f, -0.12f),
                new Vector3(0.47f, 0.44f, 0.028f), engineTemperatureNormalMaterial);
            engineTemperatureFace = face.GetComponent<Renderer>();

            BuildEngineTemperatureIcon(root.transform, tempIcon);

            CreateFuelCube(root.transform, "Thermometer Tube Outer", new Vector3(EngineTemperatureThermometerX, 0.015f, -0.155f),
                new Vector3(0.075f, 0.37f, 0.018f), cream);
            CreateFuelCube(root.transform, "Thermometer Tube Inner", new Vector3(EngineTemperatureThermometerX, 0.015f, -0.18f),
                new Vector3(0.038f, 0.335f, 0.020f), engineTemperatureNormalMaterial);
            CreateFuelCylinder(root.transform, "Thermometer Bulb Outer", new Vector3(EngineTemperatureThermometerX, -0.19f, -0.155f),
                new Vector3(0.075f, 0.018f, 0.075f), cream, Quaternion.Euler(90f, 0f, 0f));
            GameObject bulb = CreateFuelCylinder(root.transform, "Thermometer Bulb Inner", new Vector3(EngineTemperatureThermometerX, -0.19f, -0.18f),
                new Vector3(0.044f, 0.020f, 0.044f), engineTemperatureMercuryMaterial, Quaternion.Euler(90f, 0f, 0f));
            engineTemperatureBulbRenderer = bulb.GetComponent<Renderer>();

            GameObject mercury = CreateFuelCube(root.transform, "Mercury Column",
                new Vector3(EngineTemperatureThermometerX, -0.135f, EngineTemperatureMercuryDefaultZ),
                new Vector3(0.022f, EngineTemperatureMercuryMinHeight, 0.018f), engineTemperatureMercuryMaterial);
            engineTemperatureMercury = mercury.transform;
            engineTemperatureMercuryRenderer = mercury.GetComponent<Renderer>();
            CacheEngineTemperatureMercuryBasePose(engineTemperatureMercury);
            CacheEngineTemperatureIconParts(root.transform);

            for (int i = 0; i <= 10; i++)
            {
                float y = Mathf.Lerp(-0.14f, 0.19f, i / 10f);
                CreateFuelCube(root.transform, "Temp Tick " + i, new Vector3(0.205f, y, -0.18f),
                    new Vector3(i % 5 == 0 ? 0.075f : 0.052f, 0.010f, 0.014f), cream);
            }

            CreateFuelText(root.transform, "Temp 130", "130", new Vector3(0.285f, 0.19f, -0.18f), 0.010f, cream);
            CreateFuelText(root.transform, "Temp 0", "0", new Vector3(0.275f, -0.145f, -0.18f), 0.012f, cream);
            CreateFuelText(root.transform, "Temp Label", "TEMP", new Vector3(0.105f, -0.235f, -0.18f), 0.0085f, cream);
            engineTemperatureCounter = CreateFuelText(root.transform, "Temp Counter", "0 C",
                new Vector3(0.105f, 0.245f, -0.18f), 0.0085f, cream);
        }

        /// <summary>
        /// Narrow vertical sweet-spot RPM strip — same idea as the driver HUD tach:
        /// fill = rpm01, green window = sweet band, color shifts below/in/above the zone.
        /// </summary>
        private void BuildRpmGauge()
        {
            GameObject root = new GameObject("RPM Gauge");
            root.transform.SetParent(transform, false);
            // Between battery indicator (~-1.02) and temperature gauge (~-0.77).
            root.transform.localPosition = new Vector3(-0.90f, 1.2781f, 3.2119f);
            root.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
            root.transform.localScale = Vector3.one * 0.34f;

            Material metal = GetFuelMaterial("GaugeMetal", "RPM Gauge Metal", new Color(0.055f, 0.06f, 0.065f));
            Material cream = GetFuelMaterial("GaugeMarks", "RPM Gauge Marks", new Color(0.86f, 0.82f, 0.62f), true);
            Material face = GetFuelMaterial("GaugeFace", "RPM Gauge Face", new Color(0.02f, 0.022f, 0.024f));
            rpmFillMaterial = CreateFuelMaterial("RPM Fill", new Color(0.2f, 0.9f, 0.28f), true);
            rpmSweetZoneMaterial = CreateFuelMaterial("RPM Sweet Zone", new Color(0.25f, 1f, 0.45f, 0.55f), true);
            rpmLedMaterial = CreateFuelMaterial("RPM LED", new Color(0.08f, 0.12f, 0.08f), true);

            CreateFuelCube(root.transform, "Back Plate", Vector3.zero, new Vector3(0.24f, 0.54f, 0.09f), metal);
            CreateFuelCube(root.transform, "Gauge Face", new Vector3(0f, 0f, -0.12f),
                new Vector3(0.18f, 0.44f, 0.028f), face);

            // Empty track.
            CreateFuelCube(root.transform, "Track", new Vector3(0f, 0.01f, -0.17f),
                new Vector3(0.055f, RpmFillMaxHeight + 0.02f, 0.016f), face);

            GameObject fill = CreateFuelCube(root.transform, "Fill Column",
                new Vector3(0f, RpmTrackBottomY + RpmFillMinHeight * 0.5f, -0.19f),
                new Vector3(0.034f, RpmFillMinHeight, 0.018f), rpmFillMaterial);
            rpmFillColumn = fill.transform;
            rpmFillRenderer = fill.GetComponent<Renderer>();

            // Sweet-spot window (height/position updated every frame from SweetSpot params).
            GameObject zone = CreateFuelCube(root.transform, "Sweet Zone",
                new Vector3(0f, 0.01f, -0.185f),
                new Vector3(0.07f, 0.08f, 0.01f), rpmSweetZoneMaterial);
            rpmSweetZone = zone.transform;
            rpmSweetZoneRenderer = zone.GetComponent<Renderer>();

            GameObject led = CreateFuelCube(root.transform, "Sweet LED",
                new Vector3(0f, 0.235f, -0.18f),
                new Vector3(0.05f, 0.028f, 0.016f), rpmLedMaterial);
            rpmLedRenderer = led.GetComponent<Renderer>();

            CreateFuelText(root.transform, "RPM Label", "RPM", new Vector3(0f, -0.235f, -0.18f), 0.0085f, cream);
            rpmCounter = CreateFuelText(root.transform, "RPM Counter", "0",
                new Vector3(0f, 0.275f, -0.18f), 0.0075f, cream);
        }

        private void CacheRpmGauge(Transform root)
        {
            if (root == null)
            {
                return;
            }

            Transform fill = root.Find("Fill Column");
            rpmFillColumn = fill;
            rpmFillRenderer = fill != null ? fill.GetComponent<Renderer>() : null;
            Transform zone = root.Find("Sweet Zone");
            rpmSweetZone = zone;
            rpmSweetZoneRenderer = zone != null ? zone.GetComponent<Renderer>() : null;
            Transform led = root.Find("Sweet LED");
            rpmLedRenderer = led != null ? led.GetComponent<Renderer>() : null;
            Transform counter = root.Find("RPM Counter");
            rpmCounter = counter != null ? counter.GetComponent<TextMesh>() : null;

            if (rpmFillMaterial == null)
            {
                rpmFillMaterial = CreateFuelMaterial("RPM Fill", new Color(0.2f, 0.9f, 0.28f), true);
            }

            if (rpmSweetZoneMaterial == null)
            {
                rpmSweetZoneMaterial = CreateFuelMaterial("RPM Sweet Zone", new Color(0.25f, 1f, 0.45f), true);
            }

            if (rpmLedMaterial == null)
            {
                rpmLedMaterial = CreateFuelMaterial("RPM LED", new Color(0.08f, 0.12f, 0.08f), true);
            }

            if (rpmFillRenderer != null)
            {
                rpmFillRenderer.sharedMaterial = rpmFillMaterial;
            }

            if (rpmSweetZoneRenderer != null)
            {
                rpmSweetZoneRenderer.sharedMaterial = rpmSweetZoneMaterial;
            }

            if (rpmLedRenderer != null)
            {
                rpmLedRenderer.sharedMaterial = rpmLedMaterial;
            }
        }

        private void BuildEngineTemperatureIcon(Transform root, Material material)
        {
            const float x = -0.115f;
            const float z = -0.18f;
            CreateFuelText(root, "Engine Heat Waves", "~~~", new Vector3(x, 0.145f, z), 0.016f, material);
            CreateFuelCube(root, "Engine Icon Body", new Vector3(x, 0.015f, z),
                new Vector3(0.150f, 0.095f, 0.018f), material);
            CreateFuelCube(root, "Engine Icon Left Neck", new Vector3(x - 0.092f, 0.015f, z),
                new Vector3(0.030f, 0.060f, 0.018f), material);
            CreateFuelCube(root, "Engine Icon Left Cap", new Vector3(x - 0.120f, 0.015f, z),
                new Vector3(0.020f, 0.095f, 0.018f), material);
            CreateFuelCube(root, "Engine Icon Right Neck", new Vector3(x + 0.092f, 0.015f, z),
                new Vector3(0.040f, 0.055f, 0.018f), material);
            CreateFuelCube(root, "Engine Icon Top Cap", new Vector3(x, 0.085f, z),
                new Vector3(0.082f, 0.018f, 0.018f), material);
            CreateFuelText(root, "Engine Icon Wavy Coolant", "~~", new Vector3(x, -0.002f, z - 0.002f), 0.015f, material);
        }

        private void BuildFuelFurnace()
        {
            GameObject root = new GameObject("Fuel Furnace");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(1.55f, 1.08f, -7.62f);
            fuelFurnace = root.AddComponent<MiniVanFuelFurnace>();
            fuelFurnace.Vehicle = this;
            BoxCollider interaction = root.AddComponent<BoxCollider>();
            interaction.size = new Vector3(1.55f, 2.25f, 1.20f);
            interaction.center = new Vector3(0f, 0.08f, -0.08f);

            Material steel = GetFuelMaterial("FurnaceSteel", "Furnace Steel", new Color(0.065f, 0.07f, 0.075f));
            Material edge = GetFuelMaterial("FurnaceEdge", "Furnace Edge", new Color(0.13f, 0.12f, 0.105f));
            Material soot = GetFuelMaterial("FurnaceSoot", "Furnace Soot", new Color(0.012f, 0.01f, 0.008f));
            Material rust = GetFuelMaterial("FurnaceRust", "Furnace Rust", new Color(0.28f, 0.105f, 0.035f));
            Material fire = GetFuelMaterial("FurnaceFire", "Furnace Fire", new Color(1f, 0.22f, 0.015f), true);
            Material amber = GetFuelMaterial("FurnaceLamp", "Furnace Lamp", new Color(1f, 0.52f, 0.04f), true);
            Material cream = GetFuelMaterial("FurnaceText", "Furnace Text", new Color(0.84f, 0.80f, 0.66f), true);

            CreateFuelCube(root.transform, "Firebox", Vector3.zero, new Vector3(1.20f, 1.82f, 0.82f), steel);
            CreateFuelCube(root.transform, "Top Cap", new Vector3(0f, 0.94f, 0f), new Vector3(1.28f, 0.10f, 0.90f), edge);
            CreateFuelCube(root.transform, "Bottom Foot", new Vector3(0f, -0.96f, 0f), new Vector3(1.24f, 0.12f, 0.86f), edge);
            CreateFuelCube(root.transform, "Display Plate", new Vector3(0f, 0.58f, -0.445f), new Vector3(0.88f, 0.30f, 0.055f), edge);
            CreateFuelCube(root.transform, "Counter Glass", new Vector3(0f, 0.60f, -0.48f), new Vector3(0.70f, 0.18f, 0.018f), soot);

            CreateFuelCube(root.transform, "Open Mouth", new Vector3(0f, -0.16f, -0.445f), new Vector3(0.84f, 0.76f, 0.08f), soot);
            CreateFuelCube(root.transform, "Mouth Top", new Vector3(0f, 0.27f, -0.49f), new Vector3(1.04f, 0.12f, 0.12f), rust);
            CreateFuelCube(root.transform, "Mouth Bottom", new Vector3(0f, -0.59f, -0.49f), new Vector3(1.04f, 0.12f, 0.12f), rust);
            CreateFuelCube(root.transform, "Mouth Left", new Vector3(-0.46f, -0.16f, -0.49f), new Vector3(0.12f, 0.80f, 0.12f), rust);
            CreateFuelCube(root.transform, "Mouth Right", new Vector3(0.46f, -0.16f, -0.49f), new Vector3(0.12f, 0.80f, 0.12f), rust);
            furnaceFire = CreateFuelCube(root.transform, "Fire", new Vector3(0f, -0.34f, -0.535f),
                new Vector3(0.58f, 0.34f, 0.07f), fire);
            CreateFuelCube(root.transform, "Coal Left", new Vector3(-0.20f, -0.48f, -0.58f), new Vector3(0.20f, 0.13f, 0.09f), soot);
            CreateFuelCube(root.transform, "Coal Center", new Vector3(0.02f, -0.45f, -0.59f), new Vector3(0.24f, 0.15f, 0.09f), edge);
            CreateFuelCube(root.transform, "Coal Right", new Vector3(0.24f, -0.49f, -0.58f), new Vector3(0.18f, 0.12f, 0.09f), soot);
            CreateFuelCube(root.transform, "Ash Tray", new Vector3(0f, -0.79f, -0.20f), new Vector3(0.78f, 0.20f, 0.58f), edge);
            CreateFuelCube(root.transform, "Ash Handle", new Vector3(0f, -0.80f, -0.53f), new Vector3(0.34f, 0.07f, 0.08f), rust);

            CreateFuelCube(root.transform, "Mount Left", new Vector3(-0.50f, 0.38f, 0.52f), new Vector3(0.16f, 0.72f, 0.18f), rust);
            CreateFuelCube(root.transform, "Mount Right", new Vector3(0.50f, 0.38f, 0.52f), new Vector3(0.16f, 0.72f, 0.18f), rust);

            CreateFuelCylinder(root.transform, "Chimney Elbow", new Vector3(0.68f, 0.55f, 0f),
                new Vector3(0.20f, 0.40f, 0.20f), edge, Quaternion.Euler(0f, 0f, 90f));
            CreateFuelCylinder(root.transform, "Chimney Lower", new Vector3(0.88f, 1.10f, 0f),
                new Vector3(0.20f, 0.62f, 0.20f), steel, Quaternion.identity);
            CreateFuelCylinder(root.transform, "Chimney Upper", new Vector3(0.88f, 2.04f, 0f),
                new Vector3(0.20f, 0.38f, 0.20f), steel, Quaternion.identity);
            CreateFuelCube(root.transform, "Chimney Collar", new Vector3(0.88f, 1.58f, 0f), new Vector3(0.48f, 0.14f, 0.48f), rust);
            CreateFuelCube(root.transform, "Rain Cap", new Vector3(0.88f, 2.46f, 0f), new Vector3(0.52f, 0.12f, 0.52f), edge);

            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -0.51f : 0.51f;
                float y = i < 2 ? -0.78f : 0.80f;
                CreateFuelCylinder(root.transform, "Case Bolt " + i, new Vector3(x, y, -0.47f),
                    new Vector3(0.055f, 0.025f, 0.055f), edge, Quaternion.Euler(90f, 0f, 0f));
            }

            furnaceCounter = CreateFuelText(root.transform, "Furnace Liter Counter", "100 L",
                new Vector3(0f, 0.64f, -0.515f), 0.045f, cream);
            CreateFuelText(root.transform, "Furnace Scale", "0 | | | 100",
                new Vector3(0f, 0.50f, -0.515f), 0.023f, cream);
            CreateFuelCylinder(root.transform, "Status Lamp", new Vector3(0.43f, 0.74f, -0.49f),
                new Vector3(0.07f, 0.025f, 0.07f), amber, Quaternion.Euler(90f, 0f, 0f));
        }

        private void CacheFuelGauge(Transform root)
        {
            fuelGaugeNeedle = root.Find("Needle Pivot");
            Transform counter = root.Find("Liter Counter");
            fuelGaugeCounter = counter != null ? counter.GetComponent<TextMesh>() : null;
            Transform face = root.Find("Dial Face");
            fuelGaugeFace = face != null ? face.GetComponent<Renderer>() : null;
        }

        private void CacheSpeedometer(Transform root)
        {
            speedometerNeedle = root.Find("Needle Pivot");
            Transform counter = root.Find("Speed Counter");
            speedometerCounter = counter != null ? counter.GetComponent<TextMesh>() : null;
        }

        private void CacheParkingBrakeIndicator(Transform root)
        {
            Transform icon = root.Find("Brake Icon");
            parkingBrakeIndicator = icon != null ? icon.GetComponent<TextMesh>() : null;
        }

        private void CacheCarBatteryIndicator(Transform root)
        {
            Transform icon = root.Find("Battery Icon");
            carBatteryIndicator = icon != null ? icon.GetComponent<TextMesh>() : null;
            Transform face = root.Find("Lamp Face");
            carBatteryIndicatorFace = face != null ? face.GetComponent<Renderer>() : null;
            Transform topLeft = root.Find("Battery Top Left");
            carBatteryIndicatorTopLeft = topLeft != null ? topLeft.GetComponent<Renderer>() : null;
            Transform topRight = root.Find("Battery Top Right");
            carBatteryIndicatorTopRight = topRight != null ? topRight.GetComponent<Renderer>() : null;
        }

        private void CacheEngineTemperatureGauge(Transform root)
        {
            Transform mercury = root.Find("Mercury Column");
            engineTemperatureMercury = mercury;
            engineTemperatureMercuryRenderer = mercury != null ? mercury.GetComponent<Renderer>() : null;
            CacheEngineTemperatureMercuryBasePose(mercury);
            Transform bulb = root.Find("Thermometer Bulb Inner");
            engineTemperatureBulbRenderer = bulb != null ? bulb.GetComponent<Renderer>() : null;
            Transform face = root.Find("Gauge Face");
            engineTemperatureFace = face != null ? face.GetComponent<Renderer>() : null;
            Transform counter = root.Find("Temp Counter");
            engineTemperatureCounter = counter != null ? counter.GetComponent<TextMesh>() : null;
            CacheEngineTemperatureIconParts(root);

            if (engineTemperatureMercuryMaterial == null)
            {
                engineTemperatureMercuryMaterial = GetFuelMaterial(
                    "RedEmmisive", "Engine Temperature Mercury", new Color(0.9f, 0.04f, 0.025f), true);
            }
            if (engineTemperatureMercuryHotMaterial == null)
            {
                engineTemperatureMercuryHotMaterial = CreateFuelMaterial("Engine Temperature Mercury Hot", Color.white, true);
            }
        }

        private void CacheEngineTemperatureIconParts(Transform root)
        {
            engineTemperatureIconRenderers.Clear();
            engineTemperatureIconTexts.Clear();
            if (root == null)
            {
                return;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                string name = child.name;
                if (!name.StartsWith("Engine Icon") && name != "Engine Heat Waves")
                {
                    continue;
                }

                TextMesh text = child.GetComponent<TextMesh>();
                if (text != null)
                {
                    engineTemperatureIconTexts.Add(text);
                    continue;
                }

                Renderer renderer = child.GetComponent<Renderer>();
                if (renderer != null)
                {
                    engineTemperatureIconRenderers.Add(renderer);
                }
            }
        }

        private void CacheEngineTemperatureMercuryBasePose(Transform mercury)
        {
            if (mercury == null)
            {
                engineTemperatureMercuryBaseX = EngineTemperatureThermometerX;
                engineTemperatureMercuryBaseZ = EngineTemperatureMercuryDefaultZ;
                engineTemperatureMercuryBottomY = -0.145f;
                return;
            }

            // Keep the authoring X/Z (and bottom of the column) so color/heat never shifts it sideways.
            engineTemperatureMercuryBaseX = mercury.localPosition.x;
            engineTemperatureMercuryBaseZ = mercury.localPosition.z;
            float currentHeight = Mathf.Max(0.001f, mercury.localScale.y);
            engineTemperatureMercuryBottomY = mercury.localPosition.y - currentHeight * 0.5f;
        }

        private void CacheFuelFurnace(Transform root)
        {
            fuelFurnace = root.GetComponent<MiniVanFuelFurnace>();
            if (fuelFurnace == null)
            {
                fuelFurnace = root.gameObject.AddComponent<MiniVanFuelFurnace>();
            }
            fuelFurnace.Vehicle = this;
            Transform counter = root.Find("Furnace Liter Counter");
            furnaceCounter = counter != null ? counter.GetComponent<TextMesh>() : null;
            Transform fire = root.Find("Fire");
            furnaceFire = fire != null ? fire.gameObject : null;
        }

        private void UpdateFuelSystemVisuals()
        {
            float capacity = Mathf.Max(1f, FuelCapacityLiters);
            float liters = Mathf.Clamp(FuelLiters.Value, 0f, capacity);

            if (furnaceCounter != null)
            {
                furnaceCounter.text = liters.ToString("0") + " L";
            }
            if (furnaceFire != null)
            {
                furnaceFire.SetActive(liters > 0.001f || Time.time < fuelBurnPulseUntil);
                float pulse = Time.time < fuelBurnPulseUntil ? 1f + Mathf.Sin(Time.time * 24f) * 0.16f : 0.85f;
                furnaceFire.transform.localScale = new Vector3(0.58f, 0.34f * pulse, 0.07f);
            }

            // No AKB in the bay → whole dash cluster reads as dead / unpowered.
            if (!HasEffectiveCarBatteryInstalled())
            {
                ApplyUnpoweredDashboardVisuals();
                return;
            }

            float t = liters / capacity;
            if (fuelGaugeNeedle != null)
            {
                fuelGaugeNeedle.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(65f, -65f, t));
            }
            if (fuelGaugeCounter != null)
            {
                fuelGaugeCounter.text = liters.ToString("0") + " L";
                fuelGaugeCounter.color = liters <= 0.001f ? new Color(1f, 0.12f, 0.06f) : DashboardLiveTextColor;
            }
            if (fuelGaugeFace != null)
            {
                if (gaugeNormalMaterial == null)
                {
                    gaugeNormalMaterial = GetFuelMaterial("GaugeFace", "Fuel Gauge Face", new Color(0.035f, 0.038f, 0.04f));
                }
                if (gaugeEmptyMaterial == null)
                {
                    gaugeEmptyMaterial = GetFuelMaterial("GaugeEmpty", "Fuel Gauge Empty", new Color(0.34f, 0.018f, 0.015f));
                }
                fuelGaugeFace.sharedMaterial = liters <= 0.001f ? gaugeEmptyMaterial : gaugeNormalMaterial;
            }
            float speedKph = Mathf.Clamp(Mathf.Abs(SpeedKph.Value), 0f, 200f);
            if (speedometerNeedle != null)
            {
                speedometerNeedle.localRotation = Quaternion.Euler(
                    0f, 0f, Mathf.Lerp(130f, -130f, speedKph / 200f));
            }
            int roundedSpeedKph = Mathf.RoundToInt(speedKph);
            if (speedometerCounter != null)
            {
                if (displayedSpeedKph != roundedSpeedKph)
                {
                    displayedSpeedKph = roundedSpeedKph;
                    speedometerCounter.text = displayedSpeedKph + " km/h";
                }
                speedometerCounter.color = DashboardLiveTextColor;
            }
            if (parkingBrakeIndicator != null)
            {
                parkingBrakeIndicator.color = HandbrakeLocked.Value
                    ? new Color(1f, 0.04f, 0.03f, 1f)
                    : new Color(0.08f, 1f, 0.18f, 1f);
            }
            UpdateCarBatteryIndicatorVisual();
            UpdateEngineTemperatureGaugeVisual();
            UpdateRpmGaugeVisual();
        }

        private void ApplyUnpoweredDashboardVisuals()
        {
            if (gaugeDeadMaterial == null)
            {
                gaugeDeadMaterial = GetFuelMaterial("GaugeFace", "Dashboard Dead Face", DashboardDeadColor);
            }

            if (fuelGaugeNeedle != null)
            {
                fuelGaugeNeedle.localRotation = Quaternion.Euler(0f, 0f, 65f);
            }
            if (fuelGaugeCounter != null)
            {
                fuelGaugeCounter.text = "---";
                fuelGaugeCounter.color = DashboardDeadTextColor;
            }
            if (fuelGaugeFace != null)
            {
                fuelGaugeFace.sharedMaterial = gaugeDeadMaterial;
            }

            if (speedometerNeedle != null)
            {
                speedometerNeedle.localRotation = Quaternion.Euler(0f, 0f, 130f);
            }
            if (speedometerCounter != null)
            {
                displayedSpeedKph = -1;
                speedometerCounter.text = "---";
                speedometerCounter.color = DashboardDeadTextColor;
            }

            if (parkingBrakeIndicator != null)
            {
                parkingBrakeIndicator.color = DashboardDeadTextColor;
            }

            ApplyCarBatteryLampRenderer(carBatteryIndicatorFace, DashboardDeadColor);
            ApplyCarBatteryLampRenderer(carBatteryIndicatorTopLeft, DashboardDeadColor);
            ApplyCarBatteryLampRenderer(carBatteryIndicatorTopRight, DashboardDeadColor);
            if (carBatteryIndicator != null)
            {
                carBatteryIndicator.color = Color.black;
            }

            if (engineTemperatureMercury == null && transform.Find("Engine Temperature Gauge") != null)
            {
                CacheEngineTemperatureGauge(transform.Find("Engine Temperature Gauge"));
            }
            if (engineTemperatureMercury != null)
            {
                float height = EngineTemperatureMercuryMinHeight;
                engineTemperatureMercury.localPosition = new Vector3(
                    engineTemperatureMercuryBaseX,
                    engineTemperatureMercuryBottomY + height * 0.5f,
                    engineTemperatureMercuryBaseZ);
                engineTemperatureMercury.localScale = new Vector3(0.022f, height, 0.018f);
            }
            if (engineTemperatureFace != null)
            {
                engineTemperatureFace.sharedMaterial = gaugeDeadMaterial;
            }
            ApplyTemperatureMercuryColor(DashboardDeadColor, 0.05f);
            ApplyTemperatureIconColor(DashboardDeadColor, DashboardDeadTextColor, 0.05f);
            if (engineTemperatureCounter != null)
            {
                engineTemperatureCounter.text = "---";
                engineTemperatureCounter.color = DashboardDeadTextColor;
            }

            if (rpmFillColumn == null && transform.Find("RPM Gauge") != null)
            {
                CacheRpmGauge(transform.Find("RPM Gauge"));
            }
            if (rpmFillColumn != null)
            {
                float height = RpmFillMinHeight;
                rpmFillColumn.localPosition = new Vector3(0f, RpmTrackBottomY + height * 0.5f, -0.19f);
                rpmFillColumn.localScale = new Vector3(0.034f, height, 0.018f);
                ApplyRpmDynamicMaterial(rpmFillRenderer, rpmFillMaterial, DashboardDeadColor, 0.05f);
            }
            if (rpmSweetZone != null)
            {
                ApplyRpmDynamicMaterial(rpmSweetZoneRenderer, rpmSweetZoneMaterial, DashboardDeadColor, 0.02f);
            }
            ApplyRpmDynamicMaterial(rpmLedRenderer, rpmLedMaterial, DashboardDeadColor, 0.02f);
            if (rpmCounter != null)
            {
                rpmCounter.text = "---";
                rpmCounter.color = DashboardDeadTextColor;
            }
        }

        private void UpdateEngineTemperatureGaugeVisual()
        {
            if (engineTemperatureMercury == null && transform.Find("Engine Temperature Gauge") != null)
            {
                CacheEngineTemperatureGauge(transform.Find("Engine Temperature Gauge"));
            }

            float maxTemperature = Mathf.Max(1f, EngineTemperatureMaxC);
            float temperature = Mathf.Clamp(EngineTemperatureC.Value, 0f, maxTemperature);
            float t = temperature / maxTemperature;
            // White mercury only above smoke threshold (~100C); otherwise stay red.
            bool overheated = temperature >= EngineSmokeTemperatureC;

            if (engineTemperatureMercury != null)
            {
                float height = Mathf.Lerp(EngineTemperatureMercuryMinHeight, EngineTemperatureMercuryMaxHeight, t);
                engineTemperatureMercury.localPosition = new Vector3(
                    engineTemperatureMercuryBaseX,
                    engineTemperatureMercuryBottomY + height * 0.5f,
                    engineTemperatureMercuryBaseZ);
                engineTemperatureMercury.localScale = new Vector3(0.022f, height, 0.018f);
            }

            if (engineTemperatureNormalMaterial == null)
            {
                engineTemperatureNormalMaterial = GetFuelMaterial("GaugeFace", "Engine Temperature Face", new Color(0.018f, 0.020f, 0.021f));
            }
            if (engineTemperatureHotMaterial == null)
            {
                engineTemperatureHotMaterial = GetFuelMaterial("GaugeRed", "Engine Temperature Hot Face", new Color(0.78f, 0.04f, 0.025f), true);
            }
            if (engineTemperatureMercuryMaterial == null)
            {
                engineTemperatureMercuryMaterial = GetFuelMaterial(
                    "RedEmmisive", "Engine Temperature Mercury", new Color(0.9f, 0.04f, 0.025f), true);
            }
            if (engineTemperatureMercuryHotMaterial == null)
            {
                engineTemperatureMercuryHotMaterial = CreateFuelMaterial("Engine Temperature Mercury Hot", Color.white, true);
            }

            if (engineTemperatureFace != null)
            {
                engineTemperatureFace.sharedMaterial = overheated
                    ? engineTemperatureHotMaterial
                    : engineTemperatureNormalMaterial;
            }

            Color mercuryColor = overheated
                ? Color.white
                : new Color(0.95f, 0.05f, 0.03f, 1f);
            ApplyTemperatureMercuryColor(mercuryColor, overheated ? 2.1f : 1.35f);

            Color iconColor = overheated
                ? Color.white
                : new Color(0.9f, 0.04f, 0.025f, 1f);
            Color iconText = overheated ? Color.white : new Color(0.9f, 0.04f, 0.025f, 1f);
            ApplyTemperatureIconColor(iconColor, iconText, overheated ? 1.8f : 1.1f);

            if (engineTemperatureCounter != null)
            {
                engineTemperatureCounter.text = Mathf.RoundToInt(temperature) + " C";
                engineTemperatureCounter.color = overheated ? Color.white : new Color(0.86f, 0.82f, 0.62f, 1f);
            }
        }

        private void ApplyTemperatureMercuryColor(Color color, float emission)
        {
            if (engineTemperatureMercuryMaterial == null)
            {
                engineTemperatureMercuryMaterial = GetFuelMaterial(
                    "RedEmmisive", "Engine Temperature Mercury", new Color(0.9f, 0.04f, 0.025f), true);
            }

            ApplyRpmDynamicMaterial(engineTemperatureMercuryRenderer, engineTemperatureMercuryMaterial, color, emission);
            ApplyRpmDynamicMaterial(engineTemperatureBulbRenderer, engineTemperatureMercuryMaterial, color, emission);
        }

        private void ApplyTemperatureIconColor(Color color, Color textColor, float emission)
        {
            for (int i = 0; i < engineTemperatureIconRenderers.Count; i++)
            {
                ApplyRpmDynamicMaterial(engineTemperatureIconRenderers[i], null, color, emission);
            }

            for (int i = 0; i < engineTemperatureIconTexts.Count; i++)
            {
                TextMesh text = engineTemperatureIconTexts[i];
                if (text != null)
                {
                    text.color = textColor;
                }
            }
        }

        private void UpdateRpmGaugeVisual()
        {
            if (rpmFillColumn == null && transform.Find("RPM Gauge") != null)
            {
                CacheRpmGauge(transform.Find("RPM Gauge"));
            }

            if (rpmFillColumn == null)
            {
                return;
            }

            float idle = Mathf.Max(100f, IdleRpm);
            float redline = Mathf.Max(idle + 100f, RedlineRpm);
            float rpm = EngineOn.Value ? Mathf.Clamp(EngineRpm.Value, 0f, redline) : 0f;
            float rpm01 = EngineOn.Value ? Mathf.Clamp01(Mathf.InverseLerp(idle, redline, rpm)) : 0f;
            float center = Mathf.Clamp(SweetSpotCenterRpm01, 0.15f, 0.85f);
            float half = Mathf.Max(0.04f, SweetSpotHalfWidthRpm01);
            float sweet = EngineOn.Value ? Mathf.Clamp01(EngineSweetSpot01.Value) : 0f;

            // Same fill mapping as DrawDriverEngineHud: height tracks rpm01.
            float height = Mathf.Lerp(RpmFillMinHeight, RpmFillMaxHeight, rpm01);
            rpmFillColumn.localPosition = new Vector3(0f, RpmTrackBottomY + height * 0.5f, -0.19f);
            rpmFillColumn.localScale = new Vector3(0.034f, height, 0.018f);

            // Same color logic as the HUD tach strip.
            Color fill = Color.Lerp(new Color(0.75f, 0.2f, 0.12f, 1f), new Color(0.2f, 0.9f, 0.28f, 1f), sweet);
            if (rpm01 > center + half)
            {
                fill = Color.Lerp(
                    new Color(0.95f, 0.7f, 0.12f, 1f),
                    new Color(1f, 0.2f, 0.1f, 1f),
                    Mathf.InverseLerp(center + half, 1f, rpm01));
            }

            if (!EngineOn.Value)
            {
                fill = new Color(0.12f, 0.12f, 0.12f, 1f);
            }

            ApplyRpmDynamicMaterial(rpmFillRenderer, rpmFillMaterial, fill, EngineOn.Value ? 1.1f : 0.15f);

            // Sweet-spot window marker (HUD green zone), vertical.
            if (rpmSweetZone != null)
            {
                float trackHeight = RpmTrackTopY - RpmTrackBottomY;
                float zone01Low = Mathf.Clamp01(center - half);
                float zone01High = Mathf.Clamp01(center + half);
                float zoneBottom = Mathf.Lerp(RpmTrackBottomY, RpmTrackTopY, zone01Low);
                float zoneTop = Mathf.Lerp(RpmTrackBottomY, RpmTrackTopY, zone01High);
                float zoneHeight = Mathf.Max(0.04f, zoneTop - zoneBottom);
                rpmSweetZone.localPosition = new Vector3(0f, zoneBottom + zoneHeight * 0.5f, -0.185f);
                rpmSweetZone.localScale = new Vector3(0.07f, zoneHeight, 0.01f);

                Color zoneColor = new Color(0.25f, 1f, 0.45f, 1f) * (sweet > 0.45f ? 0.95f : 0.35f);
                if (!EngineOn.Value)
                {
                    zoneColor *= 0.25f;
                }

                ApplyRpmDynamicMaterial(rpmSweetZoneRenderer, rpmSweetZoneMaterial, zoneColor, sweet > 0.45f ? 1.4f : 0.35f);
            }

            // LED lights when you're in the band (same threshold as HUD "POWER" was).
            if (rpmLedRenderer != null)
            {
                bool inBand = EngineOn.Value && sweet > 0.45f;
                Color led = inBand
                    ? Color.Lerp(new Color(0.15f, 0.85f, 0.25f, 1f), new Color(0.55f, 1f, 0.45f, 1f), sweet)
                    : new Color(0.07f, 0.08f, 0.07f, 1f);
                ApplyRpmDynamicMaterial(rpmLedRenderer, rpmLedMaterial, led, inBand ? 1.6f : 0.05f);
            }

            if (rpmCounter != null)
            {
                rpmCounter.text = EngineOn.Value ? Mathf.RoundToInt(rpm).ToString() : "---";
                rpmCounter.color = sweet > 0.6f && EngineLoad.Value > 0.2f
                    ? new Color(0.45f, 1f, 0.55f, 1f)
                    : new Color(0.86f, 0.82f, 0.62f, 1f);
            }
        }

        private static void ApplyRpmDynamicMaterial(Renderer renderer, Material shared, Color color, float emission)
        {
            if (renderer == null)
            {
                return;
            }

            Material material;
            if (Application.isPlaying)
            {
                material = renderer.material;
            }
            else if (shared != null)
            {
                material = shared;
                renderer.sharedMaterial = shared;
            }
            else
            {
                material = renderer.sharedMaterial;
            }

            if (material == null)
            {
                return;
            }

            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * Mathf.Max(0f, emission));
            }
        }

        private void UpdateCarBatteryIndicatorVisual()
        {
            bool installed = HasEffectiveCarBatteryInstalled();
            float charge = installed ? GetEffectiveCarBatteryCharge01() : 0f;
            Color red = new Color(1f, 0.04f, 0.03f, 1f);
            Color green = new Color(0.08f, 1f, 0.18f, 1f);
            Color color = installed ? Color.Lerp(red, green, charge) : DashboardDeadColor;
            bool blink = installed &&
                         charge > 0.001f &&
                         charge < Mathf.Clamp01(CarBatteryBlinkThreshold01) &&
                         Mathf.FloorToInt(Time.time * 3.2f) % 2 == 0;
            if (blink)
            {
                float dim = Mathf.Clamp01(CarBatteryBlinkDimMultiplier);
                color = new Color(color.r * dim, color.g * dim, color.b * dim, 1f);
            }

            if (carBatteryIndicator != null)
            {
                carBatteryIndicator.color = Color.black;
            }

            ApplyCarBatteryLampRenderer(carBatteryIndicatorFace, color);
            ApplyCarBatteryLampRenderer(carBatteryIndicatorTopLeft, color);
            ApplyCarBatteryLampRenderer(carBatteryIndicatorTopRight, color);
        }

        private static void ApplyCarBatteryLampRenderer(Renderer renderer, Color color)
        {
            if (renderer == null)
            {
                return;
            }

            Material material = Application.isPlaying
                ? renderer.material
                : renderer.sharedMaterial;
            material.color = color * 0.55f;
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 1.3f);
            }
        }

        private static GameObject CreateFuelCube(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            DestroyFuelObject(part.GetComponent<Collider>());
            return part;
        }

        private static GameObject CreateFuelCylinder(Transform parent, string name, Vector3 position, Vector3 scale,
            Material material, Quaternion rotation)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = rotation;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            DestroyFuelObject(part.GetComponent<Collider>());
            return part;
        }

        private static TextMesh CreateFuelText(Transform parent, string name, string text, Vector3 position,
            float characterSize, Material material)
        {
            GameObject label = new GameObject(name);
            label.transform.SetParent(parent, false);
            label.transform.localPosition = position;
            TextMesh textMesh = label.AddComponent<TextMesh>();
            textMesh.text = text;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.characterSize = characterSize;
            textMesh.fontSize = 48;
            textMesh.color = material != null ? material.color : Color.white;
            Material depthMaterial = Resources.Load<Material>("Panelka_WorldTextDepth");
            Renderer textRenderer = label.GetComponent<Renderer>();
            if (depthMaterial != null && textRenderer != null)
            {
                textRenderer.sharedMaterial = depthMaterial;
            }
            MiniVanPanelkaWorldTextDepth depth =
                label.AddComponent<MiniVanPanelkaWorldTextDepth>();
            depth.ApplyNow();
            return textMesh;
        }

        private static Material GetFuelMaterial(string resourceName, string fallbackName, Color color, bool emission = false)
        {
            Material asset = Resources.Load<Material>("FuelSystem/Materials/" + resourceName);
            return asset != null ? asset : CreateFuelMaterial(fallbackName, color, emission);
        }

        private static void DestroyFuelObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }

        private static Material CreateFuelMaterial(string name, Color color, bool emission = false)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }
            Material material = new Material(shader);
            material.name = name;
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (emission && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 1.8f);
            }
            return material;
        }
    }
}
