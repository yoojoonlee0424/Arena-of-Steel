using UnityEngine;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance;

    public float defaultDuration = 0.3f;
    public float defaultIntensity = 0.25f;
    public float defaultSpeed = 18f;
    public float maxIntensity = 0.5f;

    private Vector3 originalLocalPos;

    private float shakeTimeRemaining;
    private float currentIntensity;
    private float currentSpeed;

    private float noiseSeedX;
    private float noiseSeedY;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        originalLocalPos = transform.localPosition;

        noiseSeedX = Random.Range(0f, 1000f);
        noiseSeedY = Random.Range(0f, 1000f);
    }

    void Update()
    {
        if (shakeTimeRemaining > 0f)
        {
            shakeTimeRemaining -= Time.deltaTime;

            float t = shakeTimeRemaining / Mathf.Max(0.0001f, defaultDuration);
            float intensity = currentIntensity * t * t;

            float x = (Mathf.PerlinNoise(noiseSeedX, Time.time * currentSpeed) - 0.5f) * 2f;
            float y = (Mathf.PerlinNoise(noiseSeedY, Time.time * currentSpeed) - 0.5f) * 2f;

            Vector3 offset = new Vector3(x, y, 0f) * intensity;
            transform.localPosition = originalLocalPos + offset;
        }
        else
        {
            transform.localPosition = Vector3.Lerp(
                transform.localPosition,
                originalLocalPos,
                Time.deltaTime * 10f
            );
        }
    }

    public void Shake()
    {
        Shake(defaultDuration, defaultIntensity, defaultSpeed);
    }


    public void Shake(float duration, float intensity, float speed)
    {
        shakeTimeRemaining = Mathf.Max(shakeTimeRemaining, duration);
        currentIntensity = Mathf.Clamp(intensity, 0f, maxIntensity);
        currentSpeed = speed;
    }
}