using UnityEngine;

namespace CyberRunner.Environment
{
    /// <summary>
    /// Phase 1 zero-allocation pooled neon highway runway.
    /// Pre-allocates a small ring buffer of stylized cyberpunk road segments and recycles them
    /// ahead of the runner so Phase 1 can run endlessly on low-end Android phones.
    /// Expanded into full multi-biome procedural track generation in Phases 2 & 3.
    /// </summary>
    public sealed class Phase1RunwayScroller : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform playerTransform;

        [Header("Pool Geometry")]
        [SerializeField] private int segmentCount = 4;
        [SerializeField] private float segmentLength = 40f;
        [SerializeField] private float roadWidth = 9.6f;

        [Header("Materials")]
        [SerializeField] private Material roadDarkMaterial;
        [SerializeField] private Material neonCyanMaterial;
        [SerializeField] private Material neonPurpleMaterial;
        [SerializeField] private Material buildingDarkMaterial;

        private Transform[] segments;
        private float furthestSegmentZ;
        private bool built;

        public void Configure(
            Transform player,
            Material roadDark,
            Material neonCyan,
            Material neonPurple,
            Material buildingDark)
        {
            playerTransform = player;
            roadDarkMaterial = roadDark;
            neonCyanMaterial = neonCyan;
            neonPurpleMaterial = neonPurple;
            buildingDarkMaterial = buildingDark;
        }

        private void Awake()
        {
            EnsureMaterials();
            BuildPoolIfNeeded();
        }

        private void Update()
        {
            if (!built || playerTransform == null)
            {
                return;
            }

            float recycleThresholdZ = playerTransform.position.z - (segmentLength * 1.15f);
            for (int i = 0; i < segments.Length; i++)
            {
                Transform seg = segments[i];
                if (seg.position.z < recycleThresholdZ)
                {
                    furthestSegmentZ += segmentLength;
                    seg.position = new Vector3(0f, 0f, furthestSegmentZ);
                }
            }
        }

        private void EnsureMaterials()
        {
            if (roadDarkMaterial == null)
            {
                roadDarkMaterial = Resources.Load<Material>("Materials/CyberRoad_Dark");
            }

            if (neonCyanMaterial == null)
            {
                neonCyanMaterial = Resources.Load<Material>("Materials/CyberNeon_Cyan");
            }

            if (neonPurpleMaterial == null)
            {
                neonPurpleMaterial = Resources.Load<Material>("Materials/CyberNeon_Purple");
            }

            if (buildingDarkMaterial == null)
            {
                buildingDarkMaterial = Resources.Load<Material>("Materials/CyberBuilding_Dark");
            }

            Shader simpleLit = Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Standard");
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? simpleLit;

            if (roadDarkMaterial == null && simpleLit != null)
            {
                roadDarkMaterial = new Material(simpleLit);
                roadDarkMaterial.SetColor("_BaseColor", new Color(0.05f, 0.07f, 0.12f, 1f));
            }

            if (buildingDarkMaterial == null && simpleLit != null)
            {
                buildingDarkMaterial = new Material(simpleLit);
                buildingDarkMaterial.SetColor("_BaseColor", new Color(0.04f, 0.05f, 0.10f, 1f));
            }

            if (neonCyanMaterial == null && unlit != null)
            {
                neonCyanMaterial = new Material(unlit);
                neonCyanMaterial.SetColor("_BaseColor", new Color(0.0f, 0.94f, 1.0f, 1f));
            }

            if (neonPurpleMaterial == null && unlit != null)
            {
                neonPurpleMaterial = new Material(unlit);
                neonPurpleMaterial.SetColor("_BaseColor", new Color(0.68f, 0.22f, 1.0f, 1f));
            }
        }

        private void BuildPoolIfNeeded()
        {
            if (built)
            {
                return;
            }

            segments = new Transform[segmentCount];
            float startZ = 0f;

            for (int i = 0; i < segmentCount; i++)
            {
                float z = startZ + (i * segmentLength);
                segments[i] = CreateRunwaySegment(i, z);
                furthestSegmentZ = z;
            }

            built = true;
        }

        private Transform CreateRunwaySegment(int index, float zPos)
        {
            var root = new GameObject($"RunwaySegment_{index}");
            Transform tr = root.transform;
            tr.SetParent(transform, false);
            tr.position = new Vector3(0f, 0f, zPos);

            // Drivable road deck (top surface strictly at Y = 0.0m, BoxCollider enabled for CharacterController).
            CreateMesh("RoadDeck", PrimitiveType.Cube, tr,
                new Vector3(0f, -0.5f, segmentLength * 0.5f),
                new Vector3(roadWidth, 1.0f, segmentLength),
                roadDarkMaterial,
                keepCollider: true);

            // Outer neon edge curbs (left & right).
            float edgeX = (roadWidth * 0.5f) - 0.16f;
            CreateMesh("LeftNeonCurb", PrimitiveType.Cube, tr,
                new Vector3(-edgeX, 0.04f, segmentLength * 0.5f),
                new Vector3(0.20f, 0.08f, segmentLength),
                neonCyanMaterial,
                keepCollider: false);

            CreateMesh("RightNeonCurb", PrimitiveType.Cube, tr,
                new Vector3(edgeX, 0.04f, segmentLength * 0.5f),
                new Vector3(0.20f, 0.08f, segmentLength),
                neonCyanMaterial,
                keepCollider: false);

            // Subtle 3-lane guide dividers (previewing Phase 2's 3-lane highway geometry: -2.8m, 0m, +2.8m).
            CreateMesh("LaneDividerLeft", PrimitiveType.Cube, tr,
                new Vector3(-1.45f, 0.015f, segmentLength * 0.5f),
                new Vector3(0.08f, 0.02f, segmentLength),
                neonPurpleMaterial,
                keepCollider: false);

            CreateMesh("LaneDividerRight", PrimitiveType.Cube, tr,
                new Vector3(1.45f, 0.015f, segmentLength * 0.5f),
                new Vector3(0.08f, 0.02f, segmentLength),
                neonPurpleMaterial,
                keepCollider: false);

            // Lightweight cyberpunk roadside pylons & distant neon skyscraper silhouettes for speed perception.
            int pylonPairs = 2;
            float spacing = segmentLength / pylonPairs;
            for (int p = 0; p < pylonPairs; p++)
            {
                float localZ = (p + 0.5f) * spacing;
                CreatePylon(tr, -edgeX - 0.65f, localZ, p % 2 == 0 ? neonCyanMaterial : neonPurpleMaterial);
                CreatePylon(tr, edgeX + 0.65f, localZ, p % 2 == 0 ? neonPurpleMaterial : neonCyanMaterial);
            }

            // Two minimalist stylized skyscraper blocks flanking the highway.
            float leftHeight = 18f + ((index * 7) % 14);
            float rightHeight = 22f + ((index * 11) % 16);
            CreateSkyscraperSilhouette(tr, -13.5f, segmentLength * 0.35f, leftHeight, neonCyanMaterial);
            CreateSkyscraperSilhouette(tr, 13.5f, segmentLength * 0.70f, rightHeight, neonPurpleMaterial);

            return tr;
        }

        private void CreatePylon(Transform parent, float xPos, float zPos, Material glowMat)
        {
            CreateMesh("PylonPost", PrimitiveType.Cube, parent,
                new Vector3(xPos, 2.1f, zPos),
                new Vector3(0.32f, 4.2f, 0.32f),
                buildingDarkMaterial,
                keepCollider: false);

            CreateMesh("PylonGlow", PrimitiveType.Cube, parent,
                new Vector3(xPos, 3.6f, zPos),
                new Vector3(0.38f, 0.9f, 0.38f),
                glowMat,
                keepCollider: false);
        }

        private void CreateSkyscraperSilhouette(Transform parent, float xPos, float zPos, float height, Material trimMat)
        {
            CreateMesh("SkyscraperBody", PrimitiveType.Cube, parent,
                new Vector3(xPos, (height * 0.5f) - 2f, zPos),
                new Vector3(7.5f, height, 11f),
                buildingDarkMaterial,
                keepCollider: false);

            float trimX = xPos > 0f ? xPos - 3.78f : xPos + 3.78f;
            CreateMesh("SkyscraperNeonTrim", PrimitiveType.Cube, parent,
                new Vector3(trimX, height * 0.52f, zPos),
                new Vector3(0.14f, height * 0.65f, 0.35f),
                trimMat,
                keepCollider: false);
        }

        private static void CreateMesh(
            string name,
            PrimitiveType primitiveType,
            Transform parent,
            Vector3 localPos,
            Vector3 localScale,
            Material material,
            bool keepCollider)
        {
            GameObject go = GameObject.CreatePrimitive(primitiveType);
            go.name = name;

            if (!keepCollider)
            {
                Collider col = go.GetComponent<Collider>();
                if (col != null)
                {
                    Destroy(col);
                }
            }

            Transform tr = go.transform;
            tr.SetParent(parent, false);
            tr.localPosition = localPos;
            tr.localRotation = Quaternion.identity;
            tr.localScale = localScale;

            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }
    }
}
