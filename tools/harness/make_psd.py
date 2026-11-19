"""Ghi file PSD thử (nhiều lớp, độ mờ, lớp ẩn, CMYK, xám) cho bộ kiểm thử ảnh. Chỉ dùng khi phát triển."""
import struct
import sys

import numpy as np


def pascal(name):
    b = name.encode("latin-1")[:255]
    s = bytes([len(b)]) + b
    return s + b"\0" * ((-len(s)) % 4)


def write_psd(path, size, mode, layers, merged):
    """layers: list (name, top, left, array HxWxC (C kênh màu + 1 alpha), opacity, hidden). merged: HxWxC."""
    h, w = size
    nch = merged.shape[2]
    out = bytearray(b"8BPS" + struct.pack(">H6xHIIHH", 1, nch, h, w, 8, mode))
    out += struct.pack(">I", 0)  # color mode data
    out += struct.pack(">I", 0)  # image resources
    recs, chdata = bytearray(), bytearray()
    for name, top, left, arr, opacity, hidden in layers:
        lh, lw, c = arr.shape
        ids = list(range(c - 1)) + [-1]
        recs += struct.pack(">iiiiH", top, left, top + lh, left + lw, c)
        for i, cid in enumerate(ids):
            recs += struct.pack(">hI", cid, 2 + lh * lw)
        extra = struct.pack(">I", 0) + struct.pack(">I", 0) + pascal(name)
        recs += b"8BIMnorm" + struct.pack(">BBBB", opacity, 0, 2 if hidden else 0, 0) + struct.pack(">I", len(extra)) + extra
        order = list(range(c - 1)) + [c - 1]
        for i in order:
            chdata += struct.pack(">H", 0) + arr[:, :, i].tobytes()
    info = struct.pack(">h", -len(layers)) + recs + chdata
    if len(info) % 2:
        info += b"\0"
    lm = struct.pack(">I", len(info)) + info + struct.pack(">I", 0)
    out += struct.pack(">I", len(lm)) + lm
    out += struct.pack(">H", 0)
    for i in range(nch):
        out += merged[:, :, i].tobytes()
    open(path, "wb").write(out)


def main(d):
    H, W = 240, 320
    x = np.linspace(0, 255, W)[None, :].repeat(H, 0)
    y = np.linspace(0, 255, H)[:, None].repeat(W, 1)
    bg = np.dstack([x, y, 255 - x, np.full((H, W), 255)]).astype(np.uint8)
    yy, xx = np.mgrid[0:120, 0:160]
    disc = (((xx - 80) / 80) ** 2 + ((yy - 60) / 60) ** 2 <= 1).astype(np.uint8) * 255
    red = np.dstack([np.full((120, 160), 230), np.full((120, 160), 30), np.full((120, 160), 40), disc]).astype(np.uint8)
    blue = np.dstack([np.zeros((60, 200)), np.zeros((60, 200)), np.full((60, 200), 255), np.full((60, 200), 255)]).astype(np.uint8)
    merged = bg[:, :, :3].copy()
    m = disc[:, :, None] / 255 * 0.6
    merged[60:180, 80:240] = (merged[60:180, 80:240] * (1 - m) + red[:, :, :3] * m).astype(np.uint8)
    write_psd(f"{d}/layers.psd", (H, W), 3, [("Nen", 0, 0, bg, 255, False), ("Hinh tron 60%", 60, 80, red, 153, False),
                                              ("Lop an", 10, 10, blue, 255, True)], merged)
    write_psd(f"{d}/single.psd", (H, W), 3, [("Layer 1", 0, 0, bg, 255, False)], bg[:, :, :3])
    cmyk = np.dstack([255 - x, 255 - y, x, np.full((H, W), 200), np.full((H, W), 255)]).astype(np.uint8)
    write_psd(f"{d}/cmyk.psd", (H, W), 4, [("CMYK", 0, 0, cmyk, 255, False)], cmyk[:, :, :4])
    g = np.dstack([x, np.full((H, W), 255)]).astype(np.uint8)
    write_psd(f"{d}/gray.psd", (H, W), 1, [("Xam", 0, 0, g, 255, False)], g[:, :, :1])


if __name__ == "__main__":
    main(sys.argv[1])
