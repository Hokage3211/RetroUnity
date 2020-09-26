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
        public float targetFPS = 50.99986f;

        public Renderer Display;

        private bool gameLoaded = false;

        private void Awake() {
            //LoadGame(Application.streamingAssetsPath + "/" + RomName);
        }

        public void loadGame()
        {
            string path = UnityEditor.EditorUtility.OpenFilePanel("Select Game ROM File", Application.streamingAssetsPath, "");
            string corePath = UnityEditor.EditorUtility.OpenFilePanel("Select Emulator Core File", Application.streamingAssetsPath, "dll");
            if (path != "" && corePath != "")
                LoadRom(path, corePath);
        }

        public void LoadGame(string path)
        {
            LoadRom(path);
        }

        private void Update() {
            if (gameLoaded) {
                _frameTimer += Time.deltaTime;
                float timePerFrame = 1 / targetFPS;
                if (!double.IsNaN(wrapper.GetAVInfo().timing.fps))
                    timePerFrame = 1f / (float)wrapper.GetAVInfo().timing.fps;

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
            if (Input.GetKeyDown(KeyCode.PageUp))
            {
                loadGame();
            }
        }

        public void LoadRom(string path, string corePath = "") {

            RAMPath = path.Substring(0, path.LastIndexOf('.'));
            STATEPath = RAMPath + ".state";
            RAMPath += ".srm";
            STATEPath = STATEPath.Insert(STATEPath.LastIndexOf("/"), "/States");
            RAMPath = RAMPath.Insert(RAMPath.LastIndexOf("/"), "/RAM");

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

            if (corePath == "")
                wrapper = new LibretroWrapper.Wrapper(Application.streamingAssetsPath + "/" + CoreName);
            else
                wrapper = new LibretroWrapper.Wrapper(corePath);

            wrapper.Init();
            wrapper.LoadGame(path);
            if (RAMPath != null)
                wrapper.LoadRAM(RAMPath);

            gameLoaded = true;
            wrapper.initialized = true;
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
            if (RAMPath != null && wrapper != null)
            {
                wrapper.SaveRAM(RAMPath);
                wrapper.DeInit();
                wrapper.initialized = false;
            }
            WindowsDLLHandler.Instance.UnloadCore();

            gameLoaded = false;
            
        }
    }
}
