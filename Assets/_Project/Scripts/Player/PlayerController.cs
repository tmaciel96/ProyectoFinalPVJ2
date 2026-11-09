// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
// Basado en el PlayerController del TP1 (The Pumpkin Ritual, Tamara Maciel).
//
// Controlador del sepulturero, con el CharacterController:
//  - Movimiento relativo a la cámara y gravedad. Mira hacia donde camina; al pegar o tirar con el mouse
//    gira hacia el puntero (apuntado 3D sobre colinas y desniveles).
//  - M1-5: lanza piedras desde el pool "Stone" del ObjectPooler; cada una gasta una del StoneInventory
//    (máximo 10, M1-4), que se reponen en los montículos (StonePile).
//  - M1-1: los controles se leen del PlayerInputReader (InputSystem_Actions).
//  - Si al Player le faltan PlayerShovel, PlayerInteractor, StoneInventory o PlayerInputReader, los agrega
//    en runtime con un aviso en consola.
// =================================================================================================

using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movimiento")]
    public float speed = 6f;
    public float gravity = -20f;
    private float verticalVelocity;

    [Header("Retroceso (golpe del espectro, M4-3)")]
    [Tooltip("Metros por segundo que pierde el empujón por segundo")]
    public float knockbackDecay = 25f;
    private Vector3 knockbackVelocity;

    [Header("Lanzar piedra (clic derecho, K o gatillo derecho)")]
    [Tooltip("Segundos mínimos entre lanzamientos")]
    [FormerlySerializedAs("fireRate")]
    public float throwCooldown = 0.35f;
    private float nextThrow;
    [Tooltip("Desde dónde sale la piedra (hijo FirePoint del Player)")]
    [FormerlySerializedAs("firePoint")]
    public Transform throwPoint;

    [Tooltip("Prefab del proyectil de piedra (Projectile_Stone). Se registra solo en el ObjectPooler con stonePoolTag.")]
    public GameObject stoneProjectilePrefab;
    public string stonePoolTag = "Stone";
    public int stonePoolSize = 12;

    private StoneInventory stones;
    private PlayerInputReader input;

    [Header("Referencias")]
    public Camera cam;
    private CharacterController controller;
    private PlayerHealth health;
    private Vector3 currentAimPoint3D;

    [Header("Animación")]
    [Tooltip("Animator del modelo (Player.controller). Si queda vacío se busca en los hijos; si no hay, se ignora.")]
    public Animator animator;
    [Tooltip("Suavizado de MoveX/MoveZ en el Blend Tree")]
    public float animDampTime = 0.1f;

    public enum FacingMode
    {
        MoveDirection,  // mira hacia donde camina; gira al mouse solo al pegar o tirar con el mouse
        MouseAim        // siempre mira al mouse
    }

    [Header("Rotación")]
    public FacingMode facingMode = FacingMode.MoveDirection;
    [Tooltip("Grados por segundo al girar hacia donde camina")]
    public float turnSpeed = 720f;
    [Tooltip("Segundos que sigue mirando al mouse después de pegar o tirar (modo MoveDirection)")]
    public float aimHoldTime = 0.45f;
    private float aimHoldUntil;
    private Vector3 currentMoveDirection;

    private static readonly int AnimMoveX = Animator.StringToHash("MoveX");
    private static readonly int AnimMoveZ = Animator.StringToHash("MoveZ");
    private static readonly int AnimAttack = Animator.StringToHash("Attack");
    private static readonly int AnimThrow = Animator.StringToHash("Throw");

    void Awake()
    {
        input = EnsureInput(gameObject);
    }

    /// <summary>El PlayerInputReader del objeto; si el prefab no lo tiene, se agrega en runtime.</summary>
    internal static PlayerInputReader EnsureInput(GameObject owner)
    {
        PlayerInputReader reader = owner.GetComponent<PlayerInputReader>();
        if (reader == null)
        {
            Debug.LogWarning("[PlayerController] Falta PlayerInputReader en el Player: se agrega en runtime. Sumalo al prefab.");
            reader = owner.AddComponent<PlayerInputReader>();
        }
        return reader;
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        health = GetComponent<PlayerHealth>();
        if (cam == null) cam = Camera.main;

        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator != null) animator.applyRootMotion = false; // el movimiento lo maneja el CharacterController

        // La pala vive en su propio componente; si el prefab todavía no lo tiene, se agrega con valores por defecto
        if (GetComponent<PlayerShovel>() == null)
        {
            Debug.LogWarning("[PlayerController] Falta PlayerShovel en el Player: se agrega en runtime. Sumalo al prefab para ajustar daño y alcance.");
            gameObject.AddComponent<PlayerShovel>();
        }

        // Lo mismo con la interacción (M1-7)
        if (GetComponent<PlayerInteractor>() == null)
        {
            Debug.LogWarning("[PlayerController] Falta PlayerInteractor en el Player: se agrega en runtime. Sumalo al prefab.");
            gameObject.AddComponent<PlayerInteractor>();
        }

        // Y con el inventario de piedras (M1-4)
        stones = GetComponent<StoneInventory>();
        if (stones == null)
        {
            Debug.LogWarning("[PlayerController] Falta StoneInventory en el Player: se agrega en runtime. Sumalo al prefab.");
            stones = gameObject.AddComponent<StoneInventory>();
        }

        // El pool de piedras se registra en runtime: así no hace falta editar la escena
        if (stoneProjectilePrefab != null && ObjectPooler.Instance != null)
            ObjectPooler.Instance.RegisterPool(stonePoolTag, stoneProjectilePrefab, stonePoolSize);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (throwPoint == null)
        {
            Transform found = transform.Find("FirePoint");
            if (found != null) throwPoint = found;
        }
    }

    void OnEnable()
    {
        PlayerHealth.OnPlayerRespawned += ResetThrowState;
    }

    void OnDisable()
    {
        PlayerHealth.OnPlayerRespawned -= ResetThrowState;
    }

    void Update()
    {
        // Mientras se reproduce la animación de muerte o hay un diálogo abierto, el jugador no se mueve ni tira
        if ((health != null && health.IsDead) || DialogueSystem.BlocksGameplay)
        {
            UpdateMoveAnimation(Vector3.zero);
            return;
        }

        HandleMovement();
        HandleAiming();
        HandleThrowing();
        HandleRotation();
    }

    private void HandleMovement()
    {
        // Teclado: -1, 0 o 1 en cada eje; stick: valores intermedios (camina más lento si se inclina poco)
        Vector2 moveInput = input != null ? input.Move : Vector2.zero;
        float h = moveInput.x;
        float v = moveInput.y;

        Vector3 moveDirection = Vector3.zero;
        if (cam != null)
        {
            Vector3 camForward = cam.transform.forward;
            Vector3 camRight = cam.transform.right;
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            moveDirection = Vector3.ClampMagnitude(camForward * v + camRight * h, 1f);
        }
        else
        {
            moveDirection = Vector3.ClampMagnitude(new Vector3(h, 0f, v), 1f);
        }

        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        Vector3 finalMove = (moveDirection * speed) + new Vector3(0f, verticalVelocity, 0f) + knockbackVelocity;
        controller.Move(finalMove * Time.deltaTime);
        knockbackVelocity = Vector3.MoveTowards(knockbackVelocity, Vector3.zero, knockbackDecay * Time.deltaTime);

        currentMoveDirection = moveDirection;
        UpdateMoveAnimation(moveDirection);
    }

    /// <summary>
    /// Pasa la dirección de movimiento (mundo) al espacio local del personaje: así el Blend Tree 2D
    /// muestra correr de frente, de espaldas o de costado según hacia dónde mira.
    /// </summary>
    private void UpdateMoveAnimation(Vector3 worldMoveDirection)
    {
        if (animator == null) return;

        Vector3 local = transform.InverseTransformDirection(worldMoveDirection);
        animator.SetFloat(AnimMoveX, local.x, animDampTime, Time.deltaTime);
        animator.SetFloat(AnimMoveZ, local.z, animDampTime, Time.deltaTime);
    }

    /// <summary>Dispara la animación del golpe con la pala (la lógica de daño es la tarea M1-3).</summary>
    public void PlayAttackAnimation()
    {
        if (animator != null) animator.SetTrigger(AnimAttack);
    }

    private void HandleAiming()
    {
        Vector2? pointer = input != null ? input.PointerPosition : null;
        if (pointer == null || cam == null) return;

        Vector2 mouseScreenPos = pointer.Value;
        Ray ray = cam.ScreenPointToRay(mouseScreenPos);

        // Raycast en 3D para detectar exactamente la superficie donde el jugador hace clic (colinas, enemigos elevados, rampas)
        RaycastHit[] hits = Physics.RaycastAll(ray, 150f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        bool found3D = false;
        foreach (var hit in hits)
        {
            // Ignorar al propio jugador
            if (hit.collider.transform.root == transform.root || hit.collider.CompareTag("Player"))
                continue;

            currentAimPoint3D = hit.point;
            found3D = true;
            break;
        }

        // Si el raycast no pegó en nada (ej. cielo), proyectar en el plano horizontal de respaldo
        if (!found3D)
        {
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
            if (groundPlane.Raycast(ray, out float enter))
            {
                currentAimPoint3D = ray.GetPoint(enter);
            }
            else
            {
                currentAimPoint3D = transform.position + transform.forward * 10f;
            }
        }

    }

    /// <summary>
    /// MouseAim: siempre al mouse. MoveDirection: hacia donde camina, salvo justo después de pegar o tirar
    /// con el mouse, que mira al puntero durante aimHoldTime para que el golpe vaya donde se apuntó.
    /// </summary>
    private void HandleRotation()
    {
        if (facingMode == FacingMode.MouseAim || Time.time < aimHoldUntil)
        {
            FaceAimPointInstant();
            return;
        }

        if (currentMoveDirection.sqrMagnitude > 0.01f)
        {
            Quaternion target = Quaternion.LookRotation(currentMoveDirection, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
        }
    }

    private void FaceAimPointInstant()
    {
        // El avatar del jugador rota horizontalmente en el plano XZ para no inclinarse hacia el suelo
        Vector3 horizontalLook = new Vector3(currentAimPoint3D.x, transform.position.y, currentAimPoint3D.z);
        if ((horizontalLook - transform.position).sqrMagnitude > 0.05f)
        {
            transform.LookAt(horizontalLook);
        }
    }

    /// <summary>
    /// Gira de inmediato hacia el mouse y lo sostiene aimHoldTime. Lo usan la pala y la piedra cuando
    /// la acción se hizo con el mouse; con J/K se pega hacia donde ya mira el personaje.
    /// </summary>
    public void FaceAimForAction()
    {
        if (cam == null || input == null || input.PointerPosition == null) return;
        HandleAiming(); // punto actualizado aunque se llame antes que el Update de este script
        FaceAimPointInstant();
        aimHoldUntil = Time.time + aimHoldTime;
    }

    private void HandleThrowing()
    {
        // Controles del GDD: clic izquierdo/J = pala (PlayerShovel), clic derecho/K = piedra
        // Un lanzamiento por pulsación (no ráfaga): las piedras son pocas
        if (input == null || !input.ThrowPressed) return;
        bool fromPointer = input.ThrowFromPointer;

        if (Time.time >= nextThrow)
        {
            // Sin piedras no se lanza (StoneInventory avisa OnOutOfStones)
            if (stones != null && !stones.TryConsume()) return;

            nextThrow = Time.time + throwCooldown;
            // Con el mouse se apunta al puntero; con K o el gamepad se tira hacia adelante
            if (fromPointer) FaceAimForAction();
            else currentAimPoint3D = transform.position + transform.forward * 10f + Vector3.up * 1f;
            ThrowStone();
        }
    }

    /// <summary>Al reaparecer (PlayerHealth). Las piedras las repone StoneInventory.</summary>
    public void ResetThrowState()
    {
        nextThrow = 0f;
        aimHoldUntil = 0f;
        currentAimPoint3D = transform.position + transform.forward * 10f;
        knockbackVelocity = Vector3.zero;
    }

    /// <summary>Empuja al sepulturero en una dirección horizontal (se frena solo con knockbackDecay).</summary>
    public void ApplyKnockback(Vector3 direction, float force)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f || force <= 0f) return;
        knockbackVelocity = direction.normalized * force;
    }

    private void ThrowStone()
    {
        Vector3 spawnPos = throwPoint != null ? throwPoint.position : transform.position + transform.forward * 1.3f + Vector3.up * 1f;

        // Dirección en 3D hacia el punto apuntado: la piedra sube en colinas y baja en zanjas
        Vector3 throwDir = (currentAimPoint3D - spawnPos).normalized;
        if (throwDir.sqrMagnitude < 0.001f || Vector3.Dot(transform.forward, throwDir) < 0.1f)
        {
            throwDir = transform.forward;
        }
        Quaternion spawnRot = Quaternion.LookRotation(throwDir);

        GameObject stone = null;
        if (ObjectPooler.Instance != null && ObjectPooler.Instance.HasPool(stonePoolTag))
            stone = ObjectPooler.Instance.SpawnFromPool(stonePoolTag, spawnPos, spawnRot);

        // Sin ObjectPooler en la escena (escenas de prueba) se instancia directo
        if (stone == null && stoneProjectilePrefab != null)
            stone = Instantiate(stoneProjectilePrefab, spawnPos, spawnRot);

        if (stone == null)
            Debug.LogWarning("[PlayerController] No hay prefab de piedra: asigná Projectile_Stone en stoneProjectilePrefab.");

        // M1-5: animación de lanzar piedra (la piedra ya se descontó en HandleShooting)
        if (animator != null) animator.SetTrigger(AnimThrow);
    }
}
