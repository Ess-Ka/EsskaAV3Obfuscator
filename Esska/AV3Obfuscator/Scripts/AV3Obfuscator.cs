using UnityEngine;

namespace Esska.AV3Obfuscator {

    [HelpURL("https://github.com/Ess-Ka/EsskaAV3Obfuscator")]
    [DisallowMultipleComponent]
    public class AV3Obfuscator : MonoBehaviour {

        [HideInInspector]
        public ObfuscationConfiguration config = new ObfuscationConfiguration();

    }
}