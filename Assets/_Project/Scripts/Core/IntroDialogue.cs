// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
// Autora: Tamara Maciel
//
// Diálogo introductorio al empezar el nivel. Ahora es un cliente de DialogueSystem (M2-2):
// pone al GameManager en Intro, reproduce las líneas y al terminar pasa a Jugando (M2-7).
// Si la escena todavía no tiene un DialogueSystem propio, lo agrega acá y le pasa las referencias
// del Panel_Dialogue que ya estaban cargadas en este componente (no hace falta tocar la escena).
// Si introData tiene un DialogueData asignado (Data/Dialogos/Dialogo_Intro), usa ese en vez de las líneas de abajo (M2-9).
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
        new DialogueLine { speaker = "Sepulturero", message = "Listo, la última tumba barrida. Ahora a casa, que me espera un mate bien caliente..." },
        new DialogueLine { speaker = "Sepulturero", message = "¿Eh? ¿Quién cerró el portón? ¡Y esta niebla que brilla...!" },
        new DialogueLine { speaker = "Espíritu del Portón", message = "Tranquilo, sepulturero. Esta noche de luna llena el cementerio se selló con una magia antigua." },
        new DialogueLine { speaker = "Espíritu del Portón", message = "Mirá el portón: tiene cinco runas apagadas. Cada una despierta cuando ayudás a un alma que no puede descansar." },
        new DialogueLine { speaker = "Espíritu del Portón", message = "La Bruja, el Guardián Esqueleto, el Vampiro, el Niño Fantasma y el Gato Negro te esperan, cada uno en su sector." },
        new DialogueLine { speaker = "Espíritu del Portón", message = "Con cada favor se enciende una runa y se abre la reja que lleva al sector siguiente. Con las cinco, el portón te deja salir." },
        new DialogueLine { speaker = "Sepulturero", message = "Bueno... un turno extra no mata a nadie. Bah, acá adentro, digamos." },
        new DialogueLine { speaker = "Espíritu del Portón", message = "Hablá con [E] o Espacio. Tu pala (clic izquierdo o J) y tus piedras (clic derecho o K) te van a cuidar de lo que ronda en la niebla. Suerte." }
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
