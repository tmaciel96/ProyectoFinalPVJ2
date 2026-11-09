// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// PlayerInputReader (M1-1, contrato compartido): único lugar donde se leen los controles.
// Usa el mapa "Player" de InputSystem_Actions.inputactions:
//   Move      WASD / flechas / stick izquierdo
//   Point     posición del mouse (para apuntar la pala y la piedra)
//   Attack    clic izquierdo / J / X del gamepad                 -> pala
//   Throw     clic derecho / K / gatillo derecho o B del gamepad -> piedra
//   Interact  E / Espacio / A del gamepad                         -> interactuar y avanzar diálogos
//   Pause     Esc / Start                                         -> pausa (M5-5)
// Para cambiar una tecla se edita el asset, no el código. Los controles táctiles (M1-12) se suman con
// On-Screen Stick / On-Screen Button del Input System, que imitan un gamepad y entran por estas acciones.
//
// Uso: PlayerInputReader.Instance.AttackPressed (solo es true el frame en que se apretó), o los eventos
// OnAttack, OnThrow, OnInteract y OnPause.
// =================================================================================================

using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-100)] // antes que los scripts que leen sus valores
public class PlayerInputReader : MonoBehaviour
{
    [Tooltip("InputSystem_Actions.inputactions. Si queda vacío se usan las acciones del proyecto o unas por defecto.")]
    public InputActionAsset actions;
    [Tooltip("Nombre del mapa de acciones del jugador dentro del asset")]
    public string mapName = "Player";

    /// <summary>El lector activo (lo usan el diálogo y la pausa, que no están en el Player).</summary>
    public static PlayerInputReader Instance { get; private set; }

    // Eventos (el bool dice si la acción vino del mouse, para girar hacia el puntero)
    public static event Action<bool> OnAttack;
    public static event Action<bool> OnThrow;
    public static event Action OnInteract;
    public static event Action OnPause;

    /// <summary>Dirección de movimiento (-1..1 en cada eje), sin normalizar el stick.</summary>
    public Vector2 Move => move != null ? Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f) : Vector2.zero;
    /// <summary>Posición del puntero en pantalla; null si no hay mouse.</summary>
    public Vector2? PointerPosition => Mouse.current != null && point != null ? point.ReadValue<Vector2>() : (Vector2?)null;

    public bool AttackPressed => attackFrame == Time.frameCount;
    public bool ThrowPressed => throwFrame == Time.frameCount;
    public bool InteractPressed => interactFrame == Time.frameCount;
    public bool PausePressed => pauseFrame == Time.frameCount;
    /// <summary>El último golpe con la pala se hizo con el mouse.</summary>
    public bool AttackFromPointer { get; private set; }
    /// <summary>La última piedra se tiró con el mouse.</summary>
    public bool ThrowFromPointer { get; private set; }

    private InputActionMap map;
    private InputAction move, point, attack, throwAction, interact, pause;
    private int attackFrame = -1, throwFrame = -1, interactFrame = -1, pauseFrame = -1;
    private bool ownsMap; // el mapa por defecto se crea acá y se destruye acá

    void Awake()
    {
        if (Instance != null && Instance != this)
            Debug.LogWarning("[PlayerInputReader] Hay más de uno en la escena; se usa el último.");
        Instance = this;

        InputActionAsset source = actions != null ? actions : InputSystem.actions;
        map = source != null ? source.FindActionMap(mapName) : null;
        if (map == null)
        {
            Debug.LogWarning("[PlayerInputReader] Sin asset de acciones: se usan los controles por defecto. Asigná InputSystem_Actions en el Player.");
            map = BuildDefaultMap();
            ownsMap = true;
        }

        move = map.FindAction("Move");
        point = map.FindAction("Point");
        attack = map.FindAction("Attack");
        throwAction = map.FindAction("Throw");
        interact = map.FindAction("Interact");
        pause = map.FindAction("Pause");
    }

    void OnEnable()
    {
        if (map == null) return;
        if (attack != null) attack.performed += HandleAttack;
        if (throwAction != null) throwAction.performed += HandleThrow;
        // started y no performed: en InputSystem_Actions la acción Interact tiene "Hold", y performed
        // recién llega después de mantener la tecla medio segundo. started llega al apretar, con o sin Hold.
        if (interact != null) interact.started += HandleInteract;
        if (pause != null) pause.performed += HandlePause;
        map.Enable();
    }

    void OnDisable()
    {
        if (map == null) return;
        if (attack != null) attack.performed -= HandleAttack;
        if (throwAction != null) throwAction.performed -= HandleThrow;
        if (interact != null) interact.started -= HandleInteract;
        if (pause != null) pause.performed -= HandlePause;
        map.Disable();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (ownsMap && map != null) map.Dispose();
    }

    private static bool FromPointer(InputAction.CallbackContext ctx) => ctx.control != null && ctx.control.device is Pointer;

    private void HandleAttack(InputAction.CallbackContext ctx)
    {
        attackFrame = Time.frameCount;
        AttackFromPointer = FromPointer(ctx);
        OnAttack?.Invoke(AttackFromPointer);
    }

    private void HandleThrow(InputAction.CallbackContext ctx)
    {
        throwFrame = Time.frameCount;
        ThrowFromPointer = FromPointer(ctx);
        OnThrow?.Invoke(ThrowFromPointer);
    }

    private void HandleInteract(InputAction.CallbackContext ctx)
    {
        interactFrame = Time.frameCount;
        OnInteract?.Invoke();
    }

    private void HandlePause(InputAction.CallbackContext ctx)
    {
        pauseFrame = Time.frameCount;
        OnPause?.Invoke();
    }

    /// <summary>Los mismos controles del asset, por si el Player no lo tiene asignado.</summary>
    private static InputActionMap BuildDefaultMap()
    {
        var m = new InputActionMap("Player");

        var mv = m.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
        mv.AddBinding("<Gamepad>/leftStick");
        mv.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w").With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/s").With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/a").With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/d").With("Right", "<Keyboard>/rightArrow");

        m.AddAction("Point", InputActionType.Value, "<Mouse>/position", expectedControlLayout: "Vector2");

        var atk = m.AddAction("Attack", InputActionType.Button, "<Mouse>/leftButton");
        atk.AddBinding("<Keyboard>/j");
        atk.AddBinding("<Gamepad>/buttonWest");

        var thr = m.AddAction("Throw", InputActionType.Button, "<Mouse>/rightButton");
        thr.AddBinding("<Keyboard>/k");
        thr.AddBinding("<Gamepad>/rightTrigger");
        thr.AddBinding("<Gamepad>/buttonEast");

        var inter = m.AddAction("Interact", InputActionType.Button, "<Keyboard>/e");
        inter.AddBinding("<Keyboard>/space");
        inter.AddBinding("<Gamepad>/buttonSouth");

        var pau = m.AddAction("Pause", InputActionType.Button, "<Keyboard>/escape");
        pau.AddBinding("<Gamepad>/start");

        return m;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        OnAttack = null;
        OnThrow = null;
        OnInteract = null;
        OnPause = null;
    }
}
