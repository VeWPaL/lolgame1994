using UnityEngine;

namespace Depths.Unity.Audio
{
    /// <summary>
    /// The end of the chain: sits next to the game's one AudioListener, so OnAudioFilterRead sees the
    /// whole mix (after the mixer) and runs the master compressor on it, as the JS master does.
    /// </summary>
    [RequireComponent(typeof(AudioListener))]
    public sealed class MasterBus : MonoBehaviour
    {
        Compressor _comp;

        void Awake() { _comp = new Compressor(AudioSettings.outputSampleRate); }

        // the audio thread; Compressor allocates nothing per call
        void OnAudioFilterRead(float[] data, int channels) { _comp?.Process(data, channels); }
    }
}
