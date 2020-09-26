using System;
using UnityEngine;

namespace RetroUnity {
    [RequireComponent(typeof(AudioSource))]
    public class Speaker : MonoBehaviour {

        private AudioSource _speaker;
        public AudioSource audioSource { get { return _speaker; } }

        //private void Update()
        //{
        //    if (!started && LibretroWrapper.Wrapper.AudioBatch.Count != 0)
        //    {
        //        startAudio();
        //    }
        //}

        private void Start()
        {
            if (_speaker == null)
                _speaker = GetComponent<AudioSource>();
        }

        public void startAudio()
        {
            if (_speaker == null) return;
            //var audioConfig = AudioSettings.GetConfiguration();
            //audioConfig.sampleRate = 32000;
            //AudioSettings.Reset(audioConfig);
            //AudioClip clip = AudioClip.Create("Libretro", LibretroWrapper.Wrapper.AudioBatchSize / 2, 2, 44100, true, OnAudioRead);
            //AudioClip clip = AudioClip.Create("Libretro", 256, 2, 32000, true);
            //_speaker.clip = clip;
            _speaker.Play();
            //_speaker.loop = true;
            //Debug.Log("Unity sample rate: " + audioConfig.sampleRate);
            //Debug.Log("Unity buffer size: " + audioConfig.dspBufferSize);
        }
        
        public void stopAudio()
        {
            if (_speaker == null) return;

            _speaker.Stop();
            LibretroWrapper.Wrapper.AudioBatch.Clear();
        }

        private int timesReset = 0;
        public const int audioResetValue = 10000;
        private void OnAudioFilterRead(float[] data, int channels) {
            // wait until enough data is available
            if (LibretroWrapper.Wrapper.AudioBatch.Count < data.Length)
                return;
            int i;
            for (i = 0; i < data.Length; i++)
                data[i] = LibretroWrapper.Wrapper.AudioBatch[i];
            // remove data from the beginning
            if (LibretroWrapper.Wrapper.AudioBatch.Count < audioResetValue + data.Length + data.Length) //if there is excess data with room to not skip
                LibretroWrapper.Wrapper.AudioBatch.RemoveRange(0, i);
            else
            {
                timesReset++;
                LibretroWrapper.Wrapper.AudioBatch.RemoveRange(0, audioResetValue); //clear out excess data, since my method method has no audio lag, but seems to provide trailing numbers
            }
        }

        private void OnGUI() {
            GUI.Label(new Rect(0f, 0f, 300f, 20f), LibretroWrapper.Wrapper.AudioBatch.Count.ToString() + " - " + timesReset);
            if (timesReset > 100){timesReset = 0;}
        }
    }
}
