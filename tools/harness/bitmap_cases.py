"""Ca kiểm chứng cho mẫu do CorelDRAW 2026 tạo (samples/bmp: ảnh TIF/PSD/PNG/JPG/BMP/GIF + xử lý bitmap;
samples/gen27: 42 mẫu đồ họa). Đáp án: <tên>_21.cdr do CorelDRAW 2026 tự Save As 2019.

  python bitmap_cases.py <CConvert.Cli.exe> <thư mục out>
Rồi mở bằng verify-2019.ps1 và so bằng tools/analysis/compare_many.py (như many_cases.py).
"""
import json
import os
import shutil
import sys

sys.path.insert(0, os.path.dirname(__file__))
from many_cases import REPO, convert  # noqa: E402

PLANS = {"27": ["2019", "2018", "X7"], "25": ["2019", "X7"]}


def main(cli, out):
    conv, work = os.path.join(out, "conv"), os.path.join(out, "work")
    shutil.rmtree(conv, ignore_errors=True)
    shutil.rmtree(work, ignore_errors=True)
    os.makedirs(conv)
    cases, failures = [], []
    for folder, group in (("samples/bmp", "F"), ("samples/gen27", "G")):
        d = os.path.join(REPO, folder)
        for f in sorted(os.listdir(d)):
            if not f.endswith("_21.cdr"):
                continue
            design = f[: -len("_21.cdr")]
            ref = os.path.join(d, f)
            for src_v, targets in PLANS.items():
                src = os.path.join(d, f"{design}_{src_v}.cdr")
                if not os.path.exists(src):
                    continue
                for t in targets:
                    name = f"{design}__{src_v}-{t}"
                    res, log = convert(cli, src, t, os.path.join(work, name))
                    if res is None:
                        failures.append({"case": name, "log": log})
                        print("THẤT BẠI", name)
                        continue
                    shutil.copy(res, os.path.join(conv, name + ".cdr"))
                    cases.append({"case": name, "group": group, "ref": os.path.relpath(ref, REPO), "warnings": log.count("[CẢNH BÁO]")})
    refs = sorted({c["ref"] for c in cases})
    json.dump({"cases": cases, "failures": failures, "refs": refs}, open(os.path.join(out, "manifest.json"), "w", encoding="utf-8"),
              ensure_ascii=False, indent=1)
    files = [os.path.join(REPO, r) for r in refs] + [os.path.join(conv, c["case"] + ".cdr") for c in cases]
    open(os.path.join(out, "files.txt"), "w", encoding="utf-8-sig").write("\n".join(files))
    print(f"{len(cases)} ca, {len(failures)} thất bại, {len(refs)} đáp án")


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main(sys.argv[1], os.path.abspath(sys.argv[2]))
