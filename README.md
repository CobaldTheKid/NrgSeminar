# Hibridni prikaz mesh-Gaussian splat modelov v Unityju

Ta projekt predstavlja praktičen cevovod za pripravo, poravnavo in prikaz hibridnih 3D objektov, sestavljenih iz mrežnega modela in Gaussian splat predstavitve. Glavni cilj je podpreti realnočasovno LOD-preklapljanje v Unityju:

- bližnji pogled: prikaz Gaussian splat modela
- oddaljeni pogled: prikaz poenostavljenega mesh modela
- prehodno območje: gladek prehod med obema predstavitvama

Repozitorij vsebuje Unity projekt, prilagojen Gaussian splat package, Blender orodja za sintetični večpogledni zajem, Python skripte za poravnavo mesh-splat modelov ter seminarsko poročilo.

## Struktura repozitorija

```text
repo/
  README.md
  LICENSE
  package/
  projects/
    GaussianExample-URP/
  tools/
    blender/
    alignment/
  report.pdf
```

### `package/`

Prilagojen Unity Gaussian splatting package za prikaz splat modelov v Unityju.

Ta mapa vsebuje spremembe na nivoju package-a, potrebne za:

- nadzor prosojnosti splat modela
- podporo za runtime blending
- prilagojene shaderje in renderer skripte, uporabljene v projektu

### `projects/GaussianExample-URP/`

Glavni Unity URP demo projekt.

Ta mapa vsebuje:

- scene
- materiale
- lastne skripte
- lastne shaderje
- primere alignment podatkov
- postavitve hibridnih objektov za testiranje in evalvacijo

### `tools/blender/`

Blender skripte za sintetično generiranje vhodnih podatkov.

Te skripte se uporabljajo za:

- postavitev kamer okoli objekta
- izris večpoglednih slik
- pripravo sintetičnih vhodnih slik za Meshroom ali Gaussian Splatting training

### `tools/alignment/`

Python skripte za polavtomatsko poravnavo mesh in splat modelov.

Te skripte se uporabljajo za:

- nalaganje vzorčenih točk mesh modela
- nalaganje centrov Gaussianov iz `.ply`
- izračun transformacije poravnave
- izvoz rezultata v JSON za Unity

### `report/`

Seminarsko poročilo v pdf obliki.

## Glavni cevovod

Celoten workflow poteka po naslednjih korakih:

1. Zajem vhodnih podatkov
   - realne fotografije
   - ali sintetični Blender renderji
2. Rekonstrukcija Gaussian splat modela
   - učenje Gaussian splat predstavitve
   - izvoz `.ply`
3. Rekonstrukcija mesh modela
   - uporaba Meshroom fotogrametrije
   - ali ročno modeliranje v Blenderju pri preprostih objektih
4. Uvoz obeh modelov v Unity
5. Poravnava mesh in splat modela
   - izvoz vzorčnih točk mesh modela iz Unityja
   - zagon Python alignment skripte
   - uporaba alignment JSON zapisa v Unityju
6. Aktivacija hibridnega prikaza
   - hard switch
   - dither crossfade
   - transparent crossfade

## Odvisnosti

Projekt uporablja naslednja zunanja orodja:

- Unity (URP projekt)
- UnityGaussianSplatting
- Blender
- Meshroom
- COLMAP
- Gaussian Splatting training kodo
- Python 3.10+
- `numpy`
- `open3d`
- `plyfile`

Glede na training setup boste morda potrebovali še:

- PyTorch s CUDA podporo
- dodatne Gaussian Splatting odvisnosti

## Unity projekt

Unity projekt se nahaja v:

```text
projects/GaussianExample-URP/
```

### Glavne Unity komponente

Unity projekt vsebuje več pomembnih sistemov:

#### Orodja za poravnavo

- izvoz točk mesh modela iz Unityja
- uvoz in uporaba alignment JSON zapisa

#### LOD / blending skripte

- `V1`: hard switch
- `V2`: dither crossfade
- `V3`: transparent crossfade

#### Lasten shader

- dither fade mesh shader, uporabljen pri V2 prehodu

## Sintetični zajem v Blenderju

Blender skripte se nahajajo v:

```text
tools/blender/
```

### Tipičen workflow

1. Odpri ali uvozi objekt v Blender
2. Zaženi skripto za postavitev kamer
3. Preveri postavitev kamer
4. Zaženi skripto za izris pogledov
5. Uporabi izrisane slike kot vhod za:
   - Meshroom
   - Gaussian Splatting training

## Poravnava mesh-splat modelov

Alignment orodja se nahajajo v:

```text
tools/alignment/
```

### 1. korak: izvoz vzorčnih točk mesh modela iz Unityja

V Unityju uporabi urejevalniško orodje za izvoz:

- `*.meshpoints.json`

Ta datoteka vsebuje vzorčene površinske točke mesh modela.

### 2. korak: zagon Python alignment skripte

Primer ukaza:

```cmd
python compute_alignment.py --mesh-points path\to\MyMesh.meshpoints.json --ply path\to\model.ply --output path\to\alignment_transform.json
```

### 3. korak: uporaba transformacije v Unityju

Nastavi ustvarjeni alignment JSON v Unity komponenti za poravnavo in ga uporabi na korenskem objektu mesh modela.

### Opombe

- poravnava je polavtomatska
- samodejni rezultat običajno zagotovi dobro grobo poravnavo
- manjši ročni popravki so lahko še vedno potrebni

## Gaussian Splatting training

Training se ne izvaja neposredno v Unityju.

Tipičen zunanji workflow:

1. Pripravi slike
2. Zaženi COLMAP oziroma pretvorbo vhodnih podatkov
3. Natreniraj Gaussian splat model
4. Izvozi `.ply`
5. Uvozi `.ply` v Unity

Če so med trainingom omogočeni checkpointi, lahko training kasneje nadaljuješ.

## Načini prikaza

Projekt podpira tri strategije prikaza:

### V1: Hard Switch

Enostaven preklop na podlagi razdalje med:

- splat modelom
- mesh modelom

### V2: Dither Crossfade

Uporablja:

- lasten dither shader na mesh modelu
- runtime fade prosojnosti splat modela

Prednosti:

- bolj pravilno globinsko obnašanje
- manj tipičnih transparentnih artefaktov mesh modela

Slabosti:

- pri nekaterih objektih je dither vzorec viden

### V3: Transparent Crossfade

Uporablja:

- transparenten URP/Lit mesh material
- runtime fade splat modela

Prednosti:

- mehkejši vizualni prehod

Slabosti:

- med fade prehodom se lahko zadnji deli mesh modela vidijo skozi sprednje ploskve

## Tipični primeri uporabe

Repozitorij smo testirali na več reprezentativnih objektih:

- Book
- Laptop
- Log

Opaženo obnašanje:

- `Book`: dither je bolj viden, transparentni prehod lahko pokaže globinske artefakte
- `Laptop`: dither deluje posebej dobro zaradi bogatejše teksture
- `Log`: obe metodi delujeta dobro; transparentni prehod je vizualno najbolj prepričljiv, kadar se mesh in splat zelo dobro ujemata

## Kako ponoviti hibridni setup

### Minimalen Unity workflow

1. Odpri `projects/GaussianExample-URP/`
2. Uvozi ali nastavi:
   - mesh model
   - splat model
3. Izvozi vzorčne točke mesh modela
4. Zaženi alignment skripto
5. Uporabi alignment JSON
6. Izberi način prehoda:
   - hard switch
   - dither
   - transparent
7. Nastavi meje za bližnji, prehodni in oddaljeni pogled

### Minimalen Blender workflow

1. Odpri objekt v Blenderju
2. Ustvari camera rig
3. Izriši poglede
4. Uporabi slike za:
   - Meshroom
   - Gaussian training

## Opombe o artefaktih

Opazili smo dve glavni vrsti artefaktov:

### Dither prehod

- lahko pokaže viden pikčast oziroma diskreten vzorec
- zmanjša se ga lahko z:
  - krajšim prehodnim območjem
  - premikom prehoda dlje od kamere

### Transparentni prehod

- lahko pokaže zadnje dele mesh modela skozi sprednje ploskve
- zmanjša se ga lahko z:
  - krajšim prehodnim območjem
  - uporabo transparentnega prehoda pri večji oddaljenosti kamere

## Poročilo

Seminarsko poročilo je vključeno v:

```text
report/
```

Poročilo opisuje:

- motivacijo
- cevovod
- implementacijo
- evalvacijo
- omejitve
- zaključke

## Licenca

Dodajte ustrezno licenco, na primer:

- MIT
- BSD
- CC BY (za poročilo, če je potrebno)

Če so vključene ali prilagojene tretje knjižnice oziroma datoteke, naj bodo njihove izvorne licence ustrezno ohranjene in navedene.
