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

        private double _frameTimer;
        public float targetFPS = 50.987f;

        public bool overrideSpeed = false;
        public int boostedFPS = 60;

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

        private void Update() {
            if (gameLoaded) {
                _frameTimer += Time.deltaTime;
                double timePerFrame = 1 / targetFPS;
                if (!double.IsNaN(wrapper.GetAVInfo().timing.fps))
                {
                    timePerFrame = 1 / wrapper.GetAVInfo().timing.fps;
                    targetFPS = (float)wrapper.GetAVInfo().timing.fps;
                }

                if (overrideSpeed)
                    timePerFrame = 1f / boostedFPS;

                while (_frameTimer >= timePerFrame)
                {
                    wrapper.Update();
                    _frameTimer -= timePerFrame;
                }

                if (Input.GetKeyDown(KeyCode.G))
                    saveState();
                else if (Input.GetKeyDown(KeyCode.H))
                    loadState();
                else if (Input.GetKeyDown(KeyCode.J))
                    unloadROM();

                if (LibretroWrapper.tex != null)
                {
                    Display.material.mainTexture = LibretroWrapper.tex;
                }
            }
            
            if (Input.GetKeyDown(KeyCode.PageUp))
            {
                loadGame();
            }
        }

        public void LoadRom(string romPath, string corePath = "") {
            if (gameLoaded)
                unloadROM();

            RAMPath = romPath.Substring(0, romPath.LastIndexOf('.'));
            STATEPath = RAMPath + ".state";
            RAMPath += ".srm";
            STATEPath = STATEPath.Insert(STATEPath.LastIndexOf("/"), "/SaveStates");
            RAMPath = RAMPath.Insert(RAMPath.LastIndexOf("/"), "/RAMSaves");

#if !UNITY_ANDROID || UNITY_EDITOR
            // Doesn't work on Android because you can't do File.Exists in StreamingAssets folder.
            // Should figure out a different way to perform check later.
            // If the file doesn't exist the application gets stuck in a loop.
            if (!File.Exists(romPath)) {
                Debug.LogError(romPath + " not found.");
                return;
            }
#endif
            Display.material.color = Color.white;

            if (corePath == "")
                wrapper = new LibretroWrapper.Wrapper(Application.streamingAssetsPath + "/" + CoreName);
            else
                wrapper = new LibretroWrapper.Wrapper(corePath);

            wrapper.Init();

            bool returned = wrapper.LoadGame(romPath);
            if (RAMPath != null)
                wrapper.LoadRAM(RAMPath);

            gameLoaded = returned;
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
            unloadROM();
        }

        public void unloadROM()
        {
            if (RAMPath != null && RAMPath != "" && wrapper != null)
            {
                wrapper.SaveRAM(RAMPath);
                wrapper.DeInit();
            }
            WindowsDLLHandler.Instance.UnloadCore();
            Display.material.mainTexture = null;
            RAMPath = "";
            STATEPath = "";
            gameLoaded = false;
        }
    }
}
