"""So sánh kết quả mở bằng CorelDRAW 2019 cho các bản đích X7–2018:
file C_Converter tạo (<tên>_25_<đích>.cdr) với file CorelDRAW 2019 tự Save As (<tên>_<v>.cdr).

  python compare_targets.py <thư mục verify>
"""
import collections
import json
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from compare_render import first_diff, pixel_diff, strip  # noqa: E402

TARGETS = {"X7": "17", "X8": "18", "2017": "19", "2018": "20"}

if __name__ == "__main__":
    root = sys.argv[1]
    report = {e["file"]: e for e in json.load(open(os.path.join(root, "report.json"), encoding="utf-8-sig"))}
    stats = collections.Counter()
    for t, v in TARGETS.items():
        for ours_name in sorted(n for n in report if n.endswith(f"_25_{t}")):
            name = ours_name[: -len(f"_25_{t}")]
            ref = report.get(f"{name}_{v}")
            if ref is None:
                continue
            ours = report[ours_name]
            problems = []
            if not ours["opened"]:
                problems.append("KHÔNG MỞ ĐƯỢC: " + ours.get("error", ""))
            else:
                d = first_diff(strip(ours), strip(ref))
                if d:
                    problems.append("cấu trúc: " + d)
                for page in ref.get("pages", []):
                    i = page["index"]
                    pd = pixel_diff(os.path.join(root, f"{ours_name}.p{i}.png"), os.path.join(root, f"{name}_{v}.p{i}.png"))
                    if pd:
                        problems.append(f"trang {i}: {pd}")
            stats[(t, not problems)] += 1
            if problems:
                print(f"KHÁC  {t:>4}  {name}")
                for p in problems:
                    print("         - " + p)
    print()
    for t in TARGETS:
        ok, bad = stats[(t, True)], stats[(t, False)]
        print(f"{t:>5}: {ok}/{ok + bad} giống hệt bản CorelDRAW tự lưu (mở bằng CorelDRAW 2019)")
