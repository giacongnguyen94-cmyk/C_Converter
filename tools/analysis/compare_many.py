"""So sánh kết quả mở bằng CorelDRAW 2019 cho bộ ca tools/harness/many_cases.py:
mỗi file C_Converter tạo với file đáp án CorelDRAW tự Save As (cấu trúc tài liệu + ảnh từng trang).

  python compare_many.py <thư mục out của many_cases> [<thư mục verify>]
"""
import collections
import json
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from compare_render import first_diff, pixel_diff, strip  # noqa: E402

GROUPS = {
    "A": "nguồn 2024 → X7 · X8 · 2017 · 2018 · 2019",
    "B": "nguồn 2019 → X7 · X8 · 2017 · 2018",
    "C": "nguồn 2018 · 2017 · X8 → bản thấp hơn",
    "D": "chuỗi 2024 → 2020/2021/2022/2023 → 2019, và 2024 → 2022 → X7",
    "E": "T00/T01 của người dùng, nguồn 2020 – 2024 → X7 … 2019",
}

if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    out = sys.argv[1]
    vdir = sys.argv[2] if len(sys.argv) > 2 else os.path.join(out, "v")
    manifest = json.load(open(os.path.join(out, "manifest.json"), encoding="utf-8"))
    report = {e["file"]: e for e in json.load(open(os.path.join(vdir, "report.json"), encoding="utf-8-sig"))}
    stats = collections.defaultdict(collections.Counter)
    diffs = collections.defaultdict(list)
    for c in manifest["cases"]:
        name = c["case"]
        ref_name = os.path.splitext(os.path.basename(c["ref"]))[0]
        ours, ref = report.get(name), report.get(ref_name)
        g = stats[c["group"]]
        if ours is None or ref is None:
            g["chưa mở"] += 1
            continue
        g["tổng"] += 1
        if not ours["opened"]:
            g["không mở được"] += 1
            diffs[name].append("KHÔNG MỞ ĐƯỢC: " + ours.get("error", ""))
            continue
        g["mở được"] += 1
        problems = []
        if ours.get("error") != ref.get("error"):
            problems.append(f"lỗi khi đọc: C_Converter={ours.get('error')!r} Corel={ref.get('error')!r}")
        d = first_diff(strip({k: v for k, v in ours.items() if k != "error"}), strip({k: v for k, v in ref.items() if k != "error"}))
        if d:
            problems.append("cấu trúc: " + d)
        for page in ref.get("pages", []):
            i = page["index"]
            pd = pixel_diff(os.path.join(vdir, f"{name}.p{i}.png"), os.path.join(vdir, f"{ref_name}.p{i}.png"))
            if pd:
                problems.append(f"trang {i}: {pd}")
        if problems:
            diffs[name] = problems
        else:
            g["giống hệt"] += 1
    for name, problems in sorted(diffs.items()):
        print("KHÁC  " + name)
        for p in problems:
            print("        - " + p)
    print()
    by_design = collections.Counter(n.split("__")[0] for n in diffs)
    print("Thiết kế có khác biệt:", dict(by_design))
    print()
    for k in sorted(stats):
        s = stats[k]
        print(f"{k}  {GROUPS[k]}")
        print(f"   {s['tổng']} ca · mở được {s['mở được']}/{s['tổng']} · giống hệt file Corel tự lưu {s['giống hệt']}/{s['tổng']}"
              + (f" · chưa mở {s['chưa mở']}" if s["chưa mở"] else ""))
    t = sum((stats[k] for k in stats), collections.Counter())
    print(f"\nTỔNG  {t['tổng']} ca · mở được {t['mở được']} · giống hệt {t['giống hệt']}")
