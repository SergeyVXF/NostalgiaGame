using UnityEngine;

namespace MiniVanGame
{
    public static class MiniVanFuelRules
    {
        public const float StandardFuelLiters = 10f;
        public const float ZombiePartFuelLiters = 50f;

        public static float GetFuelLiters(MiniVanInventoryItem item)
        {
            if (item == MiniVanInventoryItem.TraceHead)
            {
                return ZombiePartFuelLiters * 0.2f;
            }

            if (IsZombiePart(item))
            {
                return ZombiePartFuelLiters;
            }

            switch (item)
            {
                case MiniVanInventoryItem.Bat:
                case MiniVanInventoryItem.Flour:
                case MiniVanInventoryItem.TomatoPaste:
                case MiniVanInventoryItem.Cheese:
                case MiniVanInventoryItem.Sausage:
                case MiniVanInventoryItem.Dough:
                case MiniVanInventoryItem.RoundDough:
                case MiniVanInventoryItem.GratedCheese:
                case MiniVanInventoryItem.SlicedSausage:
                case MiniVanInventoryItem.RawPizza:
                case MiniVanInventoryItem.CookedPizza:
                case MiniVanInventoryItem.BurnedPizza:
                case MiniVanInventoryItem.PizzaBox:
                case MiniVanInventoryItem.BoxedPizza:
                    return StandardFuelLiters;
                default:
                    return 0f;
            }
        }

        public static bool IsZombiePart(MiniVanInventoryItem item)
        {
            return item == MiniVanInventoryItem.ZombieTorso ||
                   item == MiniVanInventoryItem.ZombieArm ||
                   item == MiniVanInventoryItem.ZombieLeg ||
                   item == MiniVanInventoryItem.ZombieHead;
        }
    }

    public static class MiniVanFuelPartFactory
    {
        private static Material bodyMaterial;
        private static Material shadeMaterial;
        private static Material topMaterial;
        private static Material eyeMaterial;

        public static GameObject CreateVisual(MiniVanInventoryItem item, Transform parent, bool keepColliders,
            Transform sourceVisual = null)
        {
            GameObject root = new GameObject(item.ToString());
            if (parent != null)
            {
                root.transform.SetParent(parent, false);
            }

            if (sourceVisual != null && TryCloneZombiePart(item, sourceVisual, root.transform, keepColliders))
            {
                return root;
            }

            BuildMatchingFallback(item, root.transform, keepColliders);
            return root;
        }

        private static bool TryCloneZombiePart(MiniVanInventoryItem item, Transform sourceVisual, Transform parent,
            bool keepColliders)
        {
            string[] names;
            string pivotName;
            switch (item)
            {
                case MiniVanInventoryItem.ZombieHead:
                    names = new[] { "Head", "Head Top", "Left Eye", "Right Eye", "Mouth A", "Mouth B", "Mouth C" };
                    pivotName = "Head";
                    break;
                case MiniVanInventoryItem.ZombieArm:
                    pivotName = Random.value < 0.5f ? "Left Arm" : "Right Arm";
                    names = new[] { pivotName };
                    break;
                case MiniVanInventoryItem.ZombieLeg:
                    bool useLeft = Random.value < 0.5f;
                    pivotName = useLeft ? "Left Leg" : "Right Leg";
                    names = new[] { pivotName, useLeft ? "Left Foot" : "Right Foot" };
                    break;
                default:
                    names = new[] { "Body", "Back Shade" };
                    pivotName = "Body";
                    break;
            }

            Transform pivot = sourceVisual.Find(pivotName);
            if (pivot == null)
            {
                return false;
            }

            Vector3 pivotPosition = pivot.localPosition;
            int cloned = 0;
            for (int i = 0; i < names.Length; i++)
            {
                Transform source = sourceVisual.Find(names[i]);
                if (source == null)
                {
                    continue;
                }

                GameObject clone = Object.Instantiate(source.gameObject, parent, false);
                clone.name = source.name;
                clone.transform.localPosition = source.localPosition - pivotPosition;
                SetColliderState(clone, keepColliders);
                cloned++;
            }

            return cloned > 0;
        }

        private static void BuildMatchingFallback(MiniVanInventoryItem item, Transform parent, bool keepColliders)
        {
            switch (item)
            {
                case MiniVanInventoryItem.ZombieHead:
                    AddCube(parent, "Head", Vector3.zero, new Vector3(0.74f, 0.62f, 0.50f), GetBodyMaterial(), keepColliders);
                    AddCube(parent, "Head Top", new Vector3(0f, 0.33f, 0f), new Vector3(0.72f, 0.08f, 0.48f), GetTopMaterial(), keepColliders);
                    AddCube(parent, "Left Eye", new Vector3(-0.16f, 0.08f, 0.27f), new Vector3(0.07f, 0.13f, 0.035f), GetEyeMaterial(), keepColliders);
                    AddCube(parent, "Right Eye", new Vector3(0.16f, 0.08f, 0.27f), new Vector3(0.07f, 0.13f, 0.035f), GetEyeMaterial(), keepColliders);
                    AddCube(parent, "Mouth A", new Vector3(-0.12f, -0.15f, 0.275f), new Vector3(0.05f, 0.18f, 0.03f), GetEyeMaterial(), keepColliders, Quaternion.Euler(0f, 0f, -35f));
                    AddCube(parent, "Mouth B", new Vector3(0.02f, -0.16f, 0.275f), new Vector3(0.05f, 0.20f, 0.03f), GetEyeMaterial(), keepColliders, Quaternion.Euler(0f, 0f, 35f));
                    AddCube(parent, "Mouth C", new Vector3(0.18f, -0.15f, 0.275f), new Vector3(0.05f, 0.18f, 0.03f), GetEyeMaterial(), keepColliders, Quaternion.Euler(0f, 0f, -35f));
                    break;
                case MiniVanInventoryItem.ZombieArm:
                    AddCube(parent, "Left Arm", Vector3.zero, new Vector3(0.22f, 0.95f, 0.22f), GetTopMaterial(), keepColliders, Quaternion.Euler(70f, 0f, 70f));
                    break;
                case MiniVanInventoryItem.ZombieLeg:
                    AddCube(parent, "Left Leg", Vector3.zero, new Vector3(0.26f, 0.55f, 0.30f), GetBodyMaterial(), keepColliders);
                    AddCube(parent, "Left Foot", new Vector3(0f, -0.20f, 0.14f), new Vector3(0.28f, 0.16f, 0.48f), GetShadeMaterial(), keepColliders);
                    break;
                default:
                    AddCube(parent, "Body", Vector3.zero, new Vector3(0.70f, 1.55f, 0.42f), GetBodyMaterial(), keepColliders);
                    AddCube(parent, "Back Shade", new Vector3(0.22f, 0.06f, -0.235f), new Vector3(0.25f, 1.60f, 0.04f), GetShadeMaterial(), keepColliders);
                    break;
            }
        }

        private static void AddCube(Transform parent, string name, Vector3 position, Vector3 scale,
            Material material, bool keepCollider, Quaternion rotation = default)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = rotation == default ? Quaternion.identity : rotation;
            part.transform.localScale = scale;
            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            if (!keepCollider)
            {
                Collider collider = part.GetComponent<Collider>();
                if (collider != null)
                {
                    Object.Destroy(collider);
                }
            }
        }

        private static void SetColliderState(GameObject root, bool keepColliders)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (keepColliders)
                {
                    colliders[i].enabled = true;
                }
                else
                {
                    Object.Destroy(colliders[i]);
                }
            }
        }

        private static Material GetBodyMaterial()
        {
            return bodyMaterial != null ? bodyMaterial : bodyMaterial = CreateMaterial(
                "Zombie Body Exact", new Color(0.08f, 0.72f, 0.22f, 1f));
        }

        private static Material GetShadeMaterial()
        {
            return shadeMaterial != null ? shadeMaterial : shadeMaterial = CreateMaterial(
                "Zombie Shade Exact", new Color(0.03f, 0.44f, 0.28f, 1f));
        }

        private static Material GetTopMaterial()
        {
            return topMaterial != null ? topMaterial : topMaterial = CreateMaterial(
                "Zombie Top Exact", new Color(0.66f, 0.95f, 0.02f, 1f));
        }

        private static Material GetEyeMaterial()
        {
            return eyeMaterial != null ? eyeMaterial : eyeMaterial = CreateMaterial(
                "Zombie Eye Exact", new Color(1f, 0.02f, 0.08f, 1f));
        }

        private static Material CreateMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader);
            material.name = name;
            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            return material;
        }
    }

    public static class MiniVanFuelPartSpawner
    {
        public static void SpawnDeathParts(Vector3 position, Vector3 impulse, int seed, Transform sourceVisual = null)
        {
            Random.State previous = Random.state;
            Random.InitState(seed);
            MiniVanInventoryItem limb = Random.value < 0.5f
                ? MiniVanInventoryItem.ZombieArm
                : MiniVanInventoryItem.ZombieLeg;
            MiniVanInventoryItem[] parts =
            {
                MiniVanInventoryItem.ZombieTorso,
                limb,
                MiniVanInventoryItem.ZombieHead
            };

            for (int i = 0; i < parts.Length; i++)
            {
                MiniVanInventoryItem item = parts[i];
                GameObject root = MiniVanFuelPartFactory.CreateVisual(item, null, true, sourceVisual);
                root.name = "ZombieFuelPart_" + seed + "_" + i + "_" + item;
                root.transform.position = position + Vector3.up * (0.45f + i * 0.18f) + Random.insideUnitSphere * 0.18f;
                root.transform.rotation = Random.rotation;

                MiniVanPizzaItem pickup = root.AddComponent<MiniVanPizzaItem>();
                pickup.Item = item;
                pickup.Type = MiniVanPizzaItemType.Ingredient;
                pickup.PickupRadius = 2.3f;
                pickup.CanHoldInHands = true;
                pickup.CanPutInInventory = true;

                Rigidbody body = root.AddComponent<Rigidbody>();
                body.mass = item == MiniVanInventoryItem.ZombieTorso ? 2.4f : 1.2f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.linearDamping = 0.18f;
                body.angularDamping = 0.42f;
                body.maxLinearVelocity = 90f;
                body.linearVelocity = impulse * 0.18f + Vector3.up * Random.Range(1.6f, 2.8f) + Random.insideUnitSphere * 1.6f;
                body.angularVelocity = Random.insideUnitSphere * 6f;

                MiniVanZombiePartPhysics partPhysics = root.AddComponent<MiniVanZombiePartPhysics>();
                partPhysics.ConfigureRoadDebris();
            }

            Random.state = previous;
        }
    }
}
