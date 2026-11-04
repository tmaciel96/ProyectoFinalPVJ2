// =================================================================================================
// Proyecto: The Pumpkin Ritual - La Hora de las Brujas (TP Programación 2 - UNICABA)
// Autora: Tamara Maciel
// Correo: tmaciel96@gmail.com
// Repositorio: https://github.com/tmaciel96/PumpkinPolyTP
// 
// Descripción del script:
// Controlador del sistema de diálogo introductorio y narrativa al iniciar el nivel. Presenta
// la historia del Guardián de la Calabaza con efecto máquina de escribir (typewriter), pausa el
// temporizador de la partida durante la lectura, permite avanzar con [Espacio], [E] o clic del ratón,
// y transiciona fluidamente hacia el gameplay normal al terminar.
// =================================================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class IntroDialogue : MonoBehaviour
{
    [System.Serializable]
    public struct DialogueLine
    {
        public string speaker;
        [TextArea(2, 4)]
        public string message;
    }

    [Header("Historia / Líneas de Diálogo")]
    public List<DialogueLine> dialogueLines = new List<DialogueLine>()
    {
        new DialogueLine { speaker = "Espíritu Ancestral", message = "Despierta, Guardián de la Calabaza... La maldición de la noche de brujas ha caído sobre el cementerio." },
        new DialogueLine { speaker = "Espíritu Ancestral", message = "Los fuegos fatuos y sombras rondan la niebla. Debes purificar los 3 Altares Ancestrales con tu fuego rúnico." },
        new DialogueLine { speaker = "Espíritu Ancestral", message = "Tienes hasta las 3:33 para escapar por el portal... Si el reloj marca la Hora de las Brujas, tu alma quedará atrapada para siempre." }
    };

    [Header("Referencias de UI")]
    public GameObject dialoguePanel;
    public TextMeshProUGUI speakerText;
    public TextMeshProUGUI messageText;
    public Button nextButton;
    public Button skipButton;

    [Header("Efecto de Escritura")]
    public float typingSpeed = 0.025f;
    public AudioSource voiceBeepAudio;

    private int currentLineIndex = 0;
    private bool isTyping = false;
    private Coroutine typingCoroutine;

    void Start()
    {
        // Pausar el temporizador del juego mientras lee la historia
        if (GameManager.Instance != null)
        {
            GameManager.Instance.timerIsActive = false;
        }

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(true);
        }

        if (nextButton != null)
        {
            nextButton.onClick.AddListener(NextLine);
        }

        if (skipButton != null)
        {
            skipButton.onClick.AddListener(EndDialogue);
        }

        StartDialogue();
    }

    void Update()
    {
        // Avanzar diálogo con Barra Espaciadora o Clic
        if (dialoguePanel != null && dialoguePanel.activeSelf)
        {
            bool advance = (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame))
                        || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);

            if (advance)
            {
                NextLine();
            }
        }
    }

    public void StartDialogue()
    {
        currentLineIndex = 0;
        DisplayCurrentLine();
    }

    private void DisplayCurrentLine()
    {
        if (currentLineIndex >= dialogueLines.Count)
        {
            EndDialogue();
            return;
        }

        DialogueLine line = dialogueLines[currentLineIndex];

        if (speakerText != null)
        {
            speakerText.text = line.speaker;
        }

        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        typingCoroutine = StartCoroutine(TypeSentence(line.message));
    }

    private IEnumerator TypeSentence(string sentence)
    {
        isTyping = true;
        if (messageText != null) messageText.text = "";

        foreach (char letter in sentence.ToCharArray())
        {
            if (messageText != null) messageText.text += letter;
            if (voiceBeepAudio != null && !voiceBeepAudio.isPlaying) voiceBeepAudio.Play();
            yield return new WaitForSeconds(typingSpeed);
        }

        isTyping = false;
    }

    public void NextLine()
    {
        // Si aún está escribiendo, autocompletar la frase de inmediato
        if (isTyping)
        {
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            if (messageText != null) messageText.text = dialogueLines[currentLineIndex].message;
            isTyping = false;
            return;
        }

        currentLineIndex++;
        DisplayCurrentLine();
    }

    public void EndDialogue()
    {
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }

        // Iniciar el reloj de las 00:00 a las 3:33
        if (GameManager.Instance != null)
        {
            GameManager.Instance.timerIsActive = true;
        }

        Debug.Log("<color=orange>¡Diálogo inicial finalizado! Comienza la Hora de las Brujas.</color>");
    }
}

