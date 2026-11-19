"""Đối chiếu khối text (txsm) sau khi C_Converter hạ xuống 2019 với file Corel tự Save As 2019.

  python txsm_check.py <CConvert.Cli.exe> <mẫu nguồn glob> <hậu tố nguồn, vd _27.cdr>
"""
import glob
import os
import subprocess
import sys
import tempfile

sys.path.insert(0, os.path.dirname(__file__))
import cdrtree  # noqa: E402

if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    cli, pattern, suffix = sys.argv[1:4]
    tmp = tempfile.mkdtemp(prefix="cconv_tx_")
    same = total = 0
    for f in sorted(glob.glob(pattern)):
        ref = f[: -len(suffix)] + "_21.cdr"
        if not os.path.exists(ref):
            continue
        a = [d for _, cc, d in cdrtree.load(ref) if cc == "txsm"]
        if not a:
            continue
        subprocess.run([cli, f, "--to", "2019", "--out", tmp], capture_output=True)
        out = os.path.join(tmp, os.path.basename(f)[:-4] + "_2019.cdr")
        b = [d for _, cc, d in cdrtree.load(out) if cc == "txsm"]
        k = sum(x == y for x, y in zip(a, b))
        same += k
        total += len(a)
        if k != len(a) or len(a) != len(b):
            print(f"KHÁC {os.path.basename(f)}: {k}/{len(a)} khối trùng (C_Converter {len(b)} khối)")
        os.remove(out)
    print(f"Tổng: {same}/{total} khối text trùng từng byte")
