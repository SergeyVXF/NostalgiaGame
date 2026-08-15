"""
Hand-painted style albedo atlas for the AutoService building.

No per-pixel procedural noise: every tile is built from a small palette,
large low-frequency patches (posterized), and deliberate strokes -
drips, plank lines, corrugation ribs, rust blooms, painted letters.

Runs inside Blender (numpy ships with Blender). Saving uses bpy.
"""
from __future__ import annotations

import math
from typing import Dict, List, Sequence, Tuple

import numpy as np

ATLAS = 2048
GRID = 8
TILE = ATLAS // GRID

# square tiles: key -> (col, row); row 0 == bottom of the image
TILES: Dict[str, Tuple[int, int]] = {
    "plaster": (0, 0),
    "plaster_worn": (1, 0),
    "blue_plinth": (2, 0),
    "corrugated": (3, 0),
    "corr_rust": (4, 0),
    "wood": (5, 0),
    "rubber": (6, 0),
    "rust": (7, 0),
    "concrete": (0, 1),
    "blue_metal": (1, 1),
    "car_blue": (2, 1),
    "car_orange": (3, 1),
    "car_beige": (4, 1),
    "car_green": (5, 1),
    "roof": (6, 1),
    "dark": (7, 1),
    "red": (0, 2),
    "yellow": (1, 2),
    "frame": (2, 2),
    "engine": (3, 2),
    "paper": (4, 2),
    "dirt": (5, 2),
    "glass": (6, 2),
    "tool": (7, 2),
    "interior_wall": (0, 3),
    "crate": (1, 3),
    "barrel": (2, 3),
    "chrome": (3, 3),
    "glass_dark": (4, 3),
    "hubcap": (5, 3),
    "headlight": (6, 3),
    "taillight": (7, 3),
    "seat": (0, 4),
    "interior_dark": (1, 4),
    "rust_dark": (2, 4),
    "bumper": (3, 4),
    "floor": (4, 4),
    "asphalt": (5, 4),
    "grime": (6, 4),
    "canvas": (7, 4),
    "crane": (0, 5),
    "crane_worn": (1, 5),
    "hazard": (2, 5),
    "magnet": (3, 5),
    "cab": (4, 5),
    "steel": (5, 5),
}

# the sign gets a wide strip (4 tiles x 1 tile == 1024x256 == 4:1)
SIGN_RECT_TILES = (0, 7, 4, 1)  # col, row, cols_wide, rows_tall


def uv_rect(key: str) -> Tuple[float, float, float, float]:
    """Return (u0, v0, du, dv) for a tile key, with a small inset."""
    if key == "sign":
        col, row, cw, ch = SIGN_RECT_TILES
        pad = 0.0015
        return (col / GRID + pad, row / GRID + pad, cw / GRID - 2 * pad, ch / GRID - 2 * pad)
    col, row = TILES[key]
    pad = 0.006
    return (col / GRID + pad, row / GRID + pad, 1.0 / GRID - 2 * pad, 1.0 / GRID - 2 * pad)


# ---------------------------------------------------------------------------
# painting primitives
# ---------------------------------------------------------------------------
def rgb(hexstr: str) -> np.ndarray:
    h = hexstr.lstrip("#")
    return np.array([int(h[i : i + 2], 16) / 255.0 for i in (0, 2, 4)], dtype=np.float32)


def srgb_to_linear(c: np.ndarray) -> np.ndarray:
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def _bilerp(grid: np.ndarray, h: int, w: int) -> np.ndarray:
    gh, gw = grid.shape
    yi = np.linspace(0.0, gh - 1.0, h)
    xi = np.linspace(0.0, gw - 1.0, w)
    y0 = np.floor(yi).astype(int)
    x0 = np.floor(xi).astype(int)
    y1 = np.minimum(y0 + 1, gh - 1)
    x1 = np.minimum(x0 + 1, gw - 1)
    fy = (yi - y0)[:, None]
    fx = (xi - x0)[None, :]
    fy = fy * fy * (3 - 2 * fy)
    fx = fx * fx * (3 - 2 * fx)
    a = grid[np.ix_(y0, x0)]
    b = grid[np.ix_(y0, x1)]
    c = grid[np.ix_(y1, x0)]
    d = grid[np.ix_(y1, x1)]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def blobs(h: int, w: int, cells: int, seed: int, octaves: int = 2) -> np.ndarray:
    """Low-frequency field in 0..1 - big soft patches, never pixel noise."""
    out = np.zeros((h, w), dtype=np.float32)
    amp = 1.0
    total = 0.0
    c = cells
    for i in range(octaves):
        rs = np.random.RandomState(seed + i * 977)
        out += amp * _bilerp(rs.rand(c + 1, c + 1).astype(np.float32), h, w)
        total += amp
        amp *= 0.5
        c *= 2
    return out / total


def steps(t: np.ndarray, colors: Sequence[np.ndarray]) -> np.ndarray:
    """Map a 0..1 field onto flat palette bands (posterized, painterly)."""
    n = len(colors)
    idx = np.clip((t * n).astype(int), 0, n - 1)
    pal = np.stack(colors)
    return pal[idx]


def over(base: np.ndarray, color: np.ndarray, mask: np.ndarray) -> np.ndarray:
    m = np.clip(mask, 0.0, 1.0)[..., None]
    return base * (1.0 - m) + color[None, None, :] * m


def grid_xy(h: int, w: int) -> Tuple[np.ndarray, np.ndarray]:
    """Pixel centres. Row 0 is the BOTTOM row (Blender image order), so ys
    already measures upwards - never invert it again downstream."""
    ys, xs = np.mgrid[0:h, 0:w]
    return xs.astype(np.float32) + 0.5, ys.astype(np.float32) + 0.5


def drips(h: int, w: int, seed: int, count: int, top: float = 1.0, length: float = 0.55) -> np.ndarray:
    """Vertical grime runs starting near the top edge (y is up)."""
    rs = np.random.RandomState(seed)
    xs, ys = grid_xy(h, w)
    mask = np.zeros((h, w), dtype=np.float32)
    for _ in range(count):
        cx = rs.rand() * w
        wid = rs.uniform(0.006, 0.022) * w
        ln = rs.uniform(0.35, 1.0) * length * h
        y_top = top * h
        prof = np.clip(1.0 - np.abs(xs - cx) / wid, 0.0, 1.0)
        prof = prof ** 0.7
        down = np.clip((y_top - ys) / max(ln, 1.0), 0.0, 1.0)
        fade = np.clip(1.0 - down, 0.0, 1.0) ** 0.8
        band = np.where((ys <= y_top) & (ys >= y_top - ln), 1.0, 0.0)
        mask = np.maximum(mask, prof * fade * band * rs.uniform(0.35, 0.85))
    return mask


def seg(h: int, w: int, p0: Sequence[float], p1: Sequence[float], width: float) -> np.ndarray:
    """Thick line segment, coords normalized 0..1 (y up), width in x-units."""
    xs, ys = grid_xy(h, w)
    x = xs / w
    y = ys / h
    ax, ay = float(p0[0]), float(p0[1])
    bx, by = float(p1[0]), float(p1[1])
    px, py = bx - ax, by - ay
    l2 = px * px + py * py
    if l2 < 1e-9:
        l2 = 1e-9
    t = np.clip(((x - ax) * px + (y - ay) * py) / l2, 0.0, 1.0)
    dx = x - (ax + t * px)
    dy = y - (ay + t * py)
    d = np.sqrt(dx * dx + dy * dy)
    return np.clip((width * 0.5 - d) * (2.0 * w / max(w, h)) * 6.0 + 0.5, 0.0, 1.0)


def ring(
    h: int,
    w: int,
    cx: float,
    cy: float,
    rx: float,
    ry: float,
    width: float,
    a0: float = -math.pi,
    a1: float = math.pi,
) -> np.ndarray:
    xs, ys = grid_xy(h, w)
    x = xs / w
    y = ys / h
    nx = (x - cx) / max(rx, 1e-6)
    ny = (y - cy) / max(ry, 1e-6)
    d = np.sqrt(nx * nx + ny * ny)
    band = np.clip((width * 0.5 / rx - np.abs(d - 1.0)) * 8.0 + 0.5, 0.0, 1.0)
    ang = np.arctan2(ny, nx)
    inside = (ang >= a0) & (ang <= a1)
    return band * inside


def rect(h: int, w: int, x0: float, y0: float, x1: float, y1: float) -> np.ndarray:
    xs, ys = grid_xy(h, w)
    x = xs / w
    y = ys / h
    return ((x >= x0) & (x <= x1) & (y >= y0) & (y <= y1)).astype(np.float32)


def edge_darken(img: np.ndarray, amount: float = 0.16, falloff: float = 0.12) -> np.ndarray:
    """Painterly AO around the tile border."""
    h, w = img.shape[:2]
    xs, ys = grid_xy(h, w)
    x = xs / w
    y = ys / h
    d = np.minimum(np.minimum(x, 1 - x), np.minimum(y, 1 - y))
    k = np.clip(d / falloff, 0.0, 1.0)
    # kept gentle: URP lighting darkens the result again on top of this
    shade = 1.0 - amount * 0.65 * (1.0 - k)
    return img * shade[..., None]


def bottom_grime(img: np.ndarray, color: np.ndarray, height: float = 0.3, strength: float = 0.6) -> np.ndarray:
    h, w = img.shape[:2]
    _, ys = grid_xy(h, w)
    y = ys / h
    m = np.clip((height - y) / max(height, 1e-6), 0.0, 1.0) ** 1.5 * strength
    return over(img, color, m)


# ---------------------------------------------------------------------------
# tile painters
# ---------------------------------------------------------------------------
def t_plaster(res: int, seed: int, worn: bool = False) -> np.ndarray:
    pal = [rgb("#e3ddcd"), rgb("#dbd4c3"), rgb("#d1cab8"), rgb("#c5bda9")]
    img = steps(blobs(res, res, 2, seed, 1), pal)
    # broad damp patches - kept low contrast because one tile is stretched
    # across a whole 16 m wall, so anything punchy turns into a giant stain
    img = over(img, rgb("#c9c1ad"), (blobs(res, res, 2, seed + 31) > 0.70) * 0.30)
    img = over(img, rgb("#bab294"), (blobs(res, res, 5, seed + 7) > 0.84) * 0.22)
    img = over(img, rgb("#9a927c"), drips(res, res, seed + 12, 9, top=1.0, length=0.85) * 0.30)
    if worn:
        # exposed masonry patches
        m = (blobs(res, res, 4, seed + 55) > 0.70).astype(np.float32)
        img = over(img, rgb("#9c7a5e"), m * 0.85)
        bricks = np.zeros((res, res), dtype=np.float32)
        for i in range(0, res, 22):
            bricks[max(i - 1, 0) : i + 1, :] = 1.0
        img = over(img, rgb("#7d5f47"), bricks * m * 0.8)
    img = bottom_grime(img, rgb("#6b6450"), 0.22, 0.5)
    return edge_darken(img, 0.14)


def t_blue_plinth(res: int, seed: int) -> np.ndarray:
    pal = [rgb("#3d6fa0"), rgb("#356392"), rgb("#2c5580"), rgb("#24466b")]
    img = steps(blobs(res, res, 3, seed, 2), pal)
    # paint chipped off -> plaster shows through, but only in small flecks
    chip = (blobs(res, res, 7, seed + 17) > 0.86).astype(np.float32)
    img = over(img, rgb("#c9c2ae"), chip * 0.75)
    img = over(img, rgb("#8d7a5a"), (blobs(res, res, 9, seed + 23) > 0.90) * 0.45)
    img = over(img, rgb("#1c3550"), drips(res, res, seed + 5, 6, length=0.6) * 0.35)
    img = bottom_grime(img, rgb("#4a4436"), 0.26, 0.5)
    return edge_darken(img, 0.16)


def t_corrugated(res: int, seed: int, rusty: bool = False) -> np.ndarray:
    if rusty:
        pal = [rgb("#8a5a35"), rgb("#7a4d2c"), rgb("#6b4326"), rgb("#5a3720")]
    else:
        pal = [rgb("#9aa1a4"), rgb("#8d9497"), rgb("#7e8589"), rgb("#6f767a")]
    img = steps(blobs(res, res, 3, seed, 1), pal)
    xs, _ = grid_xy(res, res)
    period = max(res // 10, 6)
    phase = (xs % period) / period
    # crisp rib: bright crown, dark valley
    crown = np.clip(1.0 - np.abs(phase - 0.30) * 6.0, 0.0, 1.0)
    valley = np.clip(1.0 - np.abs(phase - 0.80) * 5.0, 0.0, 1.0)
    img = over(img, pal[0] * 1.22, crown * 0.55)
    img = over(img, pal[-1] * 0.62, valley * 0.6)
    if not rusty:
        img = over(img, rgb("#7d4a28"), (blobs(res, res, 4, seed + 44) > 0.76) * 0.55)
    img = bottom_grime(img, rgb("#4a3524"), 0.28, 0.55)
    return edge_darken(img, 0.12)


def t_wood(res: int, seed: int, plank_px: int = 40, warm: bool = True) -> np.ndarray:
    pal = (
        [rgb("#9a6a3c"), rgb("#8b5f34"), rgb("#7b532c"), rgb("#6b4726")]
        if warm
        else [rgb("#8a7c63"), rgb("#7c6f58"), rgb("#6d614c"), rgb("#5d5341")]
    )
    img = np.zeros((res, res, 3), dtype=np.float32)
    rs = np.random.RandomState(seed)
    y = 0
    while y < res:
        hgt = plank_px
        c = pal[rs.randint(0, len(pal))]
        img[y : y + hgt, :] = c
        # grain: two or three darker sweeps per plank
        for _ in range(3):
            gy = y + rs.randint(4, max(hgt - 4, 5))
            wob = (np.sin(np.linspace(0, rs.uniform(2, 6), res)) * 2.0).astype(int)
            rows = np.clip(gy + wob, y, min(y + hgt - 1, res - 1))
            img[rows, np.arange(res)] = c * 0.82
        # plank gap
        img[max(y - 2, 0) : y + 1, :] = c * 0.45
        y += hgt
    img = over(img, rgb("#4a3a26"), (blobs(res, res, 4, seed + 9) > 0.78) * 0.35)
    return edge_darken(img, 0.15)


def t_rust(res: int, seed: int, dark: bool = False) -> np.ndarray:
    pal = (
        [rgb("#5a3a22"), rgb("#4a2f1b"), rgb("#3b2515"), rgb("#2d1c10")]
        if dark
        else [rgb("#a06238"), rgb("#8c5330"), rgb("#743f22"), rgb("#5d3119")]
    )
    img = steps(blobs(res, res, 3, seed, 2), pal)
    img = over(img, rgb("#b8763f"), (blobs(res, res, 5, seed + 13) > 0.72) * 0.6)
    img = over(img, rgb("#3a2412"), (blobs(res, res, 6, seed + 29) > 0.80) * 0.55)
    img = over(img, rgb("#2e1d10"), drips(res, res, seed + 4, 6, length=0.8) * 0.45)
    return edge_darken(img, 0.14)


def t_concrete(res: int, seed: int, floor: bool = False) -> np.ndarray:
    pal = [rgb("#b4b0a6"), rgb("#aba69c"), rgb("#a29d93"), rgb("#98938a")]
    img = steps(blobs(res, res, 2, seed, 1), pal)
    rs = np.random.RandomState(seed + 2)
    for _ in range(4):
        x0, y0 = rs.rand(), rs.rand()
        img = over(
            img,
            rgb("#6f6b63"),
            seg(res, res, (x0, y0), (np.clip(x0 + rs.uniform(-0.4, 0.4), 0, 1), np.clip(y0 + rs.uniform(-0.4, 0.4), 0, 1)), 0.007) * 0.6,
        )
    if floor:
        # oil stains and tyre scuffs
        img = over(img, rgb("#3a3833"), (blobs(res, res, 4, seed + 61) > 0.74) * 0.7)
        img = over(img, rgb("#5a564e"), (blobs(res, res, 2, seed + 71) > 0.55) * 0.25)
    else:
        img = over(img, rgb("#7c776d"), (blobs(res, res, 4, seed + 61) > 0.75) * 0.4)
    return edge_darken(img, 0.13)


def t_flat(res: int, seed: int, base: str, spread: float = 0.06, patch: str = None, patch_amt: float = 0.35) -> np.ndarray:
    b = rgb(base)
    pal = [b * (1.0 + spread), b, b * (1.0 - spread), b * (1.0 - spread * 1.8)]
    img = steps(blobs(res, res, 2, seed, 1), [np.clip(c, 0, 1) for c in pal])
    if patch:
        img = over(img, rgb(patch), (blobs(res, res, 5, seed + 19) > 0.76) * patch_amt)
    return edge_darken(img, 0.12)


def t_car(res: int, seed: int, base: str, rusty: float = 0.5) -> np.ndarray:
    b = rgb(base)
    # car paint stays close to its base colour - only a light panel-to-panel
    # shift, otherwise the body reads as camouflage instead of a paint job
    pal = [np.clip(b * 1.08, 0, 1), b, np.clip(b * 0.94, 0, 1), np.clip(b * 0.88, 0, 1)]
    img = steps(blobs(res, res, 2, seed, 1), pal)
    # rust blooms creep up from the sills
    r = blobs(res, res, 4, seed + 21)
    _, ys = grid_xy(res, res)
    low = np.clip((0.55 - ys / res) * 2.0, 0.0, 1.0)
    img = over(img, rgb("#8a5029"), np.clip((r - (1.0 - rusty * 0.55)) * 6.0, 0, 1) * low * 0.85)
    img = over(img, rgb("#5b3319"), np.clip((blobs(res, res, 7, seed + 37) - 0.74) * 8.0, 0, 1) * low * 0.7)
    # dust film at the bottom
    img = bottom_grime(img, rgb("#6d6552"), 0.3, 0.45)
    # a couple of paint scratches
    rs = np.random.RandomState(seed + 5)
    for _ in range(2):
        x0, y0 = rs.rand(), rs.rand()
        img = over(img, rgb("#c8c2b2"), seg(res, res, (x0, y0), (x0 + rs.uniform(-0.3, 0.3), y0 + rs.uniform(-0.1, 0.1)), 0.005) * 0.45)
    return edge_darken(img, 0.13)


def t_roof(res: int, seed: int) -> np.ndarray:
    pal = [rgb("#8e9094"), rgb("#84868a"), rgb("#797b7f"), rgb("#6d6f73")]
    img = steps(blobs(res, res, 4, seed, 1), pal)
    # membrane panel seams
    panel = max(res // 4, 8)
    xs, ys = grid_xy(res, res)
    seam = ((xs % panel) < 2).astype(np.float32) + ((ys % panel) < 2).astype(np.float32)
    img = over(img, rgb("#5c5e62"), np.clip(seam, 0, 1) * 0.7)
    # tar patches / repairs
    img = over(img, rgb("#4e4b47"), (blobs(res, res, 5, seed + 12) > 0.74) * 0.75)
    img = over(img, rgb("#7a5334"), (blobs(res, res, 6, seed + 45) > 0.82) * 0.5)
    return edge_darken(img, 0.10)


def t_rubber(res: int, seed: int) -> np.ndarray:
    pal = [rgb("#3a3d41"), rgb("#33363a"), rgb("#2c2f33"), rgb("#26292c")]
    img = steps(blobs(res, res, 3, seed, 1), pal)
    xs, _ = grid_xy(res, res)
    tread = ((xs % max(res // 16, 4)) < max(res // 32, 2)).astype(np.float32)
    img = over(img, rgb("#1c1e21"), tread * 0.6)
    img = over(img, rgb("#4a453c"), (blobs(res, res, 5, seed + 8) > 0.80) * 0.3)
    return edge_darken(img, 0.18)


def t_barrel(res: int, seed: int) -> np.ndarray:
    img = t_rust(res, seed, dark=False)
    img = over(img, rgb("#3f6a49"), (blobs(res, res, 2, seed + 3) > 0.45) * 0.55)
    _, ys = grid_xy(res, res)
    y = 1.0 - ys / res
    hoop = ((np.abs(y - 0.30) < 0.045) | (np.abs(y - 0.70) < 0.045)).astype(np.float32)
    img = over(img, rgb("#4a3a28"), hoop * 0.7)
    return img


def t_glass(res: int, seed: int, dark: bool = False) -> np.ndarray:
    base = "#2b3740" if dark else "#8fa8b4"
    img = t_flat(res, seed, base, 0.10)
    # diagonal reflection band
    img = over(img, rgb("#d6e4ea") if not dark else rgb("#63757f"), seg(res, res, (0.05, 0.15), (0.75, 1.05), 0.22) * 0.35)
    img = over(img, rgb("#b9c9d2") if not dark else rgb("#4c5b64"), seg(res, res, (0.35, 0.05), (0.95, 0.75), 0.10) * 0.3)
    if not dark:
        img = over(img, rgb("#6c7d86"), (blobs(res, res, 5, seed + 6) > 0.80) * 0.3)
    return edge_darken(img, 0.10)


def t_engine(res: int, seed: int) -> np.ndarray:
    img = t_flat(res, seed, "#3b3f42", 0.12)
    img = over(img, rgb("#6b6f72"), (blobs(res, res, 5, seed + 11) > 0.68) * 0.5)
    img = over(img, rgb("#8a5a2c"), (blobs(res, res, 7, seed + 27) > 0.80) * 0.45)
    img = over(img, rgb("#1d2022"), drips(res, res, seed + 3, 4, length=0.5) * 0.4)
    return edge_darken(img, 0.14)


def t_crate(res: int, seed: int) -> np.ndarray:
    img = t_wood(res, seed, plank_px=max(res // 5, 10), warm=True)
    # frame boards around the edge
    frame = np.zeros((res, res), dtype=np.float32)
    b = max(res // 12, 6)
    frame[:b, :] = 1.0
    frame[-b:, :] = 1.0
    frame[:, :b] = 1.0
    frame[:, -b:] = 1.0
    img = over(img, rgb("#6a4526"), frame * 0.55)
    return img


def t_crane(res: int, seed: int, worn: bool = False) -> np.ndarray:
    """Construction yellow that has stood outside for a decade."""
    pal = [rgb("#d9a437"), rgb("#c8952a"), rgb("#b58524"), rgb("#a2751f")]
    img = steps(blobs(res, res, 2, seed, 1), pal)
    amount = 0.85 if worn else 0.55
    img = over(img, rgb("#8a5029"), np.clip((blobs(res, res, 4, seed + 21) - 0.62) * 5.0, 0, 1) * amount)
    img = over(img, rgb("#5b3319"), np.clip((blobs(res, res, 7, seed + 37) - 0.78) * 7.0, 0, 1) * amount)
    img = over(img, rgb("#6b5a2a"), drips(res, res, seed + 9, 7, length=0.8) * 0.4)
    img = bottom_grime(img, rgb("#5a4a28"), 0.22, 0.45)
    return edge_darken(img, 0.14)


def t_hazard(res: int, seed: int) -> np.ndarray:
    """Diagonal warning stripes."""
    img = steps(blobs(res, res, 2, seed, 1), [rgb("#d9a437"), rgb("#c8952a")])
    xs, ys = grid_xy(res, res)
    period = max(res // 5, 8)
    stripe = (((xs + ys) % period) < period * 0.5).astype(np.float32)
    img = over(img, rgb("#26282a"), stripe)
    img = over(img, rgb("#7a4a28"), (blobs(res, res, 5, seed + 13) > 0.74) * 0.45)
    img = bottom_grime(img, rgb("#4a4028"), 0.25, 0.5)
    return edge_darken(img, 0.14)


def t_magnet(res: int, seed: int) -> np.ndarray:
    pal = [rgb("#4a4e52"), rgb("#42464a"), rgb("#3a3e42"), rgb("#33373a")]
    img = steps(blobs(res, res, 2, seed, 1), pal)
    _, ys = grid_xy(res, res)
    band = (np.abs(ys / res - 0.62) < 0.07).astype(np.float32)
    img = over(img, rgb("#8a5029"), band * 0.55)
    img = over(img, rgb("#6b4526"), (blobs(res, res, 5, seed + 17) > 0.76) * 0.5)
    return edge_darken(img, 0.16)


def t_canvas(res: int, seed: int) -> np.ndarray:
    img = t_flat(res, seed, "#5c6249", 0.12)
    img = over(img, rgb("#464b38"), (blobs(res, res, 4, seed + 15) > 0.62) * 0.4)
    return img


# ---------------------------------------------------------------------------
# the sign: АВТОСЕРВИС painted straight into the atlas
# ---------------------------------------------------------------------------
def _glyph(res_h: int, res_w: int, ch: str, sw: float) -> np.ndarray:
    """Stroke-built Cyrillic glyph mask in its own box (y up, 0..1)."""
    h, w = res_h, res_w
    m = np.zeros((h, w), dtype=np.float32)

    def line(p0, p1):
        return seg(h, w, p0, p1, sw)

    def arc(cx, cy, rx, ry, a0=-math.pi, a1=math.pi):
        return ring(h, w, cx, cy, rx, ry, sw, a0, a1)

    if ch == "А":
        m = np.maximum(m, line((0.06, 0.0), (0.5, 1.0)))
        m = np.maximum(m, line((0.94, 0.0), (0.5, 1.0)))
        m = np.maximum(m, line((0.22, 0.30), (0.78, 0.30)))
    elif ch == "В":
        m = np.maximum(m, line((0.12, 0.0), (0.12, 1.0)))
        m = np.maximum(m, line((0.12, 1.0), (0.66, 1.0)))
        m = np.maximum(m, arc(0.66, 0.78, 0.26, 0.22, -math.pi / 2, math.pi / 2))
        m = np.maximum(m, line((0.12, 0.56), (0.68, 0.56)))
        m = np.maximum(m, arc(0.68, 0.28, 0.28, 0.28, -math.pi / 2, math.pi / 2))
        m = np.maximum(m, line((0.12, 0.0), (0.68, 0.0)))
    elif ch == "Т":
        m = np.maximum(m, line((0.04, 1.0), (0.96, 1.0)))
        m = np.maximum(m, line((0.5, 1.0), (0.5, 0.0)))
    elif ch == "О":
        m = np.maximum(m, arc(0.5, 0.5, 0.44, 0.5))
    elif ch == "С":
        m = np.maximum(m, arc(0.52, 0.5, 0.44, 0.5))
        # cut the right side open
        cut = rect(h, w, 0.72, 0.30, 1.0, 0.70)
        m = m * (1.0 - cut)
    elif ch == "Е":
        m = np.maximum(m, line((0.14, 0.0), (0.14, 1.0)))
        m = np.maximum(m, line((0.14, 1.0), (0.92, 1.0)))
        m = np.maximum(m, line((0.14, 0.52), (0.80, 0.52)))
        m = np.maximum(m, line((0.14, 0.0), (0.92, 0.0)))
    elif ch == "Р":
        m = np.maximum(m, line((0.14, 0.0), (0.14, 1.0)))
        m = np.maximum(m, line((0.14, 1.0), (0.62, 1.0)))
        m = np.maximum(m, arc(0.62, 0.76, 0.30, 0.24, -math.pi / 2, math.pi / 2))
        m = np.maximum(m, line((0.14, 0.52), (0.62, 0.52)))
    elif ch == "И":
        m = np.maximum(m, line((0.12, 0.0), (0.12, 1.0)))
        m = np.maximum(m, line((0.88, 0.0), (0.88, 1.0)))
        m = np.maximum(m, line((0.12, 0.0), (0.88, 1.0)))
    return np.clip(m, 0.0, 1.0)


def paint_sign(h: int, w: int, seed: int, text: str = "АВТОСЕРВИС") -> np.ndarray:
    # weathered wooden board
    board = steps(blobs(h, w, 3, seed + 2), [rgb("#8a5a30"), rgb("#7c4f29"), rgb("#6d4423"), rgb("#5e3a1d")])
    plank = np.zeros((h, w), dtype=np.float32)
    for y in range(0, h, max(h // 3, 12)):
        plank[max(y - 2, 0) : y + 1, :] = 1.0
    board = over(board, rgb("#4a2d16"), plank * 0.55)
    board = over(board, rgb("#54331a"), (blobs(h, w, 5, seed + 8) > 0.74) * 0.45)
    img = board
    # dark frame
    frame = np.zeros((h, w), dtype=np.float32)
    b = max(h // 14, 3)
    frame[:b, :] = 1.0
    frame[-b:, :] = 1.0
    frame[:, :b] = 1.0
    frame[:, -b:] = 1.0
    img = over(img, rgb("#452a14"), frame * 0.8)

    # letters
    n = len(text)
    margin = 0.045
    avail = 1.0 - 2 * margin
    adv = avail / n
    gw = int(adv * w * 0.86)
    gh = int(h * 0.62)
    sw = 0.20
    ink = rgb("#e9d9a6")
    shadow = rgb("#3a2410")
    y0 = int(h * 0.19)
    for i, ch in enumerate(text):
        gm = _glyph(gh, gw, ch, sw)
        x0 = int((margin + i * adv + adv * 0.07) * w)
        sl_y = slice(y0, y0 + gh)
        sl_x = slice(x0, x0 + gw)
        sub = img[sl_y, sl_x]
        if sub.shape[0] != gh or sub.shape[1] != gw:
            continue
        # drop shadow, then ink
        sh = np.zeros_like(gm)
        sh[:-3, 3:] = gm[3:, :-3]
        sub = over(sub, shadow, sh * 0.55)
        sub = over(sub, ink, gm)
        img[sl_y, sl_x] = sub

    # wear over the letters so they read as old paint
    img = over(img, rgb("#6d4423"), (blobs(h, w, 8, seed + 41) > 0.86) * 0.5)
    img = bottom_grime(img, rgb("#3f2812"), 0.2, 0.4)
    return edge_darken(img, 0.10, 0.05)


# ---------------------------------------------------------------------------
# assembly
# ---------------------------------------------------------------------------
def build_atlas_array() -> np.ndarray:
    img = np.zeros((ATLAS, ATLAS, 3), dtype=np.float32)
    r = TILE

    painters = {
        "plaster": lambda s: t_plaster(r, s),
        "plaster_worn": lambda s: t_plaster(r, s, worn=True),
        "blue_plinth": lambda s: t_blue_plinth(r, s),
        "corrugated": lambda s: t_corrugated(r, s),
        "corr_rust": lambda s: t_corrugated(r, s, rusty=True),
        "wood": lambda s: t_wood(r, s),
        "rubber": lambda s: t_rubber(r, s),
        "rust": lambda s: t_rust(r, s),
        "concrete": lambda s: t_concrete(r, s),
        "blue_metal": lambda s: t_flat(r, s, "#2f5f96", 0.12, "#7a4a28", 0.3),
        "car_blue": lambda s: t_car(r, s, "#3f6a92", 0.55),
        "car_orange": lambda s: t_car(r, s, "#a2542c", 0.7),
        "car_beige": lambda s: t_car(r, s, "#c3bda6", 0.6),
        "car_green": lambda s: t_car(r, s, "#5c7c46", 0.65),
        "roof": lambda s: t_roof(r, s),
        "dark": lambda s: t_flat(r, s, "#1a1c1e", 0.10),
        "red": lambda s: t_flat(r, s, "#9c3327", 0.12, "#5a2016", 0.3),
        "yellow": lambda s: t_flat(r, s, "#c9a132", 0.12, "#6d5a1e", 0.3),
        "frame": lambda s: t_flat(r, s, "#6e7275", 0.12, "#7c4c28", 0.35),
        "engine": lambda s: t_engine(r, s),
        "paper": lambda s: t_flat(r, s, "#ccc6b0", 0.08, "#8a8168", 0.25),
        "dirt": lambda s: t_flat(r, s, "#6f5c3d", 0.14, "#4e3f28", 0.4),
        "glass": lambda s: t_glass(r, s),
        "tool": lambda s: t_flat(r, s, "#55595c", 0.14, "#7a4a28", 0.3),
        "interior_wall": lambda s: t_flat(r, s, "#b9c1bb", 0.10, "#8a8f88", 0.35),
        "crate": lambda s: t_crate(r, s),
        "barrel": lambda s: t_barrel(r, s),
        "chrome": lambda s: t_flat(r, s, "#a9adb0", 0.14, "#6d7174", 0.3),
        "glass_dark": lambda s: t_glass(r, s, dark=True),
        "hubcap": lambda s: t_flat(r, s, "#8c9093", 0.14, "#6b4526", 0.35),
        "headlight": lambda s: t_flat(r, s, "#d9d3ae", 0.10, "#8f8a70", 0.3),
        "taillight": lambda s: t_flat(r, s, "#8e2b22", 0.12, "#4e1810", 0.3),
        "seat": lambda s: t_flat(r, s, "#6b5540", 0.12, "#4a3a2a", 0.35),
        "interior_dark": lambda s: t_flat(r, s, "#26282a", 0.12),
        "rust_dark": lambda s: t_rust(r, s, dark=True),
        "bumper": lambda s: t_flat(r, s, "#8b8f92", 0.14, "#6b4526", 0.4),
        "floor": lambda s: t_concrete(r, s, floor=True),
        "asphalt": lambda s: t_flat(r, s, "#57585a", 0.12, "#3f4042", 0.35),
        "grime": lambda s: t_flat(r, s, "#4a4336", 0.14, "#2f2a20", 0.4),
        "canvas": lambda s: t_canvas(r, s),
        "crane": lambda s: t_crane(r, s),
        "crane_worn": lambda s: t_crane(r, s, worn=True),
        "hazard": lambda s: t_hazard(r, s),
        "magnet": lambda s: t_magnet(r, s),
        "cab": lambda s: t_flat(r, s, "#6d7a63", 0.10, "#7a4a28", 0.4),
        "steel": lambda s: t_flat(r, s, "#7a7f83", 0.12, "#6b4526", 0.35),
    }

    for i, (key, (col, row)) in enumerate(sorted(TILES.items(), key=lambda kv: kv[1])):
        tile = painters[key](1000 + i * 17)
        img[row * r : (row + 1) * r, col * r : (col + 1) * r] = np.clip(tile, 0.0, 1.0)

    col, row, cw, ch = SIGN_RECT_TILES
    sign = paint_sign(ch * r, cw * r, 4242)
    img[row * r : (row + ch) * r, col * r : (col + cw) * r] = np.clip(sign, 0.0, 1.0)

    return img


def save_atlas(path: str, name: str = "AutoService_Atlas"):
    """Build and write the atlas PNG. Returns the bpy image."""
    import bpy

    arr = build_atlas_array()
    lin = srgb_to_linear(arr)
    rgba = np.dstack([lin, np.ones((ATLAS, ATLAS), dtype=np.float32)])

    img = bpy.data.images.get(name)
    if img is not None:
        bpy.data.images.remove(img)
    img = bpy.data.images.new(name, ATLAS, ATLAS, alpha=False)
    img.pixels.foreach_set(rgba.ravel())
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    return img
