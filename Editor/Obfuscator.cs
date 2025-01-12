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

        #region Constants

        /// <summary>
        /// List of reserved parameters. The will not be obfuscated.
        /// https://creators.vrchat.com/avatars/animator-parameters/
        /// </summary>
        public static readonly List<string> VRC_RESERVED_ANIMATOR_PARAMETERS = new List<string>() {
            "AFK",
            "AngularY",
            "AvatarVersion",
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

        /// <summary>
        /// List of allowed phys bone suffixes. The suffixes will not be obfuscated.
        /// https://creators.vrchat.com/avatars/avatar-dynamics/physbones
        /// </summary>
        public static readonly List<string> VRC_PHYS_BONE_SUFFIXES = new List<string>() {
            "_IsGrabbed",
            "_IsPosed",
            "_Angle",
            "_Stretch",
            "_Squish"
        };

        /// <summary>
        /// List of transform names referenced in MMD animation clips. 
        /// Skips obfuscation of transform name and blend shapes on it, depending on 
        /// the "Preserve MMD" setting. 
        /// </summary>
        public static readonly List<string> TRANSFORM_NAMES_MMD = new List<string>() {
            "Body"
        };

        private const string TITLE = "AV3Obfuscator";
        private const string FOLDER = "Obfuscated";
        private const string SUFFIX = "_Obfuscated";

        #endregion

        #region Runtime Variables

        private ObfuscationConfiguration config;
        private string folder;
        private List<string> allParameters;
        private Dictionary<Animator, Transform> armatureTransforms;
        private Dictionary<AnimationClip, AnimationClip> obfuscatedAnimationClips;
        private Dictionary<AudioClip, AudioClip> obfuscatedAudioClips;
        private Dictionary<Avatar, Avatar> obfuscatedAvatars;
        private Dictionary<AvatarMask, AvatarMask> obfuscatedAvatarMasks;
        private Dictionary<string, string> obfuscatedBlendShapeNames;
        private Dictionary<BlendTree, BlendTree> obfuscatedBlendTrees;
        private Dictionary<AnimatorController, AnimatorController> obfuscatedControllers;
        private Dictionary<VRCExpressionsMenu, VRCExpressionsMenu> obfuscatedExpressionMenus;
        private Dictionary<Material, Material> obfuscatedMaterials;
        private Dictionary<Mesh, Mesh> obfuscatedMeshes;
        private Dictionary<string, string> obfuscatedParameters;
        private Dictionary<Texture, Texture> obfuscatedTextures;
        private Dictionary<string, string> obfuscatedTransformNames;

        #endregion

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

            Init();

            CollectAnimatorsAndArmatureTransforms(descriptor);

            if (armatureTransforms[animator] == null)
                throw new System.Exception("Armature not found.");

            EditorUtility.DisplayProgressBar(TITLE, "Obfuscate Transforms", 0.1f);
            ObfuscateTransforms(obfuscatedGameObject.transform); // has to run first

            EditorUtility.DisplayProgressBar(TITLE, "Obfuscate Avatars", 0.2f);
            ObfuscateAvatars(); // has to run after ObfuscateTransforms

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
                ObfuscateTextures(); // has to run after ObfuscateMaterials/ObfuscateControllers/ObfuscateClips has collected all materials

                if (config.obfuscateTextures)
                    ObfuscateRenderTextures(descriptor);
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

            Selection.activeGameObject = obfuscatedGameObject;
        }

        private void Init() {
            allParameters = new List<string>();
            armatureTransforms = new Dictionary<Animator, Transform>();
            obfuscatedAnimationClips = new Dictionary<AnimationClip, AnimationClip>();
            obfuscatedAudioClips = new Dictionary<AudioClip, AudioClip>();
            obfuscatedAvatars = new Dictionary<Avatar, Avatar>();
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

        /// <summary>
        /// Collects all animators/armature transforms found in the hierarchy of the descriptor. 
        /// This has to run before the transforms has been obfuscated.
        /// Retains the armature transform for every animator.
        /// </summary>
        /// <param name="descriptor"></param>
        private void CollectAnimatorsAndArmatureTransforms(VRCAvatarDescriptor descriptor) {
            Animator[] animators = descriptor.transform.GetComponentsInChildren<Animator>(true);

            foreach (var item in animators) {
                armatureTransforms.Add(item, GetArmatureTransform(item));
            }
        }

        /// <summary>
        /// Obfuscates all transforms recursively beginning from the root transform.
        /// </summary>
        /// <param name="rootTransform"></param>
        /// <param name="transform"></param>
        private void ObfuscateTransforms(Transform rootTransform, Transform transform = null) {

            if (transform == null)
                transform = rootTransform;

            for (int i = 0; i < transform.childCount; i++) {
                Transform child = transform.GetChild(i);

                if (config.preserveMMD && child.parent == rootTransform && TRANSFORM_NAMES_MMD.Contains(child.name))
                    continue;

                child.name = ObfuscateTransformName(child.name);
                ObfuscateTransforms(rootTransform, child);
            }
        }

        /// <summary>
        /// Obfuscates the name of a transform.
        /// Transforms with the same name gets the same obfuscated name.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private string ObfuscateTransformName(string name) {

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

        /// <summary>
        /// Obfuscates an entrire path (e.g. Armature/Hips/Spine/Chest/Neck/Head).
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private string ObfuscateTransformPath(string path) {
            string[] names = path.Split(new string[] { "/" }, System.StringSplitOptions.None);

            for (int i = 0; i < names.Length; i++) {
                names[i] = ObfuscateTransformName(names[i]);
            }

            return string.Join("/", names);
        }

        /// <summary>
        /// Obfuscates all avatars on the collected animators.
        /// </summary>
        /// <param name="descriptor"></param>
        private void ObfuscateAvatars() {

            foreach (var item in armatureTransforms) {
                item.Key.avatar = ObfuscateAvatar(item.Key, item.Value);
            }
        }

        /// <summary>
        /// Obfuscates the avatar of an animator.
        /// To avoid conflics with <see cref="AvatarBuilder.BuildHumanAvatar"/>, 
        /// all children will temporary be unparented, except the armature itself.
        /// </summary>
        /// <param name="animator"></param>
        /// <param name="armatureTransform"></param>
        /// <returns></returns>
        /// <exception cref="System.Exception"></exception>
        private Avatar ObfuscateAvatar(Animator animator, Transform armatureTransform) {

            if (animator.avatar == null)
                return null;

            if (obfuscatedAvatars.ContainsKey(animator.avatar)) {
                return obfuscatedAvatars[animator.avatar];
            }
            else if (animator.avatar.isValid) {
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

                    obfuscatedAvatars.Add(animator.avatar, obfuscatedAvatar);

                    return obfuscatedAvatar;
                }
            }

            throw new System.Exception($"Obfuscation of avatar '{animator.avatar.name}' failed");
        }

        /// <summary>
        /// Obfuscates all meshes and blend shapes found in the hierarchy of the descriptor.
        /// Ignores blend shapes on the "Body" transform, depending on the "Preserve MMD" setting. 
        /// </summary>
        /// <param name="descriptor"></param>
        private void ObfuscateMeshesAndBlendShapes(VRCAvatarDescriptor descriptor) {
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

        /// <summary>
        /// Obfuscates a single mesh and it's blend shapes.
        /// Built-in meshes will be ignored.
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="obfuscateBlendShapes">True, if blend shapes should be obfuscated</param>
        /// <returns></returns>
        private Mesh ObfuscateMeshAndBlendShapes(Mesh mesh, bool obfuscateBlendShapes) {

            if (mesh == null)
                return null;

            string meshPath = AssetDatabase.GetAssetPath(mesh);

            if (string.IsNullOrEmpty(meshPath) || !mesh.isReadable) {
                Debug.LogError($"Mesh '{mesh.name}' cannot be obfuscated. It will be ignored.");
                return mesh;
            }
            else if (meshPath.Contains("unity default resources")) {
                Debug.LogError($"Unity built-in mesh '{mesh.name}' cannot be obfuscated. It will be ignored.");
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
                            blendShapeName = ObfuscateBlendShapeName(blendShapeName);

                        obfuscatedMesh.AddBlendShapeFrame(blendShapeName, weight, deltaVertices, deltaNormals, deltaTangents);
                    }
                }

                AssetDatabase.CreateAsset(obfuscatedMesh, GetObfuscatedPath<Mesh>());
                obfuscatedMeshes.Add(mesh, obfuscatedMesh);

                return obfuscatedMesh;
            }
        }

        /// <summary>
        /// Obfuscates the name of a blend shape.
        /// </summary>
        /// <param name="blendShapeName"></param>
        /// <returns></returns>
        private string ObfuscateBlendShapeName(string blendShapeName) {

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

        /// <summary>
        /// Obfuscates materials of all renderers found in the hierarchy of the descriptor.
        /// </summary>
        /// <param name="descriptor"></param>
        private void ObfuscateMaterials(VRCAvatarDescriptor descriptor) {
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

        /// <summary>
        /// Obfuscates a single material.
        /// Built-in material will be ignored.
        /// </summary>
        /// <param name="material"></param>
        /// <returns></returns>
        /// <exception cref="System.Exception"></exception>
        private Material ObfuscateMaterial(Material material) {

            if (material == null)
                return null;

            string path = AssetDatabase.GetAssetPath(material);

            if (string.IsNullOrEmpty(path)) {
                Debug.LogError($"Material '{material.name}' cannot be obfuscated. It will be ignored.");
                return material;
            }
            else if (path.Contains("unity_builtin")) {
                Debug.LogError($"Unity built-in material '{material.name}' cannot be obfuscated. It will be ignored.");
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

            throw new System.Exception($"Obfuscation of material '{material.name}' failed");
        }

        /// <summary>
        /// Obfuscates all animator controllers referenced in the descriptor or additional 
        /// ones found in the hierarchy.
        /// </summary>
        /// <param name="descriptor"></param>
        /// <param name="animator"></param>
        private void ObfuscateControllers(VRCAvatarDescriptor descriptor, Animator animator) {
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
                Debug.LogError("Controller in animator component cannot be obfuscated. You should set an controller, which is part of your playable layers (e.g. FX controller).", animator);

            // Obfuscate additional animators
            Animator[] animators = descriptor.GetComponentsInChildren<Animator>(true);

            foreach (var item in animators) {

                if (item == animator)
                    continue;

                if (item.runtimeAnimatorController != null) {
                    AnimatorController obfuscatedController = ObfuscateController((AnimatorController)item.runtimeAnimatorController);

                    if (obfuscatedController != null)
                        item.runtimeAnimatorController = obfuscatedController;
                }
            }
        }

        /// <summary>
        /// Obfuscates a single animator controller.
        /// This includes layers, parameters, avatar masks, state machines, states, 
        /// transitions, state behaviours, blend trees and animation clips.
        /// </summary>
        /// <param name="controller"></param>
        /// <returns></returns>
        /// <exception cref="System.Exception"></exception>
        private AnimatorController ObfuscateController(AnimatorController controller) {
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
                    parameter.name = ObfuscateParameterName(parameter.name);
                }

                obfuscatedController.parameters = parameters.ToArray();

                // Layers, avatar masks, state machines
                List<AnimatorControllerLayer> layers = new List<AnimatorControllerLayer>(obfuscatedController.layers);

                foreach (var layer in layers) {
                    layer.name = GUID.Generate().ToString();

                    if (layer.avatarMask != null)
                        layer.avatarMask = ObfuscateAvatarMask(layer.avatarMask);

                    ObfuscateStateMachine(layer.stateMachine);
                }

                obfuscatedController.layers = layers.ToArray();

                return obfuscatedController;
            }

            throw new System.Exception($"Obfuscation of controller '{controller.name}' failed");
        }

        /// <summary>
        /// Obfuscates the name of a parameter.
        /// Ignores reserved or excluded parameters.
        /// Preserves the suffix for phys bone parameters.
        /// </summary>
        /// <param name="parameterName"></param>
        /// <returns></returns>
        private string ObfuscateParameterName(string parameterName) {

            if (string.IsNullOrEmpty(parameterName))
                return parameterName;

            if (!allParameters.Contains(parameterName))
                allParameters.Add(parameterName);

            if (!config.obfuscateExpressionParameters || !config.obfuscateParameters)
                return parameterName;

            if (VRC_RESERVED_ANIMATOR_PARAMETERS.Contains(parameterName))
                return parameterName;

            if (!config.obfuscatedParameters.Contains(parameterName))
                return parameterName;

            if (obfuscatedParameters.ContainsKey(parameterName))
                return obfuscatedParameters[parameterName];

            string obfuscatedParameter = GUID.Generate().ToString();

            // Use same GUID for same phys bone parameter and do not obfuscate suffix

            // Parameter in phys bone component
            // Nose            ->  {GUID}

            // Parameter in controller
            // Nose_IsGrabbed  ->  {GUID}_IsGrabbed
            // Nose_Angle      ->  {GUID}_Angle
            // Nose_Stretch    ->  {GUID}_Stretch

            string physBoneParameterSuffix = GetPhysBoneParameterSuffix(parameterName);

            if (!string.IsNullOrEmpty(physBoneParameterSuffix)) {
                string physBoneParameter = GetPhysBoneParameter(parameterName);
                bool foundSamePhysBoneParameter = false;

                foreach (var suffix in VRC_PHYS_BONE_SUFFIXES) {

                    if (suffix == physBoneParameterSuffix)
                        continue;

                    string samePhysBoneParameterName = physBoneParameter + suffix;

                    if (obfuscatedParameters.ContainsKey(samePhysBoneParameterName)) {
                        obfuscatedParameter = GetPhysBoneParameter(obfuscatedParameters[samePhysBoneParameterName]) + physBoneParameterSuffix;
                        foundSamePhysBoneParameter = true;
                        break;
                    }
                }

                if (!foundSamePhysBoneParameter)
                    obfuscatedParameter += physBoneParameterSuffix;
            }

            obfuscatedParameters.Add(parameterName, obfuscatedParameter);

            return obfuscatedParameter;
        }

        /// <summary>
        /// Obfuscates a single avatar mask.
        /// </summary>
        /// <param name="avatarMask"></param>
        /// <returns></returns>
        /// <exception cref="System.Exception"></exception>
        private AvatarMask ObfuscateAvatarMask(AvatarMask avatarMask) {
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

            throw new System.Exception($"Obfuscation of avatar mask '{avatarMask.name}' failed");
        }

        /// <summary>
        /// Obfuscates a single state machine.
        /// </summary>
        /// <param name="stateMachine"></param>
        private void ObfuscateStateMachine(AnimatorStateMachine stateMachine) {
            stateMachine.name = GUID.Generate().ToString();

            foreach (var transition in stateMachine.entryTransitions) {
                ObfuscateTransition(transition);
            }

            foreach (var transition in stateMachine.anyStateTransitions) {
                ObfuscateTransition(transition);
            }

            foreach (var behaviour in stateMachine.behaviours) {
                ObfuscateBehaviour(behaviour);
            }

            foreach (var state in stateMachine.states) {
                ObfuscateState(state.state);
            }

            foreach (var subStateMachine in stateMachine.stateMachines) {
                ObfuscateStateMachine(subStateMachine.stateMachine);
            }
        }

        /// <summary>
        /// Obfuscates a single state.
        /// </summary>
        /// <param name="state"></param>
        private void ObfuscateState(AnimatorState state) {
            state.name = GUID.Generate().ToString();

            state.cycleOffsetParameter = ObfuscateParameterName(state.cycleOffsetParameter);
            state.mirrorParameter = ObfuscateParameterName(state.mirrorParameter);
            state.speedParameter = ObfuscateParameterName(state.speedParameter);
            state.timeParameter = ObfuscateParameterName(state.timeParameter);

            foreach (var transition in state.transitions) {
                ObfuscateTransition(transition);
            }

            foreach (var behaviour in state.behaviours) {
                ObfuscateBehaviour(behaviour);
            }

            if (state.motion is AnimationClip)
                state.motion = ObfuscateAnimationClip((AnimationClip)state.motion);
            else if (state.motion is BlendTree)
                state.motion = ObfuscateBlendTree((BlendTree)state.motion);
        }

        /// <summary>
        /// Obfuscates a single state machine behaviour.
        /// </summary>
        /// <param name="behaviour"></param>
        private void ObfuscateBehaviour(StateMachineBehaviour behaviour) {

            if (behaviour is VRCAvatarParameterDriver) {
                VRCAvatarParameterDriver parameterDriver = (VRCAvatarParameterDriver)behaviour;

                foreach (var parameter in parameterDriver.parameters) {
                    parameter.name = ObfuscateParameterName(parameter.name);
                    parameter.source = ObfuscateParameterName(parameter.source);
                }
            }
            else if (behaviour is VRCAnimatorPlayAudio) {
                VRCAnimatorPlayAudio animatorPlayAudio = (VRCAnimatorPlayAudio)behaviour;

                if (!string.IsNullOrEmpty(animatorPlayAudio.SourcePath))
                    animatorPlayAudio.SourcePath = ObfuscateTransformPath(animatorPlayAudio.SourcePath);

                animatorPlayAudio.ParameterName = ObfuscateParameterName(animatorPlayAudio.ParameterName);

                if (config.obfuscateAudioClips) {

                    for (int i = 0; i < animatorPlayAudio.Clips.Length; i++) {
                        animatorPlayAudio.Clips[i] = ObfuscateAudioClip(animatorPlayAudio.Clips[i]);
                    }
                }
            }
        }

        /// <summary>
        /// Obfuscates a single animation clip.
        /// VRC proxy animations will be ignored.
        /// </summary>
        /// <param name="clip"></param>
        /// <returns></returns>
        /// <exception cref="System.Exception"></exception>
        private AnimationClip ObfuscateAnimationClip(AnimationClip clip) {
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
                            bindings[i].propertyName = "blendShape." + ObfuscateBlendShapeName(blendShapeName);
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

            throw new System.Exception($"Obfuscation of animation clip '{clip.name}' failed");
        }

        /// <summary>
        /// Obfuscates a single blend tree.
        /// </summary>
        /// <param name="blendTree"></param>
        /// <returns></returns>
        /// <exception cref="System.Exception"></exception>
        private BlendTree ObfuscateBlendTree(BlendTree blendTree) {

            if (obfuscatedBlendTrees.ContainsKey(blendTree)) {
                return obfuscatedBlendTrees[blendTree];
            }
            else {
                string obfuscatedBlendTreeGUID = GUID.Generate().ToString();

                string newPath = GetObfuscatedPath<BlendTree>(obfuscatedBlendTreeGUID);
                BlendTree obfuscatedBlendTree = blendTree;

                if (AssetDatabase.IsMainAsset(blendTree)) {

                    if (AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(blendTree), newPath)) {
                        obfuscatedBlendTree = AssetDatabase.LoadAssetAtPath<BlendTree>(newPath);
                        obfuscatedBlendTrees.Add(blendTree, obfuscatedBlendTree);
                    }
                    else {
                        throw new System.Exception($"Obfuscation of blend tree '{blendTree.name}' failed");
                    }
                }

                obfuscatedBlendTree.name = obfuscatedBlendTreeGUID;

                obfuscatedBlendTree.blendParameter = ObfuscateParameterName(obfuscatedBlendTree.blendParameter);
                obfuscatedBlendTree.blendParameterY = ObfuscateParameterName(obfuscatedBlendTree.blendParameterY);

                List<ChildMotion> childMotions = new List<ChildMotion>(obfuscatedBlendTree.children);

                for (int i = 0; i < childMotions.Count; i++) {

                    if (childMotions[i].motion is AnimationClip) {
                        ChildMotion childMotion = childMotions[i];
                        childMotion.motion = ObfuscateAnimationClip((AnimationClip)childMotion.motion);
                        childMotion.directBlendParameter = ObfuscateParameterName(childMotion.directBlendParameter);
                        childMotions[i] = childMotion;
                    }
                    else if (obfuscatedBlendTree.children[i].motion is BlendTree) {
                        ChildMotion childMotion = childMotions[i];
                        childMotion.motion = ObfuscateBlendTree((BlendTree)obfuscatedBlendTree.children[i].motion);
                        childMotion.directBlendParameter = ObfuscateParameterName(childMotion.directBlendParameter);
                        childMotions[i] = childMotion;
                    }
                }

                obfuscatedBlendTree.children = childMotions.ToArray();

                return obfuscatedBlendTree;
            }

            throw new System.Exception($"Obfuscation of blend tree '{blendTree.name}' failed");
        }

        /// <summary>
        /// Obfuscates a single transition.
        /// </summary>
        /// <param name="transition"></param>
        private void ObfuscateTransition(AnimatorTransitionBase transition) {
            List<AnimatorCondition> conditions = new List<AnimatorCondition>(transition.conditions);

            for (int i = 0; i < conditions.Count; i++) {
                AnimatorCondition condition = conditions[i];
                condition.parameter = ObfuscateParameterName(condition.parameter);
                conditions[i] = condition;
            }

            transition.conditions = conditions.ToArray();
        }

        /// <summary>
        /// Obfuscates expression parameters and expresssion menus referenced in the descriptor.
        /// </summary>
        /// <param name="descriptor"></param>
        private void ObfuscateExpressionsAndMenus(VRCAvatarDescriptor descriptor) {

            if (descriptor.expressionParameters != null) {
                string newPath = GetObfuscatedPath<VRCExpressionParameters>();

                if (AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(descriptor.expressionParameters), newPath)) {
                    descriptor.expressionParameters = AssetDatabase.LoadAssetAtPath<VRCExpressionParameters>(newPath);

                    if (descriptor.expressionParameters.parameters != null) {

                        foreach (var expressionParameter in descriptor.expressionParameters.parameters) {

                            if (string.IsNullOrEmpty(expressionParameter.name))
                                continue;

                            if (!allParameters.Contains(expressionParameter.name))
                                Debug.LogError($"VRC expression parameter '{expressionParameter.name}' is not used in any controller. It's recommended to remove it.", descriptor.expressionParameters);

                            expressionParameter.name = ObfuscateParameterName(expressionParameter.name);
                        }
                    }
                }
            }

            if (descriptor.expressionsMenu != null)
                descriptor.expressionsMenu = ObfuscateExpressionMenu(descriptor.expressionsMenu);
        }

        /// <summary>
        /// Obfuscates an expression menu including all it's sub menus.
        /// </summary>
        /// <param name="menu"></param>
        /// <returns></returns>
        /// <exception cref="System.Exception"></exception>
        private VRCExpressionsMenu ObfuscateExpressionMenu(VRCExpressionsMenu menu) {
            string newPath = GetObfuscatedPath<VRCExpressionsMenu>();

            if (obfuscatedExpressionMenus.ContainsKey(menu))
                return obfuscatedExpressionMenus[menu];
            else if (AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(menu), newPath)) {
                VRCExpressionsMenu obfuscatedMenu = AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(newPath);

                obfuscatedExpressionMenus.Add(menu, obfuscatedMenu);

                foreach (var control in obfuscatedMenu.controls) {
                    control.parameter.name = ObfuscateParameterName(control.parameter.name);

                    foreach (var subParameter in control.subParameters) {
                        subParameter.name = ObfuscateParameterName(subParameter.name);
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

            throw new System.Exception($"Obfuscation of VRC expression menu '{menu.name}' failed");
        }

        /// <summary>
        /// Obfuscates all phys bones and contact receivers found in the hierarchy of the descriptor.
        /// </summary>
        /// <param name="descriptor"></param>
        private void ObfuscatePhysBonesAndContactReceivers(VRCAvatarDescriptor descriptor) {
            VRCPhysBone[] physBones = descriptor.GetComponentsInChildren<VRCPhysBone>(true);
            VRCContactReceiver[] contactReceivers = descriptor.GetComponentsInChildren<VRCContactReceiver>(true);

            foreach (var physBone in physBones) {

                if (!string.IsNullOrEmpty(physBone.parameter)) {

                    foreach (var suffix in VRC_PHYS_BONE_SUFFIXES) {
                        string physBoneParameterName = physBone.parameter + suffix;

                        if (obfuscatedParameters.ContainsKey(physBoneParameterName)) {
                            physBone.parameter = GetPhysBoneParameter(obfuscatedParameters[physBoneParameterName]);
                            break;
                        }
                    }
                }
            }

            foreach (var contactReceiver in contactReceivers) {
                contactReceiver.parameter = ObfuscateParameterName(contactReceiver.parameter);
            }
        }

        /// <summary>
        /// Obfuscates all textures referenced with obfuscated materials.
        /// </summary>
        private void ObfuscateTextures() {

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

        /// <summary>
        /// Obfuscates render textures of all cameras found in the hierarchy of the descriptor.
        /// </summary>
        /// <param name="descriptor"></param>
        private void ObfuscateRenderTextures(VRCAvatarDescriptor descriptor) {
            Camera[] cameras = descriptor.GetComponentsInChildren<Camera>(true);

            foreach (var camera in cameras) {

                if (camera.targetTexture != null)
                    camera.targetTexture = (RenderTexture)ObfuscateTexture(camera.targetTexture);
            }
        }

        /// <summary>
        /// Obfuscates a single texture.
        /// Built-in textures will be ignored.
        /// </summary>
        /// <param name="texture"></param>
        /// <returns></returns>
        /// <exception cref="System.Exception"></exception>
        private Texture ObfuscateTexture(Texture texture) {

            if (texture == null)
                return null;

            string path = AssetDatabase.GetAssetPath(texture);

            if (string.IsNullOrEmpty(path)) {
                Debug.LogError($"Texture '{texture.name}' cannot be obfuscated. It will be ignored.");
                return texture;
            }
            else if (path.Contains("unity_builtin")) {
                Debug.LogError($"Unity built-in texture '{texture.name}' cannot be obfuscated. It will be ignored.");
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

            throw new System.Exception($"Obfuscation of texture '{texture.name}' failed");
        }

        /// <summary>
        /// Obfuscates audio clips of all audio sources found in the hierarchy of the descriptor.
        /// </summary>
        /// <param name="descriptor"></param>
        private void ObfuscateAudioClips(VRCAvatarDescriptor descriptor) {
            AudioSource[] audioSources = descriptor.GetComponentsInChildren<AudioSource>(true);

            foreach (var audioSource in audioSources) {

                if (audioSource.clip != null)
                    audioSource.clip = ObfuscateAudioClip(audioSource.clip);
            }
        }

        /// <summary>
        /// Obfuscates a single audio clip.
        /// </summary>
        /// <param name="ausdioClip"></param>
        /// <returns></returns>
        /// <exception cref="System.Exception"></exception>
        private AudioClip ObfuscateAudioClip(AudioClip ausdioClip) {

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

            throw new System.Exception($"Obfuscation of audio clip '{ausdioClip.name}' failed");
        }

        /// <summary>
        /// True, if it is an obfuscated root game object.
        /// </summary>
        /// <param name="gameObject"></param>
        /// <returns></returns>
        private bool IsObfuscatedGameObject(GameObject gameObject) {
            return (gameObject.name.Length == (32 + SUFFIX.Length) && gameObject.name.EndsWith(SUFFIX));
        }

        /// <summary>
        /// Gets an obfuscated path with the matching file ending depending on it's type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="guid"></param>
        /// <returns></returns>
        private string GetObfuscatedPath<T>(string guid = "") {

            if (guid == "")
                guid = GUID.Generate().ToString();

            string path = $"{folder}/{guid}";

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

        /// <summary>
        /// Gets the parameter without the an allowed phys bone suffixes 
        /// (e.g. Nose_IsGrabbed -> Nose).
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        private string GetPhysBoneParameter(string parameter) {

            foreach (var suffix in VRC_PHYS_BONE_SUFFIXES) {

                if (parameter.EndsWith(suffix))
                    return parameter.Substring(0, parameter.Length - suffix.Length);
            }

            return "";
        }

        /// <summary>
        /// Gets only the suffix of a parameter which contains an allowed phys bone suffix 
        /// (e.g. Nose_IsGrabbed -> _IsGrabbed).
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        private string GetPhysBoneParameterSuffix(string parameter) {

            foreach (var suffix in VRC_PHYS_BONE_SUFFIXES) {

                if (parameter.EndsWith(suffix))
                    return suffix;
            }

            return "";
        }

        /// <summary>
        /// Gets the armature transform of an animator.
        /// A valid armature transform must start with "Armature" and must contain a 
        /// "Hips" transform.
        /// </summary>
        /// <param name="animator"></param>
        /// <returns></returns>
        private Transform GetArmatureTransform(Animator animator) {
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