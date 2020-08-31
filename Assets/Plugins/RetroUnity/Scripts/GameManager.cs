using System.IO;
using RetroUnity.Utility;
using UnityEngine;

namespace RetroUnity {
    public class GameManager : MonoBehaviour {

        [SerializeField] private string CoreName = "snes9x_libretro.dll";
        [SerializeField] private string RomName = "Chrono Trigger (USA).sfc";
        private string RAMPath = "";
        private string STATEPath = "";
        private LibretroWrapper.Wrapper wrapper;

        private float _frameTimer;

        public Renderer Display;

        private void Awake() {
            LoadRom(Application.streamingAssetsPath + "/" + RomName);
        }

        private void Update() {
            if (wrapper != null) {
                _frameTimer += Time.deltaTime;
                float timePerFrame = 1f / (float)wrapper.GetAVInfo().timing.fps;

                while (_frameTimer >= timePerFrame)
                {
                    wrapper.Update();
                    _frameTimer -= timePerFrame;
                }

                if (Input.GetKeyDown(KeyCode.G))
                    saveState();
                else if (Input.GetKeyDown(KeyCode.H))
                    loadState();
            }
            if (LibretroWrapper.tex != null) {
                Display.material.mainTexture = LibretroWrapper.tex;
            }
        }

        public void LoadRom(string path) {

            RAMPath = path.Substring(0, path.LastIndexOf('.'));
            STATEPath = RAMPath + ".state";
            RAMPath += ".srm";

#if !UNITY_ANDROID || UNITY_EDITOR
            // Doesn't work on Android because you can't do File.Exists in StreamingAssets folder.
            // Should figure out a different way to perform check later.
            // If the file doesn't exist the application gets stuck in a loop.
            if (!File.Exists(path)) {
                Debug.LogError(path + " not found.");
                return;
            }
#endif
            Display.material.color = Color.white;

            wrapper = new LibretroWrapper.Wrapper(Application.streamingAssetsPath + "/" + CoreName);

            wrapper.Init();
            wrapper.LoadGame(path);
            wrapper.LoadRAM(RAMPath);

        }

        public void saveState()
        {
            wrapper.SaveState(STATEPath);
        }

        public void loadState()
        {
            wrapper.LoadState(STATEPath);
        }

        private void OnDestroy() {
            wrapper.SaveRAM(RAMPath);
            wrapper.DeInit();
            WindowsDLLHandler.Instance.UnloadCore();
        }
    }
}
