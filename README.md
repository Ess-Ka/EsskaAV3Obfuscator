**IMPORTANT:** V2.0: Installation method has changed! Please read the "Installation" section below.


# Esska AV3Obfuscator

This software allows you to obfuscate your VRChat avatar.

![grafik](https://github.com/Ess-Ka/EsskaAV3Obfuscator/assets/84975839/6075fb22-9308-4852-b7a0-f008881a41a1)

## What is obfuscation?

Obfuscation means, remove all human readable content and replace it by something random. In case of AV3Obfuscator, all strings or filenames will be obfuscated. Someone which steals your avatar, has still access to your assets (e.g. meshes, materials and textures). But if your avatar is obfuscated, it will be more complicated to work with your data. Someone which want see the content of your game object, sees only garbage.

![grafik](https://user-images.githubusercontent.com/84975839/172045078-a90af8e5-17b0-410b-838c-28424dff3e9a.png)

![grafik](https://user-images.githubusercontent.com/84975839/172045220-3480adbb-e58d-4164-9b5a-c7bb0c97106b.png)

AV3Obfuscator does not encrypt any of your data. But you can combinate avatar encryption together with obfuscation. 

## What will be obfuscated?

Following data will be obfuscated by default:

- Entire transform hierarchy
- Controllers, layers, state machines, states, blend trees
- Avatar (of animator component)
- Avatar masks
- Animation clips

Optional, following data can be obfuscated:

- VRC expression parameters + menus
- Parameters (except of reserved VRC parameters)
- Meshes
- Blend shapes
- Materials
- Textures
- Audio clips

## What will not be obfuscated?

Currently, shaders will not be obfuscated. Additional animators below the hierarchy are not supported.

## Installation

Add the package to VRChat Creator Companion:
https://ess-ka.github.io/EsskaPackageListing

After the package was added, click on the "Project" tab in the Creator Companion and select "Manage Project" on your project. Then choose the package in the list and install it.

## Usage

Add the AV3Obfuscator component to your root avatar game object. Choose the options described below and run the obfuscation. If you upload the avatar for the first time, you have to transfer the avatar ID to your original file.

**IMPORTANT:** If your avatar was not obfuscated before, delete it from VRChat servers first! If you dont do that, your not obfuscated version can still be accessed over the VRChat servers. Someone who want see the content could simply download an older version of it.

### Preserve MMD

If you enable this checkbox, the name of the "Body" transform and all Blend Shapes on it will not be obfuscated. This ensures that MMD still works.

### VRC Expressions + Menus

![grafik](https://user-images.githubusercontent.com/84975839/172045160-3599712c-f9a0-4c0e-9c3e-7bb39b893dc4.png)

Obfuscates VRC expression parameters, menu and submenus.

### Parameters

![grafik](https://user-images.githubusercontent.com/84975839/172045110-0bf33ec7-d2f8-478a-b24b-a665da12c296.png)

Obfuscates the selected parameters used by any controller, state, state behaviour, blend tree, transition, phys bone component or used by any VRC expression menu. Reserved VRC parameters cannot be obfuscated.

#### GTAvaCrypt Parameters ####

If you use GTAvaCrypt (V1 or V2), you can run obfuscation after encryption, but you should unselect the parameters related to GTAvaCrypt.

#### OSC Parameters ####

If you use OSC driven parameters (e.g. VRCFT FaceTracking), you should unselect these parameters.

### Meshes

![grafik](https://user-images.githubusercontent.com/84975839/172045255-eb83c061-cedc-4b52-842e-e99902d851c3.png)

Obfuscates meshes of any mesh filter, skinned mesh renderer, particle system or particle system renderer.

### Blend Shapes

![grafik](https://user-images.githubusercontent.com/84975839/172045267-567e3508-c2f7-40eb-bea8-61d8a3fcbb27.png)

Obfuscates blend shapes of any used mesh. This will break face animations in MMD dances. If you want prevent that, set the "Preserve MMD" checkbox on top.

### Materials

![grafik](https://user-images.githubusercontent.com/84975839/172045276-5ec7a318-7200-4e20-bf6f-7f7fe7b76443.png)

Obfuscates materials of any mesh renderer, skinned mesh renderer, particle system renderer or animation clip.

### Textures

![grafik](https://user-images.githubusercontent.com/84975839/172046927-2c6408f2-d010-4b23-b97c-87e06284de1c.png)

Obfuscates textures of any used material, render textures used by any camera or icons used in VRC menus.

### Audio Clips

![grafik](https://user-images.githubusercontent.com/84975839/172045320-d9deb184-deaf-4209-9d37-3e56b7ba6652.png)

Obfuscates audio clips of any audio source.

### Obfuscate

![grafik](https://github.com/Ess-Ka/EsskaAV3Obfuscator/assets/84975839/8786e8a4-51de-45f7-b518-343f0261d45a)

This starts the obfuscation. Your avatar game object will be copied and all obfuscated data will be stored in the Assets/Obfuscated folder. Depending on amount of files, this process may take some time. Errors will be shown in console window.

### Clear Obfuscated Scene Data

![grafik](https://github.com/Ess-Ka/EsskaAV3Obfuscator/assets/84975839/a907a509-71e7-45ca-93dc-04793501b360)

Removes the obfuscated game object in the scene and its related obfuscated data in the Assets/Obfuscated folder.

### Clear Obfuscated Data

![grafik](https://github.com/Ess-Ka/EsskaAV3Obfuscator/assets/84975839/1e3a359f-e28b-426d-b169-f0e45dfe820d)

Removes the obfuscated game object in the scene and all obfuscated data in the Assets/Obfuscated folder.
