# Esska AV3Obfuscator

This software allows you to obfuscate your VRChat avatar.

![grafik](https://user-images.githubusercontent.com/84975839/172044969-6f212d45-a45f-4b0b-afaf-f43862618153.png)

## What is obfuscation?

Obfuscation means, remove all human readable content and replace it by something random. In case of AV3Obfuscator, all strings or filenames will be obfuscated. Someone which steals your avatar, has still access to your assets (e.g. Meshes, Materials and Textures). But if your avatar is obfuscated, it will be more complicated to work with your data. Someone which want see the content of your GameObject, sees only garbage.

![grafik](https://user-images.githubusercontent.com/84975839/172045220-3480adbb-e58d-4164-9b5a-c7bb0c97106b.png)

AV3Obfuscator does not encrypt any of your data. But you can combinate avatar encryption together with obfuscation. 

## What will be obfuscated?

Following data will be obfuscated by default:

- Entire Transform hierarchy
- Controllers
- Avatar (of Animator component)
- Avatar Masks
- Animation Clips

Optional, following data can be obfuscated:

- Layers, State Machines, States, Blend Trees
- Parameters (except of reserved VRC parameters)
- VRC Expression Parameters + Menus
- Meshes
- Blend Shapes
- Materials
- Textures
- Audio Clips

## What will not be obfuscated?

Currently, Shaders will not be obfuscated.

## Usage

After import the UnityPackage, add the AV3Obfuscator component to your root avatar GameObect. Choose the options described below and run the obfuscation. If you upload the avatar for the first time, you have to transfer the avatar ID to your original file.

### Layers, State Machines, States, Blend Trees

![grafik](https://user-images.githubusercontent.com/84975839/172045078-a90af8e5-17b0-410b-838c-28424dff3e9a.png)

Obfuscates Layers, State Machines, States, Blend Trees of any used Controller.

### VRC Expressions + Menus

![grafik](https://user-images.githubusercontent.com/84975839/172045160-3599712c-f9a0-4c0e-9c3e-7bb39b893dc4.png)

Obfuscates VRC Expression Parameters, Menu and Submenus.

### Parameters

![grafik](https://user-images.githubusercontent.com/84975839/172045110-0bf33ec7-d2f8-478a-b24b-a665da12c296.png)

Obfuscates the selected parameters. Reserved VRC parameters cannot be obfuscated.

#### GTAvaCrypt Parameters ####

If you use GTAvaCrypt (V1 or V2), you can run obfuscation after encryption, but you should unselect the parameters related to GTAvaCrypt.

#### OSC Parameters ####

If you use OSC driven parameters (e.g. VRCFT FaceTracking), you should unselect these parameters.

### Meshes

![grafik](https://user-images.githubusercontent.com/84975839/172045255-eb83c061-cedc-4b52-842e-e99902d851c3.png)

Obfuscates Meshes of any MeshFilter, SkinnedMeshRenderer or ParticleSystem.

### Blend Shapes

![grafik](https://user-images.githubusercontent.com/84975839/172045267-567e3508-c2f7-40eb-bea8-61d8a3fcbb27.png)

Obfuscates Blend Shapes of any used Mesh. This will break face animations in MMD dances.

### Materials

![grafik](https://user-images.githubusercontent.com/84975839/172045276-5ec7a318-7200-4e20-bf6f-7f7fe7b76443.png)

Obfuscates Materials of any MeshRenderer, SkinnedMeshRenderer, ParticleSystemRenderer or AnimationClip.

### Textures

![grafik](https://user-images.githubusercontent.com/84975839/172046927-2c6408f2-d010-4b23-b97c-87e06284de1c.png)

Obfuscates Textures of any used Material.

### Audio Clips

![grafik](https://user-images.githubusercontent.com/84975839/172045320-d9deb184-deaf-4209-9d37-3e56b7ba6652.png)

Obfuscates Audio Clips of any AudioSource.

### Obfuscate

![grafik](https://user-images.githubusercontent.com/84975839/172045336-bde72aed-80bb-4bfb-b7a5-6f18973b6115.png)

This starts the obfuscation. Your avatar GameObject will be copied and all obfuscated data will be stored in the Assets/Obfuscated folder. Depending on amount of files, this process may take some time. Errors will be shown in console window.

### Clear Obfuscated Data

![grafik](https://user-images.githubusercontent.com/84975839/172045352-7d1844c1-ee24-4746-9e83-36697b1a2827.png)

Removes the obfuscated GameObject and the obfuscated data in the Assets/Obfuscated folder.
