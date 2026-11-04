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

    [Header("Disparo (Clic Izquierdo o Espacio)")]
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
    private Vector3 currentAimPoint3D;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        if (cam == null) cam = Camera.main;

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
        HandleMovement();
        HandleAiming();
        HandleShooting();
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

        // El avatar del jugador rota horizontalmente en el plano XZ para no inclinarse hacia el suelo
        Vector3 horizontalLook = new Vector3(currentAimPoint3D.x, transform.position.y, currentAimPoint3D.z);
        if ((horizontalLook - transform.position).sqrMagnitude > 0.05f)
        {
            transform.LookAt(horizontalLook);
        }
    }

    private void HandleShooting()
    {
        bool mouseShoot = Mouse.current != null && Mouse.current.leftButton.isPressed;
        bool spaceShoot = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;

        if ((mouseShoot || spaceShoot) && Time.time >= nextFire)
        {
            if (isReloading) return;

            if (currentAmmo <= 0)
            {
                StartReload();
                return;
            }

            nextFire = Time.time + fireRate;
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

        currentAmmo--;
        OnAmmoChanged?.Invoke(currentAmmo, maxAmmo, false);

        if (currentAmmo <= 0)
        {
            StartReload();
        }
    }

    public bool WasInteractPressed()
    {
        if (Keyboard.current != null)
        {
            return Keyboard.current.eKey.wasPressedThisFrame;
        }
        return false;
    }
}
