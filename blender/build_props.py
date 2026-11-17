"""Builds the avatar-stations props: the cloud seat, the portal and the seats.

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


# --------------------------------------------------------------------------
# Seats
# --------------------------------------------------------------------------

SEATS = PropSpec("Seats", "seats", triangle_budget=1100, parts={"Ring": None, "Row": None})

SEAT_HEIGHT = 0.32
RING_RADIUS = 1.15
SCREEN_AHEAD = 1.6
SCREEN_CENTRE = 1.3
SCREEN_HALF = (0.96, 0.54)
ROW_RADIUS = 2.6
ROW_ANGLES = (-30.0, -10.0, 10.0, 30.0)


def seat_places():
    """Where the four seats stand in each arrangement, as (right, forward) in metres from the prop's root."""
    ring = [(RING_RADIUS * math.sin(math.radians(a)), RING_RADIUS * math.cos(math.radians(a)))
            for a in (45.0, 135.0, 225.0, 315.0)]
    row = [(ROW_RADIUS * math.sin(math.radians(a)), SCREEN_AHEAD - ROW_RADIUS * math.cos(math.radians(a)))
           for a in ROW_ANGLES]
    return ring, row


def build_seats(mesh):
    """Four cushions, twice: in a ring round a lamp, and in a row before a screen.

    Each arrangement is one part, because a seat left in the world is a mesh
    of its own and eight cushions that moved separately would be eight
    meshes. Unity shows one part or the other. The root carries nothing.
    """
    colours = ("lavender", "pink", "sky", "banana")
    ring, row = seat_places()

    def cushion(part, right, forward, colour):
        part.ellipsoid(X * right + FORWARD * forward + UP * (SEAT_HEIGHT * 0.5), (0.30, 0.30, SEAT_HEIGHT * 0.5),
                       ["violet", colour, colour, colour, "white"], segments=8, rings=5)

    circle = mesh.part("Ring")
    for (right, forward), colour in zip(ring, colours):
        cushion(circle, right, forward, colour)
    # Something to sit round: a lamp on a short stand.
    circle.lathe(UP * 0.0, UP, [(0.0, 0.16), (0.03, 0.17), (0.05, 0.05), (0.34, 0.04), (0.36, 0.09),
                                (0.47, 0.15), (0.60, 0.11), (0.66, 0.0)],
                 ["ink", "ink", "violet", "orange", "banana", "banana", "white"], segments=8, cap_start="ink")

    theatre = mesh.part("Row")
    for (right, forward), colour in zip(row, colours):
        cushion(theatre, right, forward, colour)
    # The screen's surround. Unity hangs the picture itself in the opening.
    screen = FORWARD * SCREEN_AHEAD + UP * SCREEN_CENTRE
    corners = ring_points(screen, X, UP, (SCREEN_HALF[0] + 0.03) * math.sqrt(2.0), (SCREEN_HALF[1] + 0.03) * math.sqrt(2.0), 4, 0.5)
    theatre.sweep(corners, FORWARD, [(0.035, 0.035), (-0.035, 0.035), (-0.035, -0.035), (0.035, -0.035)],
                  ["violet", "ink", "violet", "ink"], closed=True, hard_section=True)
    # A back, so the picture is not seen through from behind.
    theatre.polygon(ring_points(screen + FORWARD * 0.02, X, UP, SCREEN_HALF[0] * math.sqrt(2.0),
                                SCREEN_HALF[1] * math.sqrt(2.0), 4, 0.5), "ink", FORWARD)
    for side in (-1.0, 1.0):
        foot = FORWARD * SCREEN_AHEAD + X * (side * 0.62)
        leg = [ring_points(foot + UP * height, X, BACK, 0.03, 0.03, 4, 0.5)
               for height in (0.0, SCREEN_CENTRE - SCREEN_HALF[1] - 0.03)]
        theatre.loft(leg, "ink", cap_start="ink")
        theatre.lathe(foot, UP, [(0.0, 0.14), (0.025, 0.14), (0.04, 0.04)], "violet", segments=6, cap_start="violet")
    top = SCREEN_CENTRE + SCREEN_HALF[1] + 0.05
    rabbit_ears(theatre, [(-0.2, -SCREEN_AHEAD, top), (0.2, -SCREEN_AHEAD, top)], (0.075, 0.045, 0.16), 16.0, "lavender", "pink")
    return {"Ring": UP * 0.0, "Row": UP * 0.0}


PROPS = [
    (CLOUD_SEAT, build_cloud_seat),
    (PORTAL, build_portal),
    (SEATS, build_seats),
]

if __name__ == "__main__":
    main_build(PROPS, REPO / "blender", MODELS)
