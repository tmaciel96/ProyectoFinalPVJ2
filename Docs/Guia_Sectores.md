# El Turno del Sepulturero — Guía de armado por sector

Cada integrante del grupo arma **un sector completo**: decoración, luces, NPC, misión, enemigos, checkpoint y reja.
Los cinco sectores comparten el mismo terreno y cementerio de `01_GameLevel`; esta guía dice qué poner en cada uno y cómo configurarlo.

| Sector | Misión | NPC | Componente de misión | Responsable |
|---|---|---|---|---|
| 1 | Ingredientes del caldero | Bruja | `QuestCollection` | |
| 2 | Horda de zombis | Guardián Esqueleto | `AreaClearQuest` | |
| 3 | Apagar las velas | Vampiro | `CandleQuest` | |
| 4 | Espectros de las tumbas malditas | Niño Fantasma | `AreaClearQuest` | |
| 5 | Ofrendas perdidas | Gato Negro | `QuestCollection` | |

> Completá la columna **Responsable** y avisá en el grupo antes de empezar.

---

## 1. Reglas para no pisarnos

1. **Se trabaja en `Test_Misiones`**, nunca en `01_GameLevel`. Cuando un sector esté aprobado, una sola persona lo pasa al nivel final.
2. **Una escena de Unity no se puede mergear.** Si dos personas guardan `Test_Misiones` a la vez, se pierde el trabajo de una. Para evitarlo:
   - Armá tu sector **como prefab**: `Assets/_Project/Prefabs/Sectores/Sector_N_<Nombre>.prefab`. El prefab es tuyo y nadie más lo toca.
   - En la escena solo se suelta el prefab, en (0,0,0). Esa edición de la escena la hace **una persona por vez** (avisar en el grupo, commitear y pushear enseguida).
3. **No muevas nada de `- - - - COMÚN - - - -`** (terreno, cámara, HUD, GameManager, QuestManager, ObjectPooler, Player, portón principal, muros exteriores). Si necesitás cambiar algo de ahí, avisá.
4. Una rama por persona: `feature/sector-N-<nombre>`, con PR a `main`.
5. Diálogos, misiones, enemigos y rejas usan scripts que ya existen. **No hace falta programar** para lo que dice esta guía; lo que todavía falta programar está marcado con 🔧.

---

## 2. Estructura de cada sector

Todo lo de tu sector va debajo de un único objeto vacío en (0,0,0), con esta estructura (la misma que ya tiene `MISIÓN 1` en la escena):

```
- - - -  MISIÓN N - <NPC> - - - -
  P E O P L E      ← el NPC (con su componente de misión)
  P R O P S        ← decoración: lápidas, árboles, cercos, caminos
  V F X            ← partículas: niebla, fuego, brillos
  L I G H T I N G  ← luces del sector
  W A L L S        ← muros que separan este sector del siguiente
  M I S S I O N    ← objetos de la misión (ingredientes, velas, ofrendas...)
  E N E M I E S    ← enemigos sueltos y CursedSpawner
  Checkpoint_SN    ← punto de reaparición a la entrada
  Reja_SN          ← reja hacia el sector siguiente (no hay en el sector 5)
```

**Mover objetos de padre no les cambia la posición en el mundo** si los padres están en (0,0,0), así que podés tomar props y luces de los grupos generales (`P R O P S`, `L I G H T I N G`, `N A T U R E`) y pasarlos a tu sector.

---

## 3. Pasos comunes a todos los sectores

### 3.1 Límites
- El sector queda cerrado por muros, con **un único paso** hacia el sector siguiente: ahí va la reja.
- Usá los muros de `W A L L S` (duplicándolos) o cipreses/cercos con collider. Sin collider, el jugador pasa caminando.

### 3.2 NPC
1. Arrastrá el modelo dentro de `P E O P L E` (ver el modelo sugerido en cada sector).
2. **En el mismo objeto del modelo** (no en la carpeta), *Add Component* → `NPCInteractable`:
   - `Quest`: la misión del sector.
   - `Npc Name`: el nombre que aparece en el diálogo.
   - Los 5 diálogos, desde `Assets/_Project/Data/Dialogos`:

     | Campo | Asset |
     |---|---|
     | Locked Dialogue | `Dialogo_<NPC>_Bloqueado` |
     | Offer Dialogue | `Dialogo_<NPC>_Pedido` |
     | Reminder Dialogue | `Dialogo_<NPC>_Recordatorio` |
     | Delivery Dialogue | `Dialogo_<NPC>_Entrega` |
     | Thanks Dialogue | `Dialogo_<NPC>_Gracias` |
3. **En ese mismo objeto**, el componente de misión de la tabla de arriba (`QuestCollection`, `AreaClearQuest` o `CandleQuest`), con la misma `Quest`. Es el que decide cuándo el NPC acepta la entrega; sin él, el NPC nunca completa la misión.
4. Se habla con un toque de **E** o **Espacio** a menos de 2,5 m (`Interaction Radius`). Al acercarte, el HUD muestra "Presiona [E] para hablar con …".
5. Opcional: un "!" sobre la cabeza en `Quest Available Marker` y un "?" en `Ready To Deliver Marker`.
6. Opcional: un texto del HUD en `Progress Text` del componente de misión muestra el avance ("Ingredientes: 1 / 3").

### 3.3 Checkpoint
1. Objeto vacío `Checkpoint_SN` a la **entrada** del sector, con un **Box Collider** que cubra el paso, y el componente `Checkpoint`.
2. `Sector` = N − 1 (Bruja 0, Esqueleto 1, Vampiro 2, Niño 3, Gato 4).
3. `Respawn Point`: un hijo vacío dentro del sector, mirando hacia adentro (la flecha azul es el frente). Vacío = usa el mismo objeto.
4. Solo en el sector 1: tildar `Active On Start` y usar el `SpawnPoint` como `Respawn Point`.

### 3.4 Reja hacia el sector siguiente (sectores 1 a 4)
1. Objeto vacío `Reja_SN` con el componente `QuestGate`, en el paso hacia el sector siguiente.
2. Adentro, la hoja de la reja (por ejemplo `gate-graveyard-door-l`), con un **Box Collider no trigger**.
3. En `QuestGate`:
   - `Quest`: la misión de **este** sector.
   - `Gate Leaf`: la hoja.
   - `Open Rotation` (0, 100, 0) para que gire; si abre para el lado equivocado, (0, −100, 0). Para que se deslice, usar `Open Offset`.
   - `Next Sector Checkpoint`: el `Checkpoint` del sector **siguiente** (se activa solo al abrirse). Como ese checkpoint está en el prefab de otra persona, este campo **se asigna en la escena** cuando los dos prefabs ya están puestos, no dentro del prefab.
4. Si la reja tiene dos hojas (`door-l` y `door-r`), cada una lleva su propio `QuestGate` con la misma `Quest` y rotaciones opuestas. `Next Sector Checkpoint` va en una sola.
5. La reja cerrada tiene que cortar el NavMesh (🔧 M4-8), así los zombis no la atraviesan.

### 3.5 Luces y ambiente
- **Sectores 1 y 2:** iluminados, luz de luna normal.
- **Sectores 3, 4 y 5:** en **penumbra**. La penumbra la hace `PenumbraFader` al apagar las velas del Vampiro (sector 3); desde ahí el jugador recorre el resto con el farol (`PlayerLantern`, ya está en el `Player.prefab`).
- Cada sector con un **color propio** ayuda a orientarse (sugeridos abajo).
- Las luces con sombra son caras en WebGL: usá `Shadow Type = No Shadows` en las luces chicas.

### 3.6 Enemigos

#### Los prefabs (`Prefabs/Enemies`)

| Prefab | Script | Vida / daño | Comportamiento |
|---|---|---|---|
| `Enemy_Zombie` | `ZombieEnemy` | 60 / 20 | Lento, camina a los tumbos. Usa el NavMesh. |
| `Enemy_Skeleton` | `ZombieEnemy` | 45 / 15 | Como el zombi, un poco más rápido y frágil. |
| `Enemy_Espectro` | `SpectreEnemy` | 40 / 35 | Flota sobre el terreno, rápido, empuja al jugador al pegarle. No usa NavMesh. |
| `Enemy_CursedTomb` | `CursedTomb` | — | Tumba fija que dispara `Bullet_DarkOrb`. |

Todos patrullan (entre `Patrol Points` o al azar dentro de `Patrol Radius`), persiguen al jugador cuando entra en `Detection Radius` y vuelven si se aleja más de `Lose Aggro Radius`. Al golpearlos parpadean, retroceden y se aturden un instante. Los valores se ajustan en el prefab; si los cambiás para tu sector, hacelo en la instancia (override), no en el prefab.

> Los prefabs se regeneran con **Sepulturero › Crear prefabs de enemigos**. Ojo: el menú pisa los valores de vida, daño y velocidad del prefab.

#### Enemigos sueltos
- Arrastrá el prefab dentro de `E N E M I E S`. Si la misión del sector tiene que contarlo (sectores 2 y 4), agregale el componente `QuestEnemy`.
- Ponelos lejos del checkpoint: al reaparecer, el jugador no tiene que caer encima de un enemigo.

#### Spawners (`CursedSpawner`)
Para grupos y hordas. Objeto vacío dentro de `E N E M I E S` con el componente `CursedSpawner`:

| Campo | Qué hace |
|---|---|
| `Trigger On Proximity` / `Trigger Radius` | Aparecen cuando el jugador entra en el radio. **Destildalo** si el spawner lo dispara una misión (sectores 2 y 4). |
| `Enemies To Spawn` / `Spawn Interval` | Cuántos y cada cuántos segundos. |
| `Spawn Points` | Puntos de aparición (hijos vacíos), en orden. Vacío = al azar dentro de `Spawn Radius`. |
| `Enemy Prefabs` | **Lo recomendado:** arrastrá acá `Enemy_Zombie`, `Enemy_Skeleton` y/o `Enemy_Espectro`. Cada prefab arma su propio pool, que crece solo. |
| `Pick Mode` | `Random` o `InOrder` entre los prefabs de la lista. |
| `Enemy Pool Tags` | Solo si `Enemy Prefabs` está vacío: usa los pools del `ObjectPooler` de la escena (`Enemy_Zombie`, `Enemy_Skeleton`, `Enemy_Espectro`). Si un tag no existe en la escena, se ignora. |
| `On Area Cleared` | Evento al morir el último enemigo de este spawner. |

Todo lo que crea un spawner lleva `QuestEnemy`, así que las misiones de limpiar el área lo cuentan solas. Con el spawner seleccionado se ven los gizmos: violeta el radio de activación, naranja el área o los puntos de aparición.

#### NavMesh
Zombis y esqueletos caminan sobre el NavMesh (`NavMeshSurface` en `COMÚN`). Si en tu sector no hay NavMesh cerca, igual se mueven, pero en línea recta y atravesando props. 🔧 M4-8: el NavMesh se reconstruye cuando el nivel esté armado; no lo bakees en tu prefab.

### 3.7 Runa en el portón principal
No se arma en el sector. Los 5 `RuneAltar` del portón están en `COMÚN`; el de tu misión se enciende solo cuando la misión se completa.

### 3.8 Prefabs de misión (`Prefabs/Items`)
Se regeneran con **Sepulturero › Crear prefabs de ítems de misión**.

| Prefab | Para | Componente |
|---|---|---|
| `Item_Ingrediente_01` / `02` / `03` | Bruja | `QuestItem` (Quest = Bruja) |
| `Item_Caldero` | Bruja | `QuestStateSwap`: pasa de vacío a lleno con burbujas al completar |
| `Item_Vela_Altar` | Vampiro | `AltarCandle` |
| `Item_Farol` | Vampiro | Modelo del farol (ya colgado del Player) |
| `Item_Ofrenda_Gato` | Gato | `QuestItem` (Quest = GatoNegro) |

Los `QuestItem` se juntan caminando por encima, **solo con la misión En progreso**. Con `Hide Until Quest Starts` quedan ocultos hasta que el NPC da la misión.

---

## 4. Sector por sector

### Sector 1 — La Bruja
- **Ambiente:** el claro de la bruja, el sector de entrada. Hongos, calabazas, escobas, un caldero burbujeante. Color: **verde**.
- **NPC:** `Woman_Witch_Halloween` · `Quest` = Bruja · `Npc Name` = Bruja · diálogos `Dialogo_Bruja_*` · `QuestCollection` (Quest = Bruja, Item Label = Ingredientes).
- **Decoración sugerida** (`Poly Halloween/Prefabs/Halloween`): `Witch_Hat`, `Broom_Old`, `Book_Stand_Halloween`, `Mushroom_Magical_*`, `Pumpkin_Carved_Lit_*`, `Spiderweb_*`.
- **Misión** (`M I S S I O N`):
  - `Item_Caldero` al lado de la bruja.
  - `Item_Ingrediente_01`, `02` y `03` repartidos por el sector, lejos uno del otro.
  - `Required` en 0 cuenta solos los ingredientes que haya en la escena.
- **Enemigos:** pocos y fáciles, es el sector para aprender a pelear: 2 o 3 `Enemy_Zombie` sueltos o un spawner con `Trigger On Proximity`.
- **Reja 1** → sector 2, con `Quest` = Bruja.

### Sector 2 — El Guardián Esqueleto
- **Ambiente:** un cementerio **abierto**, sin mausoleo, con muchas tumbas en filas. Color: **blanco hueso / gris**.
- **NPC:** un esqueleto de `ThirdParty/Skeleton` (sin script de enemigo) · `Quest` = Esqueleto · `Npc Name` = Guardián Esqueleto · diálogos `Dialogo_Esqueleto_*` · `AreaClearQuest` (Quest = Esqueleto, Enemy Label = Zombis).
- **Decoración sugerida:** `Gravestone_Old_*`, `Coffin_Old`, `Hole_Graveyard`, `Shovel_Gravedigger`, `Tree_Scary_Dead`, `Lantern_Street_Fixed_Halloween`.
- **Misión:**
  - Un **área despejada** entre las tumbas donde aparece la horda.
  - Uno o más `CursedSpawner` con `Enemy Prefabs` = `Enemy_Zombie` (y algún `Enemy_Skeleton`), 4 a 6 `Spawn Points` alrededor del área y **`Trigger On Proximity` destildado**.
  - En `AreaClearQuest`: los spawners en `Activate On Quest Start`, `Area Center` en el medio del área y `Area Radius` que la cubra. Los spawners se dejan **apagados** en la escena: los prende la misión cuando el Guardián la da.
  - `Required Kills` en 0 suma el `Enemies To Spawn` de esos spawners.
- **Enemigos:** la horda de zombis débiles.
- **Reja 2** → sector 3, con `Quest` = Esqueleto.

### Sector 3 — El Vampiro
- **Ambiente:** el **mausoleo / capilla**, el único del mapa. Adentro, muchas velas encendidas; afuera, oscuridad. Color: **rojo oscuro / violeta**.
- **NPC:** sin modelo propio todavía (M5-11); usar `Man_Killer_Halloween` como provisorio · `Quest` = Vampiro · `Npc Name` = Vampiro · diálogos `Dialogo_Vampiro_*` · `CandleQuest` (Quest = Vampiro).
- **Decoración sugerida:** `Coffin_New`, `Book_Evil`, `Candle_Skull_Lit`, `Candle_Stick_*_Lit`, `Candle_Spine_Lit`, `Spiderweb_*`, `Gibbet`.
- **Misión:**
  - **6 a 8 `Item_Vela_Altar`** dentro del mausoleo. Se apagan con **E**, solo con la misión En progreso.
  - Un `PenumbraFader` (Quest = Vampiro, Trigger = AllCandlesOut) en `L I G H T I N G`, con la luna en `Moon Light`: al apagar la última vela oscurece todo de a poco.
  - Al completar la misión el sepulturero recibe el farol (`PlayerLantern`), no hay que armar nada.
- **Enemigos:** pocos o ninguno adentro (es una misión de exploración). Alguna `Enemy_CursedTomb` afuera.
- **Reja 3** → sector 4, con `Quest` = Vampiro.

### Sector 4 — El Niño Fantasma
- **Ambiente:** las **tumbas malditas**: lápidas rotas, niebla densa, árboles secos. Penumbra. Color: **celeste / azul frío**.
- **NPC:** `Boy_Ghost_Halloween` · `Quest` = NinoFantasma · `Npc Name` = Niño Fantasma · diálogos `Dialogo_NinoFantasma_*` · `AreaClearQuest` (Quest = NinoFantasma, Enemy Label = Espectros).
- **Decoración sugerida:** `Gravestone_Old_Classic_Broken`, `Gravestone_Old_Tower`, `Stump_Possesed`, `Tree_Scary_Dead`, `Scarecrow_*`, juguetes viejos o `Candy_*` cerca del niño.
- **Misión:**
  - Las tumbas malditas agrupadas en una zona, con algunas `Enemy_CursedTomb`.
  - `CursedSpawner` con `Enemy Prefabs` = `Enemy_Espectro`, `Trigger On Proximity` destildado, apagado en la escena y asignado en `Activate On Quest Start` de la `AreaClearQuest` (igual que el sector 2).
  - Las tumbas no cuentan para la misión salvo que les agregues `QuestEnemy`.
- **Enemigos:** el combate más difícil del juego: espectros (más rápidos, más daño, empujan) y tumbas que disparan.
- **Reja 4** → sector 5, con `Quest` = NinoFantasma.

### Sector 5 — El Gato Negro
- **Ambiente:** el rincón más viejo del cementerio, junto al portón principal. Penumbra con ofrendas que brillan. Color: **violeta / dorado**.
- **NPC:** sin modelo propio todavía (M5-11); usar una caja o cápsula negra como provisorio · `Quest` = GatoNegro · `Npc Name` = Gato Negro · diálogos `Dialogo_Gato_*` · `QuestCollection` (Quest = GatoNegro, Item Label = Ofrendas).
- **Decoración sugerida:** `Lanterns_On_String_*`, `String_Of_Flags_*`, `Gravestone_Old_Cross`, `Candle_Finger_Holder_Lit`, `Pumpkin_Candy`.
- **Misión:**
  - **5 `Item_Ofrenda_Gato`** escondidas entre las lápidas.
- **Enemigos:** pocos; el desafío es encontrar las ofrendas a oscuras.
- **Sin reja:** al completarse, se abre el **portón principal** (ya está en `COMÚN`).

---

## 5. Cómo probar tu sector

Con el juego en Play, el menú **Sepulturero › Misiones** sirve para saltear los sectores anteriores:

1. **Completar la actual** completa la misión que toca. Para probar tu sector sin jugar los anteriores, usalo una vez por cada sector anterior.
2. Hablá con tu NPC: tiene que salir `Pedido`; al terminar, la misión queda **En progreso**. Si le hablás de nuevo, sale `Recordatorio`.
3. **Jugá la misión** (juntá los ítems, apagá las velas o matá a todos). Al terminar, el NPC muestra el "?" y al hablarle sale `Entrega`. Si querés saltearla, **Completar la actual** de nuevo.
4. Al completarse tiene que pasar todo esto:
   - Se enciende la runa de tu misión en el portón.
   - El HUD suma una runa.
   - Tu reja se abre.
   - El checkpoint del sector siguiente queda activo.
5. Hablá con el NPC otra vez: sale `Gracias`.
6. **Morí** dentro de tu sector (dejate pegar): tenés que reaparecer en tu checkpoint.
7. **Mostrar estado en consola** muestra el estado de las 5 misiones; **Reiniciar misiones** vuelve todo al principio.

Si un spawner no crea nada, la consola avisa `no pudo crear ningún enemigo`: asigná `Enemy Prefabs`.

### Checklist antes del PR
- [ ] Todo el sector está debajo de su objeto `MISIÓN N` y guardado como prefab.
- [ ] El NPC tiene `NPCInteractable` y el componente de misión en su propio objeto, con la misma `Quest` y los 5 diálogos.
- [ ] La misión se puede completar jugando, sin el menú.
- [ ] Los spawners tienen `Enemy Prefabs` asignado; los de la misión están apagados y en `Activate On Quest Start`.
- [ ] Hay un único paso al sector siguiente y la reja lo bloquea (el jugador no pasa con la reja cerrada).
- [ ] El checkpoint tiene el número de sector correcto y ningún enemigo cerca.
- [ ] La reja tiene asignado el checkpoint del sector siguiente.
- [ ] Ningún prop deja al jugador trabado (probá caminar por todo el sector).
- [ ] La consola no muestra errores en rojo.
- [ ] El commit no incluye cambios en `01_GameLevel` ni en escenas personales.
