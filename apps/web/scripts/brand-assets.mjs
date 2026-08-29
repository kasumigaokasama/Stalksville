// Generates Stalksville's binary brand assets from the same geometry as favicon.svg:
//   public/favicon.ico         16/32/48 px BMP entries (uncompressed, alpha)
//   public/apple-touch-icon.png 180 px PNG
// Node built-ins only — run with:  node scripts/brand-assets.mjs
import { deflateSync } from 'node:zlib';
import { writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');

// Geometry in the shared 64×64 viewBox — keep in sync with favicon.svg and BrandMark.
const BADGE_RADIUS = 14;
const HEAD = [
  [16, 11], [26, 20], [48, 11], [51, 30], [41, 45], [32, 57], [23, 45], [13, 30],
];
const EYES = [
  { cx: 25, cy: 30, r: 3.2 },
  { cx: 39, cy: 30, r: 3.2 },
];
const COLORS = {
  badge: [124, 154, 255], // --stl-accent  #7c9aff
  head: [11, 14, 20],     // --stl-bg      #0b0e14
  eye: [125, 211, 252],   // --stl-observed #7dd3fc
};

const inRoundedSquare = (x, y) => {
  const qx = Math.abs(x - 32) - (32 - BADGE_RADIUS);
  const qy = Math.abs(y - 32) - (32 - BADGE_RADIUS);
  if (qx <= 0 && qy <= 0) return true;
  if (qx > 0 && qy > 0) return qx * qx + qy * qy <= BADGE_RADIUS * BADGE_RADIUS;
  return qx <= 0 || qy <= 0;
};

const inPolygon = (x, y, poly) => {
  let inside = false;
  for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) {
    const [xi, yi] = poly[i];
    const [xj, yj] = poly[j];
    if (yi > y !== yj > y && x < ((xj - xi) * (y - yi)) / (yj - yi) + xi) inside = !inside;
  }
  return inside;
};

const inCircle = (x, y, c) => (x - c.cx) ** 2 + (y - c.cy) ** 2 <= c.r * c.r;

/** Topmost opaque color at a point in 64-space, or null for transparent. */
const sample = (x, y) => {
  if (EYES.some((e) => inCircle(x, y, e))) return COLORS.eye;
  if (inPolygon(x, y, HEAD)) return COLORS.head;
  if (inRoundedSquare(x, y)) return COLORS.badge;
  return null;
};

/** Renders the mark at `size` px with 4×4 supersampling; returns straight-alpha RGBA. */
const render = (size) => {
  const ss = 4;
  const px = new Uint8Array(size * size * 4);
  for (let y = 0; y < size; y++) {
    for (let x = 0; x < size; x++) {
      let r = 0, g = 0, b = 0, a = 0;
      for (let sy = 0; sy < ss; sy++) {
        for (let sx = 0; sx < ss; sx++) {
          const c = sample(((x + (sx + 0.5) / ss) / size) * 64, ((y + (sy + 0.5) / ss) / size) * 64);
          if (c) {
            // Premultiply while accumulating so transparent samples never darken edges.
            r += c[0];
            g += c[1];
            b += c[2];
            a++;
          }
        }
      }
      const i = (y * size + x) * 4;
      if (a > 0) {
        px[i] = Math.round(r / a);
        px[i + 1] = Math.round(g / a);
        px[i + 2] = Math.round(b / a);
        px[i + 3] = Math.round((a / (ss * ss)) * 255);
      }
    }
  }
  return px;
};

/** Uncompressed 32-bpp BMP ICO entry (BGRA, bottom-up, empty AND mask). */
const icoEntry = (size) => {
  const maskRow = Math.ceil(size / 32) * 4;
  const maskBytes = maskRow * size;
  const data = Buffer.alloc(40 + size * size * 4 + maskBytes);
  data.writeUInt32LE(40, 0);          // biSize
  data.writeInt32LE(size, 4);         // biWidth
  data.writeInt32LE(size * 2, 8);     // biHeight (pixels + AND mask)
  data.writeUInt16LE(1, 12);          // biPlanes
  data.writeUInt16LE(32, 14);         // biBitCount
  data.writeUInt32LE(size * size * 4 + maskBytes, 20); // biSizeImage
  const pixels = render(size);
  for (let y = 0; y < size; y++) {
    const src = pixels.subarray(y * size * 4, (y + 1) * size * 4);
    const dst = 40 + (size - 1 - y) * size * 4; // bottom-up
    for (let x = 0; x < size; x++) {
      data[dst + x * 4 + 0] = src[x * 4 + 2];   // B
      data[dst + x * 4 + 1] = src[x * 4 + 1];   // G
      data[dst + x * 4 + 2] = src[x * 4 + 0];   // R
      data[dst + x * 4 + 3] = src[x * 4 + 3];   // A
    }
  }
  return data;
};

const buildIco = (sizes) => {
  const entries = sizes.map((s) => ({ size: s, data: icoEntry(s) }));
  const header = Buffer.alloc(6 + entries.length * 16);
  header.writeUInt16LE(1, 2); // icon type
  header.writeUInt16LE(entries.length, 4);
  let offset = header.length;
  entries.forEach((e, i) => {
    const o = 6 + i * 16;
    header[o] = e.size;                 // width (0 would mean 256)
    header[o + 1] = e.size;             // height
    header.writeUInt16LE(1, o + 4);     // color planes
    header.writeUInt16LE(32, o + 6);    // bits per pixel
    header.writeUInt32LE(e.data.length, o + 8);
    header.writeUInt32LE(offset, o + 12);
    offset += e.data.length;
  });
  return Buffer.concat([header, ...entries.map((e) => e.data)]);
};

// --- Minimal PNG encoder (RGBA, filter 0) ---
const CRC_TABLE = Array.from({ length: 256 }, (_, n) => {
  let c = n;
  for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
  return c >>> 0;
});
const crc32 = (buf) => {
  let c = 0xffffffff;
  for (const b of buf) c = CRC_TABLE[(c ^ b) & 0xff] ^ (c >>> 8);
  return (c ^ 0xffffffff) >>> 0;
};
const chunk = (type, data) => {
  const out = Buffer.alloc(12 + data.length);
  out.writeUInt32BE(data.length, 0);
  out.write(type, 4, 'ascii');
  data.copy(out, 8);
  out.writeUInt32BE(crc32(out.subarray(4, 8 + data.length)), 8 + data.length);
  return out;
};
const buildPng = (size) => {
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(size, 0);
  ihdr.writeUInt32BE(size, 4);
  ihdr[8] = 8;  // bit depth
  ihdr[9] = 6;  // RGBA
  const raw = Buffer.alloc(size * (size * 4 + 1));
  const pixels = render(size);
  for (let y = 0; y < size; y++) {
    raw[y * (size * 4 + 1)] = 0; // filter: none
    raw.set(pixels.subarray(y * size * 4, (y + 1) * size * 4), y * (size * 4 + 1) + 1);
  }
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk('IHDR', ihdr),
    chunk('IDAT', deflateSync(raw, { level: 9 })),
    chunk('IEND', Buffer.alloc(0)),
  ]);
};

writeFileSync(join(root, 'public/favicon.ico'), buildIco([16, 32, 48]));
writeFileSync(join(root, 'public/apple-touch-icon.png'), buildPng(180));
console.log('brand assets written: public/favicon.ico, public/apple-touch-icon.png');
