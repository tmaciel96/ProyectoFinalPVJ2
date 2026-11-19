using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

// Opciones compartidas entre el menú principal y el nivel.
public class GameOptionsMenu : MonoBehaviour
{
    private const string VolumeKey = "MasterVolume";
    public static bool IsOpen { get; private set; }
    private static int closedFrame = -1;
    public static bool BlocksInput => IsOpen || Time.frameCount == closedFrame;
    private GameObject panel;
    private Slider slider;
    private TextMeshProUGUI valueText;
    private float previousTimeScale;
    private float previousVolume = 1f;
    private Canvas canvas;
    private bool isMainMenu;

    void Awake() { isMainMenu = SceneManager.GetActiveScene().name == "00_MainMenu"; }
    void OnEnable() { PlayerInputReader.OnPause += HandlePause; }
    void OnDisable() { PlayerInputReader.OnPause -= HandlePause; }
    private void HandlePause()
    {
        if (isMainMenu) return;
        if (IsOpen) Close();
        else if (GameManager.Instance == null || GameManager.Instance.State != GameManager.GameState.Victoria) Open();
    }

    void Start()
    {
        AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
        if (AudioListener.volume > 0f) previousVolume = AudioListener.volume;
        BuildUI();
    }

    private void BuildUI()
    {
        var root = new GameObject("Opciones HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        // Un canvas raíz ocupa toda la pantalla aunque el controlador esté dentro de otro canvas.
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        if (isMainMenu)
        {
            // Botón de sonido con los colores del menú y un ícono de parlante.
            var soundButton = MakeButton(root.transform, "Sonido", "", new Vector2(-65, -58), new Vector2(80, 65), new Vector2(1, 1));
            soundButton.GetComponent<Image>().color = new Color(0.27f, 0.045f, 0.25f, 1f);
            var rim = soundButton.gameObject.AddComponent<Outline>();
            rim.effectColor = new Color(0.95f, 0.51f, 0.13f, 1f);
            rim.effectDistance = new Vector2(3, -3);
            soundButton.onClick.AddListener(Open);
            var iconColor = new Color(1f, 0.89f, 0.67f);
            var body = MakeImage(soundButton.transform, "Parlante", iconColor, new Vector2(-14, 0), new Vector2(11, 16), new Vector2(0.5f, 0.5f));
            body.raycastTarget = false;
            var cone = MakeImage(soundButton.transform, "Cono", iconColor, new Vector2(-5, 0), new Vector2(9, 27), new Vector2(0.5f, 0.5f));
            cone.raycastTarget = false;
            var wave = MakeImage(soundButton.transform, "Onda", iconColor, new Vector2(9, 0), new Vector2(4, 16), new Vector2(0.5f, 0.5f));
            wave.raycastTarget = false;
            var waveTop = MakeImage(soundButton.transform, "Onda superior", iconColor, new Vector2(8, 10), new Vector2(4, 9), new Vector2(0.5f, 0.5f));
            waveTop.rectTransform.localRotation = Quaternion.Euler(0, 0, 35);
            waveTop.raycastTarget = false;
            var waveBottom = MakeImage(soundButton.transform, "Onda inferior", iconColor, new Vector2(8, -10), new Vector2(4, 9), new Vector2(0.5f, 0.5f));
            waveBottom.rectTransform.localRotation = Quaternion.Euler(0, 0, -35);
            waveBottom.raycastTarget = false;
        }
        var style = Resources.Load<GameObject>("AudioMenuStyle");
        Sprite frameSprite = null;
        Sprite separatorSprite = null;
        TMP_FontAsset menuFont = null;
        if (style != null)
        {
            foreach (var image in style.GetComponentsInChildren<Image>(true))
            {
                if (image.name == "Marco") frameSprite = image.sprite;
                if (image.name == "Separador") separatorSprite = image.sprite;
            }
            foreach (var text in style.GetComponentsInChildren<TMP_Text>(true))
                if (text.name == "Titulo") { menuFont = text.font; break; }
        }
        var panelImage = MakeImage(root.transform, "PanelOpciones", Color.white, Vector2.zero, new Vector2(820, 522), new Vector2(0.5f, 0.5f));
        panel = panelImage.gameObject;
        if (frameSprite != null) { panelImage.sprite = frameSprite; panelImage.preserveAspect = true; }
        else panelImage.color = new Color(0.12f, 0.10f, 0.09f, 0.97f);
        var titleText = MakeText(panel.transform, "Titulo", "AUDIO", new Vector2(0, 148), new Vector2(400, 55), 34);
        if (menuFont != null) titleText.font = menuFont;
        if (separatorSprite != null)
        {
            var separator = MakeImage(panel.transform, "Separador", Color.white, new Vector2(0, 90), new Vector2(350, 53), new Vector2(0.5f, 0.5f));
            separator.sprite = separatorSprite;
            separator.preserveAspect = true;
            separator.raycastTarget = false;
        }
        var close = MakeButton(panel.transform, "Cerrar", "X", new Vector2(320, 160), new Vector2(48, 48), new Vector2(0.5f, 0.5f));
        close.onClick.AddListener(Close);
        var volumeLabel = MakeText(panel.transform, "Etiqueta", "Volumen general", new Vector2(-100, 35), new Vector2(280, 45), 26);
        if (menuFont != null) volumeLabel.font = menuFont;
        volumeLabel.fontStyle = FontStyles.Bold;
        valueText = MakeText(panel.transform, "Porcentaje", "", new Vector2(183, 35), new Vector2(100, 45), 25);
        if (menuFont != null) valueText.font = menuFont;
        valueText.fontStyle = FontStyles.Bold;
        var sliderObject = new GameObject("Volumen", typeof(RectTransform), typeof(Slider));
        sliderObject.transform.SetParent(panel.transform, false);
        var sr = sliderObject.GetComponent<RectTransform>();
        Place(sr, new Vector2(0, -25), new Vector2(430, 35), new Vector2(0.5f, 0.5f));
        slider = sliderObject.GetComponent<Slider>();
        var background = MakeImage(sliderObject.transform, "Fondo", new Color(0.20f, 0.035f, 0.19f), Vector2.zero, new Vector2(430, 18), new Vector2(0.5f, 0.5f));
        var trackOutline = background.gameObject.AddComponent<Outline>();
        trackOutline.effectColor = new Color(0.94f, 0.50f, 0.12f);
        trackOutline.effectDistance = new Vector2(2, -2);
        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderObject.transform, false);
        var area = fillArea.GetComponent<RectTransform>();
        area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one; area.offsetMin = new Vector2(8, 8); area.offsetMax = new Vector2(-8, -8);
        var fill = MakeImage(fillArea.transform, "Fill", new Color(1f, 0.68f, 0.20f), Vector2.zero, Vector2.zero, Vector2.zero);
        fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one; fill.rectTransform.offsetMin = Vector2.zero; fill.rectTransform.offsetMax = Vector2.zero;
        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(sliderObject.transform, false);
        var handleRect = handleArea.GetComponent<RectTransform>();
        handleRect.anchorMin = Vector2.zero; handleRect.anchorMax = Vector2.one;
        handleRect.offsetMin = new Vector2(12, 0); handleRect.offsetMax = new Vector2(-12, 0);
        var handle = MakeImage(handleArea.transform, "Indicador", new Color(1f, 0.76f, 0.26f), Vector2.zero, new Vector2(22, 22), new Vector2(0.5f, 0.5f));
        handle.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
        var handleOutline = handle.gameObject.AddComponent<Outline>();
        handleOutline.effectColor = new Color(0.44f, 0.12f, 0.10f);
        handleOutline.effectDistance = new Vector2(2, -2);
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.fillRect = fill.rectTransform;
        slider.minValue = 0; slider.maxValue = 1;
        slider.SetValueWithoutNotify(AudioListener.volume);
        slider.onValueChanged.AddListener(SetVolume);
        var mute = MakeButton(panel.transform, "Silenciar", "SILENCIAR / ACTIVAR", new Vector2(0, -100), new Vector2(300, 48), new Vector2(0.5f, 0.5f));
        mute.GetComponent<Image>().color = new Color(0.31f, 0.04f, 0.29f);
        var muteOutline = mute.gameObject.AddComponent<Outline>();
        muteOutline.effectColor = new Color(0.95f, 0.50f, 0.12f);
        muteOutline.effectDistance = new Vector2(3, -3);
        var muteLabel = mute.GetComponentInChildren<TextMeshProUGUI>();
        if (menuFont != null) muteLabel.font = menuFont;
        muteLabel.fontStyle = FontStyles.Bold;
        muteLabel.enableAutoSizing = true;
        muteLabel.fontSizeMin = 14; muteLabel.fontSizeMax = 20;
        mute.onClick.AddListener(ToggleMute);
        UpdateValue();
        panel.SetActive(false);
    }

    private void Open()
    {
        if (IsOpen) return;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        IsOpen = true;
        panel.SetActive(true);
    }
    private void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        closedFrame = Time.frameCount;
        panel.SetActive(false);
        Time.timeScale = previousTimeScale;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }
    private void SetVolume(float value)
    {
        AudioListener.volume = Mathf.Clamp01(value);
        if (value > 0.001f) previousVolume = value;
        PlayerPrefs.SetFloat(VolumeKey, AudioListener.volume);
        PlayerPrefs.Save();
        UpdateValue();
    }
    private void ToggleMute() => slider.value = AudioListener.volume > 0.001f ? 0f : previousVolume;
    private void UpdateValue() { if (valueText != null) valueText.text = Mathf.RoundToInt(AudioListener.volume * 100f) + "%"; }
    void OnDestroy()
    {
        if (IsOpen) { Time.timeScale = previousTimeScale; IsOpen = false; }
        if (canvas != null) Destroy(canvas.gameObject);
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState() { IsOpen = false; closedFrame = -1; }

    private static void Place(RectTransform r, Vector2 position, Vector2 size, Vector2 anchor)
    { r.anchorMin = anchor; r.anchorMax = anchor; r.pivot = new Vector2(0.5f, 0.5f); r.anchoredPosition = position; r.sizeDelta = size; }
    private static Image MakeImage(Transform parent, string name, Color color, Vector2 position, Vector2 size, Vector2 anchor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false); Place(go.GetComponent<RectTransform>(), position, size, anchor);
        var image = go.GetComponent<Image>(); image.color = color; return image;
    }
    private static TextMeshProUGUI MakeText(Transform parent, string name, string text, Vector2 position, Vector2 size, float fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false); Place(go.GetComponent<RectTransform>(), position, size, new Vector2(0.5f, 0.5f));
        var label = go.GetComponent<TextMeshProUGUI>(); label.text = text; label.fontSize = fontSize; label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.95f, 0.88f, 0.72f); label.raycastTarget = false; return label;
    }
    private static Button MakeButton(Transform parent, string name, string label, Vector2 position, Vector2 size, Vector2 anchor)
    {
        var image = MakeImage(parent, name, new Color(0.25f, 0.19f, 0.14f), position, size, anchor);
        var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        if (!string.IsNullOrEmpty(label)) MakeText(image.transform, "Texto", label, Vector2.zero, size, label.Length > 3 ? 20 : 30);
        return button;
    }
}
