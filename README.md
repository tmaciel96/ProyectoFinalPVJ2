# El Turno del Sepulturero

Proyecto final de **Programación 2 – UNICABA**. Juego 3D en Unity basado en el GDD "El Turno del Sepulturero".

## Versión de Unity

**Unity 6000.3.20f1** (Unity 6.3). Usar exactamente esta versión (Unity Hub > Installs > Install Editor > "Unity 6.3" / archive).
Otra versión puede reserializar escenas y generar conflictos en git.

- Render pipeline: **URP**
- Input: **Input System** 1.19.0
- NavMesh: **AI Navigation** 2.0.13

## Cómo abrir el proyecto

1. Instalar **Git LFS** (`git lfs install`) antes de clonar. Los png, fbx y otros binarios van por LFS.
2. `git clone https://github.com/tmaciel96/ProyectoFinalPVJ2.git`
3. Abrir la carpeta con Unity Hub usando la versión indicada arriba. La primera importación puede tardar varios minutos.
4. Abrir `Assets/_Project/Scenes/00_MainMenu.unity` y dar Play.

## Escenas

| Escena | Uso |
|---|---|
| `00_MainMenu` | Menú principal |
| `01_GameLevel` | Nivel jugable (escena compartida: coordinar antes de editarla) |
| `Tamy_Dev_Scene` | Escena personal de trabajo de Tamara |

Las dos escenas de build están en `File > Build Profiles` (00_MainMenu y 01_GameLevel).

## Reglas de trabajo en equipo

- Las escenas y prefabs se guardan en **texto YAML** (Edit > Project Settings > Editor > Asset Serialization = Force Text). No cambiar.
- **No editar la misma escena a la vez**: cada uno trabaja en su escena de desarrollo o en prefabs y se integra después.
- El **TerrainData** (`Assets/_Project/Terrain/`) es binario y no se puede mergear: solo una persona lo modifica por vez.
- Mover o renombrar assets siempre **desde Unity**, nunca desde el explorador, para no perder los `.meta`.
- Subir siempre los `.meta` junto con los archivos.

## Estructura

```
Assets/
  _Project/      Código, prefabs, escenas, terreno, iluminación propios
  ThirdParty/    Packages de terceros (ver CREDITS.md)
  TextMesh Pro/
```

## Créditos

Ver [CREDITS.md](CREDITS.md).
