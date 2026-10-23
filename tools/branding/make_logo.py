"""Tạo logo C_Converter (PNG cho giao diện + ICO cho file exe).

  python tools/branding/make_logo.py

Thiết kế: ô vuông bo góc nền đen, chữ "C" vàng dày, trong lòng chữ C là mũi tên đi xuống
(hạ phiên bản). Vẽ ở 4x độ phân giải rồi thu nhỏ để khử răng cưa.
"""
import os

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "..", "src", "CConvert.App", "Assets")

BG_TOP = (34, 34, 34)
BG_BOTTOM = (10, 10, 10)
YELLOW = (255, 196, 0)
YELLOW_DARK = (230, 160, 0)


def draw(size=1024, ss=4):
    S = size * ss
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))

    # Nền: ô bo góc với chuyển màu dọc đen than → đen.
    grad = Image.new("RGBA", (S, S))
    gd = ImageDraw.Draw(grad)
    for y in range(S):
        t = y / (S - 1)
        c = tuple(int(BG_TOP[i] + (BG_BOTTOM[i] - BG_TOP[i]) * t) for i in range(3)) + (255,)
        gd.line([(0, y), (S, y)], fill=c)
    mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, S - 1, S - 1], radius=int(S * 0.22), fill=255)
    img.paste(grad, (0, 0), mask)

    d = ImageDraw.Draw(img)
    # Viền vàng mảnh bên trong.
    inset = int(S * 0.035)
    d.rounded_rectangle([inset, inset, S - 1 - inset, S - 1 - inset], radius=int(S * 0.19),
                        outline=YELLOW_DARK, width=int(S * 0.012))

    # Chữ "C": cung tròn dày, hở bên phải.
    cx, cy, r = S * 0.5, S * 0.5, S * 0.30
    w = int(S * 0.115)
    d.arc([cx - r, cy - r, cx + r, cy + r], start=40, end=320, fill=YELLOW, width=w)
    # Bo tròn hai đầu cung.
    import math
    for ang in (40, 320):
        a = math.radians(ang)
        rr = r - w / 2
        ex, ey = cx + rr * math.cos(a), cy + rr * math.sin(a)
        d.ellipse([ex - w / 2, ey - w / 2, ex + w / 2, ey + w / 2], fill=YELLOW)

    # Mũi tên đi xuống trong lòng chữ C.
    aw = S * 0.065          # nửa bề rộng thân
    top, mid, bottom = S * 0.355, S * 0.495, S * 0.635
    head = S * 0.13         # nửa bề rộng đầu mũi tên
    ax = cx + S * 0.02
    d.rounded_rectangle([ax - aw, top, ax + aw, mid + S * 0.02], radius=int(aw * 0.5), fill=YELLOW)
    d.polygon([(ax - head, mid), (ax + head, mid), (ax, bottom)], fill=YELLOW)

    return img.resize((size, size), Image.LANCZOS)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    big = draw(1024)
    big.resize((256, 256), Image.LANCZOS).save(os.path.join(OUT, "logo.png"))
    big.save(os.path.join(HERE, "logo_1024.png"))
    big.save(os.path.join(OUT, "app.ico"), sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
    print("đã ghi", OUT)
