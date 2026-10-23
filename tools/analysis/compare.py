"""So sánh file do C_Converter hạ phiên bản với file do CorelDRAW tự Save As.

  python compare.py <thư mục mẫu> <thư mục kết quả C_Converter>

Với mỗi <tên>_25.cdr đã chuyển thành <tên>_25_2019.cdr, so với <tên>_21.cdr theo từng mục ZIP
và (cho content/) theo từng chunk. Bỏ qua các trường ngẫu nhiên / bộ đếm lưu.
"""
import glob
import json
import os
import re
import sys
import zipfile

sys.path.insert(0, os.path.dirname(__file__))
from cdrtree import load  # noqa: E402

IGNORE = {
    "META-INF/metadata.xml": [rb"<crl:Revision>-?\d+</crl:Revision>"],
    "styles/document.cdss": [rb'"guid": "[0-9a-f-]+"'],
}


def leaves_by_path(path):
    return {p: d for p, _, d in load(path)}


def compare(ours, corel):
    a, b = zipfile.ZipFile(ours), zipfile.ZipFile(corel)
    problems = []
    if a.namelist() != b.namelist():
        only_a = sorted(set(a.namelist()) - set(b.namelist()))
        only_b = sorted(set(b.namelist()) - set(a.namelist()))
        problems.append(f"danh sách mục khác: chỉ C_Converter={only_a} chỉ Corel={only_b}"
                        if only_a or only_b else "thứ tự mục khác")
    content_diff = False
    for n in b.namelist():
        if n not in a.namelist():
            continue
        x, y = a.read(n), b.read(n)
        for p in IGNORE.get(n, []):
            x, y = re.sub(p, b"", x), re.sub(p, b"", y)
        if x == y:
            continue
        def norm(t):  # bỏ qua thứ tự phần tử và thứ tự thuộc tính/namespace trong thẻ
            return sorted(b" ".join(sorted(line.replace(b">", b" >").split())) for line in t.splitlines())
        if n.endswith(".xml") and norm(x) == norm(y):
            continue  # Corel 2019 chỉ sắp xếp lại thứ tự phần tử XML
        if n.startswith("content/"):
            content_diff = True
        elif n.endswith(".cdss"):
            try:
                same = json.loads(x) == json.loads(y)
            except ValueError:
                same = False
            problems.append(f"{n} ({'chỉ khác định dạng' if same else 'khác nội dung'})")
        else:
            problems.append(n)
    if content_diff:
        la, lb = leaves_by_path(ours), leaves_by_path(corel)
        if list(la) != list(lb):
            problems.append(f"cây chunk khác ({len(la)} vs {len(lb)} chunk lá)")
        diff = [p for p in lb if p in la and la[p] != lb[p]]
        if diff:
            problems.append("chunk khác: " + ", ".join(diff[:8]) + (" …" if len(diff) > 8 else ""))
    return problems


if __name__ == "__main__":
    sample_dir, out_dir = sys.argv[1], sys.argv[2]
    ok = content_ok = 0
    files = sorted(glob.glob(os.path.join(out_dir, "*_25_2019.cdr")))
    for f in files:
        name = os.path.basename(f)[: -len("_25_2019.cdr")]
        corel = os.path.join(sample_dir, name + "_21.cdr")
        problems = compare(f, corel)
        if not any(p.startswith(("cây chunk", "chunk khác", "danh sách")) for p in problems):
            content_ok += 1
        if problems:
            print(f"KHÁC   {name}")
            for p in problems:
                print("        - " + p)
        else:
            ok += 1
            print(f"KHỚP   {name}")
    print(f"\n{ok}/{len(files)} khớp từng byte với CorelDRAW (bỏ qua GUID/bộ đếm lưu)")
    print(f"{content_ok}/{len(files)} khớp từng byte phần dữ liệu bản vẽ (content/)")
