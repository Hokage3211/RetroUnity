/* This file reuses code from the following file: https://github.com/fr500/R.net/blob/34a9c867684e6a7891280de5b8c373482247fc93/R.net/libretro-sharpie.cs
 * See the original license below:  
 * 
 * R.net project
 *  Copyright (C) 2010-2015 - Andrés Suárez
 *  Copyright (C) 2010-2011 - Iván Fernandez
 *
 *  libretro.net is free software: you can redistribute it and/or modify it under the terms
 *  of the GNU General Public License as published by the Free Software Found-
 *  ation, either version 3 of the License, or (at your option) any later version.
 *
 *  libretro.net is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
 *  without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR
 *  PURPOSE.  See the GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License along with libretro.net.
 *  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using RetroUnity.Utility;
using UnityEngine;
using Unity.Collections;
using UnityEngine.Profiling;
using Unity.Jobs;

namespace RetroUnity {
    public partial class LibretroWrapper : MonoBehaviour {
    
        private static Speaker _speaker;

        public static Texture2D tex;
        public static int pix;
        public static int w;
        public static int h;
        public static int p;

        public static byte[] Src;
        public static byte[] Dst;
    
        public enum PixelFormat {
            // 0RGB1555, native endian. 0 bit must be set to 0.
            // This pixel format is default for compatibility concerns only.
            // If a 15/16-bit pixel format is desired, consider using RGB565.
            RetroPixelFormat_0RGB1555 = 0,

            // XRGB8888, native endian. X bits are ignored.
            RetroPixelFormatXRGB8888 = 1,

            // RGB565, native endian. This pixel format is the recommended format to use if a 15/16-bit format is desired
            // as it is the pixel format that is typically available on a wide range of low-power devices.
            // It is also natively supported in APIs like OpenGL ES.
            RetroPixelFormatRGB565 = 2,

            // Ensure sizeof() == sizeof(int).
            RetroPixelFormatUnknown = int.MaxValue
        }

        private void Start() {
            _speaker = GameObject.Find("Speaker").GetComponent<Speaker>();
        }

        //Shouldn't be part of the wrapper, will remove later
        [StructLayout(LayoutKind.Sequential)]
        public class Pixel {
            public float Alpha;
            public float Red;
            public float Green;
            public float Blue;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SystemAVInfo {
            public Geometry geometry;
            public Timing timing;
        }

        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct GameInfo {
            public char* path;
            public void* data;
            public uint size;
            public char* meta;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Geometry {
            public uint base_width;
            public uint base_height;
            public uint max_width;
            public uint max_height;
            public float aspect_ratio;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Timing {
            public double fps;
            public double sample_rate;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct retro_frame_time_callback
        {
            public IntPtr callback; // retro_frame_time_callback_t
            public long reference;
        }

        private enum retro_hw_context_type
        {
            RETRO_HW_CONTEXT_NONE = 0,
            RETRO_HW_CONTEXT_OPENGL = 1,
            RETRO_HW_CONTEXT_OPENGLES2 = 2,
            RETRO_HW_CONTEXT_OPENGL_CORE = 3,
            RETRO_HW_CONTEXT_OPENGLES3 = 4,
            RETRO_HW_CONTEXT_OPENGLES_VERSION = 5,
            RETRO_HW_CONTEXT_VULKAN = 6,
            RETRO_HW_CONTEXT_DIRECT3D = 7
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct retro_hw_render_callback
        {
            public retro_hw_context_type context_type;
            public IntPtr context_reset;           // retro_hw_context_reset_t
            public IntPtr get_current_framebuffer; // retro_hw_get_current_framebuffer_t
            public IntPtr get_proc_address;        // retro_hw_get_proc_address_t
            [MarshalAs(UnmanagedType.U1)] public bool depth;
            [MarshalAs(UnmanagedType.U1)] public bool stencil;
            [MarshalAs(UnmanagedType.U1)] public bool bottom_left_origin;
            public uint version_major;
            public uint version_minor;
            [MarshalAs(UnmanagedType.U1)] public bool cache_context;

            public IntPtr context_destroy; // retro_hw_context_reset_t

            [MarshalAs(UnmanagedType.U1)] public bool debug_context;
        }

        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct retro_memory_descriptor
        {
            public ulong flags;
            public void* ptr;
            public ulong offset;
            public ulong start;
            public ulong select;
            public ulong disconnect;
            public ulong len;
            public char* addrspace;
        }

        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct retro_memory_map
        {
            public retro_memory_descriptor* descriptors;
            public uint num_descriptors;
        }

        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct SystemInfo {

            public char* library_name;
            public char* library_version;
            public char* valid_extensions;

            [MarshalAs(UnmanagedType.U1)]
            public bool need_fullpath;

            [MarshalAs(UnmanagedType.U1)]
            public bool block_extract;
        }

        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct retro_subsystem_memory_info
        {
            public char* extension;
            public uint type;
        }

        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct retro_subsystem_rom_info
        {
            public char* desc;
            public char* valid_extensions;
            [MarshalAs(UnmanagedType.U1)] public bool need_fullpath;
            [MarshalAs(UnmanagedType.U1)] public bool block_extract;
            [MarshalAs(UnmanagedType.U1)] public bool required;
            public retro_subsystem_memory_info* memory;
            public uint num_memory;
        }

        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct retro_subsystem_info
        {
            public char* desc;
            public char* ident;
            public retro_subsystem_rom_info* roms;
            public uint num_roms;
            public uint id;
        }

        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct retro_variable
        {
            /* Variable to query in RETRO_ENVIRONMENT_GET_VARIABLE.
             * If NULL, obtains the complete environment string if more
             * complex parsing is necessary.
             * The environment string is formatted as key-value pairs
             * delimited by semicolons as so:
             * "key1=value1;key2=value2;..."
             */
            public readonly char* key;

            /* Value to be obtained. If key does not exist, it is set to NULL. */
            public char* value;
        };

        [System.Serializable]
        public class CoreOptions
        {
            public string CoreName = string.Empty;
            public List<string> Options = new List<string>();
        }

        [System.Serializable]
        public class CoreOptionsList
        {
            public List<CoreOptions> Cores = new List<CoreOptions>();
        }

        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct retro_core_option_definition
        {
            public char* key;
            public char* desc;
            public char* info;
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.Struct, SizeConst = RETRO_NUM_CORE_OPTION_VALUES_MAX)]
            public retro_core_option_value[] values; // retro_core_option_value[RETRO_NUM_CORE_OPTION_VALUES_MAX]
            public char* default_value;
        }

        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct retro_core_options_intl
        {
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.Struct)]
            public IntPtr us;    // retro_core_option_definition*
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.Struct)]
            public IntPtr local; // retro_core_option_definition*
        }

        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct retro_core_option_value
        {
            public char* value;
            public char* label;
        }

        public const int RETRO_NUM_CORE_OPTION_VALUES_MAX = 128;

        public class Environment {

            public const int RETRO_API_VERSION = 1;

            private const int RETRO_ENVIRONMENT_EXPERIMENTAL = 0x10000;
            private const int RETRO_ENVIRONMENT_PRIVATE = 0x20000;

            

            public const uint RetroEnvironmentSetRotation = 1;
            public const uint RetroEnvironmentGetOverscan = 2;
            public const uint RetroEnvironmentGetCanDupe = 3;
            //public const uint RetroEnvironmentGetVariable = 4;
            //public const uint RetroEnvironmentSetVariables = 5;
            public const uint RetroEnvironmentSetMessage = 6;
            public const uint RetroEnvironmentShutdown = 7;
            public const uint RetroEnvironmentSetPerformanceLevel = 8;
            public const uint RetroEnvironmentGetSystemDirectory = 9;
            public const uint RetroEnvironmentSetPixelFormat = 10;
            public const uint RetroEnvironmentSetInputDescriptors = 11;
            public const uint RetroEnvironmentSetKeyboardCallback = 12;
            public const uint RETRO_ENVIRONMENT_SET_DISK_CONTROL_INTERFACE = 13;
            public const uint RETRO_ENVIRONMENT_SET_HW_RENDER = 14;
            public const uint RETRO_ENVIRONMENT_GET_VARIABLE = 15;
            public const uint RETRO_ENVIRONMENT_SET_VARIABLES = 16;
            public const uint RETRO_ENVIRONMENT_GET_VARIABLE_UPDATE = 17;
            public const uint RETRO_ENVIRONMENT_SET_SUPPORT_NO_GAME = 18;
            public const uint RETRO_ENVIRONMENT_GET_LIBRETRO_PATH = 19;
            public const uint RETRO_ENVIRONMENT_SET_FRAME_TIME_CALLBACK = 21;
            public const uint RETRO_ENVIRONMENT_SET_AUDIO_CALLBACK = 22;
            public const uint RETRO_ENVIRONMENT_GET_CORE_ASSETS_DIRECTORY = 30;
            public const uint RETRO_ENVIRONMENT_GET_SAVE_DIRECTORY = 31;
            public const uint RETRO_ENVIRONMENT_SET_SYSTEM_AV_INFO = 32;
            public const uint RETRO_ENVIRONMENT_SET_SUBSYSTEM_INFO = 34;
            public const uint RETRO_ENVIRONMENT_SET_MEMORY_MAPS = 36 | RETRO_ENVIRONMENT_EXPERIMENTAL;
            public const uint RETRO_ENVIRONMENT_SET_GEOMETRY = 37;
            public const uint RETRO_ENVIRONMENT_GET_AUDIO_VIDEO_ENABLE = 47 | RETRO_ENVIRONMENT_EXPERIMENTAL;
            public const uint RETRO_ENVIRONMENT_GET_CORE_OPTIONS_VERSION = 52;
            public const uint RETRO_ENVIRONMENT_SET_CORE_OPTIONS_INTL = 54;
        }

        public partial class Wrapper {
            public const int AudioBatchSize = 4096;
            public static List<float> AudioBatch = new List<float>(65536);
            public static int BatchPosition;
            private PixelFormat _pixelFormat;
            private bool _requiresFullPath;
            private SystemAVInfo _av;
            private Pixel[] _frameBuffer;
            public static int Pix = 0;
            public static int w = 0;
            public static int h = 0;
            public static int p = 0;
            public static uint Button;
            public static uint Keep;

            //Prevent GC on delegates as long as the wrapper is running
            private Libretro.RetroEnvironmentDelegate _environment;
            private Libretro.RetroVideoRefreshDelegate _videoRefresh;
            private Libretro.RetroAudioSampleDelegate _audioSample;
            private Libretro.RetroAudioSampleBatchDelegate _audioSampleBatch;
            private Libretro.RetroInputPollDelegate _inputPoll;
            private Libretro.RetroInputStateDelegate _inputState;
            public Wrapper(string coreToLoad) {
                Libretro.InitializeLibrary(coreToLoad);
            }

            public bool initialized = false;
            string coreName;
            public unsafe void Init() {

                LoadCoreOptionsFile();

                int apiVersion = Libretro.RetroApiVersion();
                SystemInfo info = new SystemInfo();
                Libretro.RetroGetSystemInfo(ref info);

                coreName = Marshal.PtrToStringAnsi((IntPtr)info.library_name);
                string coreVersion = Marshal.PtrToStringAnsi((IntPtr)info.library_version);
                string validExtensions = Marshal.PtrToStringAnsi((IntPtr)info.valid_extensions);
                _requiresFullPath = info.need_fullpath;
                bool blockExtract = info.block_extract;

                Debug.Log("Core information:");
                Debug.Log("API Version: " + apiVersion);
                Debug.Log("Core Name: " + coreName);
                Debug.Log("Core Version: " + coreVersion);
                Debug.Log("Valid Extensions: " + validExtensions);
                Debug.Log("Block Extraction: " + blockExtract);
                Debug.Log("Requires Full Path: " + _requiresFullPath);

                _environment = RetroEnvironment;
                _videoRefresh = RetroVideoRefresh;
                _audioSample = RetroAudioSample;
                _audioSampleBatch = RetroAudioSampleBatch;
                _inputPoll = RetroInputPoll;
                _inputState = RetroInputState;

                Debug.Log("Setting up environment:");

                Libretro.RetroSetEnvironment(_environment);
                Libretro.RetroSetVideoRefresh(_videoRefresh);
                Libretro.RetroSetAudioSample(_audioSample);
                Libretro.RetroSetAudioSampleBatch(_audioSampleBatch);
                Libretro.RetroSetInputPoll(_inputPoll);
                Libretro.RetroSetInputState(_inputState);

                Libretro.RetroInit();
            }

            public void DeInit()
            {
                Libretro.RetroDeInit();
            }

            public bool Update() {
                Libretro.RetroRun();
                return true;
            }

            public SystemAVInfo GetAVInfo() {
                return _av;
            }

            public Pixel[] GetFramebuffer() {
                return _frameBuffer;
            }

            private unsafe void RetroVideoRefresh(void* data, uint width, uint height, uint pitch) {

                // Process Pixels one by one for now...this is not the best way to do it 
                // should be using memory streams or something

                //Declare the pixel buffer to pass on to the renderer
                if(_frameBuffer == null || _frameBuffer.Length != width * height)
                    _frameBuffer = new Pixel[width * height];

                //Get the array from unmanaged memory as a pointer
                var pixels = (IntPtr)data;
                //Gets The pointer to the row start to use with the pitch
                //IntPtr rowStart = pixels;

                //Get the size to move the pointer
                //int size = 24;

                uint i;
                uint j;

                switch (_pixelFormat) {
                    case PixelFormat.RetroPixelFormat_0RGB1555:

                        LibretroWrapper.w = Convert.ToInt32(width);
                        LibretroWrapper.h = Convert.ToInt32(height);
                        if (tex == null) {
                            tex = new Texture2D(LibretroWrapper.w, LibretroWrapper.h, TextureFormat.RGB565, false);
                        }
                        LibretroWrapper.p = Convert.ToInt32(pitch);

                        //size = Marshal.SizeOf(typeof(short));
                        for (i = 0; i < height; i++) {
                            for (j = 0; j < width; j++) {
                                short packed = Marshal.ReadInt16(pixels);
                                _frameBuffer[i * width + j] = new Pixel {
                                    Alpha = 1
                                    ,
                                    Red = ((packed >> 10) & 0x001F) / 31.0f
                                    ,
                                    Green = ((packed >> 5) & 0x001F) / 31.0f
                                    ,
                                    Blue = (packed & 0x001F) / 31.0f
                                };
                                var color = new Color(((packed >> 10) & 0x001F) / 31.0f, ((packed >> 5) & 0x001F) / 31.0f, (packed & 0x001F) / 31.0f, 1.0f);
                                tex.SetPixel((int)i, (int)j, color);
                                //pixels = (IntPtr)((int)pixels + size);
                            }
                            tex.filterMode = FilterMode.Trilinear;
                            tex.Apply();
                            //pixels = (IntPtr)((int)rowStart + pitch);
                            //rowStart = pixels;
                        }
                        break;
                    case PixelFormat.RetroPixelFormatXRGB8888:
                        LibretroWrapper.w = Convert.ToInt32(width);
                        LibretroWrapper.h = Convert.ToInt32(height);
                        if (tex == null)
                        {
                            tex = new Texture2D(LibretroWrapper.w, LibretroWrapper.h, TextureFormat.BGRA32, false);
                        }
                        if (tex.format != TextureFormat.BGRA32 || tex.width != LibretroWrapper.w || tex.height != LibretroWrapper.h)
                        {
                            tex = new Texture2D(LibretroWrapper.w, LibretroWrapper.h, TextureFormat.BGRA32, false);
                        }

                        new ARGB8888Job
                        {
                            SourceData = (uint*)data,
                            Width = LibretroWrapper.w,
                            Height = LibretroWrapper.h,
                            PitchPixels = pitch,
                            TextureData = tex.GetRawTextureData<uint>()
                        }.Schedule().Complete();

                        tex.Apply();
                        break;

                    case PixelFormat.RetroPixelFormatRGB565:

                        var imagedata565 = new IntPtr(data);
                        LibretroWrapper.w = Convert.ToInt32(width);
                        LibretroWrapper.h = Convert.ToInt32(height);
                        if (tex == null) {
                            tex = new Texture2D(LibretroWrapper.w, LibretroWrapper.h, TextureFormat.RGB565, false);
                        }
                        LibretroWrapper.p = Convert.ToInt32(pitch);
                        int srcsize565 = 2 * (LibretroWrapper.p * LibretroWrapper.h);
                        int dstsize565 = 2 * (LibretroWrapper.w * LibretroWrapper.h);
                        if (Src == null || Src.Length != srcsize565)
                            Src = new byte[srcsize565];
                        if (Dst == null || Dst.Length != dstsize565)
                            Dst = new byte[dstsize565];
                        Marshal.Copy(imagedata565, Src, 0, srcsize565);
                        int m565 = 0;
                        for (int y = 0; y < LibretroWrapper.h; y++) {
                            for (int k = 0 * 2 + y * LibretroWrapper.p; k < LibretroWrapper.w * 2 + y * LibretroWrapper.p; k++) {
                                Dst[m565] = Src[k];
                                m565++;
                            }
                        }
                        tex.LoadRawTextureData(Dst);
                        tex.filterMode = FilterMode.Trilinear;
                        tex.Apply();
                        break;
                    case PixelFormat.RetroPixelFormatUnknown:
                        Debug.LogWarning("Unknown Pixel Format!");
                        _frameBuffer = null;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            private void RetroAudioSample(short left, short right) {
                //// Unused.
                //if (initialized)
                //{
                //    float value = left * -0.000030517578125f;
                //    value = Mathf.Clamp(value, -1.0f, 1.0f); // Unity's audio only takes values between -1 and 1.
                //    AudioBatch.Add(value);

                //    value = right * -0.000030517578125f;
                //    value = Mathf.Clamp(value, -1.0f, 1.0f); // Unity's audio only takes values between -1 and 1.
                //    AudioBatch.Add(value);
                //}
            }

            bool prepAudio = false;
            int tillReadAudio = 0;
            private unsafe void RetroAudioSampleBatch(short* data, uint frames) {
                if (initialized)
                {
                    if (!prepAudio)
                    {
                        for (int i = 0; i < frames * 2 && !prepAudio; ++i)
                        {
                            if (data[i] * 0.000030517578125f >= 0.05) //wait till populated valid data is sent through
                            {
                                prepAudio = true;
                                _speaker.startAudio();
                            }
                        }
                    }
                    if (prepAudio)
                    {
                        if (tillReadAudio <= 0)
                        {
                            for (int i = 0; i < frames * 2; ++i)
                            {
                                float value = data[i] * 0.000030517578125f;
                                value = Mathf.Clamp(value, -1.0f, 1.0f); // Unity's audio only takes values between -1 and 1.
                                AudioBatch.Add(value);
                            }
                        }
                        else
                            tillReadAudio--;
                    }
                }
            }


            public CoreOptions coreOptions;
            private CoreOptionsList _coreOptionsList;
            public retro_memory_descriptor[] descriptors;
            private int rotation;

            private const uint SUBSYSTEM_MAX_SUBSYSTEMS = 20;
            private const uint SUBSYSTEM_MAX_SUBSYSTEM_ROMS = 10;

            private readonly retro_subsystem_info[] subsystem_data = new retro_subsystem_info[SUBSYSTEM_MAX_SUBSYSTEMS];
            private readonly unsafe retro_subsystem_rom_info*[] subsystem_data_roms = new retro_subsystem_rom_info*[SUBSYSTEM_MAX_SUBSYSTEMS];
            private uint subsystem_current_count;

            private uint lastCommand = 0;
            private unsafe bool RetroEnvironment(uint cmd, void* data) {
                int hey = 0;
                lastCommand = cmd;
                switch (cmd) {
                    case Environment.RetroEnvironmentGetOverscan:
                        bool* outOverscan = (bool*)data;
                        *outOverscan = false;
                        break;
                    case Environment.RetroEnvironmentGetCanDupe:
                        bool* outCanDupe = (bool*)data;
                        *outCanDupe = true;
                        break;
                    //case Environment.RetroEnvironmentSetMessage:
                    //    break;
                    //case Environment.RetroEnvironmentSetRotation:
                    //    break;
                    //case Environment.RetroEnvironmentShutdown:
                    //    break;
                    //case Environment.RetroEnvironmentSetPerformanceLevel:
                    //    break;
                    case Environment.RetroEnvironmentGetSystemDirectory:
                        char** array = (char**)data;
                        string systemDirectory = Application.streamingAssetsPath + "/" + "System";
                        *array = StringToChar(systemDirectory);
                        //Debug.Log("Polled system directory");
                        break;
                    case Environment.RetroEnvironmentSetPixelFormat:
                        _pixelFormat = *(PixelFormat*) data;
                        switch (_pixelFormat) {
                            case PixelFormat.RetroPixelFormat_0RGB1555:
                                break;
                            case PixelFormat.RetroPixelFormatRGB565:
                                break;
                            case PixelFormat.RetroPixelFormatXRGB8888:
                                break;
                            case PixelFormat.RetroPixelFormatUnknown:
                                break;
                        }
                        break;
                    //case Environment.RetroEnvironmentSetKeyboardCallback:
                    //    break;
                    case Environment.RETRO_ENVIRONMENT_GET_VARIABLE:
                        //retro_variable v = *(retro_variable*)data;
                        //string keyName = Marshal.PtrToStringAnsi((IntPtr)v.key);
                        //Debug.Log("cmd 15 Asking for variable: " + keyName);
                        retro_variable* outVariable = (retro_variable*)data;

                        string key = Marshal.PtrToStringAnsi((IntPtr)outVariable->key);
                        if (coreOptions != null)
                        {
                            string coreOption = coreOptions.Options.Find(x => x.StartsWith(key, StringComparison.OrdinalIgnoreCase));
                            if (coreOption != null)
                            {
                                if (key == "snes9x_audio_interpolation") //weird hack that fixes pitchy base noise in SNES super mario world?
                                    return false;
                                outVariable->value = StringToChar(coreOption.Split(';')[1]);
                            }
                            else
                            {
                                Debug.LogWarning($"Core option {key} not found!");
                                return false;
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"Core didn't set its options for key '{key}'.");
                            return false;
                        }
                        break;
                    case Environment.RETRO_ENVIRONMENT_GET_VARIABLE_UPDATE:
                        *(bool*)data = false; //say there has been no variable updates
                        break;
                    case Environment.RETRO_ENVIRONMENT_GET_CORE_ASSETS_DIRECTORY:
                        char** assetsArray = (char**)data;
                        string sysDir = Application.streamingAssetsPath + "/" + "System";
                        *assetsArray = StringToChar(sysDir);
                        break;
                    case Environment.RETRO_ENVIRONMENT_GET_SAVE_DIRECTORY:
                        char** saveStr = (char**)data;
                        string savDir = Application.streamingAssetsPath + "/" + "SaveDirectory";
                        *saveStr = StringToChar(savDir);
                        break;
                    case Environment.RETRO_ENVIRONMENT_GET_AUDIO_VIDEO_ENABLE:
                        int result = 0;
                        result |= 1; // if video enabled
                        result |= 2; // if audio enabled

                        int* outAudioVideoEnabled = (int*)data;
                        *outAudioVideoEnabled = result;
                        break;
                    case Environment.RETRO_ENVIRONMENT_GET_CORE_OPTIONS_VERSION:
                        uint* outVersion = (uint*)data;
                        *outVersion = Environment.RETRO_API_VERSION;
                        break;

                    //considered "front-end" from here on out

                    case Environment.RetroEnvironmentSetRotation:
                        // TODO: Rotate screen (counter-clockwise)
                        // Values: 0,  1,   2,   3
                        // Result: 0, 90, 180, 270 degrees
                        uint* inRotation = (uint*)data;
                        rotation = (int)*inRotation;
                        break;
                    case Environment.RetroEnvironmentSetPerformanceLevel:
                        break;
                    case Environment.RetroEnvironmentSetInputDescriptors:
                        break;
                    case Environment.RETRO_ENVIRONMENT_SET_VARIABLES:
                        try
                        {
                            retro_variable* inVariable = (retro_variable*)data;

                            coreOptions = _coreOptionsList.Cores.Find(x => x.CoreName.Equals(coreName, StringComparison.OrdinalIgnoreCase));
                            if (coreOptions == null)
                            {
                                coreOptions = new CoreOptions { CoreName = coreName };
                                _coreOptionsList.Cores.Add(coreOptions);
                            }

                            while (inVariable->key != null)
                            {
                                string inKey = Marshal.PtrToStringAnsi((IntPtr)inVariable->key);
                                string coreOption = coreOptions.Options.Find(x => x.StartsWith(inKey, StringComparison.OrdinalIgnoreCase));
                                if (coreOption == null)
                                {
                                    string inValue = Marshal.PtrToStringAnsi((IntPtr)inVariable->value);
                                    string[] descriptionAndValues = inValue.Split(';');
                                    string[] possibleValues = descriptionAndValues[1].Trim().Split('|');
                                    string defaultValue = possibleValues[0];
                                    string value = defaultValue;
                                    coreOption = $"{inKey};{value};{string.Join("|", possibleValues)};";
                                    coreOptions.Options.Add(coreOption);
                                }
                                ++inVariable;
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogError(e);
                        }

                        SaveCoreOptionsFile();
                        break;
                    case Environment.RETRO_ENVIRONMENT_SET_SUPPORT_NO_GAME:
                        *(bool*)data = false;
                        break;
                    case Environment.RETRO_ENVIRONMENT_SET_FRAME_TIME_CALLBACK:
                        
                        break;
                    case Environment.RETRO_ENVIRONMENT_SET_SYSTEM_AV_INFO:
                        SystemAVInfo* inSystemAVnfo = (SystemAVInfo*)data;
                        _av = *inSystemAVnfo;
                        break;
                    case Environment.RETRO_ENVIRONMENT_SET_SUBSYSTEM_INFO:
                        //retro_subsystem_info* subsytemInfo = (retro_subsystem_info*)data;
                        ////Debug.Log("<color=yellow>Subsystem Info:</color>");
                        ////Debug.Log($"<color=yellow>Description:</color> {Marshal.PtrToStringAnsisubsytemInfo->desc)}");
                        ////Debug.Log($"<color=yellow>Ident:</color> {Marshal.PtrToStringAnsisubsytemInfo->ident)}");
                        //_game_type = subsytemInfo->id;
                        //_num_info = subsytemInfo->num_roms;
                        //while (subsytemInfo->roms != null)
                        //{
                        //    RetroSubsystemRomInfo* romInfo = subsytemInfo->roms;
                        //    //Debug.Log("<color=orange>Rom Info:</color>");
                        //    //Debug.Log($"<color=orange>Description:</color> {Marshal.PtrToStringAnsiromInfo->desc)}");
                        //    //Debug.Log($"<color=orange>Extensions:</color> {Marshal.PtrToStringAnsiromInfo->valid_extensions)}");
                        //    subsytemInfo++;
                        //}

                        retro_subsystem_info* inSubsytemInfo = (retro_subsystem_info*)data;
                        // settings_t* settings = configuration_settings;
                        // unsigned log_level = settings->uints.frontend_log_level;

                        subsystem_current_count = 0;

                        uint size = 0;

                        uint i = 0;
                        while (inSubsytemInfo[i].ident != null)
                        {
                            string subsystemDesc = CharsToString(inSubsytemInfo[i].desc);
                            string subsystemIdent = CharsToString(inSubsytemInfo[i].ident);
                            uint subsystemId = inSubsytemInfo[i].id;
                            for (uint j = 0; j < inSubsytemInfo[i].num_roms; j++)
                            {
                                string romDesc = CharsToString(inSubsytemInfo[i].roms[j].desc);
                                string required = inSubsytemInfo[i].roms[j].required ? "required" : "optional";
                            }
                            i++;
                        }

                        //if (log_level == RETRO_LOG_DEBUG)
                        size = i;

                        //if (log_level == RETRO_LOG_DEBUG)
                        if (size > SUBSYSTEM_MAX_SUBSYSTEMS)
                        {
                        }

                        if (subsystem_data != null)
                        {
                            for (uint k = 0; k < size && k < SUBSYSTEM_MAX_SUBSYSTEMS; k++)
                            {
                                ref retro_subsystem_info subdata = ref subsystem_data[k];

                                subdata.desc = inSubsytemInfo[k].desc;
                                subdata.ident = inSubsytemInfo[k].ident;
                                subdata.id = inSubsytemInfo[k].id;
                                subdata.num_roms = inSubsytemInfo[k].num_roms;

                                //if (log_level == RETRO_LOG_DEBUG)
                                if (subdata.num_roms > SUBSYSTEM_MAX_SUBSYSTEM_ROMS)
                                {
                                }

                                for (uint j = 0; j < subdata.num_roms && j < SUBSYSTEM_MAX_SUBSYSTEM_ROMS; j++)
                                {
                                    while (subdata.roms != null)
                                    {
                                        retro_subsystem_rom_info* romInfo = subdata.roms;
                                        romInfo->desc = inSubsytemInfo[k].roms[j].desc;
                                        romInfo->valid_extensions = inSubsytemInfo[k].roms[j].valid_extensions;
                                        romInfo->required = inSubsytemInfo[k].roms[j].required;
                                        romInfo->block_extract = inSubsytemInfo[k].roms[j].block_extract;
                                        romInfo->need_fullpath = inSubsytemInfo[k].roms[j].need_fullpath;
                                        subdata.roms++;
                                    }
                                }

                                subdata.roms = subsystem_data_roms[k];
                            }

                            subsystem_current_count = (size <= SUBSYSTEM_MAX_SUBSYSTEMS) ? size : SUBSYSTEM_MAX_SUBSYSTEMS;
                        }
                        //return false; //TODO: Remove when implemented!
                    break;
                    case Environment.RETRO_ENVIRONMENT_SET_MEMORY_MAPS:
                        retro_memory_map* map = (retro_memory_map*)data;
                        descriptors = new retro_memory_descriptor[map->num_descriptors];
                        for (uint j = 0; j < map->num_descriptors; j++)
                        {
                            descriptors[j].flags = map->descriptors[j].flags;
                            descriptors[j].ptr = map->descriptors[j].ptr;
                            descriptors[j].offset = map->descriptors[j].offset;
                            descriptors[j].start = map->descriptors[j].start;
                            descriptors[j].select = map->descriptors[j].select;
                            descriptors[j].disconnect = map->descriptors[j].disconnect;
                            descriptors[j].len = map->descriptors[j].len;
                            descriptors[j].addrspace = map->descriptors[j].addrspace;
                            //Debug.Log("Descriptor " + j + "= " + descriptors[j].start.ToString("X") + ", Length = " + descriptors[j].len.ToString("X"));
                        }
                        break;
                    case Environment.RETRO_ENVIRONMENT_SET_GEOMETRY:
                        if (initialized)
                        {
                            Geometry* inGeometry = (Geometry*)data;
                            if (_av.geometry.base_width != inGeometry->base_width
                            || _av.geometry.base_height != inGeometry->base_height
                            || _av.geometry.aspect_ratio != inGeometry->aspect_ratio)
                            {
                                _av.geometry = *inGeometry;
                                // TODO: Set video aspect ratio
                            }
                        }
                        break;
                    case Environment.RETRO_ENVIRONMENT_SET_CORE_OPTIONS_INTL:
                        retro_core_options_intl inOptionsIntl = Marshal.PtrToStructure<retro_core_options_intl>((IntPtr)data);

                        coreOptions = _coreOptionsList.Cores.Find(x => x.CoreName.Equals(coreName, StringComparison.OrdinalIgnoreCase));
                        if (coreOptions == null)
                        {
                            coreOptions = new CoreOptions { CoreName = coreName };
                            _coreOptionsList.Cores.Add(coreOptions);
                        }

                        for (int l = 0; l < RETRO_NUM_CORE_OPTION_VALUES_MAX; l++)
                        {
                            IntPtr ins = new IntPtr(inOptionsIntl.us.ToInt64() + l * Marshal.SizeOf<retro_core_option_definition>());
                            retro_core_option_definition defs = Marshal.PtrToStructure<retro_core_option_definition>(ins);
                            if (defs.key == null)
                            {
                                break;
                            }

                            string bkey = Marshal.PtrToStringAnsi((IntPtr)defs.key);

                            string coreOption = coreOptions.Options.Find(x => x.StartsWith(bkey, StringComparison.OrdinalIgnoreCase));
                            if (coreOption == null)
                            {
                                string defaultValue = CharsToString(defs.default_value);

                                List<string> possibleValues = new List<string>();
                                for (int j = 0; j < defs.values.Length; j++)
                                {
                                    retro_core_option_value val = defs.values[j];
                                    if (val.value != null)
                                    {
                                        possibleValues.Add(CharsToString(val.value));
                                    }
                                }

                                string value = string.Empty;
                                if (!string.IsNullOrEmpty(defaultValue))
                                {
                                    value = defaultValue;
                                }
                                else if (possibleValues.Count > 0)
                                {
                                    value = possibleValues[0];
                                }

                                coreOption = $"{bkey};{value};{string.Join("|", possibleValues)}";

                                coreOptions.Options.Add(coreOption);
                            }
                        }

                        SaveCoreOptionsFile();
                        break;
                    default:
                        return false;
                }
                return true;
            }

            private string CoreOptionsFile = Application.streamingAssetsPath + "/Options.txt";
            private void LoadCoreOptionsFile()
            {
                _coreOptionsList = DeserializeFromJson<CoreOptionsList>(CoreOptionsFile);
                if (_coreOptionsList == null) {_coreOptionsList = new CoreOptionsList();}
            }

            private void SaveCoreOptionsFile()
            {
                for (int i = 0; i < _coreOptionsList.Cores.Count; i++)
                {
                    _coreOptionsList.Cores[i].Options.Sort();
                }
                _ = SerializeToJson(_coreOptionsList, CoreOptionsFile);
            }

            public static bool SerializeToJson<T>(T sourceObject, string targetPath)
            {
                bool result = false;

                try{
                    string jsonString = UnityEngine.JsonUtility.ToJson(sourceObject, true);
                    File.WriteAllText(targetPath, jsonString);
                    result = true;
                }
                catch (Exception e){Debug.LogError(e);}
                return result;
            }

            public static T DeserializeFromJson<T>(string sourcePath) where T : class
            {
                T result = null;
                try{
                    string jsonString = File.ReadAllText(sourcePath);
                    result = UnityEngine.JsonUtility.FromJson<T>(jsonString);
                }
                catch (Exception e){Debug.LogError(e);}
                return result;
            }

            private static unsafe char* StringToChar(string s) {
                IntPtr p = Marshal.StringToHGlobalUni(s);
                return (char*) p.ToPointer();
            }

            private static unsafe string CharsToString(char* value)
            {
                return Marshal.PtrToStringAnsi((IntPtr)value);
            }

            private unsafe GameInfo LoadGameInfo(string file) {
                var gameInfo = new GameInfo();

                var stream = new FileStream(file, FileMode.Open);

                var data = new byte[stream.Length];
                stream.Read(data, 0, (int) stream.Length);
                IntPtr arrayPointer = Marshal.AllocHGlobal(data.Length*Marshal.SizeOf(typeof (byte)));
                Marshal.Copy(data, 0, arrayPointer, data.Length);


                gameInfo.path = StringToChar(file);
                gameInfo.size = (uint) data.Length;
                gameInfo.data = arrayPointer.ToPointer();

                stream.Close();

                return gameInfo;
            }

            public bool LoadGame(string gamePath) {
                GameInfo gameInfo = LoadGameInfo(gamePath);
                bool ret = Libretro.RetroLoadGame(ref gameInfo);

                Console.WriteLine("\nSystem information:");

                _av = new SystemAVInfo();
                Libretro.RetroGetSystemAVInfo(ref _av);

                var audioConfig = AudioSettings.GetConfiguration();
                audioConfig.sampleRate = (int)_av.timing.sample_rate;
                AudioSettings.Reset(audioConfig);

                Debug.Log("Geometry:");
                Debug.Log("Base width: " + _av.geometry.base_width);
                Debug.Log("Base height: " + _av.geometry.base_height);
                Debug.Log("Max width: " + _av.geometry.max_width);
                Debug.Log("Max height: " + _av.geometry.max_height);
                Debug.Log("Aspect ratio: " + _av.geometry.aspect_ratio);
                Debug.Log("Geometry:");
                Debug.Log("Target fps: " + _av.timing.fps);
                Debug.Log("Sample rate " + _av.timing.sample_rate);

                return ret;
            }

            public void SaveRAM(string path)
            {
                unsafe
                {
                    int size = Libretro.RetroGetMemorySize(0); //maybe look into getting the defined constant RETRO_MEMORY_SAVE_RAM
                    void* data = Libretro.RetroGetMemoryData(0);

                    byte[] saveData = new byte[size];

                    for (int i = 0; i < size; i++)
                        saveData[i] = ((byte*)data)[i];

                    System.IO.File.WriteAllBytes(path, saveData);
                }
            }

            public void LoadRAM(string path)
            {
                unsafe
                {
                    int size = Libretro.RetroGetMemorySize(0); //maybe look into getting the defined constant RETRO_MEMORY_SAVE_RAM
                    void* data = Libretro.RetroGetMemoryData(0);

                    if (File.Exists(path))
                    {
                        byte[] savedData = System.IO.File.ReadAllBytes(path);

                        if (savedData.Length == size)
                        {
                            for (int i = 0; i < size; i++)
                                ((byte*)data)[i] = savedData[i];
                        }
                    }
                }
            }

            public void SaveState(string path)
            {
                unsafe
                {
                    int size = Libretro.RetroSerializeSize();
                    byte[] toSaveData = new byte[size];
                    fixed (byte* p = &toSaveData[0])
                    {
                        Libretro.RetroSerialize(p, size);
                        System.IO.File.WriteAllBytes(path, toSaveData);
                    }
                }
            }

            public void LoadState(string path)
            {
                unsafe
                {
                    if (File.Exists(path))
                    {
                        int size = Libretro.RetroSerializeSize();
                        byte[] toLoad = System.IO.File.ReadAllBytes(path);
                        fixed (byte* p = &toLoad[0])
                        {
                            Libretro.RetroDeserialize(p, size);
                        }
                    }
                }
            }

        }

        public unsafe class Libretro {
            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            public delegate int RetroApiVersionDelegate();

            public static RetroApiVersionDelegate RetroApiVersion;

            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            public delegate void RetroInitDelegate();

            public static RetroInitDelegate RetroInit;

            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            public delegate void RetroGetSystemInfoDelegate(ref SystemInfo info);

            public static RetroGetSystemInfoDelegate RetroGetSystemInfo;

            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            public delegate void RetroGetSystemAVInfoDelegate(ref SystemAVInfo info);

            public static RetroGetSystemAVInfoDelegate RetroGetSystemAVInfo;

            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            public delegate bool RetroLoadGameDelegate(ref GameInfo game);

            public static RetroLoadGameDelegate RetroLoadGame;

            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            public delegate void RetroSetVideoRefreshDelegate(RetroVideoRefreshDelegate r);

            public static RetroSetVideoRefreshDelegate RetroSetVideoRefresh;

            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            public delegate void RetroSetAudioSampleDelegate(RetroAudioSampleDelegate r);

            public static RetroSetAudioSampleDelegate RetroSetAudioSample;

            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            public delegate void RetroSetAudioSampleBatchDelegate(RetroAudioSampleBatchDelegate r);

            public static RetroSetAudioSampleBatchDelegate RetroSetAudioSampleBatch;

            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            public delegate void RetroSetInputPollDelegate(RetroInputPollDelegate r);

            public static RetroSetInputPollDelegate RetroSetInputPoll;

            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            public delegate void RetroSetInputStateDelegate(RetroInputStateDelegate r);

            public static RetroSetInputStateDelegate RetroSetInputState;

            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            public delegate bool RetroSetEnvironmentDelegate(RetroEnvironmentDelegate r);
            public static RetroSetEnvironmentDelegate RetroSetEnvironment;
            //typedef bool (*retro_environment_t)(unsigned cmd, void *data);
            public delegate bool RetroEnvironmentDelegate(uint cmd, void* data);

            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            public delegate void RetroRunDelegate();

            public static RetroRunDelegate RetroRun;

            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            public delegate void RetroDeInitDelegate();

            public static RetroDeInitDelegate RetroDeInit;

            //typedef void (*retro_video_refresh_t)(const void *data, unsigned width, unsigned height, size_t pitch);
            public delegate void RetroVideoRefreshDelegate(void* data, uint width, uint height, uint pitch);

            //typedef void (*retro_audio_sample_t)(int16_t left, int16_t right);
            public delegate void RetroAudioSampleDelegate(short left, short right);

            //typedef size_t (*retro_audio_sample_batch_t)(const int16_t *data, size_t frames);
            public delegate void RetroAudioSampleBatchDelegate(short* data, uint frames);

            //typedef void (*retro_input_poll_t)(void);
            public delegate void RetroInputPollDelegate();

            //typedef int16_t (*retro_input_state_t)(unsigned port, unsigned device, unsigned index, unsigned id);
            public delegate short RetroInputStateDelegate(uint port, uint device, uint index, uint id);

            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            public delegate void* RetroGetMemoryDataDelegate(uint id);
            public static RetroGetMemoryDataDelegate RetroGetMemoryData;

            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            public delegate int RetroGetMemorySizeDelegate(uint id);
            public static RetroGetMemorySizeDelegate RetroGetMemorySize;

            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            public delegate int RetroSerializeSizeDeleagte();
            public static RetroSerializeSizeDeleagte RetroSerializeSize;

            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            public delegate bool RetroSerializeDelegate(void* data, int size);
            public static RetroSerializeDelegate RetroSerialize;

            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            public delegate bool RetroDeserializeDelegate(void* data, int size);
            public static RetroDeserializeDelegate RetroDeserialize;


            public static void InitializeLibrary(string dllName) {
                IDLLHandler dllHandler = null;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
                dllHandler = WindowsDLLHandler.Instance;
#elif UNITY_ANDROID
            dllHandler = AndroidDLLHandler.Instance;
#endif
                if (dllHandler == null) return;
                string path = Application.streamingAssetsPath + "/" + "libwinpthread-1.dll";
                if (File.Exists(path))
                    dllHandler.LoadCore(path);

                dllHandler.LoadCore(dllName);

                RetroApiVersion = dllHandler.GetMethod<RetroApiVersionDelegate>("retro_api_version");
                RetroInit = dllHandler.GetMethod<RetroInitDelegate>("retro_init");
                RetroGetSystemInfo = dllHandler.GetMethod<RetroGetSystemInfoDelegate>("retro_get_system_info");
                RetroGetSystemAVInfo = dllHandler.GetMethod<RetroGetSystemAVInfoDelegate>("retro_get_system_av_info");
                RetroLoadGame = dllHandler.GetMethod<RetroLoadGameDelegate>("retro_load_game");
                RetroSetVideoRefresh = dllHandler.GetMethod<RetroSetVideoRefreshDelegate>("retro_set_video_refresh");
                RetroSetAudioSample = dllHandler.GetMethod<RetroSetAudioSampleDelegate>("retro_set_audio_sample");
                RetroSetAudioSampleBatch = dllHandler.GetMethod<RetroSetAudioSampleBatchDelegate>("retro_set_audio_sample_batch");
                RetroSetInputPoll = dllHandler.GetMethod<RetroSetInputPollDelegate>("retro_set_input_poll");
                RetroSetInputState = dllHandler.GetMethod<RetroSetInputStateDelegate>("retro_set_input_state");
                RetroSetEnvironment = dllHandler.GetMethod<RetroSetEnvironmentDelegate>("retro_set_environment");
                RetroRun = dllHandler.GetMethod<RetroRunDelegate>("retro_run");
                RetroDeInit = dllHandler.GetMethod<RetroDeInitDelegate>("retro_deinit");

                RetroGetMemoryData = dllHandler.GetMethod<RetroGetMemoryDataDelegate>("retro_get_memory_data");
                RetroGetMemorySize = dllHandler.GetMethod<RetroGetMemorySizeDelegate>("retro_get_memory_size");

                RetroSerializeSize = dllHandler.GetMethod<RetroSerializeSizeDeleagte>("retro_serialize_size");
                RetroSerialize = dllHandler.GetMethod<RetroSerializeDelegate>("retro_serialize");
                RetroDeserialize = dllHandler.GetMethod<RetroDeserializeDelegate>("retro_unserialize");
                //Debug.Log("Got emthods");
            }
        }
    }
}
