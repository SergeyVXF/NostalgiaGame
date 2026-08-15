using Unity.Netcode;
using UnityEngine;

namespace MiniVanGame
{
    public class MiniVanRescueMission : MonoBehaviour
    {
        private const int FirstPassengerSeat = 2;
        private const int SecondPassengerSeat = 3;

        [Header("Fallback Scene Object Names")]
        public string SavePlaceName = "SavePlace";
        public string SavePlaceDoorName = "SavePlace_Door";
        public string BunkerName = "Bunker";
        public string BunkerDoorName = "Bunker_Door";

        [Header("Fallback Rules")]
        public float SavePlaceRadius = 24f;
        public float BunkerRadius = 30f;
        public float ZombieBlockRadius = 12f;
        public float VehicleStopSpeedKph = 0.5f;
        public float VehicleStopAngularSpeed = 0.2f;
        public float VehicleStableStopSeconds = 0.85f;
        public float BoardingSeconds = 10f;
        public float PassengerExitLockSeconds = 15;

        public float BunkerBlockLogInterval = 2f;
        public bool DebugRescueMission = true;

        private static MiniVanRescueMission instance;

        private MiniVanVehicle activeVehicle;
        private MiniVanPlayer activeDriver;
        private int activeSeatIndex = -1;
        private float seatedAtTime;
        private float lastBunkerBlockLogTime;
        private float vehicleStoppedSince = -1f;
        private bool passengerSeated;
        private bool missionActive;
        private MiniVanVehicle movementLockedVehicle;


        public static void ServerTryStartBoarding(MiniVanPlayer driver, MiniVanVehicle vehicle)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                return;
            }

            GetOrCreate().TryStartBoarding(driver, vehicle);
        }

public static void ServerNotifyPassengerSeated(MiniVanVehicle vehicle, int seatIndex)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                return;
            }

            GetOrCreate().NotifyPassengerSeated(vehicle, seatIndex);
        }


        private static MiniVanRescueMission GetOrCreate()
        {
            if (instance != null)
            {
                return instance;
            }

            instance = FindAnyObjectByType<MiniVanRescueMission>();
            if (instance != null)
            {
                return instance;
            }

            GameObject missionObject = new GameObject("MiniVan Rescue Mission");
            instance = missionObject.AddComponent<MiniVanRescueMission>();
            return instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
        }

private void Update()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer || !missionActive)
            {
                return;
            }

            if (activeVehicle == null || activeDriver == null)
            {
                ResetMission();
                return;
            }

            if (!passengerSeated)
            {
                if (Time.time >= seatedAtTime)
                {
                    seatedAtTime = Time.time + Mathf.Max(1f, BunkerBlockLogInterval);
                    Log("Passenger is still boarding Seat " + activeSeatIndex + "; minivan remains locked.");
                }

                return;
            }

            MiniVanRescueBunker bunker = FindNearestBunker(activeVehicle.transform.position);
            if (bunker == null)
            {
                vehicleStoppedSince = -1f;
                LogBunkerBlock("waiting: no Bunker stop zone in range");
                return;
            }

            if (!UpdateStableStop(activeVehicle, out string stopReason))
            {
                LogBunkerBlock("waiting: " + stopReason);
                return;
            }

            if (!HasClearZombieRadius(bunker.GetZombieCheckPosition(), bunker.ZombieBlockRadius) || !HasClearZombieRadius(activeVehicle.transform.position, bunker.ZombieBlockRadius))
            {
                LogBunkerBlock("waiting: zombie nearby");
                return;
            }

            activeVehicle.LockRescueMovementFor(PassengerExitLockSeconds, "passenger exiting to bunker");
            activeDriver.RescueStartExitClientRpc(new NetworkObjectReference(activeVehicle.NetworkObject), activeSeatIndex, bunker.GetDoorOutsidePosition());
            Log("Passenger delivered to " + bunker.name + ".");
            ResetMission();
        }

        private void TryStartBoarding(MiniVanPlayer driver, MiniVanVehicle vehicle)
        {
            if (missionActive)
            {
                Log("SavePlace horn ignored: mission already active.");
                return;
            }

            if (driver == null || vehicle == null)
            {
                return;
            }

            MiniVanRescueSavePlace savePlace = FindNearestSavePlace(vehicle.transform.position);
            if (savePlace == null)
            {
                Log("SavePlace horn ignored: no SavePlace stop zone in range.");
                return;
            }

            if (!IsVehicleStoppedNow(vehicle, out string stopReason))
            {
                Log("SavePlace horn ignored: " + stopReason + ".");
                return;
            }

            if (!HasClearZombieRadius(savePlace.GetZombieCheckPosition(), savePlace.ZombieBlockRadius) || !HasClearZombieRadius(vehicle.transform.position, savePlace.ZombieBlockRadius))
            {
                Log("SavePlace horn ignored: zombie nearby.");
                return;
            }

            int seatIndex = PickPassengerSeat(vehicle);
            if (seatIndex < 0)
            {
                Log("SavePlace horn ignored: Seat 2 and Seat 3 are occupied or missing.");
                return;
            }
            activeVehicle = vehicle;
            activeDriver = driver;
            activeSeatIndex = seatIndex;
            seatedAtTime = Time.time + BoardingSeconds;
            passengerSeated = false;
            missionActive = true;
            lastBunkerBlockLogTime = 0f;
            vehicleStoppedSince = -1f;

            LockVehicleMovement(vehicle, "passenger boarding");
            activeDriver.RescueStartBoardingClientRpc(new NetworkObjectReference(vehicle.NetworkObject), seatIndex, savePlace.GetDoorInsidePosition(), savePlace.GetDoorOutsidePosition());
            Log("Passenger called from " + savePlace.name + " to Seat " + seatIndex + ".");
        }

        private MiniVanRescueSavePlace FindNearestSavePlace(Vector3 vehiclePosition)
        {
            EnsureFallbackSavePlace();

            MiniVanRescueSavePlace[] savePlaces = FindObjectsByType<MiniVanRescueSavePlace>(FindObjectsSortMode.None);
            MiniVanRescueSavePlace best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < savePlaces.Length; i++)
            {
                MiniVanRescueSavePlace savePlace = savePlaces[i];
                if (savePlace == null || !savePlace.gameObject.activeInHierarchy)
                {
                    continue;
                }

                float distance = GetPlanarDistance(vehiclePosition, savePlace.GetCheckPosition());
                float radius = savePlace.GetCallRadius();
                if (distance <= radius && distance < bestDistance)
                {
                    best = savePlace;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private MiniVanRescueBunker FindNearestBunker(Vector3 vehiclePosition)
        {
            EnsureFallbackBunker();

            MiniVanRescueBunker[] bunkers = FindObjectsByType<MiniVanRescueBunker>(FindObjectsSortMode.None);
            MiniVanRescueBunker best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < bunkers.Length; i++)
            {
                MiniVanRescueBunker bunker = bunkers[i];
                if (bunker == null || !bunker.gameObject.activeInHierarchy)
                {
                    continue;
                }

                float distance = GetPlanarDistance(vehiclePosition, bunker.GetCheckPosition());
                float radius = bunker.GetDeliveryRadius();
                if (distance <= radius && distance < bestDistance)
                {
                    best = bunker;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private void EnsureFallbackSavePlace()
        {
            GameObject savePlaceObject = GameObject.Find(SavePlaceName);
            if (savePlaceObject == null || savePlaceObject.GetComponent<MiniVanRescueSavePlace>() != null)
            {
                return;
            }

            MiniVanRescueSavePlace savePlace = savePlaceObject.AddComponent<MiniVanRescueSavePlace>();
            savePlace.CallRadius = SavePlaceRadius;
            savePlace.ZombieBlockRadius = ZombieBlockRadius;
            GameObject doorObject = GameObject.Find(SavePlaceDoorName);
            if (doorObject != null)
            {
                savePlace.Door = doorObject.transform;
            }
        }

        private void EnsureFallbackBunker()
        {
            GameObject bunkerObject = GameObject.Find(BunkerName);
            if (bunkerObject == null || bunkerObject.GetComponent<MiniVanRescueBunker>() != null)
            {
                return;
            }

            MiniVanRescueBunker bunker = bunkerObject.AddComponent<MiniVanRescueBunker>();
            bunker.DeliveryRadius = BunkerRadius;
            bunker.ZombieBlockRadius = ZombieBlockRadius;
            GameObject doorObject = GameObject.Find(BunkerDoorName);
            if (doorObject != null)
            {
                bunker.Door = doorObject.transform;
            }
        }

        private int PickPassengerSeat(MiniVanVehicle vehicle)
        {
            if (CanUseSeat(vehicle, FirstPassengerSeat))
            {
                return FirstPassengerSeat;
            }

            if (CanUseSeat(vehicle, SecondPassengerSeat))
            {
                return SecondPassengerSeat;
            }

            return -1;
        }

        private static bool CanUseSeat(MiniVanVehicle vehicle, int seatIndex)
        {
            return vehicle != null && vehicle.GetSeat(seatIndex) != null && vehicle.IsSeatAvailable(seatIndex);
        }

        private bool UpdateStableStop(MiniVanVehicle vehicle, out string reason)
        {
            if (!IsVehicleStoppedNow(vehicle, out reason))
            {
                vehicleStoppedSince = -1f;
                return false;
            }

            if (vehicleStoppedSince < 0f)
            {
                vehicleStoppedSince = Time.time;
                reason = "minivan just stopped";
                return false;
            }

            float stableSeconds = Time.time - vehicleStoppedSince;
            if (stableSeconds < VehicleStableStopSeconds)
            {
                reason = "minivan stabilizing (" + stableSeconds.ToString("0.0") + "/" + VehicleStableStopSeconds.ToString("0.0") + " sec)";
                return false;
            }

            reason = "stopped";
            return true;
        }

        private bool IsVehicleStoppedNow(MiniVanVehicle vehicle, out string reason)
        {
            reason = "stopped";
            if (vehicle == null)
            {
                reason = "no minivan";
                return false;
            }

            float speed = Mathf.Abs(vehicle.SpeedKph.Value);
            if (speed > VehicleStopSpeedKph)
            {
                reason = "minivan still moving (" + speed.ToString("0.0") + " km/h)";
                return false;
            }

            Rigidbody body = vehicle.GetComponent<Rigidbody>();
            if (body != null && body.angularVelocity.magnitude > VehicleStopAngularSpeed)
            {
                reason = "minivan still rotating (" + body.angularVelocity.magnitude.ToString("0.00") + " rad/s)";
                return false;
            }

            return true;
        }

        private static float GetPlanarDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private bool HasClearZombieRadius(Vector3 center, float radius)
        {
            MiniVanZombie[] zombies = FindObjectsByType<MiniVanZombie>(FindObjectsSortMode.None);
            for (int i = 0; i < zombies.Length; i++)
            {
                MiniVanZombie zombie = zombies[i];
                if (zombie == null || !zombie.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (GetPlanarDistance(zombie.transform.position, center) <= radius)
                {
                    return false;
                }
            }

            return true;
        }

private void ResetMission()
        {
            ReleaseVehicleMovementLock("mission reset");
            activeVehicle = null;
            activeDriver = null;
            activeSeatIndex = -1;
            seatedAtTime = 0f;
            lastBunkerBlockLogTime = 0f;
            vehicleStoppedSince = -1f;
            passengerSeated = false;
            missionActive = false;
        }

        private void LogBunkerBlock(string message)
        {
            if (Time.time - lastBunkerBlockLogTime < BunkerBlockLogInterval)
            {
                return;
            }

            lastBunkerBlockLogTime = Time.time;
            Log("Bunker delivery " + message + ".");
        }

        private void Log(string message)
        {
            if (DebugRescueMission)
            {
                Debug.Log("[MiniVanRescue] " + message);
            }
        }
    

private void LockVehicleMovement(MiniVanVehicle vehicle, string reason)
        {
            if (movementLockedVehicle != null && movementLockedVehicle != vehicle)
            {
                movementLockedVehicle.SetRescueMovementLocked(false, "rescue mission switched vehicle");
            }

            movementLockedVehicle = vehicle;
            if (movementLockedVehicle != null)
            {
                movementLockedVehicle.SetRescueMovementLocked(true, reason);
            }
        }

        private void ReleaseVehicleMovementLock(string reason)
        {
            if (movementLockedVehicle == null)
            {
                return;
            }

            movementLockedVehicle.SetRescueMovementLocked(false, reason);
            movementLockedVehicle = null;
        }


private void NotifyPassengerSeated(MiniVanVehicle vehicle, int seatIndex)
        {
            if (!missionActive || passengerSeated || vehicle == null || vehicle != activeVehicle || seatIndex != activeSeatIndex)
            {
                return;
            }

            passengerSeated = true;
            vehicleStoppedSince = -1f;
            ReleaseVehicleMovementLock("passenger seated callback");
            Log("Passenger seated callback received for Seat " + activeSeatIndex + ".");
        }
}
}
