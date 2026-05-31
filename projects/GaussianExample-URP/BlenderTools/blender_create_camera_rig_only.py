import math

import bpy
from mathutils import Vector

RIG_COLLECTION_NAME = "MeshroomRig"
TARGET_EMPTY_NAME = "MeshroomTarget"
CAMERA_PREFIX = "MeshroomCam_"
VIEWS_PER_RING = 24
RINGS = 4
ELEVATION_MIN = -20.0
ELEVATION_MAX = 55.0
RADIUS_SCALE = 2.4
FOCAL_LENGTH = 50.0
ADD_TOP_CAMERA = True
CLEAR_OLD_RIG = True


def get_target_object():
    obj = bpy.context.active_object
    if obj is None:
        raise RuntimeError("Select the object you want to photograph before running this script.")
    return obj


def ensure_collection(name: str, clear_old: bool):
    collection = bpy.data.collections.get(name)
    if collection and clear_old:
        for obj in list(collection.objects):
            bpy.data.objects.remove(obj, do_unlink=True)
        for child in list(collection.children):
            collection.children.unlink(child)
        bpy.data.collections.remove(collection)
        collection = None

    if collection is None:
        collection = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(collection)
    return collection


def object_world_bounds(obj):
    corners = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    min_corner = Vector((min(v.x for v in corners), min(v.y for v in corners), min(v.z for v in corners)))
    max_corner = Vector((max(v.x for v in corners), max(v.y for v in corners), max(v.z for v in corners)))
    center = (min_corner + max_corner) * 0.5
    extents = max_corner - min_corner
    radius = max((v - center).length for v in corners)
    return center, extents, radius


def create_target_empty(collection, center):
    empty = bpy.data.objects.new(TARGET_EMPTY_NAME, None)
    empty.empty_display_type = 'PLAIN_AXES'
    empty.empty_display_size = 0.2
    empty.location = center
    collection.objects.link(empty)
    return empty


def create_tracking_camera(name, location, target, collection, focal_length):
    cam_data = bpy.data.cameras.new(name)
    cam_data.lens = focal_length
    cam_data.sensor_fit = 'HORIZONTAL'
    cam_data.clip_start = 0.01
    cam_data.clip_end = 1000.0

    cam_obj = bpy.data.objects.new(name, cam_data)
    cam_obj.location = location
    collection.objects.link(cam_obj)

    track = cam_obj.constraints.new(type='TRACK_TO')
    track.target = target
    track.track_axis = 'TRACK_NEGATIVE_Z'
    track.up_axis = 'UP_Y'
    return cam_obj


def spherical_position(center, radius, azimuth_deg, elevation_deg):
    az = math.radians(azimuth_deg)
    el = math.radians(elevation_deg)
    x = radius * math.cos(el) * math.cos(az)
    y = radius * math.cos(el) * math.sin(az)
    z = radius * math.sin(el)
    return center + Vector((x, y, z))


def main():
    obj = get_target_object()
    collection = ensure_collection(RIG_COLLECTION_NAME, CLEAR_OLD_RIG)
    center, extents, radius = object_world_bounds(obj)
    rig_radius = max(radius * RADIUS_SCALE, max(extents) * 1.2)
    target = create_target_empty(collection, center)

    if RINGS == 1:
        elevations = [(ELEVATION_MIN + ELEVATION_MAX) * 0.5]
    else:
        elevations = [
            ELEVATION_MIN + (ELEVATION_MAX - ELEVATION_MIN) * (i / (RINGS - 1))
            for i in range(RINGS)
        ]

    cameras = []
    index = 0
    for elevation in elevations:
        for view_idx in range(VIEWS_PER_RING):
            azimuth = (360.0 / VIEWS_PER_RING) * view_idx
            pos = spherical_position(center, rig_radius, azimuth, elevation)
            cam = create_tracking_camera(f"{CAMERA_PREFIX}{index:03d}", pos, target, collection, FOCAL_LENGTH)
            cameras.append(cam)
            index += 1

    if ADD_TOP_CAMERA:
        top_pos = center + Vector((0.0, 0.0, rig_radius * 1.1))
        cameras.append(create_tracking_camera(f"{CAMERA_PREFIX}{index:03d}", top_pos, target, collection, FOCAL_LENGTH))

    print(f"Created {len(cameras)} cameras for object '{obj.name}'.")
    print(f"Rig collection: {RIG_COLLECTION_NAME}")
    print(f"Target center: {center}")
    print(f"Rig radius: {rig_radius:.4f}")


main()