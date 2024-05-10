using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace Esska.AV3Obfuscator.Editor {

    public class Obfuscator : ScriptableObject {

        public static readonly List<string> VRC_RESERVED_ANIMATOR_PARAMETERS = new List<string>() {
            "AFK",
            "AngularY",
            "Earmuffs",
            "EyeHeightAsMeters",
            "EyeHeightAsPercent",
            "GestureLeft",
            "GestureLeftWeight",
            "GestureRight",
            "GestureRightWeight",
            "Grounded",
            "InStation",
            "IsLocal",
            "IsOnFriendsList",
            "MuteSelf",
            "ScaleFactor",
            "ScaleFactorInverse",
            "ScaleModified",
            "Seated",
            "TrackingType",
            "Upright",
            "VelocityMagnitude",
            "VelocityX",
            "VelocityY",
            "VelocityZ",
            "Viseme",
            "Voice",
            "VRMode"
        };

        public static readonly List<string> VRC_PHYS_BONES_SUFFIXES = new List<string>() {
            "_IsGrabbed",
            "_IsPosed",
            "_Angle",
            "_Stretch"
        };

        public static readonly List<string> TRANSFORM_NAMES_MMD = new List<string>() {
            "Body"
        };

        const string TITLE = "AV3Obfuscator";
        const string FOLDER = "Obfuscated";
        const string SUFFIX = "_Obfuscated";

        ObfuscationConfiguration config;
        string folder;
        List<string> allParameters;
        Dictionary<AnimationClip, AnimationClip> obfuscatedAnimationClips;
        Dictionary<AudioClip, AudioClip> obfuscatedAudioClips;
        Dictionary<AvatarMask, AvatarMask> obfuscatedAvatarMasks;
        Dictionary<string, string> obfuscatedBlendShapeNames;
        Dictionary<BlendTree, BlendTree> obfuscatedBlendTrees;
        Dictionary<AnimatorController, AnimatorController> obfuscatedControllers;
        Dictionary<VRCExpressionsMenu, VRCExpressionsMenu> obfuscatedExpressionMenus;
        Dictionary<Material, Material> obfuscatedMaterials;
        Dictionary<Mesh, Mesh> obfuscatedMeshes;
        Dictionary<string, string> obfuscatedParameters;
        Dictionary<Texture, Texture> obfuscatedTextures;
        Dictionary<string, string> obfuscatedTransformNames;

        public void ClearObfuscatedAll() {
            string[] searchFolders = new string[] { $"Assets/{FOLDER}" };

            foreach (var asset in AssetDatabase.FindAssets("", searchFolders)) {
                string path = AssetDatabase.GUIDToAssetPath(asset);
                AssetDatabase.DeleteAsset(path);
            }
        }

        public void ClearObfuscatedScene() {
            Scene scene = SceneManager.GetActiveScene();
            GameObject[] rootGameObjects = scene.GetRootGameObjects();

            foreach (GameObject rootGameObject in rootGameObjects) {

                if (IsObfuscatedGameObject(rootGameObject)) {
                    string guid = rootGameObject.name.Replace(SUFFIX, "");
                    string folder = $"Assets/{FOLDER}/{guid}";
                    string[] searchFolders = new string[] { folder };

                    foreach (var asset in AssetDatabase.FindAssets("", searchFolders)) {
                        string path = AssetDatabase.GUIDToAssetPath(asset);
                        AssetDatabase.DeleteAsset(path);
                    }

                    AssetDatabase.DeleteAsset(folder);
                }
            }
        }

        public void ClearObfuscateGameObjects() {
            Scene scene = SceneManager.GetActiveScene();
            GameObject[] rootGameObjects = scene.GetRootGameObjects();

            foreach (GameObject rootGameObject in rootGameObjects) {

                if (IsObfuscatedGameObject(rootGameObject))
                    DestroyImmediate(rootGameObject);
            }
        }

        public void Obfuscate(GameObject gameObject, ObfuscationConfiguration config) {
            this.config = config;

            if (!AssetDatabase.IsValidFolder("Assets/" + FOLDER))
                AssetDatabase.CreateFolder("Assets", FOLDER);

            folder = "Assets/" + FOLDER;

            string guid = GUID.Generate().ToString();

            AssetDatabase.CreateFolder(folder, guid);

            folder = folder + "/" + guid;

            GameObject obfuscatedGameObject = Instantiate(gameObject);
            obfuscatedGameObject.name = guid + SUFFIX;
            obfuscatedGameObject.SetActive(true);

            gameObject.SetActive(false);

            VRCAvatarDescriptor descriptor = obfuscatedGameObject.GetComponent<VRCAvatarDescriptor>();

            if (descriptor == null)
                throw new System.Exception("VRCAvatarDescriptor component is missing");

            Animator animator = obfuscatedGameObject.GetComponent<Animator>();

            if (animator == null)
                throw new System.Exception("Animator component is missing");

            if (animator.avatar == null)
                throw new System.Exception("Animator has no avatar");

            Animator[] animators = descriptor.GetComponentsInChildren<Animator>(true);

            if (animators.Length > 1)
                throw new System.Exception("More than one animator found. Obfuscation of additional animators below the hierarchy is not supported.");

            Transform armatureTransform = GetArmatureTransform(animator);

            if (armatureTransform == null)
                throw new System.Exception("Armature not found.");

            Init();

            EditorUtility.DisplayProgressBar(TITLE, "Obfuscate Transforms", 0.1f);
            ObfuscateTransforms(obfuscatedGameObject.transform, obfuscatedGameObject.transform); // has to run first

            EditorUtility.DisplayProgressBar(TITLE, "Obfuscate Avatar", 0.2f);
            animator.avatar = ObfuscateAvatar(animator, armatureTransform); // has to run after ObfuscateTransforms

            if (config.obfuscateMeshes) {
                EditorUtility.DisplayProgressBar(TITLE, "Obfuscate Meshes", 0.3f);
                ObfuscateMeshesAndBlendShapes(descriptor);
            }

            if (config.obfuscateMaterials) {
                EditorUtility.DisplayProgressBar(TITLE, "Obfuscate Materials", 0.4f);
                ObfuscateMaterials(descriptor);
            }

            EditorUtility.DisplayProgressBar(TITLE, "Obfuscate Controllers + Animation Clips", 0.5f);
            ObfuscateControllers(descriptor, animator); // has to run after ObfuscateTransforms

            if (config.obfuscateExpressionParameters) {
                EditorUtility.DisplayProgressBar(TITLE, "Obfuscate VRC Expression Parameters + Menus", 0.7f);
                ObfuscateExpressionsAndMenus(descriptor); // has to run after ObfuscateControllers

                if (config.obfuscateParameters)
                    ObfuscatePhysBonesAndContactReceivers(descriptor); // has to run after ObfuscateControllers
            }

            if (config.obfuscateMaterials && config.obfuscateTextures) {
                EditorUtility.DisplayProgressBar(TITLE, "Obfuscate Textures", 0.8f);
                ObfuscateTextures(); // has to run after ObfuscateMaterials and ObfuscateControllers->ObfuscateClips has collected all materials

                if (config.obfuscateTextures)
                    ObfuscateCameras(descriptor);
            }

            if (config.obfuscateAudioClips) {
                EditorUtility.DisplayProgressBar(TITLE, "Obfuscate Audio Clips", 0.9f);
                ObfuscateAudioClips(descriptor);
            }

            AV3Obfuscator[] obfuscators = obfuscatedGameObject.GetComponentsInChildren<AV3Obfuscator>(true);

            foreach (var obfuscator in obfuscators) {
                DestroyImmediate(obfuscator);
            }

            AssetDatabase.SaveAssets();

            EditorUtility.ClearProgressBar();
        }

        void Init() {
            allParameters = new List<string>();
            obfuscatedAnimationClips = new Dictionary<AnimationClip, AnimationClip>();
            obfuscatedAudioClips = new Dictionary<AudioClip, AudioClip>();
            obfuscatedAvatarMasks = new Dictionary<AvatarMask, AvatarMask>();
            obfuscatedBlendShapeNames = new Dictionary<string, string>();
            obfuscatedBlendTrees = new Dictionary<BlendTree, BlendTree>();
            obfuscatedControllers = new Dictionary<AnimatorController, AnimatorController>();
            obfuscatedExpressionMenus = new Dictionary<VRCExpressionsMenu, VRCExpressionsMenu>();
            obfuscatedMaterials = new Dictionary<Material, Material>();
            obfuscatedMeshes = new Dictionary<Mesh, Mesh>();
            obfuscatedParameters = new Dictionary<string, string>();
            obfuscatedTextures = new Dictionary<Texture, Texture>();
            obfuscatedTransformNames = new Dictionary<string, string>();
        }

        void ObfuscateTransforms(Transform rootTransform, Transform transform) {

            for (int i = 0; i < transform.childCount; i++) {
                Transform child = transform.GetChild(i);

                if (config.preserveMMD && child.parent == rootTransform && TRANSFORM_NAMES_MMD.Contains(child.name))
                    continue;

                child.name = ObfuscateTransformName(child.name);
                ObfuscateTransforms(rootTransform, child);
            }
        }

        string ObfuscateTransformName(string name) {

            if (string.IsNullOrEmpty(name))
                return "";

            if (obfuscatedTransformNames.ContainsKey(name))
                return obfuscatedTransformNames[name];
            else {
                string obfuscatedName = GUID.Generate().ToString();
                obfuscatedTransformNames.Add(name, obfuscatedName);
                return obfuscatedName;
            }
        }

        string ObfuscateTransformPath(string path) {
            string[] names = path.Split(new string[] { "/" }, System.StringSplitOptions.None);

            for (int i = 0; i < names.Length; i++) {
                names[i] = ObfuscateTransformName(names[i]);
            }

            return string.Join("/", names);
        }

        Avatar ObfuscateAvatar(Animator animator, Transform armatureTransform) {

            if (animator.avatar == null)
                return null;

            if (animator.avatar.isValid) {
                Avatar obfuscatedAvatar = null;

                if (animator.avatar.isHuman) {
                    List<SkeletonBone> skeletonBones = new List<SkeletonBone>();
                    List<HumanBone> humanBones = new List<HumanBone>();

                    for (int i = 0; i < animator.avatar.humanDescription.skeleton.Length; i++) {

                        skeletonBones.Add(new SkeletonBone() {
                            name = i == 0 ? animator.name : ObfuscateTransformName(animator.avatar.humanDescription.skeleton[i].name),
                            position = animator.avatar.humanDescription.skeleton[i].position,
                            rotation = animator.avatar.humanDescription.skeleton[i].rotation,
                            scale = animator.avatar.humanDescription.skeleton[i].scale
                        });
                    }

                    for (int i = 0; i < animator.avatar.humanDescription.human.Length; i++) {

                        humanBones.Add(new HumanBone() {
                            boneName = ObfuscateTransformName(animator.avatar.humanDescription.human[i].boneName),
                            humanName = animator.avatar.humanDescription.human[i].humanName,
                            limit = animator.avatar.humanDescription.human[i].limit
                        });
                    }

                    HumanDescription description = new HumanDescription() {
                        armStretch = animator.avatar.humanDescription.armStretch,
                        feetSpacing = animator.avatar.humanDescription.feetSpacing,
                        hasTranslationDoF = animator.avatar.humanDescription.hasTranslationDoF,
                        human = humanBones.ToArray(),
                        legStretch = animator.avatar.humanDescription.legStretch,
                        lowerArmTwist = animator.avatar.humanDescription.lowerArmTwist,
                        lowerLegTwist = animator.avatar.humanDescription.lowerLegTwist,
                        skeleton = skeletonBones.ToArray(),
                        upperArmTwist = animator.avatar.humanDescription.upperArmTwist,
                        upperLegTwist = animator.avatar.humanDescription.upperLegTwist
                    };

                    Transform[] children = animator.transform.GetComponentsInChildren<Transform>(true);

                    for (int i = 0; i < children.Length; i++) {

                        if (children[i].parent == animator.transform && children[i] != armatureTransform)
                            children[i].parent = null;
                    }

                    obfuscatedAvatar = AvatarBuilder.BuildHumanAvatar(animator.gameObject, description);

                    for (int i = 0; i < children.Length; i++) {

                        if (children[i].parent == null)
                            children[i].parent = animator.transform;
                    }
                }
                else {
                    obfuscatedAvatar = AvatarBuilder.BuildGenericAvatar(animator.gameObject, "");
                }

                if (obfuscatedAvatar != null && obfuscatedAvatar.isValid) {
                    AssetDatabase.CreateAsset(obfuscatedAvatar, GetObfuscatedPath<Avatar>());

                    return obfuscatedAvatar;
                }
            }

            throw new System.Exception($"Obfuscation of Avatar '{animator.avatar.name}' failed");
        }

        void ObfuscateMeshesAndBlendShapes(VRCAvatarDescriptor descriptor) {
            SkinnedMeshRenderer[] skinnedMeshRenderers = descriptor.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            MeshFilter[] meshFilters = descriptor.GetComponentsInChildren<MeshFilter>(true);
            ParticleSystem[] particleSystems = descriptor.GetComponentsInChildren<ParticleSystem>(true);
            ParticleSystemRenderer[] particleSystemRenderers = descriptor.GetComponentsInChildren<ParticleSystemRenderer>(true);

            foreach (var renderer in skinnedMeshRenderers) {

                if (config.preserveMMD && TRANSFORM_NAMES_MMD.Contains(renderer.name))
                    renderer.sharedMesh = ObfuscateMeshAndBlendShapes(renderer.sharedMesh, false);
                else
                    renderer.sharedMesh = ObfuscateMeshAndBlendShapes(renderer.sharedMesh, true);
            }

            foreach (var filter in meshFilters) {
                filter.sharedMesh = ObfuscateMeshAndBlendShapes(filter.sharedMesh, true);
            }

            foreach (var particleSystem in particleSystems) {
                ParticleSystem.ShapeModule shapeModule = particleSystem.shape;
                shapeModule.mesh = ObfuscateMeshAndBlendShapes(shapeModule.mesh, true);
            }

            foreach (var renderer in particleSystemRenderers) {
                renderer.mesh = ObfuscateMeshAndBlendShapes(renderer.mesh, true);
            }

            if (config.obfuscateBlendShapes) {

                for (int i = 0; i < descriptor.VisemeBlendShapes.Length; i++) {

                    if (obfuscatedBlendShapeNames.ContainsKey(descriptor.VisemeBlendShapes[i]))
                        descriptor.VisemeBlendShapes[i] = obfuscatedBlendShapeNames[descriptor.VisemeBlendShapes[i]];
                }
            }
        }

        Mesh ObfuscateMeshAndBlendShapes(Mesh mesh, bool obfuscateBlendShapes) {

            if (mesh == null)
                return null;

            string meshPath = AssetDatabase.GetAssetPath(mesh);

            if (string.IsNullOrEmpty(meshPath) || !mesh.isReadable) {
                Debug.LogError($"Mesh '{mesh.name}' cannot be obfuscated. It will be ignored.");
                return mesh;
            }
            else if (meshPath.Contains("unity default resources")) {
                Debug.LogError($"Unity built-in Mesh '{mesh.name}' cannot be obfuscated. It will be ignored.");
                return mesh;
            }

            if (obfuscatedMeshes.ContainsKey(mesh)) {
                return obfuscatedMeshes[mesh];
            }
            else {
                Mesh obfuscatedMesh = Instantiate(mesh);
                obfuscatedMesh.ClearBlendShapes();

                // Transfer and obfuscate blend shapes
                for (int shapeIndex = 0; shapeIndex < mesh.blendShapeCount; shapeIndex++) {

                    for (var frameIndex = 0; frameIndex < mesh.GetBlendShapeFrameCount(shapeIndex); frameIndex++) {
                        Vector3[] deltaVertices = new Vector3[mesh.vertexCount];
                        Vector3[] deltaNormals = new Vector3[mesh.vertexCount];
                        Vector3[] deltaTangents = new Vector3[mesh.vertexCount];

                        mesh.GetBlendShapeFrameVertices(shapeIndex, frameIndex, deltaVertices, deltaNormals, deltaTangents);

                        float weight = mesh.GetBlendShapeFrameWeight(shapeIndex, frameIndex);
                        string blendShapeName = mesh.GetBlendShapeName(shapeIndex);

                        if (config.obfuscateBlendShapes && obfuscateBlendShapes)
                            blendShapeName = ObfuscateBlendShape(blendShapeName);

                        obfuscatedMesh.AddBlendShapeFrame(blendShapeName, weight, deltaVertices, deltaNormals, deltaTangents);
                    }
                }

                AssetDatabase.CreateAsset(obfuscatedMesh, GetObfuscatedPath<Mesh>());
                obfuscatedMeshes.Add(mesh, obfuscatedMesh);

                return obfuscatedMesh;
            }
        }

        string ObfuscateBlendShape(string blendShapeName) {

            if (string.IsNullOrEmpty(blendShapeName))
                return blendShapeName;

            if (obfuscatedBlendShapeNames.ContainsKey(blendShapeName)) {
                return obfuscatedBlendShapeNames[blendShapeName];
            }
            else {
                string obfuscatedBlendShapeName = GUID.Generate().ToString();

                obfuscatedBlendShapeNames.Add(blendShapeName, obfuscatedBlendShapeName);

                return obfuscatedBlendShapeName;
            }
        }

        void ObfuscateMaterials(VRCAvatarDescriptor descriptor) {
            SkinnedMeshRenderer[] skinnedMeshRenderers = descriptor.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            MeshRenderer[] meshRenderers = descriptor.GetComponentsInChildren<MeshRenderer>(true);
            ParticleSystemRenderer[] particleSystemRenderers = descriptor.GetComponentsInChildren<ParticleSystemRenderer>(true);

            foreach (var renderer in skinnedMeshRenderers) {
                List<Material> sharedMaterials = new List<Material>(renderer.sharedMaterials);

                for (int i = 0; i < sharedMaterials.Count; i++) {
                    sharedMaterials[i] = ObfuscateMaterial(sharedMaterials[i]);
                }

                renderer.sharedMaterials = sharedMaterials.ToArray();
            }

            foreach (var renderer in meshRenderers) {
                List<Material> sharedMaterials = new List<Material>(renderer.sharedMaterials);

                for (int i = 0; i < sharedMaterials.Count; i++) {
                    sharedMaterials[i] = ObfuscateMaterial(sharedMaterials[i]);
                }

                renderer.sharedMaterials = sharedMaterials.ToArray();
            }

            foreach (var renderer in particleSystemRenderers) {
                List<Material> sharedMaterials = new List<Material>(renderer.sharedMaterials);

                for (int i = 0; i < sharedMaterials.Count; i++) {
                    sharedMaterials[i] = ObfuscateMaterial(sharedMaterials[i]);
                }

                renderer.sharedMaterials = sharedMaterials.ToArray();
            }
        }

        Material ObfuscateMaterial(Material material) {

            if (material == null)
                return null;

            string path = AssetDatabase.GetAssetPath(material);

            if (string.IsNullOrEmpty(path)) {
                Debug.LogError($"Material '{material.name}' cannot be obfuscated. It will be ignored.");
                return material;
            }
            else if (path.Contains("unity_builtin")) {
                Debug.LogError($"Unity built-in Material '{material.name}' cannot be obfuscated. It will be ignored.");
                return material;
            }

            string newPath = GetObfuscatedPath<Material>();

            if (obfuscatedMaterials.ContainsKey(material)) {
                return obfuscatedMaterials[material];
            }
            else if (AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(material), newPath)) {
                Material obfuscatedMaterial = AssetDatabase.LoadAssetAtPath<Material>(newPath);

                obfuscatedMaterials.Add(material, obfuscatedMaterial);

                return obfuscatedMaterial;
            }

            throw new System.Exception($"Obfuscation of Material '{material.name}' failed");
        }

        void ObfuscateControllers(VRCAvatarDescriptor descriptor, Animator animator) {
            bool runtimeAnimatorValid = false;

            for (int i = 0; i < descriptor.baseAnimationLayers.Length; i++) {

                if (descriptor.baseAnimationLayers[i].animatorController != null) {
                    AnimatorController obfuscatedController = ObfuscateController((AnimatorController)descriptor.baseAnimationLayers[i].animatorController);

                    if (obfuscatedController != null) {

                        if (animator.runtimeAnimatorController == descriptor.baseAnimationLayers[i].animatorController) {
                            animator.runtimeAnimatorController = obfuscatedController;
                            runtimeAnimatorValid = true;
                        }

                        descriptor.baseAnimationLayers[i].animatorController = obfuscatedController;
                    }
                }
            }

            for (int i = 0; i < descriptor.specialAnimationLayers.Length; i++) {

                if (descriptor.specialAnimationLayers[i].animatorController != null) {
                    AnimatorController obfuscatedController = ObfuscateController((AnimatorController)descriptor.specialAnimationLayers[i].animatorController);

                    if (obfuscatedController != null)
                        descriptor.specialAnimationLayers[i].animatorController = obfuscatedController;
                }
            }

            if (animator.runtimeAnimatorController != null && !runtimeAnimatorValid)
                Debug.LogError("Controller in Animator component cannot be obfuscated. You should set an controller, which is part of your playable layers (e.g. FX controller).", animator);
        }

        AnimatorController ObfuscateController(AnimatorController controller) {
            string newPath = GetObfuscatedPath<AnimatorController>();

            if (obfuscatedControllers.ContainsKey(controller)) {
                return obfuscatedControllers[controller];
            }
            else if (AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(controller), newPath)) {
                AnimatorController obfuscatedController = AssetDatabase.LoadAssetAtPath<AnimatorController>(newPath);

                obfuscatedControllers.Add(controller, obfuscatedController);

                // Parameters
                List<AnimatorControllerParameter> parameters = new List<AnimatorControllerParameter>(obfuscatedController.parameters);

                foreach (var parameter in parameters) {

                    if (!allParameters.Contains(parameter.name))
                        allParameters.Add(parameter.name);

                    if (config.obfuscateExpressionParameters && config.obfuscateParameters) {

                        if (VRC_RESERVED_ANIMATOR_PARAMETERS.Contains(parameter.name))
                            continue;

                        if (!config.obfuscatedParameters.Contains(parameter.name))
                            continue;

                        if (obfuscatedParameters.ContainsKey(parameter.name)) {
                            parameter.name = obfuscatedParameters[parameter.name];
                        }
                        else {
                            string newName = GUID.Generate().ToString();

                            // Use same GUID for same PhysBones parameter and do not obfuscate suffix

                            // Parameter in PhysBones component
                            // Nose            ->  {GUID}

                            // Parameter in controller
                            // Nose_IsGrabbed  ->  {GUID}_IsGrabbed
                            // Nose_Angle      ->  {GUID}_Angle
                            // Nose_Stretch    ->  {GUID}_Stretch

                            string physBonesParameterSuffix = GetPhysBonesParameterSuffix(parameter.name);

                            if (!string.IsNullOrEmpty(physBonesParameterSuffix)) {
                                string physBonesParameter = GetPhysBonesParameter(parameter.name);
                                bool foundSamePhysBonesParameter = false;

                                foreach (var suffix in VRC_PHYS_BONES_SUFFIXES) {

                                    if (suffix == physBonesParameterSuffix)
                                        continue;

                                    string samePhysBonesParameterName = physBonesParameter + suffix;

                                    if (obfuscatedParameters.ContainsKey(samePhysBonesParameterName)) {
                                        newName = GetPhysBonesParameter(obfuscatedParameters[samePhysBonesParameterName]) + physBonesParameterSuffix;
                                        foundSamePhysBonesParameter = true;
                                        break;
                                    }
                                }

                                if (!foundSamePhysBonesParameter)
                                    newName += physBonesParameterSuffix;
                            }

                            obfuscatedParameters.Add(parameter.name, newName);
                            parameter.name = newName;
                        }
                    }
                }

                obfuscatedController.parameters = parameters.ToArray();

                // Layers, avatar masks, state machines, states, blend trees, animation clips
                List<AnimatorControllerLayer> layers = new List<AnimatorControllerLayer>(obfuscatedController.layers);

                foreach (var layer in layers) {

                    if (config.obfuscateLayers)
                        layer.name = GUID.Generate().ToString();

                    if (layer.avatarMask != null)
                        layer.avatarMask = ObfuscateAvatarMask(layer.avatarMask);

                    ObfuscateStateMachine(layer.stateMachine);
                }

                obfuscatedController.layers = layers.ToArray();

                return obfuscatedController;
            }

            throw new System.Exception($"Obfuscation of Controller '{controller.name}' failed");
        }

        AvatarMask ObfuscateAvatarMask(AvatarMask avatarMask) {
            string newPath = GetObfuscatedPath<AvatarMask>();

            if (obfuscatedAvatarMasks.ContainsKey(avatarMask)) {
                return obfuscatedAvatarMasks[avatarMask];
            }
            else if (AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(avatarMask), newPath)) {
                AvatarMask obfuscatedAvatarMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(newPath);

                obfuscatedAvatarMasks.Add(avatarMask, obfuscatedAvatarMask);

                for (int i = 0; i < obfuscatedAvatarMask.transformCount; i++) {
                    string transformPath = obfuscatedAvatarMask.GetTransformPath(i);

                    if (string.IsNullOrEmpty(transformPath))
                        continue;

                    if (config.preserveMMD && TRANSFORM_NAMES_MMD.Contains(transformPath))
                        continue;

                    obfuscatedAvatarMask.SetTransformPath(i, ObfuscateTransformPath(transformPath));
                }

                return obfuscatedAvatarMask;
            }

            throw new System.Exception($"Obfuscation of AvatarMask '{avatarMask.name}' failed");
        }

        void ObfuscateStateMachine(AnimatorStateMachine stateMachine) {

            if (config.obfuscateLayers)
                stateMachine.name = GUID.Generate().ToString();

            if (config.obfuscateExpressionParameters && config.obfuscateParameters) {

                foreach (var transition in stateMachine.entryTransitions) {
                    UpdateTransitionConditionParameters(transition);
                }

                foreach (var transition in stateMachine.anyStateTransitions) {
                    UpdateTransitionConditionParameters(transition);
                }

                foreach (var behaviour in stateMachine.behaviours) {
                    ObfuscateBehaviour(behaviour);
                }
            }

            foreach (var state in stateMachine.states) {
                ObfuscateState(state.state);
            }

            foreach (var subStateMachine in stateMachine.stateMachines) {
                ObfuscateStateMachine(subStateMachine.stateMachine);
            }
        }

        void ObfuscateState(AnimatorState state) {

            if (config.obfuscateLayers)
                state.name = GUID.Generate().ToString();

            if (config.obfuscateExpressionParameters && config.obfuscateParameters) {

                if (obfuscatedParameters.ContainsKey(state.cycleOffsetParameter))
                    state.cycleOffsetParameter = obfuscatedParameters[state.cycleOffsetParameter];

                if (obfuscatedParameters.ContainsKey(state.mirrorParameter))
                    state.mirrorParameter = obfuscatedParameters[state.mirrorParameter];

                if (obfuscatedParameters.ContainsKey(state.speedParameter))
                    state.speedParameter = obfuscatedParameters[state.speedParameter];

                if (obfuscatedParameters.ContainsKey(state.timeParameter))
                    state.timeParameter = obfuscatedParameters[state.timeParameter];

                foreach (var transition in state.transitions) {
                    UpdateTransitionConditionParameters(transition);
                }

                foreach (var behaviour in state.behaviours) {
                    ObfuscateBehaviour(behaviour);
                }
            }

            if (state.motion is AnimationClip)
                state.motion = ObfuscateAnimationClip((AnimationClip)state.motion);
            else if (state.motion is BlendTree)
                state.motion = ObfuscateBlendTree((BlendTree)state.motion);
        }

        void ObfuscateBehaviour(StateMachineBehaviour behaviour) {

            if (behaviour is VRCAvatarParameterDriver) {
                VRCAvatarParameterDriver parameterDriver = (VRCAvatarParameterDriver)behaviour;

                foreach (var parameter in parameterDriver.parameters) {

                    if (parameter.name != null && obfuscatedParameters.ContainsKey(parameter.name))
                        parameter.name = obfuscatedParameters[parameter.name];

                    if (parameter.source != null && obfuscatedParameters.ContainsKey(parameter.source))
                        parameter.source = obfuscatedParameters[parameter.source];
                }
            }
            else if (behaviour is VRCAnimatorPlayAudio) {
                VRCAnimatorPlayAudio animatorPlayAudio = (VRCAnimatorPlayAudio)behaviour;

                if (animatorPlayAudio.ParameterName != null && obfuscatedParameters.ContainsKey(animatorPlayAudio.ParameterName))
                    animatorPlayAudio.ParameterName = obfuscatedParameters[animatorPlayAudio.ParameterName];

                for (int i = 0; i < animatorPlayAudio.Clips.Length; i++) {
                    animatorPlayAudio.Clips[i] = ObfuscateAudioClip(animatorPlayAudio.Clips[i]);
                }
            }
        }

        AnimationClip ObfuscateAnimationClip(AnimationClip clip) {
            string path = AssetDatabase.GetAssetPath(clip);

            if (path.Contains("ProxyAnim/proxy_"))
                return clip;

            string newPath = GetObfuscatedPath<AnimationClip>(); ;

            if (obfuscatedAnimationClips.ContainsKey(clip)) {
                return obfuscatedAnimationClips[clip];
            }
            else if (AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(clip), newPath)) {
                AnimationClip obfuscatedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(newPath);

                obfuscatedAnimationClips.Add(clip, obfuscatedClip);

                EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(obfuscatedClip);

                for (int i = 0; i < bindings.Length; i++) {

                    if (!string.IsNullOrEmpty(bindings[i].path)) {

                        if (config.preserveMMD && TRANSFORM_NAMES_MMD.Contains(bindings[i].path))
                            continue;

                        AnimationCurve curve = AnimationUtility.GetEditorCurve(obfuscatedClip, bindings[i]);
                        AnimationUtility.SetEditorCurve(obfuscatedClip, bindings[i], null);
                        bindings[i].path = ObfuscateTransformPath(bindings[i].path);

                        if (config.obfuscateMeshes && config.obfuscateBlendShapes && bindings[i].propertyName.StartsWith("blendShape.")) {
                            string blendShapeName = bindings[i].propertyName.Replace("blendShape.", "");
                            bindings[i].propertyName = "blendShape." + ObfuscateBlendShape(blendShapeName);
                        }

                        AnimationUtility.SetEditorCurve(obfuscatedClip, bindings[i], curve);
                    }
                    else if (!string.IsNullOrEmpty(bindings[i].propertyName) && obfuscatedParameters.ContainsKey(bindings[i].propertyName)) {
                        AnimationCurve curve = AnimationUtility.GetEditorCurve(obfuscatedClip, bindings[i]);
                        AnimationUtility.SetEditorCurve(obfuscatedClip, bindings[i], null);
                        bindings[i].propertyName = obfuscatedParameters[bindings[i].propertyName];
                        AnimationUtility.SetEditorCurve(obfuscatedClip, bindings[i], curve);
                    }
                    else {
                        continue;
                    }
                }

                bindings = AnimationUtility.GetObjectReferenceCurveBindings(obfuscatedClip);

                for (int i = 0; i < bindings.Length; i++) {

                    if (!string.IsNullOrEmpty(bindings[i].path)) {

                        if (config.preserveMMD && TRANSFORM_NAMES_MMD.Contains(bindings[i].path))
                            continue;

                        ObjectReferenceKeyframe[] references = AnimationUtility.GetObjectReferenceCurve(obfuscatedClip, bindings[i]);
                        AnimationUtility.SetObjectReferenceCurve(obfuscatedClip, bindings[i], null);
                        bindings[i].path = ObfuscateTransformPath(bindings[i].path);

                        if (config.obfuscateMaterials && bindings[i].isPPtrCurve) {

                            for (int r = 0; r < references.Length; r++) {

                                if (references[r].value is Material)
                                    references[r].value = ObfuscateMaterial((Material)references[r].value);
                            }
                        }

                        AnimationUtility.SetObjectReferenceCurve(obfuscatedClip, bindings[i], references);
                    }
                    else {
                        continue;
                    }
                }

                return obfuscatedClip;
            }

            throw new System.Exception($"Obfuscation of AnimationClip '{clip.name}' failed");
        }

        BlendTree ObfuscateBlendTree(BlendTree blendTree) {

            if (obfuscatedBlendTrees.ContainsKey(blendTree)) {
                return obfuscatedBlendTrees[blendTree];
            }
            else {
                string newPath = GetObfuscatedPath<BlendTree>();
                BlendTree obfuscatedBlendTree = blendTree;

                if (AssetDatabase.IsMainAsset(blendTree)) {

                    if (AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(blendTree), newPath)) {
                        obfuscatedBlendTree = AssetDatabase.LoadAssetAtPath<BlendTree>(newPath);
                        obfuscatedBlendTrees.Add(blendTree, obfuscatedBlendTree);
                    }
                    else {
                        throw new System.Exception($"Obfuscation of BlendTree '{blendTree.name}' failed");
                    }
                }

                if (config.obfuscateLayers)
                    obfuscatedBlendTree.name = GUID.Generate().ToString();

                if (config.obfuscateExpressionParameters && config.obfuscateParameters) {

                    if (obfuscatedParameters.ContainsKey(obfuscatedBlendTree.blendParameter))
                        blendTree.blendParameter = obfuscatedParameters[blendTree.blendParameter];

                    if (obfuscatedParameters.ContainsKey(obfuscatedBlendTree.blendParameterY))
                        obfuscatedBlendTree.blendParameterY = obfuscatedParameters[blendTree.blendParameterY];
                }

                List<ChildMotion> childMotions = new List<ChildMotion>(obfuscatedBlendTree.children);

                for (int i = 0; i < childMotions.Count; i++) {

                    if (childMotions[i].motion is AnimationClip) {
                        ChildMotion childMotion = childMotions[i];
                        childMotion.motion = ObfuscateAnimationClip((AnimationClip)childMotion.motion);

                        if (obfuscatedParameters.ContainsKey(childMotion.directBlendParameter))
                            childMotion.directBlendParameter = obfuscatedParameters[childMotion.directBlendParameter];

                        childMotions[i] = childMotion;
                    }
                    else if (obfuscatedBlendTree.children[i].motion is BlendTree) {
                        ChildMotion childMotion = childMotions[i];
                        childMotion.motion = ObfuscateBlendTree((BlendTree)obfuscatedBlendTree.children[i].motion);

                        if (obfuscatedParameters.ContainsKey(childMotion.directBlendParameter))
                            childMotion.directBlendParameter = obfuscatedParameters[childMotion.directBlendParameter];

                        childMotions[i] = childMotion;
                    }
                }

                obfuscatedBlendTree.children = childMotions.ToArray();

                return obfuscatedBlendTree;
            }

            throw new System.Exception($"Obfuscation of BlendTree '{blendTree.name}' failed");
        }

        void UpdateTransitionConditionParameters(AnimatorTransitionBase transition) {
            List<AnimatorCondition> conditions = new List<AnimatorCondition>(transition.conditions);

            for (int i = 0; i < conditions.Count; i++) {

                if (obfuscatedParameters.ContainsKey(conditions[i].parameter)) {
                    AnimatorCondition condition = conditions[i];
                    condition.parameter = obfuscatedParameters[conditions[i].parameter];

                    conditions[i] = condition;
                }
            }

            transition.conditions = conditions.ToArray();
        }

        void ObfuscateExpressionsAndMenus(VRCAvatarDescriptor descriptor) {

            if (descriptor.expressionParameters != null) {
                string newPath = GetObfuscatedPath<VRCExpressionParameters>();

                if (AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(descriptor.expressionParameters), newPath)) {
                    descriptor.expressionParameters = AssetDatabase.LoadAssetAtPath<VRCExpressionParameters>(newPath);

                    if (descriptor.expressionParameters.parameters != null) {

                        foreach (var expressionParameter in descriptor.expressionParameters.parameters) {

                            if (string.IsNullOrEmpty(expressionParameter.name))
                                continue;

                            if (!allParameters.Contains(expressionParameter.name))
                                Debug.LogError($"VRC Expression Parameter '{expressionParameter.name}' is not used in any controller. It's recommended to remove it.", descriptor.expressionParameters);

                            if (config.obfuscateParameters) {

                                if (obfuscatedParameters.ContainsKey(expressionParameter.name))
                                    expressionParameter.name = obfuscatedParameters[expressionParameter.name];
                            }
                        }
                    }
                }
            }

            if (descriptor.expressionsMenu != null)
                descriptor.expressionsMenu = ObfuscateExpressionMenu(descriptor.expressionsMenu);
        }

        VRCExpressionsMenu ObfuscateExpressionMenu(VRCExpressionsMenu menu) {
            string newPath = GetObfuscatedPath<VRCExpressionsMenu>();

            if (obfuscatedExpressionMenus.ContainsKey(menu))
                return obfuscatedExpressionMenus[menu];
            else if (AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(menu), newPath)) {
                VRCExpressionsMenu obfuscatedMenu = AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(newPath);

                obfuscatedExpressionMenus.Add(menu, obfuscatedMenu);

                foreach (var control in obfuscatedMenu.controls) {

                    if (config.obfuscateParameters) {

                        if (control.parameter != null && obfuscatedParameters.ContainsKey(control.parameter.name))
                            control.parameter.name = obfuscatedParameters[control.parameter.name];

                        foreach (var subParameter in control.subParameters) {

                            if (subParameter != null && obfuscatedParameters.ContainsKey(subParameter.name))
                                subParameter.name = obfuscatedParameters[subParameter.name];
                        }
                    }

                    if (config.obfuscateMaterials && config.obfuscateTextures && control.icon != null)
                        control.icon = (Texture2D)ObfuscateTexture(control.icon);


                    if (control.subMenu != null) {

                        if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
                            control.subMenu = ObfuscateExpressionMenu(control.subMenu);
                        else
                            control.subMenu = null;
                    }
                }

                return obfuscatedMenu;
            }

            throw new System.Exception($"Obfuscation of VRC Expression Menu '{menu.name}' failed");
        }

        void ObfuscatePhysBonesAndContactReceivers(VRCAvatarDescriptor descriptor) {
            VRCPhysBone[] physBones = descriptor.GetComponentsInChildren<VRCPhysBone>(true);
            VRCContactReceiver[] contactReceivers = descriptor.GetComponentsInChildren<VRCContactReceiver>(true);

            foreach (var physBone in physBones) {

                if (!string.IsNullOrEmpty(physBone.parameter)) {

                    foreach (var suffix in VRC_PHYS_BONES_SUFFIXES) {
                        string physBonesParameterName = physBone.parameter + suffix;

                        if (obfuscatedParameters.ContainsKey(physBonesParameterName)) {
                            physBone.parameter = GetPhysBonesParameter(obfuscatedParameters[physBonesParameterName]);
                            break;
                        }
                    }
                }
            }

            foreach (var contactReceiver in contactReceivers) {

                if (!string.IsNullOrEmpty(contactReceiver.parameter)) {

                    if (obfuscatedParameters.ContainsKey(contactReceiver.parameter))
                        contactReceiver.parameter = obfuscatedParameters[contactReceiver.parameter];
                }
            }
        }

        void ObfuscateTextures() {

            foreach (var item in obfuscatedMaterials) {
                Material obfuscatedMaterial = item.Value;
                string[] texturePropertyNames = obfuscatedMaterial.GetTexturePropertyNames();

                foreach (var texturePropertyName in texturePropertyNames) {
                    Texture texture = obfuscatedMaterial.GetTexture(texturePropertyName);

                    if (texture != null)
                        obfuscatedMaterial.SetTexture(texturePropertyName, ObfuscateTexture(texture));
                }
            }
        }

        void ObfuscateCameras(VRCAvatarDescriptor descriptor) {
            Camera[] cameras = descriptor.GetComponentsInChildren<Camera>(true);

            foreach (var camera in cameras) {

                if (camera.targetTexture != null)
                    camera.targetTexture = (RenderTexture)ObfuscateTexture(camera.targetTexture);
            }
        }

        Texture ObfuscateTexture(Texture texture) {

            if (texture == null)
                return null;

            string path = AssetDatabase.GetAssetPath(texture);

            if (string.IsNullOrEmpty(path)) {
                Debug.LogError($"Texture '{texture.name}' cannot be obfuscated. It will be ignored.");
                return texture;
            }
            else if (path.Contains("unity_builtin")) {
                Debug.LogError($"Unity built-in Texture '{texture.name}' cannot be obfuscated. It will be ignored.");
                return texture;
            }

            string newPath = GetObfuscatedPath<Texture>() + System.IO.Path.GetExtension(path);

            if (obfuscatedTextures.ContainsKey(texture))
                return obfuscatedTextures[texture];
            else if (AssetDatabase.CopyAsset(path, newPath)) {
                Texture obfuscatedTexture = AssetDatabase.LoadAssetAtPath<Texture>(newPath);

                obfuscatedTextures.Add(texture, obfuscatedTexture);

                return obfuscatedTexture;
            }

            throw new System.Exception($"Obfuscation of Texture '{texture.name}' failed");
        }

        void ObfuscateAudioClips(VRCAvatarDescriptor descriptor) {
            AudioSource[] audioSources = descriptor.GetComponentsInChildren<AudioSource>(true);

            foreach (var audioSource in audioSources) {

                if (audioSource.clip != null)
                    audioSource.clip = ObfuscateAudioClip(audioSource.clip);
            }
        }

        AudioClip ObfuscateAudioClip(AudioClip ausdioClip) {

            if (ausdioClip == null)
                return null;

            string path = AssetDatabase.GetAssetPath(ausdioClip);

            if (string.IsNullOrEmpty(path)) {
                Debug.LogError($"AudioClip '{ausdioClip.name}' cannot be obfuscated. It will be ignored.");
                return ausdioClip;
            }

            string newPath = GetObfuscatedPath<AudioClip>() + System.IO.Path.GetExtension(path);

            if (obfuscatedAudioClips.ContainsKey(ausdioClip))
                return obfuscatedAudioClips[ausdioClip];
            else if (AssetDatabase.CopyAsset(path, newPath)) {
                AudioClip obfuscatedAudioCLip = AssetDatabase.LoadAssetAtPath<AudioClip>(newPath);

                obfuscatedAudioClips.Add(ausdioClip, obfuscatedAudioCLip);

                return obfuscatedAudioCLip;
            }

            throw new System.Exception($"Obfuscation of AudioClip '{ausdioClip.name}' failed");
        }

        bool IsObfuscatedGameObject(GameObject gameObject) {
            return (gameObject.name.Length == (32 + SUFFIX.Length) && gameObject.name.EndsWith(SUFFIX));
        }

        string GetObfuscatedPath<T>() {
            string path = folder + "/" + GUID.Generate();

            if (typeof(T) == typeof(AnimatorController))
                path += ".controller";
            else if (typeof(T) == typeof(AnimationClip))
                path += ".anim";
            else if (typeof(T) == typeof(AudioClip))
                path += "";
            else if (typeof(T) == typeof(AvatarMask))
                path += ".mask";
            else if (typeof(T) == typeof(Material))
                path += ".mat";
            else if (typeof(T) == typeof(Texture))
                path += "";
            else
                path += ".asset";

            return path;
        }

        string GetPhysBonesParameter(string parameter) {

            foreach (var suffix in VRC_PHYS_BONES_SUFFIXES) {

                if (parameter.EndsWith(suffix))
                    return parameter.Substring(0, parameter.Length - suffix.Length);
            }

            return "";
        }

        string GetPhysBonesParameterSuffix(string parameter) {

            foreach (var suffix in VRC_PHYS_BONES_SUFFIXES) {

                if (parameter.EndsWith(suffix))
                    return suffix;
            }

            return "";
        }

        Transform GetArmatureTransform(Animator animator) {
            Transform[] children = animator.transform.GetComponentsInChildren<Transform>(true);

            for (int i = 0; i < children.Length; i++) {

                if (children[i].parent != animator.transform)
                    continue;

                if (children[i].name.StartsWith("Armature") && children[i].childCount > 0 && children[i].GetChild(0).name == "Hips")
                    return children[i];
            }

            return null;
        }
    }
}