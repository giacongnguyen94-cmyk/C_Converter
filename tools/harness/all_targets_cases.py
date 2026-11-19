"""Ca kiểm chứng mọi bản đích cho mẫu do CorelDRAW 2026 tạo (mỗi mẫu có <tên>_27.cdr và file Corel tự Save As
<tên>_<v>.cdr cho v = 26 … 17). C_Converter hạ <tên>_27 xuống từng bản đích; đáp án là file Corel cùng bản đích.

  python all_targets_cases.py <CConvert.Cli.exe> <thư mục out> <thư mục mẫu> [<thư mục mẫu> …]

Tạo <out>/old (đích X7–2019, mở bằng CorelDRAW 2019) và <out>/new (đích 2020–2025, mở bằng CorelDRAW 2026),
mỗi nơi có manifest.json + files.txt cho verify-2019.ps1 và tools/analysis/compare_many.py.
"""
import json
import os
import shutil
import sys

sys.path.insert(0, os.path.dirname(__file__))
from many_cases import convert  # noqa: E402

OLD = {"2019": "21", "2018": "20", "2017": "19", "X8": "18", "X7": "17"}
NEW = {"2025": "26", "2024": "25", "2022": "24", "2021": "23", "2020": "22"}


PARTS = {"old": OLD, "new": NEW}


def main(cli, out, folders):
    for part, targets in PARTS.items():
        root = os.path.join(out, part)
        conv = os.path.join(root, "conv")
        shutil.rmtree(root, ignore_errors=True)
        os.makedirs(conv)
        cases, failures = [], []
        for folder in folders:
            group = os.path.basename(os.path.normpath(folder))
            for f in sorted(os.listdir(folder)):
                if not f.endswith("_27.cdr"):
                    continue
                design = f[:-7]
                for t, v in targets.items():
                    ref = os.path.join(folder, f"{design}_{v}.cdr")
                    if not os.path.exists(ref):
                        continue
                    name = f"{design}__27-{t}"
                    res, log = convert(cli, os.path.join(folder, f), t, os.path.join(root, "work", name))
                    if res is None:
                        failures.append({"case": name, "log": log})
                        print("THẤT BẠI", name)
                        continue
                    shutil.copy(res, os.path.join(conv, name + ".cdr"))
                    cases.append({"case": name, "group": group, "ref": os.path.abspath(ref), "warnings": log.count("[CẢNH BÁO]")})
        shutil.rmtree(os.path.join(root, "work"), ignore_errors=True)
        refs = sorted({c["ref"] for c in cases})
        json.dump({"cases": cases, "failures": failures, "refs": refs}, open(os.path.join(root, "manifest.json"), "w", encoding="utf-8"),
                  ensure_ascii=False, indent=1)
        files = refs + [os.path.join(conv, c["case"] + ".cdr") for c in cases]
        open(os.path.join(root, "files.txt"), "w", encoding="utf-8-sig").write("\n".join(files))
        print(f"{part}: {len(cases)} ca, {len(failures)} thất bại, {len(refs)} đáp án")


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    if sys.argv[1] == "--parts":
        # vd --parts "o19=2019,X7;n26=2025,2024,2022,2021,2020,2018,2017,X8"
        allv = {**OLD, **NEW}
        PARTS.clear()
        for part in sys.argv[2].split(";"):
            k, v = part.split("=")
            PARTS[k] = {t: allv[t] for t in v.split(",")}
        del sys.argv[1:3]
    main(sys.argv[1], os.path.abspath(sys.argv[2]), sys.argv[3:])
