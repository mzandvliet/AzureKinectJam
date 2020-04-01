using UnityEngine;
using System.Collections;
using UnityEngine.Audio;
using UnityEngine.Video;

/*

https://forum.unity.com/threads/starting-microphone-failed-result-25-unsupported-file-or-audio-format.527070/


*/

[RequireComponent(typeof(AudioSource))]
public class MicrophoneFeed : MonoBehaviour {
    [SerializeField] private string _microphoneName = "Focusrite USB (Focusrite USB Audio)";
    [SerializeField] private ParticleSystem _voiceParticles;
    [SerializeField] private VideoPlayer _videoPlayer;

    private AudioSource source;
    private string device;

    private float[] _spectrum;

    private bool _triggered;
    private float _vocalEnergy;

    private void Awake() {
            bool micAvailable = false;
            foreach (string m in Microphone.devices) {
                Debug.Log(m);

                if (m.Equals(_microphoneName)) {
                    micAvailable = true;
                }
            }

            if (!micAvailable) {
                Debug.LogWarningFormat("Microphone {0} is not available", _microphoneName);
                return;
            }

            source = GetComponent<AudioSource>();
            source.Stop();
            source.loop = true;
            source.clip = Microphone.Start(_microphoneName, true, 1, AudioSettings.outputSampleRate);
            source.Play();

            int dspBufferSize, dspNumBuffers;
            AudioSettings.GetDSPBufferSize(out dspBufferSize, out dspNumBuffers);

            source.timeSamples = (Microphone.GetPosition(device) + AudioSettings.outputSampleRate - 3 * dspBufferSize * dspNumBuffers) % AudioSettings.outputSampleRate;

         _spectrum = new float[512];
    }

    private void Update() {
        source.GetSpectrumData(_spectrum, 0, FFTWindow.Rectangular);

        for (int i = 1; i < _spectrum.Length - 1; i++) {
            // Debug.DrawLine(new Vector3(i - 1, _spectrum[i] + 10, 0), new Vector3(i, _spectrum[i + 1] + 10, 0), Color.red);
            // Debug.DrawLine(new Vector3(i - 1, Mathf.Log(_spectrum[i - 1]) + 10, 2), new Vector3(i, Mathf.Log(_spectrum[i]) + 10, 2), Color.cyan);
            // Debug.DrawLine(new Vector3(Mathf.Log(i - 1), _spectrum[i - 1] - 10, 1), new Vector3(Mathf.Log(i), _spectrum[i] - 10, 1), Color.green);
            // Debug.DrawLine(new Vector3(Mathf.Log(i - 1), Mathf.Log(_spectrum[i - 1]), 3), new Vector3(Mathf.Log(i), Mathf.Log(_spectrum[i]), 3), Color.blue);

            Color c = i == 11 ? Color.green : Color.red;
            Debug.DrawLine(new Vector3(i * 0.1f, 0, 0), new Vector3(i * 0.1f, _spectrum[i] * 100f, 0), c);
        }

        const float noiseFloor = 0.004f;

        float sum = 0;
        for (int i = 10; i < 20; i++) {
           sum += Mathf.Clamp(_spectrum[i] - noiseFloor, 0f, 1f) / 10f;
        }

        _vocalEnergy += sum * 1f;
        _vocalEnergy = Mathf.Clamp01(_vocalEnergy - 0.1f * Time.deltaTime);

        if (!_triggered) {
            var emission = _voiceParticles.emission;
            emission.rateOverTime = _vocalEnergy * 5f;

            if (_vocalEnergy > 0.95f) {
                emission.rateOverTime = 0;
                _voiceParticles.Emit(100);
                
                StartCoroutine(WaitAndPlayVideo());

                _triggered = true;
            }
        }
        
    }

    private IEnumerator WaitAndPlayVideo() {
        yield return new WaitForSeconds(2f);
        // _videoPlayer.Play();
        _videoPlayer.playbackSpeed = 1f;
    }

    void OnGUI() {
        GUILayout.Label("Fourier energy: " + _spectrum[11]);
        GUILayout.Label("Vocal energy: " + _vocalEnergy);
    }
}
