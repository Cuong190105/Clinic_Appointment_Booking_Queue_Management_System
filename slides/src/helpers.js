'use strict';

const fs = require('fs');
const path = require('path');
const theme = require('./theme');

const repoRoot = path.resolve(__dirname, '..', '..');
const scriptPath = path.resolve(repoRoot, 'docs', 'PRESENTATION_SCRIPT.md');
const scriptText = fs.existsSync(scriptPath) ? fs.readFileSync(scriptPath, 'utf8') : '';

function text(slide, value, x, y, w, h, opts = {}) {
  slide.addText(value, {
    x, y, w, h,
    fontFace: theme.font,
    fontSize: theme.type.body,
    color: theme.colors.ink,
    margin: 0,
    breakLine: false,
    valign: 'mid',
    fit: 'shrink',
    ...opts,
  });
}

function rect(slide, x, y, w, h, fill, line = fill, radius = true) {
  slide.addShape(radius ? 'roundRect' : 'rect', {
    x, y, w, h,
    rectRadius: 0.08,
    fill: { color: fill },
    line: { color: line, width: 0.8 },
  });
}

function addSlideBase(slide, { section, title, source, number }) {
  slide.background = { color: theme.colors.paper };
  text(slide, section.toUpperCase(), 0.58, 0.31, 8.5, 0.22, {
    fontSize: theme.type.section, bold: true, charSpacing: 1.2,
    color: theme.colors.teal,
  });
  text(slide, title, 0.58, 0.62, 12.15, 0.58, {
    fontSize: theme.type.title, bold: true, color: theme.colors.ink,
  });
  slide.addShape('line', {
    x: 0.58, y: 1.34, w: 12.15, h: 0,
    line: { color: theme.colors.line, width: 1 },
  });
  text(slide, source, 0.58, 7.12, 10.8, 0.16, {
    fontSize: theme.type.footnote, color: theme.colors.slate,
  });
  text(slide, String(number).padStart(2, '0'), 12.03, 7.09, 0.68, 0.2, {
    fontSize: theme.type.footnote, color: theme.colors.slate, align: 'right',
  });
}

function addCard(slide, {
  x, y, w, h, title, body,
  accent = theme.colors.teal,
  fill = theme.colors.white,
  line = theme.colors.line,
  titleSize = 15.5,
  bodySize = 13,
  titleHeight = 0.38,
  titleColor = theme.colors.ink,
  bodyColor = theme.colors.slate,
}) {
  rect(slide, x, y, w, h, fill, line);
  slide.addShape('rect', {
    x, y: y + 0.02, w: 0.07, h: h - 0.04,
    fill: { color: accent }, line: { color: accent, transparency: 100 },
  });
  text(slide, title, x + 0.2, y + 0.12, w - 0.32, titleHeight, {
    fontSize: titleSize, bold: true, color: titleColor,
    valign: 'mid',
  });
  if (body) {
    const bodyY = y + 0.16 + titleHeight;
    text(slide, body, x + 0.2, bodyY, w - 0.32, h - (bodyY - y) - 0.12, {
      fontSize: bodySize, color: bodyColor,
      valign: 'top', breakLine: false,
    });
  }
}

function addArrow(slide, x1, y1, x2, y2, { color = theme.colors.navy, width = 1.6, dash = 'solid', startArrow = false } = {}) {
  const isLeft = x2 < x1;
  const isUp = y2 < y1;
  slide.addShape('line', {
    x: Math.min(x1, x2),
    y: Math.min(y1, y2),
    w: Math.abs(x2 - x1),
    h: Math.abs(y2 - y1),
    flipH: isLeft,
    flipV: isUp,
    line: {
      color,
      width,
      dashType: dash,
      beginArrowType: startArrow ? 'triangle' : 'none',
      endArrowType: 'triangle',
    },
  });
}

function addPill(slide, label, x, y, w, fill, color = theme.colors.ink, opts = {}) {
  const h = opts.h || 0.28;
  slide.addShape('roundRect', {
    x, y, w, h,
    rectRadius: 0.12,
    fill: { color: fill },
    line: { color: opts.border || fill, width: opts.border ? 0.8 : 0, transparency: opts.border ? 0 : 100 },
  });
  text(slide, label, x + 0.08, y + 0.02, w - 0.16, h - 0.04, {
    fontSize: opts.fontSize || 9.5, color, bold: true, align: 'center',
  });
}

function notesFor(number) {
  if (number === 18) {
    const qaMatch = /^## Phụ lục số liệu đối chiếu dùng khi hỏi đáp[^\n]*/m.exec(scriptText);
    const qaSection = qaMatch ? scriptText.slice(qaMatch.index).trim() : '';
    return [
      'Slide 18 — Tài liệu tham khảo và bằng chứng nghiệm thu (Phụ lục tra cứu khi hỏi đáp, không nằm trong 15 phút thuyết trình chính).',
      '',
      'Hướng dẫn sử dụng khi hỏi đáp (Q&A):',
      '- Dùng bảng mã nguồn [S], [P], [Q], [B], [R], [T], [C], [E1–E3] để truy xuất nhanh tới báo cáo dự án, ma trận SLI/SLO, tài liệu sao lưu, bộ kiểm thử SLO/QoS, hợp đồng OpenAPI và tệp JSON kết quả kiểm thử đã làm sạch (sanitized).',
      '',
      qaSection,
    ].filter(Boolean).join('\n');
  }
  const re = new RegExp(`^## Slide ${number} — [^\\n]+`, 'm');
  const match = re.exec(scriptText);
  if (!match) return `Ghi chú thuyết trình tham chiếu từ docs/PRESENTATION_SCRIPT.md, Slide ${number}.`;
  const start = match.index;
  const rest = scriptText.slice(start + match[0].length);
  const nextHeading = rest.search(/\n## /);
  const end = nextHeading < 0 ? scriptText.length : start + match[0].length + nextHeading;
  return scriptText.slice(start, end).trim();
}

module.exports = {
  addArrow,
  addCard,
  addPill,
  addSlideBase,
  notesFor,
  rect,
  text,
  theme,
};
