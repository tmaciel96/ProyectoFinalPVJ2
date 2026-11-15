// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// Billboard: el objeto siempre mira a la cámara y, opcionalmente, sube y baja. Lo usan los
// indicadores "!" y "?" sobre la cabeza de los NPCs (los arma Sepulturero/Crear prefabs de NPCs).
// =================================================================================================

using UnityEngine;

public class Billboard : MonoBehaviour
{
    [Tooltip("Cuánto sube y baja (0 = quieto)")]
    public float bobAmplitude = 0.08f;
    public float bobFrequency = 2.5f;

    private Transform cam;
    private Vector3 baseLocalPosition;

    void Awake()
    {
        baseLocalPosition = transform.localPosition;
    }

    void LateUpdate()
    {
        if (cam == null)
        {
            if (Camera.main == null) return;
            cam = Camera.main.transform;
        }
        transform.rotation = cam.rotation;
        if (bobAmplitude > 0f)
            transform.localPosition = baseLocalPosition + Vector3.up * (Mathf.Sin(Time.time * bobFrequency) * bobAmplitude);
    }
}
