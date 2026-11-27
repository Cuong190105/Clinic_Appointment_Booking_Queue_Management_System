# Bài báo dự án (LaTeX, định dạng IEEE hai cột)

Bản LaTeX của báo cáo dự án, trình bày theo định dạng bài báo khoa học (lớp `IEEEtran`, khổ A4, hai cột). Nội dung tương ứng với `docs/PROJECT_REPORT.md`.

## Biên dịch

Cần **XeLaTeX** (TeX Live 2022+, MiKTeX hoặc Overleaf) vì bài báo dùng `fontspec` cho tiếng Việt.

```bash
cd report
latexmk            # dùng .latexmkrc, xuất build/main.pdf
# hoặc
xelatex main.tex && xelatex main.tex
```

Trên Overleaf: tải thư mục `report/` lên, vào **Menu → Compiler** chọn **XeLaTeX**.

Gói cần có: `IEEEtran`, `fontspec`, `babel` (có `vietnamese`), `tikz`, `pgfplots`, `booktabs`, `tabularx`, `listings`, `enumitem`, `hyperref`. Phông: TeX Gyre Termes/Heros và DejaVu Sans Mono (có sẵn trong TeX Live).

## Cấu trúc

```
report/
├── main.tex            # tiêu đề, tác giả, thứ tự các mục
├── preamble.tex        # gói, phông, kiểu sơ đồ
└── chapters/
    ├── 00-abstract.tex       # tóm tắt + từ khóa
    ├── 01-introduction.tex
    ├── 02-theory.tex
    ├── 03-monolith.tex       # kiến trúc + ERD
    ├── 04-microservices.tex  # sơ đồ tổng quan, luồng đặt lịch, check-in
    ├── 05-sla-slo-qos.tex
    ├── 06-testing.tex        # bảng + biểu đồ benchmark
    ├── 07-review.tex
    ├── 08-conclusion.tex
    └── 99-references.tex
```

## Cần điền trước khi nộp

Trong `main.tex`, khối `\author{...}`: họ tên, MSSV, khoa, trường và email của từng thành viên.
