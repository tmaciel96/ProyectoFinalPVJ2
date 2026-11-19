// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// DialogueSystem (M2-2): muestra cualquier diálogo en el Panel_Dialogue con efecto máquina de escribir.
//   DialogueSystem.Instance.Play(data, alTerminar);   // con un DialogueData
//   DialogueSystem.Instance.Play(lineas, alTerminar); // con líneas sueltas
// Continuar, Espacio, E o clic completan la escritura y después avanzan a la próxima línea.
// Mientras está abierto, BlocksGameplay es true: el jugador no camina, no ataca y no interactúa.
//
// Si la escena no tiene un DialogueSystem, IntroDialogue lo agrega y le pasa las referencias del panel
// (así no hay que tocar 01_GameLevel por ahora).
// =================================================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class DialogueSystem : MonoBehaviour
{
    public static DialogueSystem Instance { get; private set; }

    /// <summary>Se abrió un diálogo.</summary>
    public static event Action OnDialogueStarted;
    /// <summary>Se cerró un diálogo.</summary>
    public static event Action OnDialogueEnded;

    /// <summary>True mientras hay un diálogo abierto y en el frame en que se cerró.</summary>
    public static bool BlocksGameplay =>
        Instance != null && (Instance.IsOpen || Time.frameCount == Instance.closedFrame);

    [Header("Referencias de UI")]
    public GameObject dialoguePanel;
    public TextMeshProUGUI speakerText;
    public TextMeshProUGUI messageText;
    public Button nextButton;

    [Header("Efecto de escritura")]
    public float typingSpeed = 0.025f;
    public AudioSource voiceBeepAudio;

    public bool IsOpen { get; private set; }

    private readonly List<DialogueLine> lines = new List<DialogueLine>();
    private string defaultSpeaker;
    private Action onFinished;
    private int index;
    private bool isTyping;
    private Coroutine typingRoutine;
    private int openedFrame = -1;
    private int closedFrame = -1;
    private Button hookedButton;
    private int lastAdvanceFrame = -1;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (hookedButton != null) hookedButton.onClick.RemoveListener(Next);
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        HookButtons();
        if (!IsOpen && dialoguePanel != null) dialoguePanel.SetActive(false);
    }

    /// <summary>Para cuando el sistema se crea por código (IntroDialogue le pasa su panel).</summary>
    public void Configure(GameObject panel, TextMeshProUGUI speaker, TextMeshProUGUI message,
                          Button next, AudioSource beep, float speed)
    {
        dialoguePanel = panel;
        speakerText = speaker;
        messageText = message;
        nextButton = next;
        voiceBeepAudio = beep;
        typingSpeed = speed;
        HookButtons();
    }

    private void HookButtons()
    {
        if (hookedButton == nextButton) return;
        if (hookedButton != null) hookedButton.onClick.RemoveListener(Next);
        hookedButton = nextButton;
        if (hookedButton != null) hookedButton.onClick.AddListener(Next);
    }

    // ---------------------------------------------------------------- API pública

    public void Play(DialogueData data, Action finished = null)
    {
        if (data == null) { finished?.Invoke(); return; }
        Play(data.lines, finished, data.defaultSpeaker);
    }

    public void Play(IEnumerable<DialogueLine> newLines, Action finished = null, string fallbackSpeaker = null)
    {
        if (IsOpen) End(); // un diálogo nuevo reemplaza al anterior

        lines.Clear();
        if (newLines != null) lines.AddRange(newLines);
        if (lines.Count == 0) { finished?.Invoke(); return; }

        defaultSpeaker = fallbackSpeaker;
        onFinished = finished;
        index = 0;
        lastAdvanceFrame = -1;
        IsOpen = true;
        openedFrame = Time.frameCount;

        if (dialoguePanel != null) dialoguePanel.SetActive(true);
        OnDialogueStarted?.Invoke();
        ShowLine();
    }

    /// <summary>Completa la línea si se está escribiendo; si no, pasa a la siguiente.</summary>
    public void Next()
    {
        if (!IsOpen || lastAdvanceFrame == Time.frameCount) return;
        lastAdvanceFrame = Time.frameCount;

        if (isTyping)
        {
            if (typingRoutine != null) StopCoroutine(typingRoutine);
            isTyping = false;
            if (messageText != null) messageText.maxVisibleCharacters = int.MaxValue;
            return;
        }

        index++;
        if (index >= lines.Count) End();
        else ShowLine();
    }

    /// <summary>Cierra el diálogo y llama al callback de Play.</summary>
    public void End()
    {
        if (!IsOpen) return;

        if (typingRoutine != null) StopCoroutine(typingRoutine);
        isTyping = false;
        IsOpen = false;
        closedFrame = Time.frameCount;
        if (dialoguePanel != null) dialoguePanel.SetActive(false);

        Action callback = onFinished;
        onFinished = null;
        OnDialogueEnded?.Invoke();
        callback?.Invoke();
    }

    // ---------------------------------------------------------------- Interno

    void Update()
    {
        // La tecla que abrió el diálogo (por ejemplo la E sobre un NPC) no lo avanza en el mismo frame
        if (GameOptionsMenu.BlocksInput || !IsOpen || Time.frameCount == openedFrame) return;

        // Interact (E, Espacio, A) o Attack (clic, J, X) del PlayerInputReader. Sin lector, el teclado directo.
        bool advance;
        PlayerInputReader input = PlayerInputReader.Instance;
        if (input != null)
        {
            advance = input.InteractPressed
                      || (input.AttackPressed && !(input.AttackFromPointer && PointerOverButton()));
        }
        else
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            advance = (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame))
                      || (mouse != null && mouse.leftButton.wasPressedThisFrame && !PointerOverButton());
        }
        if (advance) Next();
    }

    // El clic sobre Continuar ya lo maneja el botón.
    private bool PointerOverButton()
    {
        var es = UnityEngine.EventSystems.EventSystem.current;
        return es != null && es.IsPointerOverGameObject();
    }

    private void ShowLine()
    {
        DialogueLine line = lines[index];
        string speaker = string.IsNullOrEmpty(line.speaker) ? defaultSpeaker : line.speaker;
        if (speakerText != null)
            speakerText.text = speaker;
        if (dialoguePanel != null && dialoguePanel.TryGetComponent<DialoguePortraitUI>(out var portrait))
            portrait.ShowSpeaker(speaker);

        if (typingRoutine != null) StopCoroutine(typingRoutine);
        typingRoutine = StartCoroutine(TypeRoutine(line.message ?? string.Empty));
    }

    private IEnumerator TypeRoutine(string sentence)
    {
        isTyping = true;
        if (messageText != null)
        {
            messageText.text = sentence;
            messageText.maxVisibleCharacters = 0;
            messageText.ForceMeshUpdate();
        }
        int count = messageText != null ? messageText.textInfo.characterCount : sentence.Length;
        for (int i = 0; i < count; i++)
        {
            if (messageText != null) messageText.maxVisibleCharacters = i + 1;
            if (voiceBeepAudio != null && !voiceBeepAudio.isPlaying) voiceBeepAudio.Play();
            yield return new WaitForSeconds(typingSpeed);
        }
        if (messageText != null) messageText.maxVisibleCharacters = int.MaxValue;
        isTyping = false;
    }
}
