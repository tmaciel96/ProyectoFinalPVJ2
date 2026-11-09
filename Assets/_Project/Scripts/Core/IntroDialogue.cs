// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
// Autora: Tamara Maciel
//
// Diálogo introductorio al empezar el nivel. Ahora es un cliente de DialogueSystem (M2-2):
// pone al GameManager en Intro, reproduce las líneas y al terminar pasa a Jugando (M2-7).
// Si la escena todavía no tiene un DialogueSystem propio, lo agrega acá y le pasa las referencias
// del Panel_Dialogue que ya estaban cargadas en este componente (no hace falta tocar la escena).
// Si introData tiene un DialogueData asignado, usa ese en vez de las líneas de abajo (M2-9).
// =================================================================================================

using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class IntroDialogue : MonoBehaviour
{
    [Header("Historia")]
    [Tooltip("Si está asignado, reemplaza a las líneas de abajo")]
    public DialogueData introData;

    public List<DialogueLine> dialogueLines = new List<DialogueLine>()
    {
        new DialogueLine { speaker = "Espíritu Ancestral", message = "Despierta, Guardián de la Calabaza... La maldición de la noche de brujas ha caído sobre el cementerio." },
        new DialogueLine { speaker = "Espíritu Ancestral", message = "Los fuegos fatuos y sombras rondan la niebla. Debes purificar los 3 Altares Ancestrales con tu fuego rúnico." },
        new DialogueLine { speaker = "Espíritu Ancestral", message = "Tienes hasta las 3:33 para escapar por el portal... Si el reloj marca la Hora de las Brujas, tu alma quedará atrapada para siempre." }
    };

    [Header("Referencias de UI (se pasan al DialogueSystem si la escena no tiene uno)")]
    public GameObject dialoguePanel;
    public TextMeshProUGUI speakerText;
    public TextMeshProUGUI messageText;
    public Button nextButton;
    public Button skipButton;

    [Header("Efecto de Escritura")]
    public float typingSpeed = 0.025f;
    public AudioSource voiceBeepAudio;

    void Awake()
    {
        if (DialogueSystem.Instance == null && FindFirstObjectByType<DialogueSystem>() == null)
        {
            DialogueSystem system = gameObject.AddComponent<DialogueSystem>();
            system.Configure(dialoguePanel, speakerText, messageText, nextButton, skipButton, voiceBeepAudio, typingSpeed);
        }
    }

    void Start()
    {
        if (DialogueSystem.Instance == null)
        {
            Debug.LogWarning("IntroDialogue: no hay DialogueSystem en la escena.");
            return;
        }

        if (GameManager.Instance != null) GameManager.Instance.BeginIntro();

        if (introData != null) DialogueSystem.Instance.Play(introData, OnIntroFinished);
        else DialogueSystem.Instance.Play(dialogueLines, OnIntroFinished);
    }

    private void OnIntroFinished()
    {
        if (GameManager.Instance != null) GameManager.Instance.StartPlaying();
        Debug.Log("<color=orange>Diálogo inicial terminado.</color>");
    }
}
