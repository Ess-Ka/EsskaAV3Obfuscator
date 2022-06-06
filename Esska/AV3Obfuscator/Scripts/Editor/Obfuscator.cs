using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Esska.AV3Obfuscator {

    public class Obfuscator : ScriptableObject {

        public static readonly List<string> VRC_RESERVED_ANIMATOR_PARAMETERS = new List<string>() {
            "AFK",
            "AngularY",
            "GestureLeft",
            "GestureLeftWeight",
            "GestureRight",
            "GestureRightWeight",
            "Grounded",
            "InStation",
            "IsLocal",
            "MuteSelf",
            "Seated",
            "TrackingType",
            "Upright",
            "VelocityX",
            "VelocityY",
            "VelocityZ",
            "Viseme",
            "Voice",
            "VRMode"
        };

        const string TITLE = "AV3Obfuscator";
        const string FOLDER = "Obfuscated";
        const string SUFFIX = "_Obfuscated";

        ObfuscationConfiguration config;
        string folder;
        List<string> allParameters;
        List<string> transformNames;
        List<string> transformPaths;
        List<string> obfuscatedTransformNames;
        List<string> obfuscatedTransformPaths;
        Dictionary<AnimationClip, AnimationClip> obfuscatedAnimationClips;
        Dictionary<AudioClip, AudioClip> obfuscatedAudioClips;
        Dictionary<AvatarMask, AvatarMask> obfuscatedAvatarMasks;
        Dictionary<string, string> obfuscatedBlendShapeNames;
        Dictionary<BlendTree, BlendTree> obfuscatedBlendTrees;
        Dictionary<Material, Material> obfuscatedMaterials;
        Dictionary<Mesh, Mesh> obfuscatedMeshes;
        Dictionary<string, string> obfuscatedParameters;
        Dictionary<Texture, Texture> obfuscatedTextures;

        public void ClearObfuscatedFolder() {
            string[] obfuscatedFolder = new string[] { "Assets/" + FOLDER };

            foreach (var asset in AssetDatabase.FindAssets("", obfuscatedFolder)) {
                string path = AssetDatabase.GUIDToAssetPath(asset);
                AssetDatabase.DeleteAsset(path);
            }
        }

        public void ClearObfuscateGameObjects() {
            Scene scene = SceneManager.GetActiveScene();
            GameObject[] rootGameObjects = scene.GetRootGameObjects();

            foreach (GameObject rootGameObject in rootGameObjects) {

                if (rootGameObject.name.Length == (32 + SUFFIX.Length) && rootGameObject.name.EndsWith(SUFFIX))
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

            Init();

            EditorUtility.DisplayProgressBar(TITLE, "Obfuscate Transforms", 0.1f);
            ObfuscateTransforms(obfuscatedGameObject.transform); // has to run first

            EditorUtility.DisplayProgressBar(TITLE, "Obfuscate Avatar", 0.2f);
            animator.avatar = ObfuscateAvatar(animator); // has to run after ObfuscateTransforms

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
            }

            if (config.obfuscateMaterials && config.obfuscateTextures) {
                EditorUtility.DisplayProgressBar(TITLE, "Obfuscate Textures", 0.8f);
                ObfuscateTextures(); // has to run after ObfuscateMaterials and ObfuscateControllers->ObfuscateClips has collected all materials
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
            transformNames = new List<string>();
            transformPaths = new List<string>();
            obfuscatedTransformNames = new List<string>();
            obfuscatedTransformPaths = new List<string>();
            obfuscatedAnimationClips = new Dictionary<AnimationClip, AnimationClip>();
            obfuscatedAudioClips = new Dictionary<AudioClip, AudioClip>();
            obfuscatedAvatarMasks = new Dictionary<AvatarMask, AvatarMask>();
            obfuscatedBlendShapeNames = new Dictionary<string, string>();
            obfuscatedBlendTrees = new Dictionary<BlendTree, BlendTree>();
            obfuscatedMaterials = new Dictionary<Material, Material>();
            obfuscatedMeshes = new Dictionary<Mesh, Mesh>();
            obfuscatedParameters = new Dictionary<string, string>();
            obfuscatedTextures = new Dictionary<Texture, Texture>();
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

        void ObfuscateTransforms(Transform rootTransform) {
            CollectTransforms(rootTransform, rootTransform);
            CollectObfuscatedTransforms(rootTransform, rootTransform);
        }

        void CollectTransforms(Transform rootTransform, Transform transform) {

            for (int i = 0; i < transform.childCount; i++) {
                Transform child = transform.GetChild(i);
                transformNames.Add(child.name);
                transformPaths.Add(AnimationUtility.CalculateTransformPath(child, rootTransform));
                CollectTransforms(rootTransform, child);
            }
        }

        void CollectObfuscatedTransforms(Transform rootTransform, Transform transform) {

            for (int i = 0; i < transform.childCount; i++) {
                Transform child = transform.GetChild(i);
                child.name = GUID.Generate().ToString();
                obfuscatedTransformNames.Add(child.name);
                obfuscatedTransformPaths.Add(AnimationUtility.CalculateTransformPath(child, rootTransform));
                CollectObfuscatedTransforms(rootTransform, child);
            }
        }

        Avatar ObfuscateAvatar(Animator animator) {

            if (animator.avatar.isValid) {
                Avatar obfuscatedAvatar = null;

                if (animator.avatar.isHuman) {
                    List<SkeletonBone> skeletonBones = new List<SkeletonBone>();
                    List<HumanBone> humanBones = new List<HumanBone>();

                    for (int i = 0; i < animator.avatar.humanDescription.skeleton.Length; i++) {
                        int index = transformNames.IndexOf(animator.avatar.humanDescription.skeleton[i].name);

                        if (index >= 0) {

                            skeletonBones.Add(new SkeletonBone() {
                                name = obfuscatedTransformNames[index],
                                position = animator.avatar.humanDescription.skeleton[i].position,
                                rotation = animator.avatar.humanDescription.skeleton[i].rotation,
                                scale = animator.avatar.humanDescription.skeleton[i].scale
                            });

                        }
                        else {

                            // Root transform
                            skeletonBones.Add(new SkeletonBone() {
                                name = animator.name,
                                position = animator.avatar.humanDescription.skeleton[i].position,
                                rotation = animator.avatar.humanDescription.skeleton[i].rotation,
                                scale = animator.avatar.humanDescription.skeleton[i].scale
                            });
                        }
                    }

                    for (int i = 0; i < animator.avatar.humanDescription.human.Length; i++) {
                        int index = transformNames.IndexOf(animator.avatar.humanDescription.human[i].boneName);

                        if (index >= 0) {

                            humanBones.Add(new HumanBone() {
                                boneName = obfuscatedTransformNames[index],
                                humanName = animator.avatar.humanDescription.human[i].humanName,
                                limit = animator.avatar.humanDescription.human[i].limit
                            });
                        }
                        else
                            throw new System.Exception(string.Format("BoneName {0} not found", animator.avatar.humanDescription.human[i].boneName));
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

                    obfuscatedAvatar = AvatarBuilder.BuildHumanAvatar(animator.gameObject, description);
                }
                else {
                    obfuscatedAvatar = AvatarBuilder.BuildGenericAvatar(animator.gameObject, "");
                }

                if (obfuscatedAvatar != null && obfuscatedAvatar.isValid) {
                    AssetDatabase.CreateAsset(obfuscatedAvatar, GetObfuscatedPath<Avatar>());

                    return obfuscatedAvatar;
                }
            }

            throw new System.Exception(string.Format("Obfuscation of Avatar '{0}' failed", animator.avatar.name));
        }

        void ObfuscateMeshesAndBlendShapes(VRCAvatarDescriptor descriptor) {
            SkinnedMeshRenderer[] skinnedMeshRenderers = descriptor.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            MeshFilter[] meshFilters = descriptor.GetComponentsInChildren<MeshFilter>(true);
            ParticleSystem[] particleSystems = descriptor.GetComponentsInChildren<ParticleSystem>(true);
            ParticleSystemRenderer[] particleSystemRenderers = descriptor.GetComponentsInChildren<ParticleSystemRenderer>(true);

            foreach (var renderer in skinnedMeshRenderers) {
                renderer.sharedMesh = ObfuscateMeshAndBlendShapes(renderer.sharedMesh);
            }

            foreach (var filter in meshFilters) {
                filter.sharedMesh = ObfuscateMeshAndBlendShapes(filter.sharedMesh);
            }

            foreach (var particleSystem in particleSystems) {
                ParticleSystem.ShapeModule shapeModule = particleSystem.shape;
                shapeModule.mesh = ObfuscateMeshAndBlendShapes(shapeModule.mesh);
            }

            foreach (var renderer in particleSystemRenderers) {
                renderer.mesh = ObfuscateMeshAndBlendShapes(renderer.mesh);
            }

            if (config.obfuscateBlendShapes) {

                for (int i = 0; i < descriptor.VisemeBlendShapes.Length; i++) {

                    if (obfuscatedBlendShapeNames.ContainsKey(descriptor.VisemeBlendShapes[i]))
                        descriptor.VisemeBlendShapes[i] = obfuscatedBlendShapeNames[descriptor.VisemeBlendShapes[i]];
                }
            }
        }

        Mesh ObfuscateMeshAndBlendShapes(Mesh mesh) {

            if (mesh == null)
                return null;

            string meshPath = AssetDatabase.GetAssetPath(mesh);

            if (string.IsNullOrEmpty(meshPath) || !mesh.isReadable) {
                Debug.LogError(string.Format("Mesh '{0}' cannot be obfuscated. It will be ignored.", mesh.name));
                return mesh;
            }
            else if (meshPath.Contains("unity default resources")) {
                Debug.LogError(string.Format("Unity built-in Mesh '{0}' cannot be obfuscated. It will be ignored.", mesh.name));
                return mesh;
            }

            if (obfuscatedMeshes.ContainsKey(mesh)) {
                return obfuscatedMeshes[mesh];
            }
            else {
                Mesh obfuscatedMesh = new Mesh {
                    subMeshCount = mesh.subMeshCount,
                    vertices = mesh.vertices,
                    colors = mesh.colors,
                    normals = mesh.normals,
                    tangents = mesh.tangents,
                    bindposes = mesh.bindposes,
                    boneWeights = mesh.boneWeights,
                    uv = mesh.uv,
                    uv2 = mesh.uv2,
                    uv3 = mesh.uv3,
                    uv4 = mesh.uv4,
                    uv5 = mesh.uv5,
                    uv6 = mesh.uv6,
                    uv7 = mesh.uv7,
                    uv8 = mesh.uv8
                };

                // Transfer sub meshes
                for (var meshIndex = 0; meshIndex < mesh.subMeshCount; meshIndex++) {
                    int[] triangles = mesh.GetTriangles(meshIndex);

                    obfuscatedMesh.SetTriangles(triangles, meshIndex);
                }

                // Transfer blend shapes
                for (int shapeIndex = 0; shapeIndex < mesh.blendShapeCount; shapeIndex++) {

                    for (var frameIndex = 0; frameIndex < mesh.GetBlendShapeFrameCount(shapeIndex); frameIndex++) {
                        Vector3[] deltaVertices = new Vector3[mesh.vertexCount];
                        Vector3[] deltaNormals = new Vector3[mesh.vertexCount];
                        Vector3[] deltaTangents = new Vector3[mesh.vertexCount];

                        mesh.GetBlendShapeFrameVertices(shapeIndex, frameIndex, deltaVertices, deltaNormals, deltaTangents);

                        float weight = mesh.GetBlendShapeFrameWeight(shapeIndex, frameIndex);
                        string blendShapeName = mesh.GetBlendShapeName(shapeIndex);

                        if (config.obfuscateBlendShapes)
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
                Debug.LogError(string.Format("Material '{0}' cannot be obfuscated. It will be ignored.", material.name));
                return material;
            }
            else if (path.Contains("unity_builtin")) {
                Debug.LogError(string.Format("Unity built-in Material '{0}' cannot be obfuscated. It will be ignored.", material.name));
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

            throw new System.Exception(string.Format("Obfuscation of Material '{0}' failed", material.name));
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

            if (AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(controller), newPath)) {
                AnimatorController obfuscatedController = AssetDatabase.LoadAssetAtPath<AnimatorController>(newPath);

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

            throw new System.Exception(string.Format("Obfuscation of Controller '{0}' failed", controller.name));
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

                    if (string.IsNullOrEmpty(obfuscatedAvatarMask.GetTransformPath(i)))
                        continue;

                    int index = transformPaths.IndexOf(obfuscatedAvatarMask.GetTransformPath(i));

                    if (index >= 0) {
                        obfuscatedAvatarMask.SetTransformPath(i, obfuscatedTransformPaths[index]);
                    }
                    else {
                        Debug.LogError(string.Format("Path '{0}' in AvatarMask '{1}' doesn't exist in the Transform hierarchy. You should update the Transforms in the AvatarMask. The path will be obfuscated independently of this.", obfuscatedAvatarMask.GetTransformPath(i), avatarMask.name), avatarMask);
                        obfuscatedAvatarMask.SetTransformPath(i, GUID.Generate().ToString());
                    }
                }

                return obfuscatedAvatarMask;
            }

            throw new System.Exception(string.Format("Obfuscation of AvatarMask '{0}' failed", avatarMask.name));
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

                    if (obfuscatedParameters.ContainsKey(parameter.name))
                        parameter.name = obfuscatedParameters[parameter.name];
                }
            }
        }

        AnimationClip ObfuscateAnimationClip(AnimationClip clip) {
            string newPath = GetObfuscatedPath<AnimationClip>(); ;

            if (obfuscatedAnimationClips.ContainsKey(clip)) {
                return obfuscatedAnimationClips[clip];
            }
            else if (AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(clip), newPath)) {
                AnimationClip obfuscatedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(newPath);

                obfuscatedAnimationClips.Add(clip, obfuscatedClip);

                EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(obfuscatedClip);

                for (int i = 0; i < bindings.Length; i++) {

                    if (string.IsNullOrEmpty(bindings[i].path))
                        continue;

                    int index = transformPaths.IndexOf(bindings[i].path);

                    if (index >= 0) {
                        AnimationCurve curve = AnimationUtility.GetEditorCurve(obfuscatedClip, bindings[i]);
                        AnimationUtility.SetEditorCurve(obfuscatedClip, bindings[i], null);
                        bindings[i].path = obfuscatedTransformPaths[index];

                        if (config.obfuscateBlendShapes && bindings[i].propertyName.StartsWith("blendShape.")) {
                            string blendShapeName = bindings[i].propertyName.Replace("blendShape.", "");
                            bindings[i].propertyName = "blendShape." + ObfuscateBlendShape(blendShapeName);
                        }

                        AnimationUtility.SetEditorCurve(obfuscatedClip, bindings[i], curve);
                    }
                    else {
                        AnimationUtility.SetEditorCurve(obfuscatedClip, bindings[i], null);
                        Debug.LogError(string.Format("Path '{0}' in AnimationClip '{1}' cannot be obfuscated. Path was removed.", bindings[i].path, clip.name), clip);
                    }
                }

                bindings = AnimationUtility.GetObjectReferenceCurveBindings(obfuscatedClip);

                for (int i = 0; i < bindings.Length; i++) {

                    if (string.IsNullOrEmpty(bindings[i].path))
                        continue;

                    int index = transformPaths.IndexOf(bindings[i].path);

                    if (index >= 0) {
                        ObjectReferenceKeyframe[] references = AnimationUtility.GetObjectReferenceCurve(obfuscatedClip, bindings[i]);
                        AnimationUtility.SetObjectReferenceCurve(obfuscatedClip, bindings[i], null);
                        bindings[i].path = obfuscatedTransformPaths[index];

                        if (config.obfuscateMaterials && bindings[i].isPPtrCurve) {

                            for (int r = 0; r < references.Length; r++) {

                                if (references[r].value is Material)
                                    references[r].value = ObfuscateMaterial((Material)references[r].value);
                            }
                        }

                        AnimationUtility.SetObjectReferenceCurve(obfuscatedClip, bindings[i], references);
                    }
                    else {
                        AnimationUtility.SetObjectReferenceCurve(obfuscatedClip, bindings[i], null);
                        Debug.LogError(string.Format("Path '{0}' in AnimationClip '{1}' cannot be obfuscated. Path was removed.", bindings[i].path, clip.name), clip);
                    }
                }

                return obfuscatedClip;
            }

            throw new System.Exception(string.Format("Obfuscation of AnimationClip '{0}' failed", clip.name));
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
                        throw new System.Exception(string.Format("Obfuscation of BlendTree '{0}' failed", blendTree.name));
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
                        childMotions[i] = childMotion;
                    }
                    else if (obfuscatedBlendTree.children[i].motion is BlendTree) {
                        ChildMotion childMotion = childMotions[i];
                        childMotion.motion = ObfuscateBlendTree((BlendTree)obfuscatedBlendTree.children[i].motion);
                        childMotions[i] = childMotion;
                    }
                }

                obfuscatedBlendTree.children = childMotions.ToArray();

                return obfuscatedBlendTree;
            }

            throw new System.Exception(string.Format("Obfuscation of BlendTree '{0}' failed", blendTree.name));
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

                            if (!allParameters.Contains(expressionParameter.name))
                                Debug.LogError(string.Format("VRC Expression Parameter '{0}' is not used in any controller. It's recommended to remove it.", expressionParameter.name), descriptor.expressionParameters);

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

            if (AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(menu), newPath)) {
                VRCExpressionsMenu obfuscatedMenu = AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(newPath);

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

                    if (control.subMenu != null)
                        control.subMenu = ObfuscateExpressionMenu(control.subMenu);
                }

                return obfuscatedMenu;
            }

            throw new System.Exception(string.Format("Obfuscation of VRC Expression Menu '{0}' failed", menu.name));
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

        Texture ObfuscateTexture(Texture texture) {

            if (texture == null)
                return null;

            string path = AssetDatabase.GetAssetPath(texture);

            if (string.IsNullOrEmpty(path)) {
                Debug.LogError(string.Format("Texture '{0}' cannot be obfuscated. It will be ignored.", texture.name));
                return texture;
            }
            else if (path.Contains("unity_builtin")) {
                Debug.LogError(string.Format("Unity built-in Texture '{0}' cannot be obfuscated. It will be ignored.", texture.name));
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

            throw new System.Exception(string.Format("Obfuscation of Texture '{0}' failed", texture.name));
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
                Debug.LogError(string.Format("AudioClip '{0}' cannot be obfuscated. It will be ignored.", ausdioClip.name));
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

            throw new System.Exception(string.Format("Obfuscation of AudioClip '{0}' failed", ausdioClip.name));
        }

    }
}