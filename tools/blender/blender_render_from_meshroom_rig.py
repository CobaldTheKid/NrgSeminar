from pathlib import Path

import bpy

RIG_COLLECTION_NAME = "MeshroomRig"
CAMERA_PREFIX = "MeshroomCam_"
OUTPUT_DIR = "//meshroom_renders"
ENGINE = 'BLENDER_EEVEE'  # 'BLENDER_EEVEE' or 'BLENDER_EEVEE_NEXT' also possible
RESOLUTION = 2048
SAMPLES = 128
FILE_FORMAT = 'PNG'  # 'PNG' or 'JPEG'
JPEG_QUALITY = 95
WORLD_STRENGTH = 1.0
OVERWRITE_EXISTING = True

# Cycles acceleration options
USE_GPU = True
COMPUTE_DEVICE_TYPE = 'CUDA'  # 'OPTIX' for RTX, fallback to 'CUDA' if unavailable
USE_DENOISE = True
DENOISER = 'OPTIX'  # 'OPTIX' or 'OPENIMAGEDENOISE'
USE_ADAPTIVE_SAMPLING = True


def get_rig_cameras():
    collection = bpy.data.collections.get(RIG_COLLECTION_NAME)
    if collection is None:
        raise RuntimeError(f"Collection '{RIG_COLLECTION_NAME}' not found. Run the rig creation script first.")

    cameras = [obj for obj in collection.objects if obj.type == 'CAMERA' and obj.name.startswith(CAMERA_PREFIX)]
    cameras.sort(key=lambda cam: cam.name)
    if not cameras:
        raise RuntimeError(f"No cameras with prefix '{CAMERA_PREFIX}' found in collection '{RIG_COLLECTION_NAME}'.")
    return cameras


def setup_cycles_devices():
    prefs = bpy.context.preferences
    cycles_addon = prefs.addons.get('cycles')
    if cycles_addon is None:
        print('Cycles addon preferences not found; leaving Blender device settings unchanged.')
        return

    cprefs = cycles_addon.preferences
    available_backends = []
    for backend in ('OPTIX', 'CUDA', 'HIP', 'ONEAPI', 'METAL'):
        try:
            cprefs.compute_device_type = backend
            cprefs.get_devices()
            if cprefs.devices:
                available_backends.append(backend)
        except Exception:
            continue

    if not USE_GPU:
        bpy.context.scene.cycles.device = 'CPU'
        print('Cycles configured for CPU rendering.')
        return

    requested_backend = COMPUTE_DEVICE_TYPE.upper()
    chosen_backend = requested_backend if requested_backend in available_backends else None
    if chosen_backend is None and available_backends:
        chosen_backend = available_backends[0]

    if chosen_backend is None:
        bpy.context.scene.cycles.device = 'CPU'
        print('No GPU backend available, falling back to CPU rendering.')
        return

    cprefs.compute_device_type = chosen_backend
    cprefs.get_devices()
    for device in cprefs.devices:
        device.use = True

    bpy.context.scene.cycles.device = 'GPU'
    print(f'Cycles configured for GPU rendering via {chosen_backend}.')
    for device in cprefs.devices:
        print(f"  Device: {device.name} | type={device.type} | enabled={device.use}")


def setup_render():
    scene = bpy.context.scene
    scene.render.engine = ENGINE
    scene.render.resolution_x = RESOLUTION
    scene.render.resolution_y = RESOLUTION
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = FILE_FORMAT

    if FILE_FORMAT == 'JPEG':
        scene.render.image_settings.quality = JPEG_QUALITY

    if ENGINE == 'CYCLES':
        setup_cycles_devices()
        scene.cycles.samples = SAMPLES
        scene.cycles.use_adaptive_sampling = USE_ADAPTIVE_SAMPLING
        scene.cycles.use_denoising = USE_DENOISE
        scene.cycles.denoiser = DENOISER
        bpy.context.view_layer.cycles.use_denoising = USE_DENOISE
        print(f"Cycles denoise: {USE_DENOISE} ({DENOISER})")

    if scene.world and scene.world.node_tree:
        bg_nodes = [n for n in scene.world.node_tree.nodes if n.type == 'BACKGROUND']
        if bg_nodes:
            bg_nodes[0].inputs[1].default_value = WORLD_STRENGTH


def get_output_dir():
    out_dir = Path(bpy.path.abspath(OUTPUT_DIR))
    out_dir.mkdir(parents=True, exist_ok=True)
    return out_dir


def render_all():
    cameras = get_rig_cameras()
    setup_render()
    out_dir = get_output_dir()
    scene = bpy.context.scene
    ext = ".png" if FILE_FORMAT == 'PNG' else ".jpg"

    print(f"Rendering {len(cameras)} cameras to {out_dir}")
    for idx, cam in enumerate(cameras):
        output_path = out_dir / f"{idx:04d}_{cam.name}{ext}"
        if output_path.exists() and not OVERWRITE_EXISTING:
            print(f"Skipping existing file: {output_path}")
            continue

        scene.camera = cam
        scene.render.filepath = str(output_path)
        bpy.ops.render.render(write_still=True)
        print(f"Rendered {cam.name} -> {output_path}")

    print("Done rendering Meshroom rig cameras.")


render_all()