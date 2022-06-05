using System;
using System.Collections.Generic;

namespace Esska.AV3Obfuscator {

    [Serializable]
    public class ObfuscationConfiguration {

        public bool obfuscateLayers = true;
        public bool obfuscateExpressionParameters = true;
        public bool obfuscateParameters = true;
        public List<string> obfuscatedParameters = new List<string>();
        public bool obfuscateMeshes = true;
        public bool obfuscateBlendShapes = true;
        public bool obfuscateMaterials = true;
        public bool obfuscateTextures = true;
        public bool obfuscateAudioClips = true;
    }

}
