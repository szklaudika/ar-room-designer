# Mobile AR Room Designer

Mobilná AR aplikácia pre Android, v ktorej si používateľ naskenuje izbu a rozmiestni v nej virtuálny nábytok.
Tímový školský projekt v Unity 6 s AR Foundation 6 a ARCore.

Hotové:
1. **Detekcia plôch**: podlaha, steny a stoly/police
2. **Nábytok**: stôl, stolička, gauč, posteľ, lampa (zatiaľ jednoduché zástupné modely v reálnej veľkosti)
3. **Obrazy na stenách**: vlastné obrázky, rám sa prispôsobí pomeru strán
4. **Virtuálne okno**: výhľad na more, hory alebo mesto s paralaxou (úroveň 1)
5. **Ukotvenie**: každý objekt je pripnutý k AR kotve, aby sa neposúval
6. **Uloženie návrhu**: fotka izby bez UI a plôch, galéria priamo v aplikácii

Okno ako portál s 3D scénou (úroveň 2) **nie je hotové**. Výhľad je textúra s paralaxou.

## Požiadavky

- **Unity 6000.3.x** (projekt je vytvorený v 6000.3.10f1) s modulom **Android Build Support** (vrátane OpenJDK a Android SDK & NDK)
- **Android zariadenie s podporou ARCore** ([zoznam zariadení](https://developers.google.com/ar/devices)) a nainštalovanými *Google Play Services for AR*
- **Git** a **Git LFS** (obrázky, modely a zvuky sú uložené cez LFS)

## Naklonovanie a otvorenie projektu

```bash
git lfs install          # stačí raz na počítači
git clone https://github.com/szklaudika/ar-room-designer.git
cd ar-room-designer
git lfs pull             # pre istotu stiahne všetky LFS súbory
```

1. Otvor **Unity Hub → Add → Add project from disk** a vyber priečinok `ar-room-designer`.
2. Otvor projekt vo verzii 6000.3.x. Prvé otvorenie trvá dlhšie, pretože sa vytvára priečinok `Library/`.

## Vytvorenie scény

Scéna sa skladá automaticky Editor skriptom:

1. V Unity klikni **Tools → Room Designer → Vytvoriť / aktualizovať scénu**.
2. Vytvorí sa (alebo prepíše):
   - `Assets/_RoomDesigner/Scenes/RoomDetection.unity` obsahuje:
     - AR Session
     - XR Origin (Mobile AR) so zadnou kamerou
     - ARPlaneManager, ARRaycastManager, ARAnchorManager, RoomPlaneClassifier
     - UI: stav skenovania hore, tlačidlá vpravo, záložky a zoznam dole, galéria
     - objekt *Room Designer* s PlacementManager, DesignerMenu, SaveDesignScreenshot, DesignGallery a PlaneVisibilityToggle
   - `Prefabs/RoomPlane.prefab`: vizualizácia AR plochy
   - `Prefabs/Placeables/*.prefab`: nábytok, `Painting` (obraz), `Window` (okno)
   - `Data/`: `PlaceableCatalog` (nábytok), `PaintingLibrary` (obrazy), `WindowViewLibrary` (výhľady)
   - `Paintings/` a `WindowViews/`: zástupné obrázky (len pri prvom spustení)
   - `Materials/*.mat`
3. Scéna sa pridá do Build Settings ako prvá.

> Menu stačí spustiť **raz** jednému človeku, ktorý vytvorené súbory commitne. Ostatní si ich len stiahnu (pull).
> Ak by ho spustil každý zvlášť, vzniknú konflikty.
> Menu scénu a zástupné prefaby **prepíše**. Ručné úpravy v nich sa stratia, preto ich rob radšej v Editor skripte.
> Vlastné položky v katalógu a knižniciach ostanú zachované.

## Ovládanie na mobile

| Čo | Ako |
|---|---|
| Skenovanie | Pomaly prejdi mobilom po izbe. **Zelená** = podlaha, **žltá** = stôl/polica, **modrá** = stena, **sivá** = strop. |
| Pridanie objektu | Dole vyber záložku **Nábytok / Obrazy / Okná**, ťukni na položku (zostane oranžová) a potom na plochu v izbe. Nábytok ide len na vodorovnú plochu (podlaha, stôl), obraz a okno len na stenu. |
| Výber | Ťukni na objekt, okolo neho sa ukáže oranžový obrys. Ťuknutím do prázdna výber zrušíš. |
| Posun | Vybraný objekt ťahaj jedným prstom. Nábytok sa posúva po podlahe, obraz a okno po tej istej stene. |
| Veľkosť | Roztiahnutie alebo stiahnutie dvoma prstami (0,3× – 3×). |
| Otočenie | Krúženie dvoma prstami (len nábytok). |
| Zmazať | Červené tlačidlo, ktoré sa ukáže pri vybranom objekte. |
| Skryť plochy | Skryje farebné plochy. Umiestňovať sa dá ďalej. |
| Vymazať všetko | Treba ťuknúť dvakrát (potvrdenie). |
| Uložiť | Odfotí izbu s nábytkom bez UI, plôch a obrysu. |
| Galéria | Uložené fotky, najnovšie prvé. Ťuknutím sa fotka zväčší, dá sa zmazať (dve ťuknutia). |

Fotky sú v mobile v `Android/data/<balík aplikácie>/files/Navrhy/navrh_rrrrMMdd_HHmmss.png`
(`Application.persistentDataPath/Navrhy`). Po odinštalovaní aplikácie sa zmažú.

**Posun objektov (drift):** kotvy ho výrazne zmenšia, ale pri rýchlom pohybe sa tracking môže na chvíľu stratiť.
Pomáha pomalý pohyb, dobré svetlo a povrch so vzorom.

## Pridanie vlastného obrazu

1. Obrázok (`.png`, `.jpg`) skopíruj do `Assets/_RoomDesigner/Paintings/`. Pomer strán sa zachová.
2. Klikni **Tools → Room Designer → Vytvoriť / aktualizovať scénu**. Obraz sa pridá do `Data/PaintingLibrary`, názov je podľa súboru.
   Bez prepísania scény: otvor `Data/PaintingLibrary` a riadok (názov + textúra) pridaj ručne.

Výhľad z okna sa pridáva rovnako: priečinok `WindowViews/`, knižnica `Data/WindowViewLibrary`. Najlepší je obrázok na šírku alebo štvorec.

## Pridanie nového nábytku

1. Vlož model (`.fbx`, `.obj`) do `Assets/_RoomDesigner/Models/`. Ide do Git LFS automaticky.
2. Sprav z neho prefab (mimo názvov zástupných prefabov v `Prefabs/Placeables/`, tie sa prepisujú):
   - na koreni je **PlaceableObject** (*Category* = Nabytok, *Highlight Material* = `Materials/Highlight`) a **BoxCollider** okolo celého modelu
   - bez Rigidbody
   - pivot je dole v strede
   - predná strana smeruje na **+Z**
   - mierka 1 jednotka = 1 meter
3. V `Data/PlaceableCatalog` pridaj riadok (ID, názov, prefab). Ak chceš nahradiť zástupný model, pri jeho riadku len vymeň *Prefab*. Menu tvoju zmenu zachová.

## Build na Android

1. **File → Build Profiles → Android → Switch Platform**.
2. **Edit → Project Settings → XR Plug-in Management → záložka Android** → zaškrtnuté **Google ARCore**.
3. **Edit → Project Settings → Player → Android → Other Settings**:
   - **Rendering:** vypni *Auto Graphics API* a zo zoznamu odstráň **Vulkan**, nechaj iba **OpenGLES3**
   - **Scripting Backend:** IL2CPP
   - **Target Architectures:** iba **ARM64**
   - **Minimum API Level:** Android 7.0 „Nougat“ (**API level 24**)
4. Pripoj mobil cez USB (zapnuté *Ladenie USB* v možnostiach pre vývojárov).
5. V Build Profiles klikni **Build And Run**. Ako miesto pre `.apk` vyber priečinok `Builds/` (je v `.gitignore`), nie koreň projektu.

Logy z mobilu: **Window → Analysis → Android Logcat**.

## Štruktúra priečinkov

```
Assets/_RoomDesigner/
├── Editor/
│   ├── RoomDetectionSceneSetup.cs     menu Tools → Room Designer (scéna + UI)
│   ├── RoomDesignerAssetBuilder.cs    materiály, prefaby, katalóg, knižnice
│   └── PlaceholderTextures.cs         zástupné obrázky a nastavenie ich importu
├── Scripts/
│   ├── Detection/
│   │   ├── RoomPlaneClassifier.cs     podlaha vs. stôl/polica vs. stena (podľa výšky)
│   │   ├── PlaneColorByAlignment.cs   farba plochy podľa druhu
│   │   ├── RoomScanStatus.cs          text so stavom skenovania
│   │   └── PlaneVisibilityToggle.cs   skrytie/zobrazenie plôch
│   ├── Placement/
│   │   ├── PlacementManager.cs        umiestnenie, kotvy, výber, gestá
│   │   ├── PlaceableObject.cs         koreň každého objektu (kategória, obrys)
│   │   ├── PlaceableCatalog.cs        zoznam nábytku
│   │   ├── TextureLibrary.cs          základ pre PaintingLibrary a WindowViewLibrary
│   │   ├── TextureApplier.cs          základ pre WallPainting a WindowView
│   │   ├── WallPainting.cs            obraz s rámom
│   │   └── WindowView.cs              výhľad z okna s paralaxou
│   ├── Save/
│   │   ├── SaveDesignScreenshot.cs    uloženie fotky
│   │   └── DesignGallery.cs           galéria
│   └── UI/
│       └── DesignerMenu.cs            záložky a zoznam položiek
├── Paintings/   obrázky obrazov (sem patria vlastné)
├── WindowViews/ výhľady z okna
├── Scenes/      RoomDetection.unity                     (vytvorí Editor skript)
├── Prefabs/     RoomPlane, Placeables/                  (vytvorí Editor skript)
├── Data/        katalóg a knižnice                      (vytvorí Editor skript)
└── Materials/                                           (vytvorí Editor skript)
```

Vlastné súbory dávame len do `Assets/_RoomDesigner/`. Priečinky šablóny (`MobileARTemplateAssets`, `Samples`, `XR`, `TextMesh Pro` …) needitujeme.

## Pravidlá pre tím

- **Každý robí na vlastnej vetve** pomenovanej `feature/nazov`, napr. `feature/ui-menu`, `feature/nabytok`.
  ```bash
  git checkout main
  git pull
  git checkout -b feature/nazov
  ```
- **Do `main` sa dostane zmena iba cez Pull Request** na GitHube. Druhý člen tímu ho pozrie a schváli.
- **Scénu `RoomDetection.unity` nikdy needitujú dvaja ľudia naraz.** Unity scény sa zle mergujú. Pred úpravou scény daj vedieť ostatným. Ak potrebuješ niečo vlastné, sprav si to ako prefab.
- Commituj vždy aj `.meta` súbory. Bez nich sa rozbijú odkazy medzi assetmi.
- Pred začatím práce si vždy stiahni najnovší `main` (`git pull`).
- Commit správy píšeme krátko a po slovensky, napr. „Pridané menu na výber nábytku“.
