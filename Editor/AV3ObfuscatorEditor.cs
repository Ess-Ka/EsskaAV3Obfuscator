using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace Esska.AV3Obfuscator.Editor {

    [CustomEditor(typeof(AV3Obfuscator))]
    public partial class AV3ObfuscatorEditor : UnityEditor.Editor {

        AV3Obfuscator obfus;
        VRCAvatarDescriptor descriptor;
        bool refreshParameters;
        SortedSet<string> allParameters;

        void OnEnable() {
            obfus = (AV3Obfuscator)target;
            refreshParameters = true;
        }

        public override void OnInspectorGUI() {
            //base.OnInspectorGUI();

            if (Application.isPlaying) {
                EditorGUILayout.HelpBox("Controls are disabled during play mode", MessageType.Info, true);
                return;
            }

            descriptor = obfus.GetComponent<VRCAvatarDescriptor>();

            if (descriptor == null) {
                EditorGUILayout.HelpBox("No avatar descriptor found", MessageType.Warning, true);
                return;
            }

            Undo.RecordObject(obfus, "Modify Obfuscator");

            if (refreshParameters) {
                allParameters = new SortedSet<string>();

                for (int i = 0; i < descriptor.baseAnimationLayers.Length; i++) {

                    if (descriptor.baseAnimationLayers[i].animatorController != null) {
                        AnimatorController controller = (AnimatorController)descriptor.baseAnimationLayers[i].animatorController;

                        foreach (var parameter in controller.parameters) {

                            if (!allParameters.Contains(parameter.name))
                                allParameters.Add(parameter.name);
                        }
                    }
                }

                for (int i = 0; i < descriptor.specialAnimationLayers.Length; i++) {

                    if (descriptor.specialAnimationLayers[i].animatorController != null) {
                        AnimatorController controller = (AnimatorController)descriptor.specialAnimationLayers[i].animatorController;

                        foreach (var parameter in controller.parameters) {

                            if (!allParameters.Contains(parameter.name))
                                allParameters.Add(parameter.name);
                        }
                    }
                }

                for (int i = obfus.config.obfuscatedParameters.Count - 1; i >= 0; i--) {

                    if (!allParameters.Contains(obfus.config.obfuscatedParameters[i]))
                        obfus.config.obfuscatedParameters.RemoveAt(i);
                }

                refreshParameters = false;
            }

            GUILayout.Space(5f);
            EditorGUILayout.HelpBox("Obfuscates selectable content of your avatar. All obfuscated data will be stored in an extra folder (Assets/Obfuscated). Your files will not be changed.", MessageType.None, true);
            GUILayout.Space(10f);
            GUILayout.Label("Obfuscate (Default)", EditorStyles.boldLabel);

            GUILayout.BeginVertical(GUI.skin.box);
            {
                EditorGUILayout.LabelField("- Transforms (entire hierarchy)");
                EditorGUILayout.LabelField("- Controllers");
                EditorGUILayout.LabelField("- Avatar");
                EditorGUILayout.LabelField("- Avatar Masks");
                EditorGUILayout.LabelField("- Animation Clips");
            }
            GUILayout.EndVertical();

            GUILayout.Space(10f);

            obfus.config.showOptionalObfuscation = EditorGUILayout.Foldout(obfus.config.showOptionalObfuscation, "Obfuscate (Optional)", EditorStyles.foldoutHeader);

            if (obfus.config.showOptionalObfuscation) {

                GUILayout.BeginVertical(GUI.skin.box);
                {
                    obfus.config.obfuscateLayers = EditorGUILayout.ToggleLeft(new GUIContent("Layers, State Machines, States, Blend Trees", "Layers, State Machines, States, Blend Trees of any used Controller"), obfus.config.obfuscateLayers);
                }
                GUILayout.EndVertical();

                GUILayout.BeginVertical(GUI.skin.box);
                {
                    obfus.config.obfuscateExpressionParameters = EditorGUILayout.ToggleLeft(new GUIContent("VRC Expression Parameters + Menus", "VRC Expression Parameters, Menu and Submenus"), obfus.config.obfuscateExpressionParameters);

                    if (obfus.config.obfuscateExpressionParameters) {
                        EditorGUI.indentLevel = 1;
                        obfus.config.obfuscateParameters = EditorGUILayout.ToggleLeft(new GUIContent("Parameters", "Parameters of any used Controller"), obfus.config.obfuscateParameters);

                        if (obfus.config.obfuscateParameters) {
                            GUILayout.Space(5f);
                            EditorGUILayout.HelpBox("Select parameters, which should be obfuscated. Reserved VRC parameters cannot be obfuscated. It's recommended to unselect parameters, which are driven by OSC. After every obfuscation, parameters will lose their previous saved value in VRC Menu.", MessageType.None, true);
                            GUILayout.Space(5f);

                            obfus.config.showParameterSelection = EditorGUILayout.Foldout(obfus.config.showParameterSelection, "Parameter Selection");

                            if (obfus.config.showParameterSelection) {

                                GUILayout.BeginHorizontal();
                                {
                                    GUILayout.Space(16f);

                                    GUIStyle smallButton = new GUIStyle(GUI.skin.button) {
                                        fontSize = 9,
                                        alignment = TextAnchor.MiddleCenter
                                    };

                                    if (GUILayout.Button("Select All", smallButton)) {

                                        foreach (var parameter in allParameters) {

                                            if (!obfus.config.obfuscatedParameters.Contains(parameter)) {
                                                obfus.config.obfuscatedParameters.Add(parameter);
                                                EditorUtility.SetDirty(obfus);
                                            }
                                        }
                                    }

                                    if (GUILayout.Button("Unselect All", smallButton)) {
                                        obfus.config.obfuscatedParameters.Clear();
                                        EditorUtility.SetDirty(obfus);
                                    }

                                    GUILayout.FlexibleSpace();
                                }
                                GUILayout.EndHorizontal();

                                GUILayout.Space(5f);

                                foreach (var parameter in allParameters) {

                                    if (Obfuscator.VRC_RESERVED_ANIMATOR_PARAMETERS.Contains(parameter)) {
                                        GUI.enabled = false;
                                        EditorGUILayout.ToggleLeft(parameter, false);
                                        GUI.enabled = true;
                                    }
                                    else {
                                        bool obfuscate = EditorGUILayout.ToggleLeft(parameter, obfus.config.obfuscatedParameters.Contains(parameter));

                                        if (obfuscate && !obfus.config.obfuscatedParameters.Contains(parameter)) {
                                            obfus.config.obfuscatedParameters.Add(parameter);
                                            EditorUtility.SetDirty(obfus);
                                        }
                                        else if (!obfuscate && obfus.config.obfuscatedParameters.Contains(parameter)) {
                                            obfus.config.obfuscatedParameters.Remove(parameter);
                                            EditorUtility.SetDirty(obfus);
                                        }
                                    }
                                }
                            }

                            GUILayout.Space(5f);
                        }

                        EditorGUI.indentLevel = 0;
                    }
                }
                GUILayout.EndVertical();

                GUILayout.BeginVertical(GUI.skin.box);
                {
                    obfus.config.obfuscateMeshes = EditorGUILayout.ToggleLeft(new GUIContent("Meshes", "Meshes of any MeshFilter, SkinnedMeshRenderer, ParticleSystem or ParticleSystemRenderer"), obfus.config.obfuscateMeshes);

                    if (obfus.config.obfuscateMeshes) {
                        EditorGUI.indentLevel = 1;
                        obfus.config.obfuscateBlendShapes = EditorGUILayout.ToggleLeft(new GUIContent("Blend Shapes", "Blend Shapes of any used Mesh"), obfus.config.obfuscateBlendShapes);

                        if (obfus.config.obfuscateBlendShapes) {
                            GUILayout.Space(5f);
                            EditorGUILayout.HelpBox("Obfuscating blend shapes will break face animations in MMD dances", MessageType.None, true);
                            GUILayout.Space(5f);
                        }

                        EditorGUI.indentLevel = 0;
                    }
                }
                GUILayout.EndVertical();

                GUILayout.BeginVertical(GUI.skin.box);
                {
                    obfus.config.obfuscateMaterials = EditorGUILayout.ToggleLeft(new GUIContent("Materials", "Materials of any MeshRenderer, SkinnedMeshRenderer, ParticleSystemRenderer or AnimationClip"), obfus.config.obfuscateMaterials);

                    if (obfus.config.obfuscateMaterials) {
                        EditorGUI.indentLevel = 1;
                        obfus.config.obfuscateTextures = EditorGUILayout.ToggleLeft(new GUIContent("Textures", "Textures of any used Material, RenderTexture used of any Camera or Icons used in VRC Menus"), obfus.config.obfuscateTextures);
                        EditorGUI.indentLevel = 0;
                    }
                }
                GUILayout.EndVertical();

                GUILayout.BeginVertical(GUI.skin.box);
                {
                    obfus.config.obfuscateAudioClips = EditorGUILayout.ToggleLeft(new GUIContent("Audio Clips", "Audio Clips of any AudioSource"), obfus.config.obfuscateAudioClips);
                }
                GUILayout.EndVertical();
            }

            GUILayout.Space(10f);

            GUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("Obfuscate")) {
                    Obfuscator obfuscator = CreateInstance<Obfuscator>();
                    obfuscator.ClearObfuscateGameObjects();
                    obfuscator.Obfuscate(obfus.gameObject, obfus.config);
                }

                if (GUILayout.Button("Clear Obfuscated Data", GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.45f))) {
                    Obfuscator obfuscator = CreateInstance<Obfuscator>();
                    obfuscator.ClearObfuscateGameObjects();
                    obfuscator.ClearObfuscatedFolder();
                }
            }
            GUILayout.EndHorizontal();

            if (GUI.changed)
                EditorUtility.SetDirty(obfus);
        }
    }
}