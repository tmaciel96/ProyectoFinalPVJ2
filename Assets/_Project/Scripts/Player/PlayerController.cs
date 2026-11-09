// =================================================================================================
// Proyecto: The Pumpkin Ritual - La Hora de las Brujas (TP Programación 2 - UNICABA)
// Autora: Tamara Maciel
// Correo: tmaciel96@gmail.com
// Repositorio: https://github.com/tmaciel96/PumpkinPolyTP
// 
// Descripción del script:
// Controlador del jugador con avatar monolítico (tótem de calabaza Jack-o'-Lantern) basado 100% en el
// New Input System. Administra movimiento relativo a la cámara, gravedad, rotación horizontal XZ con
// cálculo de apuntado 3D en desniveles/colinas, sistema de munición (20 balas), recarga con [R]
// y disparo continuo rúnico conectado con el ObjectPooler.
// =================================================================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movimiento")]
    public float speed = 6f;
    public float gravity = -20f;
    private float verticalVelocity;

    [Header("Lanzar piedra (Clic Derecho o K)")]
    public float fireRate = 0.2f;
    private float nextFire;
    public Transform firePoint;
    public string bulletPoolTag = "PlayerBullet";
    
    [Tooltip("Prefab de respaldo si el ObjectPooler no está en la escena")]
    public GameObject bulletFallbackPrefab;

    [Header("Sistema de Munición y Recarga")]
    public int maxAmmo = 20;
    public int currentAmmo = 20;
    public float reloadDuration = 1.2f;
    public bool isReloading = false;
    private Coroutine reloadCoroutine;

    // Evento para informar a la UI (munición actual, máxima, si está recargando)
    public static event Action<int, int, bool> OnAmmoChanged;

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
        MouseAim        // siempre mira al mouse (comportamiento del TP1)
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

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (firePoint == null)
        {
            Transform found = transform.Find("FirePoint");
            if (found != null) firePoint = found;
        }

        currentAmmo = maxAmmo;
        OnAmmoChanged?.Invoke(currentAmmo, maxAmmo, false);
    }

    void OnEnable()
    {
        PlayerHealth.OnPlayerRespawned += ResetShootingState;
    }

    void OnDisable()
    {
        PlayerHealth.OnPlayerRespawned -= ResetShootingState;
    }

    void Update()
    {
        // Mientras se reproduce la animación de muerte o hay un diálogo abierto, el jugador no se mueve ni dispara
        if ((health != null && health.IsDead) || DialogueSystem.BlocksGameplay)
        {
            UpdateMoveAnimation(Vector3.zero);
            return;
        }

        HandleMovement();
        HandleAiming();
        HandleShooting();
        HandleRotation();
        HandleReloadInput();
    }

    private void HandleMovement()
    {
        float h = 0f;
        float v = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) h -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) h += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) v -= 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) v += 1f;
        }

        Vector3 moveDirection = Vector3.zero;
        if (cam != null)
        {
            Vector3 camForward = cam.transform.forward;
            Vector3 camRight = cam.transform.right;
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            moveDirection = (camForward * v + camRight * h).normalized;
        }
        else
        {
            moveDirection = new Vector3(h, 0f, v).normalized;
        }

        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        Vector3 finalMove = (moveDirection * speed) + new Vector3(0f, verticalVelocity, 0f);
        controller.Move(finalMove * Time.deltaTime);

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
        if (Mouse.current == null || cam == null) return;

        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
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
        if (cam == null || Mouse.current == null) return;
        HandleAiming(); // punto actualizado aunque se llame antes que el Update de este script
        FaceAimPointInstant();
        aimHoldUntil = Time.time + aimHoldTime;
    }

    private void HandleShooting()
    {
        // Controles del GDD: clic izquierdo/J = pala (PlayerShovel), clic derecho/K = piedra
        bool mouseShoot = Mouse.current != null && Mouse.current.rightButton.isPressed;
        bool keyShoot = Keyboard.current != null && Keyboard.current.kKey.isPressed;

        if ((mouseShoot || keyShoot) && Time.time >= nextFire)
        {
            if (isReloading) return;

            if (currentAmmo <= 0)
            {
                StartReload();
                return;
            }

            nextFire = Time.time + fireRate;
            // Con el mouse se apunta al puntero; con K se tira hacia adelante
            if (mouseShoot) FaceAimForAction();
            else currentAimPoint3D = transform.position + transform.forward * 10f + Vector3.up * 1f;
            Shoot();
        }
    }

    private void HandleReloadInput()
    {
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            if (!isReloading && currentAmmo < maxAmmo)
            {
                StartReload();
            }
        }
    }

    public void StartReload()
    {
        if (isReloading) return;
        if (reloadCoroutine != null) StopCoroutine(reloadCoroutine);
        reloadCoroutine = StartCoroutine(ReloadRoutine());
    }

    private IEnumerator ReloadRoutine()
    {
        isReloading = true;
        OnAmmoChanged?.Invoke(currentAmmo, maxAmmo, true);

        yield return new WaitForSeconds(reloadDuration);

        currentAmmo = maxAmmo;
        isReloading = false;
        reloadCoroutine = null;
        OnAmmoChanged?.Invoke(currentAmmo, maxAmmo, false);
    }

    public void ResetShootingState()
    {
        if (reloadCoroutine != null)
        {
            StopCoroutine(reloadCoroutine);
            reloadCoroutine = null;
        }

        isReloading = false;
        currentAmmo = maxAmmo;
        nextFire = 0f;
        currentAimPoint3D = transform.position + transform.forward * 10f;
        OnAmmoChanged?.Invoke(currentAmmo, maxAmmo, false);
    }

    private void Shoot()
    {
        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position + transform.forward * 1.3f + Vector3.up * 1f;

        // Calcular la dirección exacta en 3D (X, Y, Z) hacia el punto apuntado.
        // Esto permite que el proyectil viaje en diagonal hacia arriba en colinas o hacia abajo en zanjas.
        Vector3 shootDir = (currentAimPoint3D - spawnPos).normalized;
        if (shootDir.sqrMagnitude < 0.001f || Vector3.Dot(transform.forward, shootDir) < 0.1f)
        {
            shootDir = transform.forward;
        }
        Quaternion spawnRot = Quaternion.LookRotation(shootDir);

        bool spawned = false;

        if (ObjectPooler.Instance != null)
        {
            GameObject pooledObj = ObjectPooler.Instance.SpawnFromPool(bulletPoolTag, spawnPos, spawnRot);
            if (pooledObj != null)
            {
                Rigidbody rb = pooledObj.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = pooledObj.AddComponent<Rigidbody>();
                    rb.isKinematic = true;
                    rb.useGravity = false;
                }
                spawned = true;
            }
        }

        if (!spawned && bulletFallbackPrefab != null)
        {
            GameObject instObj = Instantiate(bulletFallbackPrefab, spawnPos, spawnRot);
            Rigidbody rb = instObj.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = instObj.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
            }
            spawned = true;
        }

        if (!spawned)
        {
            GameObject defaultBullet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            defaultBullet.name = "Auto_Bullet_PumpkinLight";
            defaultBullet.transform.position = spawnPos;
            defaultBullet.transform.rotation = spawnRot;
            defaultBullet.transform.localScale = Vector3.one * 0.5f;

            Renderer rend = defaultBullet.GetComponent<Renderer>();
            if (rend != null)
            {
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = new Color(1f, 0.55f, 0f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(1f, 0.4f, 0f) * 2f);
                rend.material = mat;
            }

            Light ptLight = defaultBullet.AddComponent<Light>();
            ptLight.type = LightType.Point;
            ptLight.color = new Color(1f, 0.6f, 0f);
            ptLight.range = 3f;
            ptLight.intensity = 2f;

            Collider col = defaultBullet.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            Rigidbody rb = defaultBullet.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            PooledProjectile proj = defaultBullet.AddComponent<PooledProjectile>();
            proj.speed = 22f;
            proj.damage = 25;
            proj.lifeTime = 2.5f;
            proj.isPlayerProjectile = true;
        }

        // El disparo actual pasa a ser "lanzar piedra" (M1-5): misma animación
        if (animator != null) animator.SetTrigger(AnimThrow);

        currentAmmo--;
        OnAmmoChanged?.Invoke(currentAmmo, maxAmmo, false);

        if (currentAmmo <= 0)
        {
            StartReload();
        }
    }
}
