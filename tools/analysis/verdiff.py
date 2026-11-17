"""Tổng hợp khác biệt giữa file 2019 (<tên>_21.cdr) và bản Corel 2019 tự Save As xuống bản cũ (<tên>_<v>.cdr).

  python verdiff.py <v> [--detail <loại>]

In theo nhóm: mục ZIP, cây chunk, loại chunk có nội dung khác, đối số loda khác. Dùng để rút quy tắc 2019 → v.
"""
import collections
import glob
import os
import re
import sys
import zipfile

sys.path.insert(0, os.path.dirname(__file__))
from cdrtree import load  # noqa: E402
from lodadiff import byte_ops, parse_loda  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))


def pairs(v):
    for older in sorted(glob.glob(os.path.join(ROOT, "samples", "older", f"*_{v}.cdr"))):
        name = os.path.basename(older)[: -len(f"_{v}.cdr")]
        for d in ("samples/gen", "out/real"):
            src = os.path.join(ROOT, d, name + "_21.cdr")
            if os.path.exists(src):
                yield name, src, older


def norm_path(p):
    return re.sub(r"\[\d+\]", "", p)


if __name__ == "__main__":
    v = sys.argv[1]
    detail = sys.argv[sys.argv.index("--detail") + 1] if "--detail" in sys.argv else None
    zip_diff, tree_diff, chunk_diff, arg_diff = collections.Counter(), collections.Counter(), collections.Counter(), collections.Counter()
    examples = {}
    n = 0
    for name, a_path, b_path in pairs(v):
        n += 1
        za, zb = zipfile.ZipFile(a_path), zipfile.ZipFile(b_path)
        for e in sorted(set(za.namelist()) | set(zb.namelist())):
            if e.startswith("content/"):
                continue
            if e not in za.namelist():
                zip_diff[f"+ {e}"] += 1
            elif e not in zb.namelist():
                zip_diff[f"- {e}"] += 1
            elif za.read(e) != zb.read(e):
                zip_diff[f"~ {e}"] += 1
        la, lb = load(a_path), load(b_path)
        pa, pb = [norm_path(p) for p, _, _ in la], [norm_path(p) for p, _, _ in lb]
        if pa != pb:
            sa, sb = collections.Counter(pa), collections.Counter(pb)
            for k in (sb - sa):
                tree_diff[f"+ {k}"] += 1
            for k in (sa - sb):
                tree_diff[f"- {k}"] += 1
        da, db = {p: (c, d) for p, c, d in la}, {p: (c, d) for p, c, d in lb}
        for p, (c, d) in db.items():
            if p not in da or da[p][1] == d:
                continue
            chunk_diff[c] += 1
            if c == "loda":
                try:
                    ta, aa = parse_loda(da[p][1])
                    tb, ab = parse_loda(d)
                except Exception:
                    arg_diff[("?", "unparsable")] += 1
                    continue
                for t in sorted(set(aa) | set(ab)):
                    if t not in aa:
                        arg_diff[(tb, f"+0x{t:x}")] += 1
                        examples.setdefault((tb, f"+0x{t:x}"), (name, p, ab[t][:48].hex()))
                    elif t not in ab:
                        arg_diff[(ta, f"-0x{t:x}")] += 1
                        examples.setdefault((ta, f"-0x{t:x}"), (name, p, aa[t][:48].hex()))
                    elif aa[t] != ab[t]:
                        arg_diff[(ta, f"~0x{t:x} {len(aa[t])}->{len(ab[t])}")] += 1
                        examples.setdefault((ta, f"~0x{t:x} {len(aa[t])}->{len(ab[t])}"), (name, p, byte_ops(aa[t], ab[t])[:3]))
            elif detail == c:
                print(f"{name} {p} {len(da[p][1])}->{len(d)} {byte_ops(da[p][1], d)[:4]}")
    print(f"== 2019 → {v}: {n} cặp file")
    print("Mục ZIP:", dict(zip_diff))
    print("Cây chunk:", dict(tree_diff))
    print("Chunk khác nội dung:", dict(chunk_diff))
    print("Đối số loda:")
    for k, c in arg_diff.most_common():
        print(f"  {c:4} obj={k[0]} {k[1]}   vd: {examples.get(k)}")
