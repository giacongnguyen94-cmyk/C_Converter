"""Bộ kiểm chứng nhiều trường hợp (chỉ dùng khi phát triển).

Dùng C_Converter (CLI) hạ các thiết kế mẫu theo mọi đường nguồn → đích có đáp án, ghi vào <out>/conv,
kèm manifest.json: mỗi ca → file đáp án do CorelDRAW tự Save As cùng thiết kế ở đúng bản đích.
Sau đó mở tất cả bằng CorelDRAW 2019 (verify-2019.ps1) và so bằng tools/analysis/compare_many.py.

  python many_cases.py <CConvert.Cli.exe> <thư mục out>

Các nhóm ca:
  A  nguồn 2024 (CorelDRAW 2024 lưu)            → X7, X8, 2017, 2018, 2019
  B  nguồn 2019 (CorelDRAW 2019 lưu)            → X7, X8, 2017, 2018
  C  nguồn 2018 / 2017 / X8 (CorelDRAW 2019 lưu) → mọi bản thấp hơn
  D  chuỗi 2024 → 2020/2021/2022/2023 → 2019 / X7 (kiểm tra đích 2020–2023 qua chính C_Converter)
  E  T00/T01 của người dùng, nguồn 2020/2021/2022/2023/2024 (CorelDRAW 2024 lưu) → X7 … 2019
"""
import json
import os
import shutil
import subprocess
import sys

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SUFFIX = {"X7": "17", "X8": "18", "2017": "19", "2018": "20", "2019": "21",
          "2020": "22", "2021": "23", "2022": "24", "2023": "24.3", "2024": "25"}
ORDER = ["X7", "X8", "2017", "2018", "2019", "2020", "2021", "2022", "2023", "2024"]
OLD = ["X7", "X8", "2017", "2018"]


def src_path(design, v):
    for d in ("samples/gen", "out/real", "samples/older"):
        p = os.path.join(REPO, d, f"{design}_{v}.cdr")
        if os.path.exists(p):
            return p
    return None


def convert(cli, src, target, out_dir):
    os.makedirs(out_dir, exist_ok=True)
    res = subprocess.run([cli, src, "--to", target, "--out", out_dir], capture_output=True, text=True, encoding="utf-8")
    out = os.path.join(out_dir, os.path.splitext(os.path.basename(src))[0] + f"_{target}.cdr")
    return (out if res.returncode == 0 and os.path.exists(out) else None), res.stdout + res.stderr


def main(cli, out):
    conv = os.path.join(out, "conv")
    work = os.path.join(out, "work")
    shutil.rmtree(conv, ignore_errors=True)
    shutil.rmtree(work, ignore_errors=True)
    os.makedirs(conv)
    designs = sorted({f[: -len("_20.cdr")] for f in os.listdir(os.path.join(REPO, "samples", "older")) if f.endswith("_20.cdr")})
    cases, failures = [], []

    def add(group, design, chain, ref):
        """chain: [nguồn (hậu tố Corel), đích 1, đích 2, …]."""
        src = src_path(design, chain[0]) if group != "E" else os.path.join(REPO, "File mau", f"{design}_{chain[0]}.cdr")
        if src is None or ref is None or not os.path.exists(src) or not os.path.exists(ref):
            return
        cur, log = src, ""
        for i, t in enumerate(chain[1:]):
            cur, l = convert(cli, cur, t, os.path.join(work, group, str(i)))
            log += l
            if cur is None:
                break
        name = f"{design}__{'-'.join(chain)}"
        if cur is None:
            failures.append({"case": name, "log": log})
            print("THẤT BẠI", name)
            return
        shutil.copy(cur, os.path.join(conv, name + ".cdr"))
        cases.append({"case": name, "group": group, "ref": os.path.relpath(ref, REPO), "warnings": log.count("[CẢNH BÁO]")})

    def ref_of(design, target):
        v = SUFFIX[target]
        return src_path(design, v)

    for d in designs:
        for t in OLD + ["2019"]:
            add("A", d, ["25", t], ref_of(d, t))
        for t in OLD:
            add("B", d, ["21", t], ref_of(d, t))
        for s in ["2018", "2017", "X8"]:
            for t in OLD[: OLD.index(s)]:
                add("C", d, [SUFFIX[s], t], ref_of(d, t))
        for mid in ["2020", "2021", "2022", "2023"]:
            add("D", d, ["25", mid, "2019"], ref_of(d, "2019"))
        add("D", d, ["25", "2022", "X7"], ref_of(d, "X7"))
    for d in ["T00", "T01"]:
        for s in ["2020", "2021", "2022", "2023", "2024"]:
            for t in OLD + ["2019"]:
                add("E", d, [SUFFIX[s], t], os.path.join(REPO, "File mau", f"{d}_{SUFFIX[t]}.cdr"))

    refs = sorted({c["ref"] for c in cases})
    json.dump({"cases": cases, "failures": failures, "refs": refs}, open(os.path.join(out, "manifest.json"), "w", encoding="utf-8"),
              ensure_ascii=False, indent=1)
    print(f"{len(cases)} ca chuyển đổi xong, {len(failures)} thất bại; {len(refs)} file đáp án.")


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main(sys.argv[1], os.path.abspath(sys.argv[2]))
