# Hybrid Mesh-Gaussian Splat Rendering in Unity

This project presents a practical pipeline for preparing, aligning, and rendering hybrid 3D objects composed of a mesh model and a Gaussian splat representation. The main goal is to support real-time LOD-style switching in Unity:

- near view: Gaussian splat rendering
- far view: simplified mesh rendering
- transition zone: smooth blend between both representations

The repository contains the Unity project, the modified Gaussian splat package, Blender tools for synthetic multi-view rendering, Python scripts for mesh-splat alignment, and the seminar report.

## Repository Structure

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
  report/
```

### `package/`

Modified Unity Gaussian splatting package used for splat rendering in Unity.

This folder contains the package-level changes needed for:

- splat opacity control
- runtime blending support
- modified shaders / renderer scripts used by the project

### `projects/GaussianExample-URP/`

Main Unity URP demo project.

This folder contains:

- scenes
- materials
- custom scripts
- custom shaders
- alignment data examples
- the hybrid object setup used for testing and evaluation

### `tools/blender/`

Blender scripts for synthetic data generation.

These scripts are used to:

- place cameras around an object
- render multi-view images
- generate synthetic image sets for Meshroom or Gaussian Splatting training

### `tools/alignment/`

Python scripts for semi-automatic alignment between mesh and splat models.

These scripts are used to:

- load sampled mesh points
- load Gaussian centers from `.ply`
- compute alignment transform
- export the result as JSON for Unity

### `report/`

Seminar report written in LaTeX.

## Main Pipeline

The full workflow is:

1. Acquire input data
   - real photographs
   - or synthetic Blender renders
2. Reconstruct Gaussian splat model
   - train a Gaussian splat representation
   - export `.ply`
3. Reconstruct mesh model
   - use Meshroom photogrammetry
   - or model manually in Blender for simple objects
4. Import both models into Unity
5. Align mesh and splat models
   - export mesh surface samples from Unity
   - run Python alignment script
   - apply alignment JSON in Unity
6. Enable hybrid rendering
   - hard switch
   - dither crossfade
   - transparent crossfade

## Dependencies

This project relies on the following external tools:

- Unity (URP project)
- UnityGaussianSplatting
- Blender
- Meshroom
- COLMAP
- Gaussian Splatting training code
- Python 3.10+
- `numpy`
- `open3d`
- `plyfile`

Depending on the training setup, you may also need:

- PyTorch with CUDA
- additional Gaussian Splatting dependencies

## Unity Project

Open the Unity project located in:

```text
projects/GaussianExample-URP/
```

### Main Unity components

The Unity project contains several important systems:

#### Alignment tools

- mesh point export from Unity
- alignment JSON import and application

#### LOD / blending scripts

- `V1`: hard switch
- `V2`: dither crossfade
- `V3`: transparent crossfade

#### Custom shader

- dither fade mesh shader used by the V2 transition

## Synthetic Data Generation in Blender

Blender scripts are located in:

```text
tools/blender/
```

### Typical workflow

1. Import or open the object in Blender
2. Run the camera rig script
3. Verify the camera placement
4. Run the render script
5. Use the rendered images as input for:
   - Meshroom
   - Gaussian Splatting training

## Mesh-Splat Alignment

Alignment tools are located in:

```text
tools/alignment/
```

### Step 1: Export mesh sample points from Unity

In Unity, use the editor export tool to generate:

- `*.meshpoints.json`

This file contains sampled mesh surface points.

### Step 2: Run Python alignment

Example command:

```cmd
python compute_alignment.py --mesh-points path\to\MyMesh.meshpoints.json --ply path\to\model.ply --output path\to\alignment_transform.json
```

### Step 3: Apply transform in Unity

Assign the generated alignment JSON to the Unity alignment component and apply it to the mesh root object.

### Notes

- alignment is semi-automatic
- the automatic result usually provides a good coarse alignment
- small manual corrections may still be required

## Gaussian Splatting Training

Training is not run directly inside Unity.

Typical external workflow:

1. Prepare images
2. Run COLMAP / conversion step
3. Train Gaussian splat model
4. Export `.ply`
5. Import `.ply` into Unity

If checkpoints are enabled during training, training can be resumed later.

## Rendering Modes

The project supports three rendering strategies:

### V1: Hard Switch

A simple distance-based switch between:

- splat model
- mesh model

### V2: Dither Crossfade

Uses:

- custom dither shader on the mesh
- runtime opacity fade on the splat

Advantages:

- better depth behavior
- avoids typical transparent mesh artifacts

Disadvantages:

- visible dither pattern on some objects

### V3: Transparent Crossfade

Uses:

- transparent URP/Lit mesh material
- runtime splat fade

Advantages:

- visually softer transition

Disadvantages:

- may show rear mesh surfaces through the front during fade

## Typical Use Cases

This repository was tested on several representative objects:

- Book
- Laptop
- Log

Observed behavior:

- `Book`: dither is more visible, transparent blending can show depth artifacts
- `Laptop`: dither works especially well due to richer texture
- `Log`: both methods work well; transparent blending is visually strongest when mesh and splat match closely

## How to Reproduce the Hybrid Setup

### Minimal Unity workflow

1. Open `projects/GaussianExample-URP/`
2. Import / assign:
   - mesh model
   - splat model
3. Export mesh point samples
4. Run alignment script
5. Apply alignment JSON
6. Choose transition mode:
   - hard switch
   - dither
   - transparent
7. Adjust near / transition / far distances

### Minimal Blender workflow

1. Open object in Blender
2. Generate camera rig
3. Render views
4. Use outputs for:
   - Meshroom
   - Gaussian training

## Notes on Artifacts

Two main artifacts were observed:

### Dither transition

- may show a visible stippled pattern
- can be reduced by:
  - shortening the transition range
  - moving the transition farther from the camera

### Transparent transition

- may show rear mesh surfaces through front surfaces
- can be reduced by:
  - shortening the transition range
  - using the transparent transition farther from the camera

## Report

The seminar report is included in:

```text
report/
```

It describes:

- motivation
- pipeline
- implementation
- evaluation
- limitations
- conclusions

## License

Choose and place the final license file here, for example:

- MIT
- BSD
- CC BY (for report content, if required)

If third-party code is included or modified, make sure the original license is preserved and clearly referenced.
