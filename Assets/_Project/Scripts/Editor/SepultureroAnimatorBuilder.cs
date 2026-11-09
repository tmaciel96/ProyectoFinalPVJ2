// =================================================================================================
// Proyecto: El Turno del Sepulturero (Proyecto Final PVJ2 - UNICABA)
//
// Genera (o regenera) los Animator Controllers del proyecto a partir de los clips de los packs:
//   Assets/_Project/Animation/Controllers/Player.controller   (Blink, Humanoid)
//   Assets/_Project/Animation/Controllers/Skeleton.controller (Skeleton de SazenGames, Humanoid)
//   Assets/_Project/Animation/Controllers/Zombie.controller   (clips del Skeleton retargeteados al zombi)
//   Assets/_Project/Animation/Masks/UpperBody.mask           (máscara del torso para la capa de ataque)
//
// Por qué un script y no YAML escrito a mano: los FBX del Skeleton y del Zombie usan IDs internos
// hasheados (internalIDToNameTable vacío), así que los fileID de sus clips no se pueden saber sin
// que Unity los importe. Este builder busca cada clip por nombre y deja los .controller en YAML.
//
// Uso: menú Sepulturero > Generar Animator Controllers. Se puede correr las veces que haga falta:
// pisa los controllers anteriores (las referencias desde prefabs se mantienen porque conserva el GUID).
// =================================================================================================

using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class SepultureroAnimatorBuilder
{
    private const string ControllersDir = "Assets/_Project/Animation/Controllers";
    private const string MasksDir = "Assets/_Project/Animation/Masks";

    private const string BlinkAnims = "Assets/ThirdParty/Blink/Art/Animations/Animations_Starter_Pack";
    private const string SkeletonAnims = "Assets/ThirdParty/Skeleton/Art/Animations";

    // Parámetros compartidos con los scripts (PlayerController, PlayerHealth, EnemyOrb)
    public const string P_MoveX = "MoveX";
    public const string P_MoveZ = "MoveZ";
    public const string P_Speed = "Speed";
    public const string P_Attack = "Attack";
    public const string P_Throw = "Throw";
    public const string P_Hit = "Hit";
    public const string P_Dead = "Dead";

    [MenuItem("Sepulturero/Generar Animator Controllers")]
    public static void BuildAll()
    {
        EnsureFolder(ControllersDir);
        EnsureFolder(MasksDir);

        AvatarMask upperBody = BuildUpperBodyMask();
        BuildPlayer(upperBody);

        BuildEnemy("Skeleton", new EnemyClips
        {
            idle = Clip($"{SkeletonAnims}/Skeleton_idle.fbx"),
            walk = Clip($"{SkeletonAnims}/Skeleton_walk_forward.fbx"),
            run = Clip($"{SkeletonAnims}/Skeleton_run_forward.fbx"),
            attack = Clip($"{SkeletonAnims}/Skeleton_slash01.fbx"),
            hit = Clip($"{SkeletonAnims}/Skeleton_take_damage.fbx"),
            death = Clip($"{SkeletonAnims}/Skeleton_death.fbx"),
            walkSpeed = 1.5f,
            runSpeed = 4.5f,
            locomotionSpeed = 1f
        });

        // El pack Zombies no trae clips: el modelo es Humanoid, así que reusa los del Skeleton.
        // Cuando consigamos clips propios de zombi (p. ej. Mixamo), cambiar solo estas rutas.
        BuildEnemy("Zombie", new EnemyClips
        {
            idle = Clip($"{SkeletonAnims}/Skeleton_idle.fbx"),
            walk = Clip($"{SkeletonAnims}/Skeleton_walk_forward.fbx"),
            run = Clip($"{SkeletonAnims}/Skeleton_run_forward.fbx"),
            attack = Clip($"{SkeletonAnims}/Skeleton_slash02.fbx"),
            hit = Clip($"{SkeletonAnims}/Skeleton_take_damage.fbx"),
            death = Clip($"{SkeletonAnims}/Skeleton_death.fbx"),
            walkSpeed = 1.2f,
            runSpeed = 3.5f,
            locomotionSpeed = 0.85f // el zombi se mueve más pesado
        });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=cyan>[Sepulturero]</color> Animator Controllers generados en " + ControllersDir);
    }

    // ---------------------------------------------------------------------------------------------
    // Player
    // ---------------------------------------------------------------------------------------------
    private static void BuildPlayer(AvatarMask upperBody)
    {
        AnimatorController ac = CreateController("Player");
        ac.AddParameter(P_MoveX, AnimatorControllerParameterType.Float);
        ac.AddParameter(P_MoveZ, AnimatorControllerParameterType.Float);
        ac.AddParameter(P_Attack, AnimatorControllerParameterType.Trigger);
        ac.AddParameter(P_Throw, AnimatorControllerParameterType.Trigger);
        ac.AddParameter(P_Hit, AnimatorControllerParameterType.Trigger);
        ac.AddParameter(P_Dead, AnimatorControllerParameterType.Bool);

        // --- Base Layer: locomoción 2D + muerte -----------------------------------------------
        AnimatorStateMachine baseSm = ac.layers[0].stateMachine;

        BlendTree moveTree = new BlendTree
        {
            name = "Locomotion",
            blendType = BlendTreeType.FreeformDirectional2D,
            blendParameter = P_MoveX,
            blendParameterY = P_MoveZ,
            useAutomaticThresholds = false
        };
        AddSubAsset(ac, moveTree);
        moveTree.AddChild(Clip($"{BlinkAnims}/Movement/Idle.fbx"), new Vector2(0f, 0f));
        moveTree.AddChild(Clip($"{BlinkAnims}/Movement/RunForward.fbx"), new Vector2(0f, 1f));
        moveTree.AddChild(Clip($"{BlinkAnims}/Movement/RunBackward.fbx"), new Vector2(0f, -1f));
        moveTree.AddChild(Clip($"{BlinkAnims}/Movement/RunLeft.fbx"), new Vector2(-1f, 0f));
        moveTree.AddChild(Clip($"{BlinkAnims}/Movement/RunRight.fbx"), new Vector2(1f, 0f));
        moveTree.AddChild(Clip($"{BlinkAnims}/Movement/RunBackwardLeft.fbx"), new Vector2(-0.7f, -0.7f));
        moveTree.AddChild(Clip($"{BlinkAnims}/Movement/RunBackwardRight.fbx"), new Vector2(0.7f, -0.7f));

        AnimatorState locomotion = baseSm.AddState("Locomotion", new Vector3(300, 100, 0));
        locomotion.motion = moveTree;
        baseSm.defaultState = locomotion;

        AnimatorState death = AddClipState(baseSm, "Death", Clip($"{BlinkAnims}/Combat/Death.fbx"), new Vector3(300, 250, 0));

        AnimatorStateTransition toDeath = baseSm.AddAnyStateTransition(death);
        ConfigureInstant(toDeath, 0.1f);
        toDeath.AddCondition(AnimatorConditionMode.If, 0, P_Dead);

        // Al reaparecer (Dead = false) vuelve a la locomoción
        AnimatorStateTransition revive = death.AddTransition(locomotion);
        ConfigureInstant(revive, 0.1f);
        revive.AddCondition(AnimatorConditionMode.IfNot, 0, P_Dead);

        // --- Upper Body: pala, piedra y golpe recibido (solo torso) ---------------------------
        AnimatorStateMachine upperSm = new AnimatorStateMachine { name = "UpperBody" };
        AddSubAsset(ac, upperSm);
        ac.AddLayer(new AnimatorControllerLayer
        {
            name = "UpperBody",
            stateMachine = upperSm,
            avatarMask = upperBody,
            blendingMode = AnimatorLayerBlendingMode.Override,
            defaultWeight = 1f
        });

        AnimatorState empty = upperSm.AddState("Empty", new Vector3(300, 100, 0));
        empty.writeDefaultValues = true;
        upperSm.defaultState = empty;

        AnimatorState attack = AddClipState(upperSm, "Attack_Shovel", Clip($"{BlinkAnims}/Combat/MeleeAttack_TwoHanded.fbx"), new Vector3(550, 0, 0));
        // Blink no trae "lanzar": PunchRight es un placeholder hasta conseguir un clip de throw.
        AnimatorState throwState = AddClipState(upperSm, "Throw_Stone", Clip($"{BlinkAnims}/Combat/PunchRight.fbx"), new Vector3(550, 100, 0));
        AnimatorState hit = AddClipState(upperSm, "Hit", Clip($"{BlinkAnims}/Combat/GetHit.fbx"), new Vector3(550, 200, 0));

        AddTriggeredAction(upperSm, attack, empty, P_Attack);
        AddTriggeredAction(upperSm, throwState, empty, P_Throw);
        AddTriggeredAction(upperSm, hit, empty, P_Hit);

        // Muerto: el torso suelta cualquier acción para que se vea la muerte del Base Layer
        AnimatorStateTransition deadClears = upperSm.AddAnyStateTransition(empty);
        ConfigureInstant(deadClears, 0.1f);
        deadClears.AddCondition(AnimatorConditionMode.If, 0, P_Dead);

        EditorUtility.SetDirty(ac);
    }

    // ---------------------------------------------------------------------------------------------
    // Enemigos (Skeleton / Zombie): misma máquina de estados, distintos clips y velocidades
    // ---------------------------------------------------------------------------------------------
    private struct EnemyClips
    {
        public AnimationClip idle, walk, run, attack, hit, death;
        public float walkSpeed, runSpeed, locomotionSpeed;
    }

    private static void BuildEnemy(string name, EnemyClips c)
    {
        AnimatorController ac = CreateController(name);
        ac.AddParameter(P_Speed, AnimatorControllerParameterType.Float);
        ac.AddParameter(P_Attack, AnimatorControllerParameterType.Trigger);
        ac.AddParameter(P_Hit, AnimatorControllerParameterType.Trigger);
        ac.AddParameter(P_Dead, AnimatorControllerParameterType.Bool);

        AnimatorStateMachine sm = ac.layers[0].stateMachine;

        // Speed = velocidad real en m/s (NavMeshAgent.velocity.magnitude)
        BlendTree moveTree = new BlendTree
        {
            name = "Locomotion",
            blendType = BlendTreeType.Simple1D,
            blendParameter = P_Speed,
            useAutomaticThresholds = false
        };
        AddSubAsset(ac, moveTree);
        moveTree.AddChild(c.idle, 0f);
        moveTree.AddChild(c.walk, c.walkSpeed);
        moveTree.AddChild(c.run, c.runSpeed);

        AnimatorState locomotion = sm.AddState("Locomotion", new Vector3(300, 100, 0));
        locomotion.motion = moveTree;
        locomotion.speed = c.locomotionSpeed;
        sm.defaultState = locomotion;

        AnimatorState attack = AddClipState(sm, "Attack", c.attack, new Vector3(550, 0, 0));
        AnimatorState hit = AddClipState(sm, "Hit", c.hit, new Vector3(550, 100, 0));
        AnimatorState death = AddClipState(sm, "Death", c.death, new Vector3(550, 250, 0));

        AddTriggeredAction(sm, attack, locomotion, P_Attack, P_Dead);
        AddTriggeredAction(sm, hit, locomotion, P_Hit, P_Dead);

        AnimatorStateTransition toDeath = sm.AddAnyStateTransition(death);
        ConfigureInstant(toDeath, 0.1f);
        toDeath.AddCondition(AnimatorConditionMode.If, 0, P_Dead);

        // Los enemigos salen del pool: al reactivarse el Animator vuelve al estado por defecto,
        // pero dejamos la salida explícita por si alguien desactiva/reactiva solo el Animator.
        AnimatorStateTransition revive = death.AddTransition(locomotion);
        ConfigureInstant(revive, 0f);
        revive.AddCondition(AnimatorConditionMode.IfNot, 0, P_Dead);

        EditorUtility.SetDirty(ac);
    }

    // ---------------------------------------------------------------------------------------------
    // Máscara del torso (Humanoid)
    // ---------------------------------------------------------------------------------------------
    private static AvatarMask BuildUpperBodyMask()
    {
        string path = $"{MasksDir}/UpperBody.mask";
        AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
        if (mask == null)
        {
            mask = new AvatarMask();
            AssetDatabase.CreateAsset(mask, path);
        }

        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
        {
            var part = (AvatarMaskBodyPart)i;
            bool upper = part == AvatarMaskBodyPart.Body
                      || part == AvatarMaskBodyPart.Head
                      || part == AvatarMaskBodyPart.LeftArm
                      || part == AvatarMaskBodyPart.RightArm
                      || part == AvatarMaskBodyPart.LeftFingers
                      || part == AvatarMaskBodyPart.RightFingers
                      || part == AvatarMaskBodyPart.LeftHandIK
                      || part == AvatarMaskBodyPart.RightHandIK;
            mask.SetHumanoidBodyPartActive(part, upper);
        }

        EditorUtility.SetDirty(mask);
        return mask;
    }

    // ---------------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Crea el controller o, si ya existe, lo vacía conservando el GUID (así no se rompen
    /// las referencias desde prefabs y escenas).
    /// </summary>
    private static AnimatorController CreateController(string name)
    {
        string path = $"{ControllersDir}/{name}.controller";
        AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (existing == null)
            return AnimatorController.CreateAnimatorControllerAtPath(path);

        foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (sub != null && sub != existing && !(sub is AnimatorController))
                Object.DestroyImmediate(sub, true);
        }

        existing.parameters = new AnimatorControllerParameter[0];
        var sm = new AnimatorStateMachine { name = "Base Layer" };
        AddSubAsset(existing, sm);
        existing.layers = new[]
        {
            new AnimatorControllerLayer { name = "Base Layer", stateMachine = sm, defaultWeight = 1f }
        };
        return existing;
    }

    private static void AddSubAsset(AnimatorController ac, Object obj)
    {
        obj.hideFlags = HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(obj, ac);
    }

    /// <summary>Busca el clip "real" dentro de un FBX (ignora tpose y __preview__).</summary>
    private static AnimationClip Clip(string fbxPath)
    {
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(fbxPath)
            .OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__") && c.name != "tpose")
            .FirstOrDefault();

        if (clip == null)
            Debug.LogError($"[Sepulturero] No se encontró un AnimationClip en {fbxPath}");
        return clip;
    }

    private static AnimatorState AddClipState(AnimatorStateMachine sm, string name, AnimationClip clip, Vector3 pos)
    {
        AnimatorState s = sm.AddState(name, pos);
        s.motion = clip;
        return s;
    }

    /// <summary>
    /// AnyState --(trigger)--> acción --(al terminar el clip)--> estado de vuelta.
    /// Si se pasa blockingBool, la acción no arranca mientras ese bool esté en true (p. ej. Dead).
    /// </summary>
    private static void AddTriggeredAction(AnimatorStateMachine sm, AnimatorState action, AnimatorState back,
                                           string trigger, string blockingBool = null)
    {
        AnimatorStateTransition enter = sm.AddAnyStateTransition(action);
        ConfigureInstant(enter, 0.05f);
        enter.canTransitionToSelf = true; // permite re-golpear o recibir golpes seguidos
        enter.AddCondition(AnimatorConditionMode.If, 0, trigger);
        if (!string.IsNullOrEmpty(blockingBool))
            enter.AddCondition(AnimatorConditionMode.IfNot, 0, blockingBool);

        AnimatorStateTransition exit = action.AddTransition(back);
        exit.hasExitTime = true;
        exit.exitTime = 0.9f;
        exit.hasFixedDuration = true;
        exit.duration = 0.15f;
    }

    private static void ConfigureInstant(AnimatorStateTransition t, float duration)
    {
        t.hasExitTime = false;
        t.hasFixedDuration = true;
        t.duration = duration;
        t.canTransitionToSelf = false;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
