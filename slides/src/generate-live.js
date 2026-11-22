'use strict';

const fs = require('fs');
const path = require('path');
const pptxgen = require('pptxgenjs');
const { theme } = require('./helpers');
const { buildLiveSlides01To08 } = require('./slides-live/live-01-08');
const { buildLiveSlides09To16 } = require('./slides-live/live-09-16');

const slidesRoot = path.resolve(__dirname, '..');
const outDir = path.join(slidesRoot, 'output');
fs.mkdirSync(outDir, { recursive: true });

const pptx = new pptxgen();
pptx.layout = theme.layout;
pptx.author = 'Nhóm đồ án Hệ thống Đặt lịch khám và Quản lý Phòng khám';
pptx.subject = 'Bản trình chiếu trực tiếp (Màn hình 55 inch, khoảng cách 2-5m)';
pptx.title = 'Hệ thống đặt lịch khám và quản lý phòng khám — Bản trình chiếu';
pptx.company = 'Đồ án học thuật';
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

buildLiveSlides01To08(pptx);
buildLiveSlides09To16(pptx);

const outFile = path.join(outDir, 'live-presentation.pptx');
const rootCopyFile = path.join(slidesRoot, 'clinic-microservices-live-55inch-vi.pptx');

pptx.writeFile({ fileName: outFile })
  .then(() => {
    fs.copyFileSync(outFile, rootCopyFile);
    console.log(`Wrote ${outFile}`);
    console.log(`Copied to ${rootCopyFile}`);
  })
  .catch((error) => {
    console.error(error);
    process.exitCode = 1;
  });
