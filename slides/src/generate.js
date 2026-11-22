'use strict';

const fs = require('fs');
const path = require('path');
const pptxgen = require('pptxgenjs');
const { theme } = require('./helpers');
const { buildSlides01To05 } = require('./slides/slides-01-05');
const { buildSlides06To10 } = require('./slides/slides-06-10');
const { buildSlides11To17 } = require('./slides/slides-11-17');

const slidesRoot = path.resolve(__dirname, '..');
const outDir = path.join(slidesRoot, 'output');
fs.mkdirSync(outDir, { recursive: true });

const pptx = new pptxgen();
pptx.layout = theme.layout;
pptx.author = 'Nhóm đồ án Hệ thống Đặt lịch khám và Quản lý Phòng khám';
pptx.subject = 'Báo cáo đồ án chuyển đổi Monolith sang Microservices';
pptx.title = 'Hệ thống đặt lịch khám, quản lý hàng đợi và hồ sơ y tế';
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

buildSlides01To05(pptx);
buildSlides06To10(pptx);
buildSlides11To17(pptx);

const outFile = path.join(outDir, 'presentation.pptx');
const rootCopyFile = path.join(slidesRoot, 'clinic-microservices-presentation-vi.pptx');

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
