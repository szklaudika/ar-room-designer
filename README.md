# Mobile AR Room Designer

Mobilná AR aplikácia pre Android, v ktorej si používateľ naskenuje izbu a rozmiestni v nej virtuálny nábytok.
Tímový školský projekt v Unity 6 s AR Foundation 6 a ARCore.

Hotové (základná verzia):
1. **Detekcia plôch**: podlaha, steny a stoly/police
2. **Nábytok**: stôl, stolička, gauč, posteľ, lampa (zatiaľ jednoduché zástupné modely v reálnej veľkosti)
3. **Obrazy na stenách**: abstrakt a krajina
4. **Virtuálne okno**: výhľad von s paralaxou
5. **Uloženie návrhu**: JSON v mobile, dá sa znova načítať

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

1. V Unity klikni **Tools → Room Designer → Vytvoriť scénu**.
2. Vytvorí sa (alebo prepíše):
   - `Assets/_RoomDesigner/Scenes/RoomDetection.unity` obsahuje:
     - AR Session
     - XR Origin (Mobile AR) so zadnou kamerou
     - ARPlaneManager, ARRaycastManager, ARAnchorManager, RoomPlaneClassifier
     - UI: stav skenovania hore, menu dole
     - objekt *Room Designer* s komponentmi RoomDesignerController a PlaneVisibilityToggle
   - `Prefabs/RoomPlane.prefab`: vizualizácia AR plochy
   - `Prefabs/Placeables/*.prefab`: nábytok, obrazy, okno
   - `Data/PlaceableCatalog.asset`: zoznam objektov v menu
   - `Materials/*.mat`
3. Scéna sa pridá do Build Settings ako prvá.

> Menu stačí spustiť **raz** jednému človeku, ktorý vytvorené súbory commitne. Ostatní si ich len stiahnu (pull).
> Ak by ho spustil každý zvlášť, vzniknú konflikty.
> Menu scénu **prepíše**. Ručné úpravy v scéne sa stratia, preto ich rob radšej v prefaboch alebo v Editor skripte.

## Ovládanie na mobile

| Čo | Ako |
|---|---|
| Skenovanie | Pomaly prejdi mobilom po izbe. **Zelená** = podlaha, **žltá** = stôl/polica, **modrá** = stena, **sivá** = strop. |
| Pridanie objektu | V menu dole ťukni na objekt (zostane oranžový) → ťukni na **zelenú** podlahu (nábytok) alebo **modrú** stenu (obraz, okno). Lampu môžeš dať aj na stôl. |
| Výber | Ťukni na objekt, okolo neho sa ukáže oranžový obrys. Ťuknutím do prázdna výber zrušíš. |
| Posun | Ťahaj vybraný objekt jedným prstom. Nábytok sa posúva po podlahe, obraz po stene. |
| Otočenie a veľkosť | Dva prsty: krúženie otáča, roztiahnutie a stiahnutie mení veľkosť (50 – 200 %). |
| Zmazať / Vymazať všetko | Zmaže vybraný objekt. *Vymazať všetko* treba ťuknúť dvakrát (potvrdenie). |
| Skryť plochy | Skryje farebné plochy, aby bolo vidno izbu s nábytkom. Umiestňovať sa dá ďalej. |
| Uložiť / Načítať | Návrh sa uloží do `room_design.json` v mobile (`Application.persistentDataPath`). |

**Obmedzenie pri načítaní:** AR si pamätá polohy vzhľadom na miesto, kde bol mobil pri **spustení aplikácie**.
Pri načítaní v novom spustení preto spusti aplikáciu z **toho istého miesta** a s mobilom otočeným **rovnakým smerom** ako pri ukladaní.
Výšku aplikácia dorovná podľa podlahy, preto najprv naskenuj podlahu a až potom daj *Načítať*.

## Nahradenie zástupných modelov

Nábytok je poskladaný z kociek a valcov v reálnej veľkosti. Skutočný model pridáš takto:
1. Vlož model (`.fbx`, `.obj`) do `Assets/_RoomDesigner/Models/`. Ide do Git LFS automaticky.
2. Sprav z neho prefab:
   - na koreni sú komponenty **PlaceableObject** (nastav *Item Id* a *Surface*) a **BoxCollider** okolo celého modelu
   - pivot je dole v strede
   - predná strana smeruje na **+Z**
   - mierka 1 jednotka = 1 meter
3. V `Data/PlaceableCatalog.asset` vymeň pri danej položke *Prefab*.

Pozor: **Tools → Room Designer → Vytvoriť scénu** katalóg a zástupné prefaby znova vygeneruje. Po výmene modelov už menu nespúšťaj, alebo uprav `RoomDesignerAssetBuilder.cs`.

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
│   ├── RoomDetectionSceneSetup.cs     menu Tools → Room Designer → Vytvoriť scénu (scéna + UI)
│   └── RoomDesignerAssetBuilder.cs    materiály, zástupné modely, katalóg
├── Scripts/
│   ├── Detection/
│   │   ├── RoomPlaneClassifier.cs     podlaha vs. stôl/polica vs. stena (podľa výšky)
│   │   ├── PlaneColorByAlignment.cs   farba plochy podľa druhu
│   │   ├── RoomScanStatus.cs          text so stavom skenovania
│   │   ├── PlaneVisibilityToggle.cs   skrytie/zobrazenie plôch
│   │   └── TapToPlaceTest.cs          pôvodný test s kockou (v scéne sa už nepoužíva)
│   ├── Placement/
│   │   ├── RoomDesignerController.cs  umiestnenie, výber, posun, gestá, uloženie
│   │   ├── PlaceableObject.cs         koreň každého objektu (ID, podlaha/stena, obrys)
│   │   ├── PlaceableCatalog.cs        zoznam objektov pre menu
│   │   └── ProceduralArtTexture.cs    obrázok obrazu a výhľad z okna generovaný v kóde
│   ├── Save/
│   │   └── DesignSaveSystem.cs        JSON uloženie a načítanie
│   └── UI/
│       ├── CatalogButton.cs           tlačidlo objektu v menu
│       └── DesignerActionButton.cs    Zmazať, Skryť plochy, Uložiť, Načítať, Vymazať všetko
├── Scenes/      RoomDetection.unity                     (vytvorí Editor skript)
├── Prefabs/     RoomPlane, Placeables/                  (vytvorí Editor skript)
├── Data/        PlaceableCatalog.asset                  (vytvorí Editor skript)
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
