// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// GameManager (M2-7): gestor principal del nivel (Singleton).
//  - Estados de la partida: Intro (habla el espíritu), Jugando y Victoria. Sin cronómetro ni derrota:
//    morir solo te devuelve al checkpoint del sector (M1-10).
//  - Runas 0/5: las cuenta el QuestManager (una por misión completada). Con las 5 se abren el portón
//    principal (QuestGate) y el portal; al cruzarlo, OnPlayerEscaped muestra la pantalla de victoria.
//  - HUD de vida, piedras, aviso de interacción y viñeta de daño; botones de la pantalla de victoria.
// =================================================================================================

using System;
using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public enum GameState { Intro, Jugando, Victoria }

    [Header("Estado de la partida")]
    [SerializeField] private GameState state = GameState.Jugando;
    public GameState State => state;
    /// <summary>Avisa los cambios de estado (HUD, música, enemigos que se calman en la intro).</summary>
    public static event Action<GameState> OnStateChanged;

    [Header("Runas (una por misión; las cuenta el QuestManager)")]
    [Tooltip("Texto \"Runas: 0 / 5\" del HUD")]
    public TextMeshProUGUI runesText;
    public ExitPortal escapePortal;

    [Header("HUD de Salud y Munición")]
    public Slider healthSlider;
    public Image healthFillImage;
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI ammoText;
    public TextMeshProUGUI interactionPrompt;

    [Header("Efecto de Daño en Pantalla (Vignette / Flash Rojo)")]
    public Image damageVignette;
    public float vignetteFadeSpeed = 3f;
    private Coroutine vignetteCoroutine;

    [Header("Pantalla de Victoria")]
    public GameObject victoryPanel;
    [Tooltip("Texto del panel de victoria (opcional): si está asignado se reemplaza por victoryMessage")]
    public TextMeshProUGUI victoryText;
    [TextArea] public string victoryMessage = "¡Las cinco runas brillan otra vez!\nLas almas del cementerio descansan: terminó el turno del sepulturero.";

    [Header("Restos del TP1 (se ocultan al empezar; se pueden borrar de la escena)")]
    [Tooltip("Texto del viejo cronómetro de las 3:33")]
    public TextMeshProUGUI timerText;
    [Tooltip("Panel de derrota por tiempo: ya no hay derrota")]
    public GameObject defeatPanel;

    private PlayerHealth playerHealth;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        Time.timeScale = 1f;

        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (defeatPanel != null) defeatPanel.SetActive(false);
        if (interactionPrompt != null) interactionPrompt.gameObject.SetActive(false);
        if (timerText != null) timerText.gameObject.SetActive(false);

        // Auto-encontrar portal si no fue asignado en el Inspector
        if (escapePortal == null)
        {
            escapePortal = FindFirstObjectByType<ExitPortal>();
        }

        // Auto-encontrar imagen Fill del slider si no fue asignada
        if (healthFillImage == null && healthSlider != null)
        {
            if (healthSlider.fillRect != null)
            {
                healthFillImage = healthSlider.fillRect.GetComponent<Image>();
            }
        }

        // CRÍTICO: La viñeta de daño NUNCA debe bloquear clics del mouse (Raycast Target = false)
        if (damageVignette != null)
        {
            damageVignette.raycastTarget = false;
            Color c = damageVignette.color;
            c.a = 0f;
            damageVignette.color = c;
            damageVignette.gameObject.SetActive(true);
        }

        // Auto-vincular botones de los paneles para que funcionen 100% garantizados
        AutoWirePanelButtons(victoryPanel);

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerHealth = playerObj.GetComponent<PlayerHealth>();
        }

        PlayerHealth.OnHealthChanged += UpdateHealthBar;
        PlayerHealth.OnPlayerDamaged += TriggerDamageVignette;
        StoneInventory.OnStonesChanged += UpdateStonesDisplay;
        PlayerInteractor.OnFocusChanged += ShowInteractionPrompt;
        QuestManager.OnQuestStateChanged += HandleQuestChanged;
        QuestManager.OnAllQuestsCompleted += HandleAllQuestsCompleted;

        UpdateRunesDisplay();

        // El inventario pudo avisar antes de que el HUD se suscribiera: pedir el valor actual
        StoneInventory stones = playerObj != null ? playerObj.GetComponent<StoneInventory>() : null;
        if (stones != null) stones.Notify();
    }

    /// <summary>
    /// Vincula automáticamente los botones Reiniciar y Menú para que respondan al clic sin fallar.
    /// </summary>
    private void AutoWirePanelButtons(GameObject panel)
    {
        if (panel == null) return;
        Button[] buttons = panel.GetComponentsInChildren<Button>(true);
        foreach (var btn in buttons)
        {
            string bName = btn.gameObject.name.ToLower();
            TextMeshProUGUI tmpText = btn.GetComponentInChildren<TextMeshProUGUI>();
            string tText = tmpText != null ? tmpText.text.ToLower() : "";

            if (bName.Contains("restart") || bName.Contains("reiniciar") || bName.Contains("reintentar") ||
                tText.Contains("reiniciar") || tText.Contains("reintentar") || tText.Contains("restart"))
            {
                btn.onClick.RemoveListener(RestartLevel);
                btn.onClick.AddListener(RestartLevel);
            }
            else if (bName.Contains("menu") || bName.Contains("salir") || bName.Contains("main") ||
                     tText.Contains("menu") || tText.Contains("menú") || tText.Contains("salir"))
            {
                btn.onClick.RemoveListener(LoadMainMenu);
                btn.onClick.AddListener(LoadMainMenu);
            }
        }
    }

    void OnDestroy()
    {
        PlayerHealth.OnHealthChanged -= UpdateHealthBar;
        PlayerHealth.OnPlayerDamaged -= TriggerDamageVignette;
        StoneInventory.OnStonesChanged -= UpdateStonesDisplay;
        PlayerInteractor.OnFocusChanged -= ShowInteractionPrompt;
        QuestManager.OnQuestStateChanged -= HandleQuestChanged;
        QuestManager.OnAllQuestsCompleted -= HandleAllQuestsCompleted;
    }

    // ---------------------------------------------------------------- Estados

    /// <summary>La llama IntroDialogue al empezar a hablar el espíritu.</summary>
    public void BeginIntro() => SetState(GameState.Intro);

    /// <summary>Fin de la intro: empieza el juego.</summary>
    public void StartPlaying()
    {
        if (state == GameState.Intro) SetState(GameState.Jugando);
    }

    private void SetState(GameState newState)
    {
        if (state == newState) return;
        state = newState;
        OnStateChanged?.Invoke(newState);
    }

    // ---------------------------------------------------------------- Runas

    private void HandleQuestChanged(QuestId id, QuestState questState) => UpdateRunesDisplay();

    private void HandleAllQuestsCompleted()
    {
        // El portón (QuestGate) y el portal escuchan al QuestManager por su cuenta; esto es por si el
        // portal estaba desactivado cuando se completó la quinta misión
        if (escapePortal == null) escapePortal = FindFirstObjectByType<ExitPortal>();
        if (escapePortal != null) escapePortal.UnlockPortal();
        Debug.Log("<color=yellow>Las 5 runas están encendidas: el portón principal se abrió.</color>");
    }

    private void UpdateRunesDisplay()
    {
        if (runesText != null)
        {
            runesText.text = $"Runas: {QuestManager.CompletedCount} / {QuestManager.QuestCount}";
        }
    }

    private void UpdateHealthBar(int current, int max)
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = max;
            healthSlider.value = current;
        }

        float pct = max > 0 ? (float)current / max : 0f;

        if (healthFillImage != null)
        {
            if (pct > 0.5f)
            {
                healthFillImage.color = new Color(0.2f, 0.85f, 0.2f);
            }
            else if (pct > 0.25f)
            {
                healthFillImage.color = new Color(1f, 0.75f, 0.1f);
            }
            else
            {
                healthFillImage.color = new Color(0.9f, 0.15f, 0.15f);
            }
        }

        if (healthText != null)
        {
            healthText.text = $"Vida: {current} / {max} HP";
        }
    }

    /// <summary>M1-4: contador de piedras (usa el mismo texto que antes mostraba la munición).</summary>
    private void UpdateStonesDisplay(int current, int max)
    {
        if (ammoText == null) return;

        string colorTag = current <= 0 ? "<color=red>" : "<color=white>";
        ammoText.text = $"Piedras: {colorTag}{current}</color> / {max}";
    }

    private void TriggerDamageVignette()
    {
        if (damageVignette == null) return;
        if (vignetteCoroutine != null) StopCoroutine(vignetteCoroutine);
        vignetteCoroutine = StartCoroutine(DamageVignetteRoutine());
    }

    private IEnumerator DamageVignetteRoutine()
    {
        Color c = Color.red;
        c.a = 0.45f;
        damageVignette.color = c;

        while (damageVignette.color.a > 0.01f)
        {
            c.a = Mathf.MoveTowards(c.a, 0f, vignetteFadeSpeed * Time.deltaTime);
            damageVignette.color = c;
            yield return null;
        }

        c.a = 0f;
        damageVignette.color = c;
    }

    // El texto "Presiona [E] para ..." lo decide el PlayerInteractor (M1-7) con el interactuable más cercano
    private void ShowInteractionPrompt(IInteractable focused)
    {
        if (interactionPrompt == null) return;

        bool show = focused != null;
        interactionPrompt.gameObject.SetActive(show);
        if (show) interactionPrompt.text = $"Presiona [E] para {focused.InteractionPrompt}";
    }

    /// <summary>M1-10: reaparece en el checkpoint del sector, sin sumar tiempo ni reiniciar sectores.</summary>
    public void OnPlayerDied()
    {
        if (playerHealth != null)
        {
            playerHealth.Respawn();
        }
    }

    public void OnPlayerEscaped()
    {
        if (state == GameState.Victoria) return;
        Debug.Log("<color=green>¡Victoria! Escapaste por el portal rúnico.</color>");
        SetState(GameState.Victoria);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (victoryPanel != null)
        {
            if (victoryText != null && !string.IsNullOrEmpty(victoryMessage)) victoryText.text = victoryMessage;
            victoryPanel.SetActive(true);
            victoryPanel.transform.SetAsLastSibling(); // Poner al frente de cualquier otra imagen
        }
    }

    public void RestartLevel()
    {
        Debug.Log("<color=cyan>Reiniciando nivel...</color>");
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void LoadMainMenu()
    {
        Debug.Log("<color=cyan>Cargando Menú Principal (00_MainMenu)...</color>");
        Time.timeScale = 1f;
        SceneManager.LoadScene("00_MainMenu");
    }

    // Con "Enter Play Mode" sin recargar dominio los estáticos sobreviven
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        OnStateChanged = null;
    }
}
