# Mobile AR Room Designer

Mobilná AR aplikácia pre Android, v ktorej si používateľ naskenuje izbu a rozmiestni v nej virtuálny nábytok.
Tímový školský projekt v Unity 6 s AR Foundation 6 a ARCore. Aktuálne je hotová 1. časť: **detekcia izby** (podlaha a steny).

## Požiadavky

- **Unity 6000.3.x** (projekt je vytvorený v 6000.3.10f1) s modulom **Android Build Support** (vrátane OpenJDK a Android SDK & NDK)
- **Android zariadenie s podporou ARCore** ([zoznam zariadení](https://developers.google.com/ar/devices)) a nainštalovanými *Google Play Services for AR*
- **Git** a **Git LFS** (obrázky, modely a zvuky sú uložené cez LFS)

## Naklonovanie a otvorenie projektu

```bash
git lfs install          # stačí raz na počítači
git clone https://github.com/<pouzivatel>/ar-room-designer.git
cd ar-room-designer
git lfs pull             # pre istotu stiahne všetky LFS súbory
```

1. Otvor **Unity Hub → Add → Add project from disk** a vyber priečinok `ar-room-designer`.
2. Otvor projekt vo verzii 6000.3.x. Prvé otvorenie trvá dlhšie, pretože sa vytvára priečinok `Library/`.

## Vytvorenie scény detekcie

Scéna sa skladá automaticky Editor skriptom:

1. V Unity klikni **Tools → Room Designer → Vytvoriť scénu detekcie**.
2. Vytvorí sa (alebo prepíše):
   - `Assets/_RoomDesigner/Scenes/RoomDetection.unity` – AR Session, XR Origin (Mobile AR) so zadnou kamerou, ARPlaneManager (horizontálne + vertikálne plochy), ARRaycastManager, UI so stavom skenovania, objekt *Room Designer* (TapToPlaceTest, PlaneVisibilityToggle)
   - `Assets/_RoomDesigner/Prefabs/RoomPlane.prefab` – vizualizácia plochy
   - `Assets/_RoomDesigner/Prefabs/TestCube.prefab` – testovacia kocka (0,1 m)
   - `Assets/_RoomDesigner/Materials/RoomPlane.mat`, `TestCube.mat`
3. Scéna sa pridá do Build Settings ako prvá.

> Menu stačí spustiť **raz** jednému človeku, ktorý vytvorené súbory commitne. Ostatní si ich len stiahnu (pull).
> Ak by ho spustil každý zvlášť, vzniknú konflikty.

Čo uvidíš na mobile:
- **zelená** plocha = podlaha, **modrá** = stena, **sivá** = strop alebo iná plocha
- hore text so stavom: *„Pomaly prejdi mobilom po izbe...“ → „Podlaha nájdená ✓“ → „Podlaha aj stena nájdená – môžeš začať ✓“* a počet podláh a stien
- ťuknutie na nájdenú plochu položí oranžovú kocku

## Build na Android

1. **File → Build Profiles → Android → Switch Platform**.
2. **Edit → Project Settings → XR Plug-in Management → záložka Android** → zaškrtnuté **Google ARCore**.
3. **Edit → Project Settings → Player → Android → Other Settings**:
   - **Rendering:** vypni *Auto Graphics API* a zo zoznamu odstráň **Vulkan**, nechaj iba **OpenGLES3**
   - **Scripting Backend:** IL2CPP
   - **Target Architectures:** iba **ARM64**
   - **Minimum API Level:** Android 7.0 „Nougat“ (**API level 24**)
4. Pripoj mobil cez USB (zapnuté *Ladenie USB* v možnostiach pre vývojárov).
5. V Build Profiles klikni **Build And Run**.

Logy z mobilu: **Window → Analysis → Android Logcat**.

## Štruktúra priečinkov

```
Assets/_RoomDesigner/
├── Editor/
│   └── RoomDetectionSceneSetup.cs   menu Tools → Room Designer → Vytvoriť scénu detekcie
├── Scripts/
│   ├── PlaneColorByAlignment.cs     farba plochy podľa orientácie (podlaha/stena/strop)
│   ├── RoomScanStatus.cs            text so stavom skenovania + počet podláh a stien
│   ├── TapToPlaceTest.cs            ťuknutie → testovacia kocka na ploche
│   └── PlaneVisibilityToggle.cs     skrytie/zobrazenie plôch (pre neskoršie UI)
├── Scenes/      RoomDetection.unity           (vytvorí Editor skript)
├── Prefabs/     RoomPlane, TestCube           (vytvorí Editor skript)
└── Materials/   RoomPlane, TestCube           (vytvorí Editor skript)
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
