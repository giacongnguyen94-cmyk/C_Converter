"""So từng byte: C_Converter hạ <tên>_27.cdr (2026) xuống mọi bản đích, đối chiếu file CorelDRAW 2026 tự Save As
cùng bản đích (<tên>_<v>.cdr). In các loại bản ghi khác nhau theo bản đích.

  python byte_targets.py <CConvert.Cli.exe> <thư mục mẫu> [<thư mục mẫu> …]
"""
import collections
import hashlib
import os
import subprocess
import sys
import tempfile
import zipfile

sys.path.insert(0, os.path.dirname(__file__))
import cdrtree  # noqa: E402

TARGETS = {"2025": "26", "2024": "25", "2023": "24.3", "2022": "24", "2021": "23", "2020": "22", "2019": "21",
           "2018": "20", "2017": "19", "X8": "18", "X7": "17"}
# Corel 2026 Save As 2023 ghi số hiệu 24 (2022) — xem ghi chú; 2023 so với _24 không áp dụng.


def leaves(path):
    return collections.Counter((cc, hashlib.md5(d).hexdigest()) for _, cc, d in cdrtree.load(path))


def entry_types(path):
    z = zipfile.ZipFile(path)
    return {i.filename for i in z.infolist()}


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    cli = sys.argv[1]
    tmp = tempfile.mkdtemp(prefix="cconv_bt_")
    total = collections.Counter()
    by_target = collections.defaultdict(collections.Counter)
    fails = []
    for folder in sys.argv[2:]:
        for f in sorted(os.listdir(folder)):
            if not f.endswith("_27.cdr"):
                continue
            design = f[:-7]
            src = os.path.join(folder, f)
            for t, v in TARGETS.items():
                ref = os.path.join(folder, f"{design}_{v}.cdr")
                if not os.path.exists(ref):
                    continue
                out = os.path.join(tmp, f"{design}_{t}.cdr")
                if os.path.exists(out):
                    os.remove(out)
                r = subprocess.run([cli, src, "--to", t, "--out", tmp], capture_output=True, text=True, encoding="utf-8")
                gen = os.path.join(tmp, f"{design}_27_{t}.cdr")
                if r.returncode != 0 or not os.path.exists(gen):
                    fails.append((design, t, r.stdout[-400:]))
                    total["thất bại"] += 1
                    continue
                a, b = leaves(gen), leaves(ref)
                da = collections.Counter(cc for cc, _ in (a - b).elements())
                db = collections.Counter(cc for cc, _ in (b - a).elements())
                kinds = tuple(sorted(set(da) | set(db)))
                total["ca"] += 1
                if not kinds:
                    total["trùng"] += 1
                    by_target[t]["trùng"] += 1
                else:
                    by_target[t]["khác"] += 1
                    print(f"{design:28} → {t:5}: " + ", ".join(f"{k} {da[k]}/{db[k]}" for k in kinds))
                os.remove(gen)
    for d, t, log in fails:
        print("THẤT BẠI", d, t, log)
    print()
    for t in TARGETS:
        if by_target[t]:
            print(f"{t:>5}: trùng mọi bản ghi {by_target[t]['trùng']}/{by_target[t]['trùng'] + by_target[t]['khác']}")
    print(dict(total))
