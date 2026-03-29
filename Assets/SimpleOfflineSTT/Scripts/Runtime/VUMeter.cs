using UnityEngine;
using UnityEngine.UI;

namespace SimpleOfflineSTT
{
    public class VUMeter : MonoBehaviour
    {
        [Header("Mic Level UI")]
        [SerializeField]
        Image _micLevelFill;

        [SerializeField]
        Color _silentColor = new Color(0.2f, 0.8f, 0.2f);

        [SerializeField]
        Color _speakingColor = new Color(1.0f, 0.6f, 0.2f);

        [SerializeField]
        float _rmsVisualMultiplier = 20.0f;

        [SerializeField]
        float _levelSmoothing = 10.0f;

        float _smoothedMicLevel = 0.0f;

        public void Reset()
        {
            _smoothedMicLevel = 0.0f;

            _micLevelFill.fillAmount = 0.0f;
        }

        public void SetRMSValue(float rms)
        {
            // Convert RMS to a usable 0-1 range
            float targetLevel = Mathf.Clamp01(rms * _rmsVisualMultiplier);

            // Smooth it so it looks nice
            _smoothedMicLevel = Mathf.Lerp(
                _smoothedMicLevel,
                targetLevel,
                Time.deltaTime * _levelSmoothing);

            // Update UI
            _micLevelFill.fillAmount = _smoothedMicLevel;
            _micLevelFill.color = Color.Lerp(
                _silentColor,
                _speakingColor,
                _smoothedMicLevel);
        }
    }
}
