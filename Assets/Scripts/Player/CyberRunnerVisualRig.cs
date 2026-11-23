using UnityEngine;

namespace CyberRunner.Player
{
    /// <summary>
    /// Procedural low-poly 3D Cyber Runner character rig with black combat suit,
    /// glowing cyan energy strips, purple accent armor, futuristic helmet visor,
    /// and athletic procedural run/jump/fall/land/slide/lane-bank/stumble/death animations.
    /// Designed to be swappable with custom rigged character skins in Phase 5.
    /// </summary>
    public sealed class CyberRunnerVisualRig : MonoBehaviour
    {
        [Header("Materials (Assigned by Build Pipeline or Loaded from Resources)")]
        [SerializeField] private Material suitDarkMaterial;
        [SerializeField] private Material neonCyanMaterial;
        [SerializeField] private Material neonPurpleMaterial;

        [Header("Procedural Animation Tuning")]
        [SerializeField] private float strideCycleFrequency = 1.05f;
        [SerializeField] private float maxLegSwingAngle = 52f;
        [SerializeField] private float maxArmSwingAngle = 46f;
        [SerializeField] private float forwardSprintLean = 12f;

        private Transform visualRoot;
        private Transform torsoPivot;
        private Transform headPivot;
        private Transform leftArmPivot;
        private Transform rightArmPivot;
        private Transform leftLegPivot;
        private Transform rightLegPivot;

        private readonly System.Collections.Generic.List<MeshRenderer> suitRenderers = new System.Collections.Generic.List<MeshRenderer>(12);
        private readonly System.Collections.Generic.List<MeshRenderer> primaryNeonRenderers = new System.Collections.Generic.List<MeshRenderer>(10);
        private readonly System.Collections.Generic.List<MeshRenderer> secondaryAccentRenderers = new System.Collections.Generic.List<MeshRenderer>(6);
        private MaterialPropertyBlock skinBlock;

        private float runCyclePhase;
        private float landingSquashTimer;
        private float stumbleFlashTimer;
        private float currentBankAngle;
        private bool rigBuilt;

        private void Awake()
        {
            EnsureMaterials();
            BuildRigIfNeeded();
        }

        public void SetMaterials(Material suitDark, Material neonCyan, Material neonPurple)
        {
            suitDarkMaterial = suitDark;
            neonCyanMaterial = neonCyan;
            neonPurpleMaterial = neonPurple;
        }

        /// <summary>
        /// Applies a cosmetic character skin palette via zero-GC MaterialPropertyBlocks (no runtime material clones).
        /// </summary>
        public void ApplySkin(Color suitColor, Color primaryNeonColor, Color secondaryAccentColor)
        {
            BuildRigIfNeeded();
            if (skinBlock == null)
            {
                skinBlock = new MaterialPropertyBlock();
            }

            ApplyColorToGroup(suitRenderers, suitColor);
            ApplyColorToGroup(primaryNeonRenderers, primaryNeonColor);
            ApplyColorToGroup(secondaryAccentRenderers, secondaryAccentColor);
        }

        private void ApplyColorToGroup(System.Collections.Generic.List<MeshRenderer> renderers, Color color)
        {
            skinBlock.Clear();
            skinBlock.SetColor("_BaseColor", color);
            skinBlock.SetColor("_Color", color);

            for (int i = 0; i < renderers.Count; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].SetPropertyBlock(skinBlock);
                }
            }
        }

        public void TriggerLandingImpact()
        {
            landingSquashTimer = 0.14f;
        }

        public void TriggerStumbleFeedback()
        {
            stumbleFlashTimer = 0.45f;
        }

        public void ResetVisualPose()
        {
            landingSquashTimer = 0f;
            stumbleFlashTimer = 0f;
            currentBankAngle = 0f;
            runCyclePhase = 0f;

            if (rigBuilt && visualRoot != null)
            {
                visualRoot.localPosition = Vector3.zero;
                visualRoot.localRotation = Quaternion.identity;
                visualRoot.localScale = Vector3.one;
            }
        }

        /// <summary>
        /// Updates procedural character animation without per-frame heap allocations.
        /// </summary>
        public void UpdateAnimation(
            PlayerState state,
            float forwardSpeed,
            float verticalVelocity,
            float deltaTime,
            float targetBankAngle = 0f)
        {
            if (!rigBuilt)
            {
                BuildRigIfNeeded();
            }

            if (landingSquashTimer > 0f)
            {
                landingSquashTimer = Mathf.Max(0f, landingSquashTimer - deltaTime);
            }

            if (stumbleFlashTimer > 0f)
            {
                stumbleFlashTimer = Mathf.Max(0f, stumbleFlashTimer - deltaTime);
            }

            float blend = 1f - Mathf.Exp(-16f * deltaTime);
            currentBankAngle = Mathf.Lerp(currentBankAngle, targetBankAngle, blend);

            float squashFactor = landingSquashTimer > 0f ? Mathf.Sin((landingSquashTimer / 0.14f) * Mathf.PI) : 0f;
            float scaleY = 1f - (0.14f * squashFactor);
            float scaleXZ = 1f + (0.08f * squashFactor);

            if (state == PlayerState.Sliding)
            {
                // Athletic futuristic cyber-slide: low crouched profile that visually fits cleanly under laser beams.
                visualRoot.localScale = Vector3.Lerp(visualRoot.localScale, Vector3.one, blend);
                visualRoot.localPosition = Vector3.Lerp(visualRoot.localPosition, new Vector3(0f, -0.56f, 0f), blend);
                visualRoot.localRotation = Quaternion.Slerp(
                    visualRoot.localRotation,
                    Quaternion.Euler(-52f, 0f, -currentBankAngle * 0.6f),
                    blend);

                torsoPivot.localRotation = Quaternion.Slerp(torsoPivot.localRotation, Quaternion.Euler(-14f, 0f, 0f), blend);
                headPivot.localRotation = Quaternion.Slerp(headPivot.localRotation, Quaternion.Euler(42f, 0f, 0f), blend);

                leftLegPivot.localRotation = Quaternion.Slerp(leftLegPivot.localRotation, Quaternion.Euler(-22f, 0f, 0f), blend);
                rightLegPivot.localRotation = Quaternion.Slerp(rightLegPivot.localRotation, Quaternion.Euler(52f, 0f, 0f), blend);
                leftArmPivot.localRotation = Quaternion.Slerp(leftArmPivot.localRotation, Quaternion.Euler(35f, 0f, 22f), blend);
                rightArmPivot.localRotation = Quaternion.Slerp(rightArmPivot.localRotation, Quaternion.Euler(-38f, 0f, -22f), blend);
                return;
            }

            if (state == PlayerState.Dead)
            {
                visualRoot.localScale = Vector3.one;
                visualRoot.localPosition = Vector3.Lerp(visualRoot.localPosition, new Vector3(0f, -0.42f, -0.25f), blend * 0.6f);
                visualRoot.localRotation = Quaternion.Slerp(visualRoot.localRotation, Quaternion.Euler(-78f, 12f, 18f), blend * 0.6f);
                torsoPivot.localRotation = Quaternion.Slerp(torsoPivot.localRotation, Quaternion.Euler(-12f, 0f, 0f), blend);
                leftArmPivot.localRotation = Quaternion.Slerp(leftArmPivot.localRotation, Quaternion.Euler(-45f, 0f, 38f), blend);
                rightArmPivot.localRotation = Quaternion.Slerp(rightArmPivot.localRotation, Quaternion.Euler(-35f, 0f, -42f), blend);
                leftLegPivot.localRotation = Quaternion.Slerp(leftLegPivot.localRotation, Quaternion.Euler(18f, 0f, 8f), blend);
                rightLegPivot.localRotation = Quaternion.Slerp(rightLegPivot.localRotation, Quaternion.Euler(-14f, 0f, -8f), blend);
                return;
            }

            visualRoot.localScale = new Vector3(scaleXZ, scaleY, scaleXZ);

            float stumbleRoll = stumbleFlashTimer > 0f ? Mathf.Sin(stumbleFlashTimer * 42f) * 14f : 0f;
            visualRoot.localRotation = Quaternion.Slerp(
                visualRoot.localRotation,
                Quaternion.Euler(0f, 0f, -currentBankAngle + stumbleRoll),
                blend);

            if (state == PlayerState.Running || state == PlayerState.Landing || state == PlayerState.Stumbled)
            {
                runCyclePhase += deltaTime * Mathf.Max(6f, forwardSpeed) * strideCycleFrequency;
                float swing = Mathf.Sin(runCyclePhase);
                float bob = Mathf.Abs(Mathf.Cos(runCyclePhase)) * 0.08f;

                visualRoot.localPosition = Vector3.Lerp(
                    visualRoot.localPosition,
                    new Vector3(0f, bob - (0.06f * squashFactor), 0f),
                    blend);

                torsoPivot.localRotation = Quaternion.Slerp(
                    torsoPivot.localRotation,
                    Quaternion.Euler(forwardSprintLean, 0f, -swing * 4.5f),
                    blend);
                headPivot.localRotation = Quaternion.Slerp(
                    headPivot.localRotation,
                    Quaternion.Euler(-forwardSprintLean * 0.5f, 0f, 0f),
                    blend);

                leftLegPivot.localRotation = Quaternion.Euler(swing * maxLegSwingAngle, 0f, 0f);
                rightLegPivot.localRotation = Quaternion.Euler(-swing * maxLegSwingAngle, 0f, 0f);
                leftArmPivot.localRotation = Quaternion.Euler(-swing * maxArmSwingAngle, 0f, 8f);
                rightArmPivot.localRotation = Quaternion.Euler(swing * maxArmSwingAngle, 0f, -8f);
            }
            else if (state == PlayerState.Jumping || state == PlayerState.Falling)
            {
                float airtimeTilt = Mathf.Clamp(verticalVelocity * 1.4f, -14f, 18f);

                visualRoot.localPosition = Vector3.Lerp(visualRoot.localPosition, Vector3.zero, blend);
                torsoPivot.localRotation = Quaternion.Slerp(
                    torsoPivot.localRotation,
                    Quaternion.Euler(10f - airtimeTilt * 0.35f, 0f, 0f),
                    blend);

                headPivot.localRotation = Quaternion.Slerp(headPivot.localRotation, Quaternion.identity, blend);

                leftLegPivot.localRotation = Quaternion.Slerp(leftLegPivot.localRotation, Quaternion.Euler(-38f, 0f, 0f), blend);
                rightLegPivot.localRotation = Quaternion.Slerp(rightLegPivot.localRotation, Quaternion.Euler(26f, 0f, 0f), blend);
                leftArmPivot.localRotation = Quaternion.Slerp(leftArmPivot.localRotation, Quaternion.Euler(-32f, 0f, 18f), blend);
                rightArmPivot.localRotation = Quaternion.Slerp(rightArmPivot.localRotation, Quaternion.Euler(28f, 0f, -18f), blend);
            }
            else
            {
                float breath = Mathf.Sin(Time.time * 2.5f) * 0.02f;
                visualRoot.localPosition = new Vector3(0f, breath, 0f);
                torsoPivot.localRotation = Quaternion.identity;
                headPivot.localRotation = Quaternion.identity;
                leftLegPivot.localRotation = Quaternion.identity;
                rightLegPivot.localRotation = Quaternion.identity;
                leftArmPivot.localRotation = Quaternion.Euler(0f, 0f, 6f);
                rightArmPivot.localRotation = Quaternion.Euler(0f, 0f, -6f);
            }
        }

        private void EnsureMaterials()
        {
            if (suitDarkMaterial == null)
            {
                suitDarkMaterial = Resources.Load<Material>("Materials/CyberSuit_Dark");
            }

            if (neonCyanMaterial == null)
            {
                neonCyanMaterial = Resources.Load<Material>("Materials/CyberNeon_Cyan");
            }

            if (neonPurpleMaterial == null)
            {
                neonPurpleMaterial = Resources.Load<Material>("Materials/CyberNeon_Purple");
            }

            Shader simpleLit = Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Standard");
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? simpleLit;

            if (suitDarkMaterial == null && simpleLit != null)
            {
                suitDarkMaterial = new Material(simpleLit);
                suitDarkMaterial.SetColor("_BaseColor", new Color(0.08f, 0.10f, 0.15f, 1f));
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

        private void BuildRigIfNeeded()
        {
            if (rigBuilt)
            {
                return;
            }

            Transform existing = transform.Find("VisualRoot");
            if (existing != null)
            {
                visualRoot = existing;
                torsoPivot = visualRoot.Find("TorsoPivot");
                headPivot = torsoPivot != null ? torsoPivot.Find("HeadPivot") : null;
                leftArmPivot = torsoPivot != null ? torsoPivot.Find("LeftArmPivot") : null;
                rightArmPivot = torsoPivot != null ? torsoPivot.Find("RightArmPivot") : null;
                leftLegPivot = visualRoot.Find("LeftLegPivot");
                rightLegPivot = visualRoot.Find("RightLegPivot");

                if (torsoPivot != null && headPivot != null && leftArmPivot != null &&
                    rightArmPivot != null && leftLegPivot != null && rightLegPivot != null)
                {
                    rigBuilt = true;
                    return;
                }
            }

            visualRoot = CreatePivot("VisualRoot", transform, Vector3.zero);
            suitRenderers.Clear();
            primaryNeonRenderers.Clear();
            secondaryAccentRenderers.Clear();

            CreatePart("Pelvis", PrimitiveType.Cube, visualRoot, new Vector3(0f, 0.92f, 0f), new Vector3(0.40f, 0.20f, 0.26f), suitDarkMaterial, suitRenderers);
            CreatePart("BeltGlow", PrimitiveType.Cube, visualRoot, new Vector3(0f, 0.96f, 0f), new Vector3(0.43f, 0.05f, 0.29f), neonPurpleMaterial, secondaryAccentRenderers);

            torsoPivot = CreatePivot("TorsoPivot", visualRoot, new Vector3(0f, 1.00f, 0f));
            CreatePart("ChestArmor", PrimitiveType.Cube, torsoPivot, new Vector3(0f, 0.28f, 0f), new Vector3(0.48f, 0.54f, 0.28f), suitDarkMaterial, suitRenderers);
            CreatePart("SpineStrip", PrimitiveType.Cube, torsoPivot, new Vector3(0f, 0.28f, -0.145f), new Vector3(0.08f, 0.44f, 0.03f), neonCyanMaterial, primaryNeonRenderers);
            CreatePart("ChestCore", PrimitiveType.Cube, torsoPivot, new Vector3(0f, 0.34f, 0.145f), new Vector3(0.24f, 0.10f, 0.03f), neonCyanMaterial, primaryNeonRenderers);
            CreatePart("LeftShoulderPad", PrimitiveType.Cube, torsoPivot, new Vector3(-0.30f, 0.48f, 0f), new Vector3(0.16f, 0.14f, 0.24f), neonPurpleMaterial, secondaryAccentRenderers);
            CreatePart("RightShoulderPad", PrimitiveType.Cube, torsoPivot, new Vector3(0.30f, 0.48f, 0f), new Vector3(0.16f, 0.14f, 0.24f), neonPurpleMaterial, secondaryAccentRenderers);

            headPivot = CreatePivot("HeadPivot", torsoPivot, new Vector3(0f, 0.58f, 0f));
            CreatePart("Helmet", PrimitiveType.Sphere, headPivot, new Vector3(0f, 0.17f, 0f), new Vector3(0.34f, 0.36f, 0.36f), suitDarkMaterial, suitRenderers);
            CreatePart("VisorGlow", PrimitiveType.Cube, headPivot, new Vector3(0f, 0.18f, 0.15f), new Vector3(0.26f, 0.09f, 0.09f), neonCyanMaterial, primaryNeonRenderers);
            CreatePart("HelmetCrest", PrimitiveType.Cube, headPivot, new Vector3(0f, 0.32f, -0.02f), new Vector3(0.06f, 0.07f, 0.28f), neonCyanMaterial, primaryNeonRenderers);

            leftArmPivot = CreatePivot("LeftArmPivot", torsoPivot, new Vector3(-0.32f, 0.46f, 0f));
            CreatePart("LeftArmMesh", PrimitiveType.Capsule, leftArmPivot, new Vector3(0f, -0.26f, 0f), new Vector3(0.13f, 0.29f, 0.13f), suitDarkMaterial, suitRenderers);
            CreatePart("LeftForearmGlow", PrimitiveType.Cube, leftArmPivot, new Vector3(-0.05f, -0.36f, 0.02f), new Vector3(0.05f, 0.20f, 0.06f), neonCyanMaterial, primaryNeonRenderers);

            rightArmPivot = CreatePivot("RightArmPivot", torsoPivot, new Vector3(0.32f, 0.46f, 0f));
            CreatePart("RightArmMesh", PrimitiveType.Capsule, rightArmPivot, new Vector3(0f, -0.26f, 0f), new Vector3(0.13f, 0.29f, 0.13f), suitDarkMaterial, suitRenderers);
            CreatePart("RightForearmGlow", PrimitiveType.Cube, rightArmPivot, new Vector3(0.05f, -0.36f, 0.02f), new Vector3(0.05f, 0.20f, 0.06f), neonCyanMaterial, primaryNeonRenderers);

            leftLegPivot = CreatePivot("LeftLegPivot", visualRoot, new Vector3(-0.13f, 0.86f, 0f));
            CreatePart("LeftLegMesh", PrimitiveType.Capsule, leftLegPivot, new Vector3(0f, -0.38f, 0f), new Vector3(0.16f, 0.40f, 0.16f), suitDarkMaterial, suitRenderers);
            CreatePart("LeftShoe", PrimitiveType.Cube, leftLegPivot, new Vector3(0f, -0.78f, 0.06f), new Vector3(0.17f, 0.12f, 0.32f), suitDarkMaterial, suitRenderers);
            CreatePart("LeftShoeSole", PrimitiveType.Cube, leftLegPivot, new Vector3(0f, -0.84f, 0.06f), new Vector3(0.18f, 0.04f, 0.34f), neonCyanMaterial, primaryNeonRenderers);

            rightLegPivot = CreatePivot("RightLegPivot", visualRoot, new Vector3(0.13f, 0.86f, 0f));
            CreatePart("RightLegMesh", PrimitiveType.Capsule, rightLegPivot, new Vector3(0f, -0.38f, 0f), new Vector3(0.16f, 0.40f, 0.16f), suitDarkMaterial, suitRenderers);
            CreatePart("RightShoe", PrimitiveType.Cube, rightLegPivot, new Vector3(0f, -0.78f, 0.06f), new Vector3(0.17f, 0.12f, 0.32f), suitDarkMaterial, suitRenderers);
            CreatePart("RightShoeSole", PrimitiveType.Cube, rightLegPivot, new Vector3(0f, -0.84f, 0.06f), new Vector3(0.18f, 0.04f, 0.34f), neonCyanMaterial, primaryNeonRenderers);

            rigBuilt = true;
        }

        private static Transform CreatePivot(string name, Transform parent, Vector3 localPos)
        {
            var go = new GameObject(name);
            Transform tr = go.transform;
            tr.SetParent(parent, false);
            tr.localPosition = localPos;
            tr.localRotation = Quaternion.identity;
            return tr;
        }

        private static void CreatePart(
            string name,
            PrimitiveType primitiveType,
            Transform parent,
            Vector3 localPos,
            Vector3 localScale,
            Material material,
            System.Collections.Generic.List<MeshRenderer> targetGroup)
        {
            GameObject part = GameObject.CreatePrimitive(primitiveType);
            part.name = name;

            Collider col = part.GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }

            Transform tr = part.transform;
            tr.SetParent(parent, false);
            tr.localPosition = localPos;
            tr.localRotation = Quaternion.identity;
            tr.localScale = localScale;

            MeshRenderer renderer = part.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                if (material != null)
                {
                    renderer.sharedMaterial = material;
                }
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                targetGroup?.Add(renderer);
            }
        }
    }
}
