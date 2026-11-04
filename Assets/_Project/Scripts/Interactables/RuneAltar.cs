// =================================================================================================
// Proyecto: The Pumpkin Ritual - La Hora de las Brujas (TP Programación 2 - UNICABA)
// Autora: Tamara Maciel
// Correo: tmaciel96@gmail.com
// Repositorio: https://github.com/tmaciel96/PumpkinPolyTP
// 
// Descripción del script:
// Altar rúnico interactuable del cementerio. Detecta la proximidad del jugador, muestra el prompt
// de interacción en el HUD ("Presiona [E] para purificar altar"), cambia sus materiales, luces y
// partículas a un estado bendito al activarse, y actualiza el contador de objetivos del GameManager.
// =================================================================================================

using UnityEngine;

public class RuneAltar : MonoBehaviour
{
    [Header("Configuración del Altar")]
    public float interactionDistance = 3f;
    public bool isActivated = false;

    [Header("Efectos Visuales de Activación")]
    public Light altarLight;
    public Color activatedLightColor = new Color(1f, 0.75f, 0.1f);
    public ParticleSystem activationVFX;
    public AudioSource activationAudio;
    public Renderer runeRenderer;
    public Material activatedMaterial;

    private Transform playerTransform;
    private PlayerController playerController;
    private bool playerInRange = false;

    void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
            playerController = player.GetComponent<PlayerController>();
        }
    }

    void Update()
    {
        if (isActivated || playerTransform == null) return;

        float distance = Vector3.Distance(transform.position, playerTransform.position);
        playerInRange = distance <= interactionDistance;

        // Comprobar interacción si el jugador está en rango
        if (playerInRange && playerController != null)
        {
            if (playerController.WasInteractPressed())
            {
                ActivateAltar();
            }
        }
    }

    public void ActivateAltar()
    {
        if (isActivated) return;
        isActivated = true;

        if (altarLight != null)
        {
            altarLight.enabled = true;
            altarLight.color = activatedLightColor;
        }

        if (runeRenderer != null && activatedMaterial != null)
        {
            runeRenderer.material = activatedMaterial;
        }

        if (activationVFX != null) activationVFX.Play();
        if (activationAudio != null) activationAudio.Play();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ActivateRune();
        }

        Debug.Log("Altar Rúnico purificado y activado.");
    }

    public bool IsPlayerInRange()
    {
        return playerInRange && !isActivated;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
    }
}

