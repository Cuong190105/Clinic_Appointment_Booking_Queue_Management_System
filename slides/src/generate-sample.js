'use strict';

const fs = require('fs');
const path = require('path');
const pptxgen = require('pptxgenjs');
const { addArrow, addCard, addPill, addSlideBase, rect, text, theme } = require('./helpers');

const repoRoot = path.resolve(__dirname, '..', '..');
const slidesRoot = path.resolve(repoRoot, 'slides');
const scriptPath = path.resolve(repoRoot, 'docs', 'PRESENTATION_SCRIPT.md');
const scriptText = fs.readFileSync(scriptPath, 'utf8');

function notesFor(number) {
  const re = new RegExp(`^## Slide ${number} — [^\\n]+`, 'm');
  const match = re.exec(scriptText);
  if (!match) return `Speaker notes refer to docs/PRESENTATION_SCRIPT.md, slide ${number}.`;
  const start = match.index;
  const next = scriptText.indexOf('\n## Slide ', start + match[0].length);
  return scriptText.slice(start, next < 0 ? scriptText.length : next).trim();
}

const pptx = new pptxgen();
pptx.layout = theme.layout;
pptx.author = 'Clinic Appointment Booking Queue Management System project team';
pptx.subject = 'Representative design review slides';
pptx.title = 'Project presentation — visual direction sample';
pptx.company = 'Academic project';
pptx.lang = 'vi-VN';
pptx.theme = {
  headFontFace: theme.font,
  bodyFontFace: theme.font,
  lang: 'vi-VN',
};
pptx.defineSlideMaster({
  title: 'BASE',
  background: { color: theme.colors.paper },
  objects: [],
});

// Sample slide 3: monolith architecture
{
  const slide = pptx.addSlide('BASE');
  addSlideBase(slide, { section: 'Hệ thống cũ', title: 'Một ứng dụng, một database dùng chung', source: '[P] §3.1  ·  [S] Slide 3', number: 3 });
  text(slide, 'Luồng triển khai monolith', 0.68, 1.57, 3.4, 0.28, { fontSize: 12, bold: true, color: theme.colors.slate });
  addCard(slide, { x: 0.72, y: 2.52, w: 1.62, h: 1.14, title: 'Frontend', body: 'Vue app', accent: theme.colors.navy, titleSize: 15, bodySize: 12.5 });
  addArrow(slide, 2.39, 3.09, 2.93, 3.09);
  rect(slide, 3.02, 1.88, 4.54, 2.76, theme.colors.sky, 'B9D4DC');
  text(slide, 'Clinic.API', 3.29, 2.05, 2.2, 0.36, { fontSize: 19, bold: true, color: theme.colors.navy });
  addPill(slide, 'CÙNG TIẾN TRÌNH', 5.66, 2.09, 1.48, 'FFFFFF', theme.colors.navy);
  addCard(slide, { x: 3.3, y: 2.62, w: 1.18, h: 1.15, title: 'API', body: 'Controllers', accent: theme.colors.navy, titleSize: 14, bodySize: 11.5 });
  addCard(slide, { x: 4.62, y: 2.62, w: 1.35, h: 1.15, title: 'Application', body: 'Use cases', accent: theme.colors.teal, titleSize: 13, bodySize: 11.5 });
  addCard(slide, { x: 6.11, y: 2.62, w: 1.18, h: 1.15, title: 'Domain', body: '+ infra', accent: theme.colors.amber, titleSize: 14, bodySize: 11.5 });
  addArrow(slide, 7.59, 3.09, 8.19, 3.09);
  addCard(slide, { x: 8.29, y: 2.43, w: 2.08, h: 1.42, title: 'ClinicDB', body: 'SQL Server\nApplicationDbContext', accent: theme.colors.teal, titleSize: 17, bodySize: 12.5 });
  text(slide, 'Gọi nội bộ nhanh  ·  transaction đa bảng đơn giản', 0.74, 5.04, 5.6, 0.34, { fontSize: 14.5, bold: true, color: theme.colors.green });
  rect(slide, 0.7, 5.54, 11.95, 0.82, theme.colors.amberLight, 'F1D5A1');
  text(slide, 'Trade-off', 0.94, 5.72, 1.08, 0.28, { fontSize: 13, bold: true, color: theme.colors.amber });
  text(slide, 'Ownership dữ liệu vẫn dùng chung; thay đổi/migration có thể ảnh hưởng nhiều nghiệp vụ.', 2.12, 5.68, 10.05, 0.34, { fontSize: 14.5, color: theme.colors.ink });
  slide.addNotes(notesFor(3));
}

// Sample slide 8: booking orchestration / SAGA
{
  const slide = pptx.addSlide('BASE');
  addSlideBase(slide, { section: 'Luồng phối hợp', title: 'Appointment điều phối SAGA đặt lịch', source: '[P] §4.2  ·  [S] Slide 8', number: 8 });
  const steps = [
    { x: 0.66, title: '1 · Request', body: 'Gateway nhận booking\n+ Idempotency-Key', accent: theme.colors.navy },
    { x: 3.1, title: '2 · Reserve', body: 'Appointment gọi\nScheduling đồng bộ', accent: theme.colors.teal },
    { x: 5.54, title: '3 · Local commit', body: 'Appointment + Outbox\ntransaction cục bộ', accent: theme.colors.navy },
    { x: 7.98, title: '4 · Confirm', body: 'Worker phát event;\nreservation được xác nhận', accent: theme.colors.teal },
    { x: 10.42, title: '5 · Notify', body: 'Consumer cập nhật\nnotification', accent: theme.colors.amber },
  ];
  for (const step of steps) addCard(slide, { x: step.x, y: 2.21, w: 2.12, h: 1.52, title: step.title, body: step.body, accent: step.accent, titleSize: 14.5, bodySize: 12.5 });
  addArrow(slide, 2.79, 2.98, 3.03, 2.98, { color: theme.colors.navy });
  addArrow(slide, 5.23, 2.98, 5.47, 2.98, { color: theme.colors.navy });
  addArrow(slide, 7.67, 2.98, 7.91, 2.98, { color: theme.colors.teal, dash: 'dash' });
  addArrow(slide, 10.11, 2.98, 10.35, 2.98, { color: theme.colors.teal, dash: 'dash' });
  addPill(slide, 'API đồng bộ', 3.36, 1.76, 1.52, theme.colors.sky, theme.colors.navy);
  addPill(slide, 'Outbox → event', 7.98, 1.76, 1.68, theme.colors.sky, theme.colors.teal);
  rect(slide, 0.75, 4.62, 11.84, 1.0, theme.colors.white, theme.colors.line);
  text(slide, 'Không có transaction ACID xuyên database', 1.04, 4.83, 4.15, 0.28, { fontSize: 16, bold: true, color: theme.colors.ink });
  text(slide, 'Mỗi service commit dữ liệu của mình; retry / bước bù giúp workflow tiến triển sau lỗi.', 5.18, 4.82, 6.98, 0.42, { fontSize: 14.5, color: theme.colors.slate });
  text(slide, 'Orchestration cho phần booking cốt lõi  ·  Event cho xác nhận và thông báo', 0.8, 6.05, 10.8, 0.32, { fontSize: 14, bold: true, color: theme.colors.teal });
  slide.addNotes(notesFor(8));
}

// Sample slide 15: native chart + editable comparison table
{
  const slide = pptx.addSlide('BASE');
  addSlideBase(slide, { section: 'Benchmark đối chiếu', title: 'High-load: trade-off giữa latency và workflow', source: '[B] §22  ·  [E1–E3]  ·  [S] Slide 15', number: 15 });
  text(slide, 'Booking high-load · p95 (ms) · median 3 lượt', 0.68, 1.54, 6.1, 0.3, { fontSize: 13.5, bold: true, color: theme.colors.slate });
  slide.addChart(pptx.ChartType.bar, [
    { name: 'Monolith', labels: ['API response', 'Successful create', 'POST → Confirmed'], values: [190.2, 214.6, 235] },
    { name: 'Microservices', labels: ['API response', 'Successful create', 'POST → Confirmed'], values: [253.6, 387.8, 762] },
  ], {
    x: 0.62, y: 1.88, w: 6.52, h: 4.37,
    catAxisLabelFontFace: theme.font, catAxisLabelFontSize: 11,
    valAxisLabelFontFace: theme.font, valAxisLabelFontSize: 10,
    valAxisMinVal: 0, valAxisMaxVal: 900, valAxisMajorUnit: 300,
    valAxisTitle: 'Milliseconds', valAxisTitleFontFace: theme.font, valAxisTitleFontSize: 10,
    chartColors: [theme.colors.navy, theme.colors.teal],
    showLegend: true, legendPos: 'b', legendFontFace: theme.font, legendFontSize: 11,
    showTitle: false, showValue: true, dataLabelPosition: 'outEnd', dataLabelColor: theme.colors.ink, dataLabelFormatCode: '0',
    showValAxisTitle: true, showValAxis: true, showCatAxis: true,
    showBorder: false,
    valGridLine: { color: theme.colors.line, size: 0.8 },
    catGridLine: { style: 'none' },
    chartArea: { fill: { color: theme.colors.paper }, border: { color: theme.colors.paper, pt: 0 } },
    plotArea: { fill: { color: theme.colors.paper }, border: { color: theme.colors.paper, pt: 0 } },
    showLeaderLines: false,
    showPercent: false,
    showSerName: false,
  });
  text(slide, 'Run outcomes', 7.48, 1.54, 4.7, 0.3, { fontSize: 13.5, bold: true, color: theme.colors.slate });
  const tx = 7.48, ty = 1.9, tw = 5.12;
  rect(slide, tx, ty, tw, 0.52, theme.colors.navy, theme.colors.navy, false);
  text(slide, 'Metric', tx + 0.16, ty + 0.13, 2.1, 0.22, { fontSize: 11.5, bold: true, color: theme.colors.white });
  text(slide, 'Mono', tx + 2.54, ty + 0.13, 1.0, 0.22, { fontSize: 11.5, bold: true, color: theme.colors.white, align: 'center' });
  text(slide, 'Micro', tx + 3.76, ty + 0.13, 1.0, 0.22, { fontSize: 11.5, bold: true, color: theme.colors.white, align: 'center' });
  const rows = [
    ['Confirmed / s', '29.84', '29.72'],
    ['Clinic peak 5xx', '2.90%', '0%'],
    ['Queue lifecycle', '0 / 865*', '836 done'],
  ];
  rows.forEach((row, i) => {
    const y = ty + 0.52 + i * 0.62;
    rect(slide, tx, y, tw, 0.62, i % 2 ? theme.colors.paper : theme.colors.white, theme.colors.line, false);
    text(slide, row[0], tx + 0.16, y + 0.17, 2.18, 0.25, { fontSize: 12.2, bold: true });
    text(slide, row[1], tx + 2.45, y + 0.17, 1.18, 0.25, { fontSize: 12.2, align: 'center' });
    text(slide, row[2], tx + 3.71, y + 0.17, 1.22, 0.25, { fontSize: 12.2, align: 'center' });
  });
  rect(slide, 7.48, 4.58, 5.12, 1.22, theme.colors.amberLight, 'F1D5A1');
  text(slide, '* Monolith walk-in defect: 865/865 failed; this is not a Queue capacity comparison.', 7.72, 4.78, 4.62, 0.82, { fontSize: 12.5, color: theme.colors.ink, valign: 'mid' });
  rect(slide, 0.68, 6.42, 11.92, 0.45, 'EAF0F4', 'EAF0F4', false);
  text(slide, 'Observed runs only · persistent DBs were not restored from identical snapshots · mixed profiles: one run/system.', 0.86, 6.53, 11.54, 0.2, { fontSize: 10.8, color: theme.colors.slate, align: 'center' });
  slide.addNotes(notesFor(15));
}

const outDir = path.join(slidesRoot, 'output');
fs.mkdirSync(outDir, { recursive: true });
const outFile = path.join(outDir, 'sample-review.pptx');
pptx.writeFile({ fileName: outFile })
  .then(() => console.log(`Wrote ${outFile}`))
  .catch((error) => { console.error(error); process.exitCode = 1; });
