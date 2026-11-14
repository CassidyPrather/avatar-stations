"""Builds the avatar-stations props: the cloud seat and the portal.

    blender -b --python blender/build_props.py [-- --only STEM] [-- --previews DIR]

Each prop is modelled from the code below, saved as `blender/<stem>.blend`,
checked against the wirenook prop contract and exported to the package's
`Models/<stem>.fbx`. See README.md beside this file.
"""

import math
import pathlib
import sys

sys.dont_write_bytecode = True

REPO = pathlib.Path(__file__).resolve().parents[1]
MODELS = REPO / "Packages" / "net.wirenook.avatar-stations" / "Models"
LIBRARY = REPO.parent / "wirenook-assets" / "tools" / "blender"
if not (LIBRARY / "wirenook_props.py").is_file():
    raise SystemExit(f"The shared prop kit is missing: {LIBRARY / 'wirenook_props.py'}\n"
                     "Clone wirenook-assets beside this repository and run again.")
sys.path.insert(0, str(LIBRARY))

from mathutils import Matrix, Vector

from wirenook_props import FORWARD, GLOW, SOLID, UP, PropSpec, Puff, cloud, cloud_tones, main_build, ring_points

X = Vector((1.0, 0.0, 0.0))
BACK = -FORWARD


def rabbit_ears(part, roots, radii, lean_degrees, swatch, lining):
    """A pair of upright ears leaning apart, each with a flat lining on its front and back."""
    for side, root in zip((-1.0, 1.0), roots):
        lean = Matrix.Rotation(side * math.radians(-lean_degrees), 3, FORWARD)
        centre = Vector(root) + lean @ (UP * radii[2] * 0.8)
        part.ellipsoid(centre, radii, swatch, segments=6, rings=5, rotation=lean)
        for facing in (FORWARD, BACK):
            inner = ring_points(Vector(), X, UP, radii[0] * 0.5, radii[2] * 0.62, 6, 0.5)
            part.polygon([centre + facing * (radii[1] * 0.93) + lean @ p for p in inner], lining, facing)


# --------------------------------------------------------------------------
# Cloud seat
# --------------------------------------------------------------------------

CLOUD_SEAT = PropSpec("CloudSeat", "cloud-seat", triangle_budget=700)


def build_cloud_seat(mesh):
    seat = mesh.part()
    # An armchair of puffs: a broad cushion, two arms, a taller back, and a
    # soft lip in front. The cushion's crown is the seat, 0.35 m up.
    cloud(seat, [
        Puff((0.0, -0.03, 0.14), (0.30, 0.25, 0.21), segments=10, rings=6),
        Puff((-0.27, -0.01, 0.17), (0.13, 0.20, 0.24)),
        Puff((0.27, -0.01, 0.17), (0.13, 0.20, 0.24)),
        Puff((-0.13, 0.15, 0.25), (0.17, 0.12, 0.26), segments=10),
        Puff((0.13, 0.15, 0.25), (0.17, 0.12, 0.26), segments=10),
        Puff((-0.16, -0.19, 0.11), (0.15, 0.11, 0.17)),
        Puff((0.16, -0.19, 0.11), (0.15, 0.11, 0.17)),
        Puff((0.0, 0.265, 0.16), (0.075, 0.075, 0.075), segments=6, rings=4),
    ], floor=0.0, tones=cloud_tones(0.07, 0.19))
    rabbit_ears(seat, [(-0.13, 0.16, 0.44), (0.13, 0.16, 0.44)], (0.06, 0.035, 0.15), 14.0, "white", "pink")
    return {}


# --------------------------------------------------------------------------
# Portal
# --------------------------------------------------------------------------

PORTAL = PropSpec("Portal", "portal", triangle_budget=700, parts={"Swirl": None}, materials=(SOLID, GLOW))


def build_portal(mesh):
    half_width, half_height = 0.44, 0.88
    centre = UP * 0.96

    frame = mesh.part()
    # A six-sided bar with flats facing front and back; its inward bevels
    # glow, as if lit by whatever is on the other side.
    bar = [(0.06, 0.0), (0.028, 0.05), (-0.028, 0.05), (-0.06, 0.0), (-0.028, -0.05), (0.028, -0.05)]
    frame.sweep(ring_points(centre, X, UP, half_width, half_height, 24), FORWARD, bar,
                ["violet", "lavender", "glow-cyan", "glow-cyan", "lavender", "violet"],
                closed=True, hard_section=True)
    rabbit_ears(frame, [(-0.17, 0.0, 1.86), (0.17, 0.0, 1.86)], (0.075, 0.045, 0.16), 16.0, "lavender", "pink")
    frame.lathe(UP * 1.925, UP, [(-0.05, 0.0), (0.0, 0.04), (0.05, 0.0)], "orange", segments=4, phase=0.5)

    # It stands in a low bank of cloud rather than on a plinth.
    cloud(frame, [
        Puff((-0.25, 0.0, 0.08), (0.21, 0.17, 0.14)),
        Puff((0.25, 0.0, 0.08), (0.21, 0.17, 0.14)),
        Puff((0.0, 0.0, 0.06), (0.18, 0.13, 0.10), segments=6, rings=4),
    ], floor=0.0, tones=cloud_tones(0.03, 0.10))

    # The pinwheel is a circle as wide as the opening, not an oval as tall
    # as it: a circle is the only shape that can turn about forward without
    # ever swinging out through the sides of the frame.
    swirl = mesh.part("Swirl", GLOW)
    arms = 12
    rings = [(0.07, 0.0), (0.17, 0.7), (0.27, 1.4), (0.36, 2.1), (half_width, 2.8)]
    colours = ["glow-cyan", "glow-lavender", "glow-magenta"]
    loops = [ring_points(centre, X, UP, radius, radius, arms, twist) for radius, twist in rings]
    swirl.polygon(loops[0], "glow-white", FORWARD)
    for inner, outer in zip(loops, loops[1:]):
        for k in range(arms):
            k1 = (k + 1) % arms
            swirl.polygon([inner[k], inner[k1], outer[k1], outer[k]], colours[(k // 2) % 3], FORWARD)
    return {"Swirl": centre}


PROPS = [
    (CLOUD_SEAT, build_cloud_seat),
    (PORTAL, build_portal),
]

if __name__ == "__main__":
    main_build(PROPS, REPO / "blender", MODELS)
