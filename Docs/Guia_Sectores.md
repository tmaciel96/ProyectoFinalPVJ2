# El Turno del Sepulturero — Guía de armado por sector

Cada integrante del grupo arma **un sector completo**: decoración, luces, NPC, misión, enemigos, checkpoint y reja.
Los cinco sectores comparten el mismo terreno y cementerio de `01_GameLevel`; esta guía dice qué poner en cada uno y cómo configurarlo.

| Sector | Misión | NPC | Responsable |
|---|---|---|---|
| 1 | Ingredientes del caldero | Bruja | |
| 2 | Horda de zombis | Guardián Esqueleto | |
| 3 | Apagar las velas | Vampiro | |
| 4 | Espectros de las tumbas malditas | Niño Fantasma | |
| 5 | Ofrendas perdidas | Gato Negro | |

> Completá la columna **Responsable** y avisá en el grupo antes de empezar.

---

## 1. Reglas para no pisarnos

1. **Se trabaja en `Test_Misiones`**, nunca en `01_GameLevel`. Cuando un sector esté aprobado, una sola persona lo pasa al nivel final.
2. **Una escena de Unity no se puede mergear.** Si dos personas guardan `Test_Misiones` a la vez, se pierde el trabajo de una. Para evitarlo:
   - Armá tu sector **como prefab**: `Assets/_Project/Prefabs/Sectores/Sector_N_<Nombre>.prefab`. El prefab es tuyo y nadie más lo toca.
   - En la escena solo se suelta el prefab, en (0,0,0). Esa edición de la escena la hace **una persona por vez** (avisar en el grupo, commitear y pushear enseguida).
3. **No muevas nada de `- - - - COMÚN - - - -`** (terreno, cámara, HUD, GameManager, ObjectPooler, Player, portón principal, muros exteriores). Si necesitás cambiar algo de ahí, avisá.
4. Una rama por persona: `feature/sector-N-<nombre>`, con PR a `main`.
5. Diálogos, misiones y rejas usan scripts que ya existen. **No hace falta programar** para lo que dice esta guía; lo que todavía falta programar está marcado con 🔧.

---

## 2. Estructura de cada sector

Todo lo de tu sector va debajo de un único objeto vacío en (0,0,0), con esta estructura (la misma que ya tiene `MISIÓN 1` en la escena):

```
- - - -  MISIÓN N - <NPC> - - - -
  P E O P L E      ← el NPC
  P R O P S        ← decoración: lápidas, árboles, cercos, caminos
  V F X            ← partículas: niebla, fuego, brillos
  L I G H T I N G  ← luces del sector
  W A L L S        ← muros que separan este sector del siguiente
  M I S S I O N    ← objetos de la misión (ingredientes, velas, ofrendas...)
  E N E M I E S    ← enemigos y spawners
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
3. Se habla con un toque de **E** o **Espacio** a menos de 2,5 m (`Interaction Radius`). Al acercarte, el HUD muestra "Presiona [E] para hablar con …".
4. Opcional: un "!" sobre la cabeza en `Quest Available Marker` y un "?" en `Ready To Deliver Marker`.

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

### 3.5 Luces y ambiente
- **Sectores 1 y 2:** iluminados, luz de luna normal.
- **Sectores 3, 4 y 5:** en **penumbra** (decisión del grupo): pocas luces, bajas y localizadas. El jugador recorre estos sectores con el farol que le da el Vampiro.
- Cada sector con un **color propio** ayuda a orientarse (sugeridos abajo).
- Las luces con sombra son caras en WebGL: usá `Shadow Type = No Shadows` en las luces chicas.

### 3.6 Enemigos
- Van dentro de `E N E M I E S`.
- Hoy existen `Enemy_Zombie`, `Enemy_Skeleton` y `Enemy_CursedTomb` en `Prefabs/Enemies`. El zombi y el espectro definitivos son M4; mientras tanto se usan esos.
- Ponelos lejos del checkpoint: al reaparecer, el jugador no tiene que caer encima de un enemigo.

### 3.7 Runa en el portón principal
No se arma en el sector. Los 5 `RuneAltar` del portón están en `COMÚN`; el de tu misión se enciende solo cuando la misión se completa.

---

## 4. Sector por sector

### Sector 1 — La Bruja
- **Ambiente:** el claro de la bruja, el sector de entrada. Hongos, calabazas, escobas, un caldero burbujeante. Color: **verde**.
- **NPC:** `Woman_Witch_Halloween` · `Quest` = Bruja · `Npc Name` = Bruja · diálogos `Dialogo_Bruja_*`.
- **Decoración sugerida** (`Poly Halloween/Prefabs/Halloween`): `Witch_Hat`, `Broom_Old`, `Cauldron_Potion_Full`, `Book_Stand_Halloween`, `Book_Magical_Eye`, `Mushroom_Magical_*`, `Pumpkin_Carved_Lit_*`, `Spiderweb_*`.
- **Misión** (`M I S S I O N`):
  - El **caldero** al lado de la bruja (`Cauldron_Potion_Full`).
  - **3 ingredientes** repartidos por el sector, lejos uno del otro: un hongo luminoso (`Mushroom_Magical_Amanita_Mature`), una flor marchita y un frasco. Con una luz puntual chica encima de cada uno para que se vean.
  - 🔧 M3-1: el script que los recoge y los entrega al caldero. Por ahora ubicá los modelos donde van.
- **Enemigos:** pocos y fáciles, es el sector para aprender a pelear: 2 o 3 `Enemy_Zombie`.
- **Reja 1** → sector 2, con `Quest` = Bruja.

### Sector 2 — El Guardián Esqueleto
- **Ambiente:** un cementerio **abierto**, sin mausoleo, con muchas tumbas en filas. Color: **blanco hueso / gris**.
- **NPC:** un esqueleto de `ThirdParty/Skeleton` (sin script de enemigo) · `Quest` = Esqueleto · `Npc Name` = Guardián Esqueleto · diálogos `Dialogo_Esqueleto_*`.
- **Decoración sugerida:** `Gravestone_Old_*`, `Coffin_Old`, `Hole_Graveyard`, `Shovel_Gravedigger`, `Tree_Scary_Dead`, `Lantern_Street_Fixed_Halloween`.
- **Misión:**
  - Un **área despejada** entre las tumbas donde aparece la horda.
  - `CursedSpawner` con 4 a 6 puntos de aparición (`Spawn Points`) alrededor del área y `Trigger Radius` que cubra la entrada.
  - 🔧 M3-2 / M4-6: contar los zombis muertos y completar la misión cuando no queda ninguno.
- **Enemigos:** la horda de zombis débiles (`Enemy_Zombie`).
- **Reja 2** → sector 3, con `Quest` = Esqueleto.

### Sector 3 — El Vampiro
- **Ambiente:** el **mausoleo / capilla**, el único del mapa. Adentro, muchas velas encendidas; afuera, oscuridad. Color: **rojo oscuro / violeta**.
- **NPC:** sin modelo propio todavía (M5-11); usar `Man_Killer_Halloween` como provisorio · `Quest` = Vampiro · `Npc Name` = Vampiro · diálogos `Dialogo_Vampiro_*`.
- **Decoración sugerida:** `Coffin_New`, `Book_Evil`, `Candle_Skull_Lit`, `Candle_Stick_*_Lit`, `Candle_Spine_Lit`, `Spiderweb_*`, `Gibbet`.
- **Misión:**
  - **6 a 8 velas** encendidas dentro del mausoleo (`Candle_*_Lit`, con su `Point Light` y la partícula `FX-candle-fire`).
  - 🔧 M3-3: el script que las apaga con E. M3-4: al apagar todas, el sector baja a penumbra. M3-5: el Vampiro entrega el farol.
- **Enemigos:** pocos o ninguno adentro (es una misión de exploración). Alguna `Enemy_CursedTomb` afuera.
- **Reja 3** → sector 4, con `Quest` = Vampiro.

### Sector 4 — El Niño Fantasma
- **Ambiente:** las **tumbas malditas**: lápidas rotas, niebla densa, árboles secos. Penumbra. Color: **celeste / azul frío**.
- **NPC:** `Boy_Ghost_Halloween` · `Quest` = NinoFantasma · `Npc Name` = Niño Fantasma · diálogos `Dialogo_NinoFantasma_*`.
- **Decoración sugerida:** `Gravestone_Old_Classic_Broken`, `Gravestone_Old_Tower`, `Stump_Possesed`, `Tree_Scary_Dead`, `Scarecrow_*`, juguetes viejos o `Candy_*` cerca del niño.
- **Misión:**
  - Las tumbas malditas agrupadas en una zona, con `Enemy_CursedTomb` y un `CursedSpawner` para los espectros.
  - 🔧 M3-6 / M4-3: el espectro definitivo y el chequeo de "zona limpia".
- **Enemigos:** el combate más difícil del juego: espectros (más rápidos, más daño) y tumbas que disparan.
- **Reja 4** → sector 5, con `Quest` = NinoFantasma.

### Sector 5 — El Gato Negro
- **Ambiente:** el rincón más viejo del cementerio, junto al portón principal. Penumbra con ofrendas que brillan. Color: **violeta / dorado**.
- **NPC:** sin modelo propio todavía (M5-11); usar una caja o cápsula negra como provisorio · `Quest` = GatoNegro · `Npc Name` = Gato Negro · diálogos `Dialogo_Gato_*`.
- **Decoración sugerida:** `Lanterns_On_String_*`, `String_Of_Flags_*`, `Gravestone_Old_Cross`, `Candle_Finger_Holder_Lit`, `Pumpkin_Candy`.
- **Misión:**
  - **5 ofrendas** (recuerdos perdidos) escondidas entre las lápidas, cada una con una luz chica dorada.
  - 🔧 M3-7: el script que las recoge y las entrega al gato.
- **Enemigos:** pocos; el desafío es encontrar las ofrendas a oscuras.
- **Sin reja:** al completarse, se abre el **portón principal** (ya está en `COMÚN`).

---

## 5. Cómo probar tu sector

Las misiones todavía no tienen su mecánica (🔧), así que se completan desde el menú de Unity, con el juego en Play:

1. **Sepulturero › Misiones › Completar la actual** completa la misión que toca. Para probar tu sector sin jugar los anteriores, usalo una vez por cada sector anterior.
2. Hablá con tu NPC: tiene que salir `Pedido`; al terminar, la misión queda **En progreso**. Si le hablás de nuevo, sale `Recordatorio`.
3. **Completar la actual** de nuevo. Tiene que pasar todo esto:
   - Se enciende la runa de tu misión en el portón.
   - El HUD suma una runa.
   - Tu reja se abre.
   - El checkpoint del sector siguiente queda activo.
4. Hablá con el NPC otra vez: sale `Gracias`.
5. **Morí** dentro de tu sector (dejate pegar): tenés que reaparecer en tu checkpoint.
6. **Sepulturero › Misiones › Mostrar estado en consola** muestra el estado de las 5 misiones.

### Checklist antes del PR
- [ ] Todo el sector está debajo de su objeto `MISIÓN N` y guardado como prefab.
- [ ] El NPC tiene `NPCInteractable` en su propio objeto, con la misión y los 5 diálogos.
- [ ] Hay un único paso al sector siguiente y la reja lo bloquea (el jugador no pasa con la reja cerrada).
- [ ] El checkpoint tiene el número de sector correcto.
- [ ] La reja tiene asignado el checkpoint del sector siguiente.
- [ ] Ningún prop deja al jugador trabado (probá caminar por todo el sector).
- [ ] La consola no muestra errores en rojo.
- [ ] El commit no incluye cambios en `01_GameLevel` ni en escenas personales.
