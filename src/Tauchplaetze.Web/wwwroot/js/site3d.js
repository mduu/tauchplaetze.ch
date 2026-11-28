// 3D-Ansicht eines Platzes (Story 007): Höhenraster aus swissBATHY3D und swissALTI3D, Luftbild SWISSIMAGE auf dem Land,
// Objekte der Skizze in ihrer Tiefe, Route mit Taucher und Blasen. Lädt three.js erst beim Klick auf den Sticker.
(function () {
  'use strict';

  const reduceMotion = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  let libraries = null;

  function loadScript(src) {
    return new Promise(function (resolve, reject) {
      const existing = document.querySelector('script[data-src="' + src + '"]');
      if (existing) {
        if (existing.dataset.loaded) { resolve(); } else { existing.addEventListener('load', resolve); existing.addEventListener('error', reject); }
        return;
      }
      const script = document.createElement('script');
      script.src = src;
      script.dataset.src = src;
      script.onload = function () { script.dataset.loaded = 'true'; resolve(); };
      script.onerror = reject;
      document.head.appendChild(script);
    });
  }

  function loadLibraries(stage) {
    if (!libraries) {
      libraries = loadScript(stage.dataset.three).then(function () { return loadScript(stage.dataset.orbit); }).catch(function (error) {
        libraries = null;
        document.querySelectorAll('script[data-src]:not([data-loaded])').forEach(function (script) { script.remove(); });
        throw error;
      });
    }
    return libraries;
  }

  let webGl = null;

  function webGlAvailable() {
    if (webGl === null) {
      webGl = hasWebGl();
    }
    return webGl;
  }

  function hasWebGl() {
    try {
      const canvas = document.createElement('canvas');
      return !!(window.WebGLRenderingContext && (canvas.getContext('webgl2') || canvas.getContext('webgl')));
    } catch (e) {
      return false;
    }
  }

  // LV95 aus WGS84, Näherungsformeln von swisstopo (rund 1 m genau).
  function toLv95(lat, lng) {
    const phi = (lat * 3600 - 169028.66) / 10000;
    const lambda = (lng * 3600 - 26782.5) / 10000;
    return {
      e: 2600072.37 + 211455.93 * lambda - 10938.51 * lambda * phi - 0.36 * lambda * phi * phi - 44.54 * lambda * lambda * lambda,
      n: 1200147.07 + 308807.95 * phi + 3745.25 * lambda * lambda + 76.63 * phi * phi - 194.56 * lambda * lambda * phi + 119.79 * phi * phi * phi
    };
  }

  function toWgs84(e, n) {
    const y = (e - 2600000) / 1000000;
    const x = (n - 1200000) / 1000000;
    const lambda = 2.6779094 + 4.728982 * y + 0.791484 * y * x + 0.1306 * y * x * x - 0.0436 * y * y * y;
    const phi = 16.9023892 + 3.238272 * x - 0.270978 * y * y - 0.002528 * x * x - 0.0447 * y * y * x - 0.0140 * x * x * x;
    return { lat: phi * 100 / 36, lng: lambda * 100 / 36 };
  }

  function format(text, values) {
    return text.replace(/\{(\d+)\}/g, function (_, i) { return values[i]; });
  }

  function escapeHtml(text) {
    return String(text).replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; });
  }

  async function loadTerrain(stage) {
    const response = await fetch(stage.dataset.terrain);
    if (!response.ok) {
      throw new Error('terrain ' + response.status);
    }
    const buffer = await response.arrayBuffer();
    const raw = new Int16Array(buffer);
    const points = parseInt(stage.dataset.points, 10);
    if (raw.length !== points * points) {
      throw new Error('terrain size ' + raw.length);
    }
    const heights = new Float32Array(raw.length);
    for (let i = 0; i < raw.length; i++) {
      heights[i] = raw[i] / 10;
    }
    return heights;
  }

  const AERIAL_TIMEOUT_MS = 8000;
  const AERIAL_FALLBACK = '#5C6B52';

  // Luftbild: Kacheln von swisstopo (Web Mercator, Zoom 18), umgerechnet auf das Raster in LV95.
  function loadAerial(origin, size, pixels) {
    const zoom = 18;
    const scale = Math.pow(2, zoom) * 256;
    function mercator(lat, lng) {
      const s = Math.sin(lat * Math.PI / 180);
      return { x: (lng + 180) / 360 * scale, y: (0.5 - Math.log((1 + s) / (1 - s)) / (4 * Math.PI)) * scale };
    }
    const corners = [[0, 0], [size, 0], [0, size], [size, size]].map(function (c) {
      const w = toWgs84(origin.e + c[0], origin.n + c[1]);
      return mercator(w.lat, w.lng);
    });
    const tx1 = Math.floor(Math.min.apply(null, corners.map(function (c) { return c.x; })) / 256);
    const tx2 = Math.floor(Math.max.apply(null, corners.map(function (c) { return c.x; })) / 256);
    const ty1 = Math.floor(Math.min.apply(null, corners.map(function (c) { return c.y; })) / 256);
    const ty2 = Math.floor(Math.max.apply(null, corners.map(function (c) { return c.y; })) / 256);
    const mosaic = document.createElement('canvas');
    mosaic.width = (tx2 - tx1 + 1) * 256;
    mosaic.height = (ty2 - ty1 + 1) * 256;
    const mctx = mosaic.getContext('2d');
    mctx.fillStyle = AERIAL_FALLBACK;
    mctx.fillRect(0, 0, mosaic.width, mosaic.height);
    const loads = [];
    for (let tx = tx1; tx <= tx2; tx++) {
      for (let ty = ty1; ty <= ty2; ty++) {
        loads.push(new Promise(function (resolve) {
          const img = new Image();
          img.crossOrigin = 'anonymous';
          const timeout = setTimeout(function () { img.src = ''; resolve(false); }, AERIAL_TIMEOUT_MS);
          img.onload = function () { clearTimeout(timeout); mctx.drawImage(img, (tx - tx1) * 256, (ty - ty1) * 256); resolve(true); };
          img.onerror = function () { clearTimeout(timeout); resolve(false); };
          img.src = 'https://wmts.geo.admin.ch/1.0.0/ch.swisstopo.swissimage/default/current/3857/' + zoom + '/' + tx + '/' + ty + '.jpeg';
        }));
      }
    }
    return Promise.all(loads).then(function (results) {
      if (!results.some(Boolean)) {
        return null;
      }
      const source = mctx.getImageData(0, 0, mosaic.width, mosaic.height).data;
      const out = document.createElement('canvas');
      out.width = pixels;
      out.height = pixels;
      const octx = out.getContext('2d');
      const image = octx.createImageData(pixels, pixels);
      for (let py = 0; py < pixels; py++) {
        const n = origin.n + size * (1 - (py + 0.5) / pixels);
        for (let px = 0; px < pixels; px++) {
          const e = origin.e + size * (px + 0.5) / pixels;
          const w = toWgs84(e, n);
          const m = mercator(w.lat, w.lng);
          const sx = Math.min(mosaic.width - 1, Math.max(0, Math.floor(m.x - tx1 * 256)));
          const sy = Math.min(mosaic.height - 1, Math.max(0, Math.floor(m.y - ty1 * 256)));
          const si = (sy * mosaic.width + sx) * 4, di = (py * pixels + px) * 4;
          image.data[di] = source[si]; image.data[di + 1] = source[si + 1]; image.data[di + 2] = source[si + 2]; image.data[di + 3] = 255;
        }
      }
      octx.putImageData(image, 0, 0);
      return out;
    }).catch(function () { return null; });
  }

  function build(stage, heights, aerial) {
    const THREE = window.THREE;
    const text = JSON.parse(stage.dataset.texts);
    const map = stage.querySelector('[data-map]');
    const kinds = JSON.parse(map.dataset.sketchKinds || '{}');
    const sketch = JSON.parse(map.dataset.sketch || '[]');
    const entries = JSON.parse(stage.dataset.entries || '[]');
    const N = parseInt(stage.dataset.points, 10);
    const CELL = parseFloat(stage.dataset.cell);
    const SIZE = (N - 1) * CELL;
    const origin = { e: parseFloat(stage.dataset.e), n: parseFloat(stage.dataset.n) };
    const HALF = SIZE / 2;
    const viewer = stage.querySelector('[data-3d-viewer]');
    const canvas = viewer.querySelector('canvas');
    const labelsEl = viewer.querySelector('[data-3d-labels]');

    /** Lokale Koordinaten in Metern: x nach Osten, y nach Norden, Mitte des Rasters bei 0. */
    function local(lat, lng) {
      const p = toLv95(lat, lng);
      return [p.e - origin.e - HALF, p.n - origin.n - HALF];
    }
    function heightAt(x, y) {
      const fi = Math.min(N - 1.001, Math.max(0, (x + HALF) / CELL));
      const fj = Math.min(N - 1.001, Math.max(0, (y + HALF) / CELL));
      const i = Math.floor(fi), j = Math.floor(fj), u = fi - i, v = fj - j;
      const h00 = heights[j * N + i], h10 = heights[j * N + i + 1], h01 = heights[(j + 1) * N + i], h11 = heights[(j + 1) * N + i + 1];
      return (h00 * (1 - u) + h10 * u) * (1 - v) + (h01 * (1 - u) + h11 * u) * v;
    }
    const toWorld = function (x, y, h) { return new THREE.Vector3(x, h, -y); };

    let minHeight = 0;
    for (let i = 0; i < heights.length; i++) { minHeight = Math.min(minHeight, heights[i]); }
    const BASE = Math.floor(minHeight) - 12;

    const down = shoreToDeep();
    const along = [-down[1], down[0]];
    const maxDepth = -minHeight;

    /** Richtung vom Ufer in die Tiefe, aus den Schwerpunkten von flachem und tiefem Grund. */
    function shoreToDeep() {
      let sx = 0, sy = 0, sc = 0, dx = 0, dy = 0, dc = 0;
      for (let j = 0; j < N; j++) {
        for (let i = 0; i < N; i++) {
          const h = heights[j * N + i], x = -HALF + i * CELL, y = -HALF + j * CELL;
          if (h > -5) { sx += x; sy += y; sc++; }
          if (h < minHeight * 0.6) { dx += x; dy += y; dc++; }
        }
      }
      const raw = sc > 0 && dc > 0 ? [dx / dc - sx / sc, dy / dc - sy / sc] : [0, -1];
      const length = Math.hypot(raw[0], raw[1]) || 1;
      return [raw[0] / length, raw[1] / length];
    }

    const renderer = new THREE.WebGLRenderer({ canvas: canvas, antialias: true, alpha: true });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    const scene = new THREE.Scene();
    const camera = new THREE.PerspectiveCamera(40, 1, 1, 3000);
    const controls = new THREE.OrbitControls(camera, canvas);
    controls.enableDamping = true; controls.dampingFactor = 0.08;
    controls.minDistance = 20; controls.maxDistance = 800;
    controls.minPolarAngle = 0; controls.maxPolarAngle = Math.PI;
    controls.enablePan = true; controls.screenSpacePanning = true;
    controls.rotateSpeed = 0.9; controls.autoRotateSpeed = 0.6;
    controls.listenToKeyEvents(canvas);
    controls.touches = { ONE: null, TWO: THREE.TOUCH.DOLLY_ROTATE };
    canvas.style.touchAction = 'pan-y';
    canvas.tabIndex = 0;
    canvas.addEventListener('wheel', zoomOnlyWithModifier, { capture: true });

    const uniforms = {
      uTime: { value: 0 },
      uFog: { value: new THREE.Color('#061519') },
      uSat: { value: aerial ? new THREE.CanvasTexture(aerial) : null },
      uHasSat: { value: aerial ? 1 : 0 },
      uHalf: { value: HALF },
      uDeep: { value: Math.max(40, -minHeight) }
    };

    addTerrain();

    function addTerrain() {
      const tGeo = new THREE.BufferGeometry();
      const pos = new Float32Array(N * N * 3);
      for (let j = 0; j < N; j++) {
        for (let i = 0; i < N; i++) {
          const k = (j * N + i) * 3, x = -HALF + i * CELL, y = -HALF + j * CELL;
          pos[k] = x; pos[k + 1] = heights[j * N + i]; pos[k + 2] = -y;
        }
      }
      const index = [];
      for (let j = 0; j < N - 1; j++) {
        for (let i = 0; i < N - 1; i++) {
          const a = j * N + i, b = a + 1, c = a + N, d = c + 1;
          index.push(a, b, c, b, d, c);
        }
      }
      tGeo.setAttribute('position', new THREE.BufferAttribute(pos, 3));
      tGeo.setIndex(index);
      tGeo.computeVertexNormals();
      const terrainMat = new THREE.ShaderMaterial({
        uniforms: uniforms,
        vertexShader: 'varying vec3 vPos; varying vec3 vNormal; void main() { vPos = position; vNormal = normal; gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }',
        fragmentShader: [
          'uniform float uTime; uniform vec3 uFog; uniform sampler2D uSat; uniform float uHasSat; uniform float uHalf; uniform float uDeep;',
          'varying vec3 vPos; varying vec3 vNormal;',
          'vec3 ramp(float d) {',
          '  vec3 c0 = vec3(0.62, 0.86, 0.74), c1 = vec3(0.20, 0.58, 0.58), c2 = vec3(0.07, 0.36, 0.42), c3 = vec3(0.03, 0.19, 0.25), c4 = vec3(0.012, 0.07, 0.10);',
          '  if (d < 8.0) return mix(c0, c1, d / 8.0);',
          '  if (d < 25.0) return mix(c1, c2, (d - 8.0) / 17.0);',
          '  if (d < 50.0) return mix(c2, c3, (d - 25.0) / 25.0);',
          '  return mix(c3, c4, clamp((d - 50.0) / max(10.0, uDeep - 50.0), 0.0, 1.0));',
          '}',
          'float caustic(vec2 p, float t) {',
          '  float c = sin(p.x * 0.9 + t * 1.1 + sin(p.y * 0.7 + t)) + sin(p.y * 1.1 - t * 0.9 + sin(p.x * 0.6 - t * 0.7)) + sin((p.x + p.y) * 0.6 + t * 0.8);',
          '  return pow(max(0.0, c / 3.0), 6.0);',
          '}',
          'void main() {',
          '  vec3 n = normalize(vNormal);',
          '  float depth = -vPos.y;',
          '  float diff = clamp(dot(n, normalize(vec3(-0.35, 1.0, 0.45))), 0.0, 1.0);',
          '  float steep = 1.0 - n.y;',
          '  vec3 col;',
          '  if (depth <= 0.0) {',
          '    vec2 suv = vec2((vPos.x + uHalf) / (2.0 * uHalf), (-vPos.z + uHalf) / (2.0 * uHalf));',
          '    vec3 land = mix(vec3(0.11, 0.19, 0.13), vec3(0.20, 0.21, 0.19), smoothstep(0.25, 0.6, steep));',
          '    vec3 sat = uHasSat > 0.5 ? texture2D(uSat, suv).rgb : land;',
          '    col = sat * (0.55 + 0.6 * diff);',
          '  } else {',
          '    col = ramp(depth);',
          '    col = mix(col, col * vec3(0.72, 0.78, 0.8), smoothstep(0.35, 0.8, steep));',
          '    col *= 0.5 + 0.7 * diff;',
          '    col += vec3(0.55, 0.95, 0.9) * caustic(vPos.xz * 0.6, uTime * 1.4) * exp(-depth / 7.0) * 0.55;',
          '    float f = depth / 5.0;',
          '    float w = fwidth(f);',
          '    float line = 1.0 - smoothstep(0.0, w * 1.4, abs(fract(f + 0.5) - 0.5));',
          '    float major = mod(floor(f + 0.5), 2.0) < 0.5 ? 1.0 : 0.0;',
          '    vec3 lineCol = mix(vec3(0.18, 0.62, 0.80), vec3(0.62, 0.92, 1.0), major);',
          '    col = mix(col, lineCol, line * (depth > 2.5 ? (0.35 + 0.5 * major) : 0.0));',
          '  }',
          '  float dist = length(cameraPosition - vPos);',
          '  float fog = 1.0 - exp(-pow(dist / 620.0, 2.0));',
          '  gl_FragColor = vec4(mix(col, uFog, fog * 0.6), 1.0);',
          '}'
        ].join('\n'),
        extensions: { derivatives: true }
      });
      scene.add(new THREE.Mesh(tGeo, terrainMat));
    }

    addSides();

    function addSides() {
      const edges = [];
      for (let i = 0; i < N; i++) { edges.push([i, 0]); }
      for (let j = 1; j < N; j++) { edges.push([N - 1, j]); }
      for (let i = N - 2; i >= 0; i--) { edges.push([i, N - 1]); }
      for (let j = N - 2; j >= 1; j--) { edges.push([0, j]); }
      edges.push([0, 0]);
      const rockPos = [], rockCol = [], waterPos = [];
      for (let k = 0; k < edges.length - 1; k++) {
        const i1 = edges[k][0], j1 = edges[k][1], i2 = edges[k + 1][0], j2 = edges[k + 1][1];
        const x1 = -HALF + i1 * CELL, y1 = -HALF + j1 * CELL, x2 = -HALF + i2 * CELL, y2 = -HALF + j2 * CELL;
        const h1 = heights[j1 * N + i1], h2 = heights[j2 * N + i2];
        const quad = function (arr, a1, a2, b1, b2) { arr.push(x1, a1, -y1, x2, a2, -y2, x2, b2, -y2, x1, a1, -y1, x2, b2, -y2, x1, b1, -y1); };
        quad(rockPos, BASE, BASE, h1, h2);
        [BASE, BASE, h2, BASE, h2, h1].forEach(function (h) {
          const t = (h - BASE) / Math.max(1, -BASE + 60);
          rockCol.push(0.012 + t * 0.03, 0.03 + t * 0.06, 0.035 + t * 0.06);
        });
        if (h1 < 0 || h2 < 0) { quad(waterPos, Math.min(h1, 0), Math.min(h2, 0), 0, 0); }
      }
      const rockGeo = new THREE.BufferGeometry();
      rockGeo.setAttribute('position', new THREE.Float32BufferAttribute(rockPos, 3));
      rockGeo.setAttribute('color', new THREE.Float32BufferAttribute(rockCol, 3));
      scene.add(new THREE.Mesh(rockGeo, new THREE.MeshBasicMaterial({ vertexColors: true, side: THREE.DoubleSide })));
      const waterSideGeo = new THREE.BufferGeometry();
      waterSideGeo.setAttribute('position', new THREE.Float32BufferAttribute(waterPos, 3));
      scene.add(new THREE.Mesh(waterSideGeo, new THREE.ShaderMaterial({
        uniforms: uniforms, transparent: true, depthWrite: false, side: THREE.DoubleSide,
        vertexShader: 'varying float vY; void main(){ vY = position.y; gl_Position = projectionMatrix * modelViewMatrix * vec4(position,1.0); }',
        fragmentShader: 'uniform float uDeep; varying float vY; void main(){ float t = clamp(-vY / uDeep, 0.0, 1.0); vec3 c = mix(vec3(0.30,0.78,0.76), vec3(0.02,0.10,0.14), pow(t,0.5)); gl_FragColor = vec4(c, mix(0.10, 0.34, t)); }'
      })));
    }

    addSurfaceAndLight();

    function addSurfaceAndLight() {
      const surfGeo = new THREE.PlaneGeometry(SIZE, SIZE, 1, 1);
      surfGeo.rotateX(-Math.PI / 2);
      scene.add(new THREE.Mesh(surfGeo, new THREE.ShaderMaterial({
        uniforms: uniforms, transparent: true, depthWrite: false, side: THREE.DoubleSide,
        vertexShader: 'varying vec3 vPos; void main(){ vPos = position; gl_Position = projectionMatrix * modelViewMatrix * vec4(position,1.0); }',
        fragmentShader: 'uniform float uTime; varying vec3 vPos; void main(){ vec2 p = vPos.xz * 0.08; float w = sin(p.x * 3.0 + uTime * 0.8) * sin(p.y * 2.3 - uTime * 0.6) + sin((p.x - p.y) * 4.1 + uTime); float glint = smoothstep(1.7, 1.95, w); vec3 c = mix(vec3(0.30, 0.78, 0.76), vec3(0.85, 1.0, 0.97), glint); gl_FragColor = vec4(c, 0.07 + glint * 0.12); }'
      })));
      const rim = new THREE.LineSegments(new THREE.EdgesGeometry(new THREE.BoxGeometry(SIZE, 0.01, SIZE)), new THREE.LineBasicMaterial({ color: 0x7FE3D6, transparent: true, opacity: 0.35 }));
      scene.add(rim);
      scene.add(new THREE.HemisphereLight(0xbfefff, 0x061519, 0.75));
      const sun = new THREE.DirectionalLight(0xffffff, 0.9); sun.position.set(-80, 160, 60); scene.add(sun);
    }

    const labelItems = [];
    function addLabel(world, html, cls) {
      const el = document.createElement('div');
      el.className = 'lbl' + (cls ? ' ' + cls : '');
      el.innerHTML = html;
      labelsEl.appendChild(el);
      labelItems.push({ world: world, el: el, width: 0, height: 0 });
    }

    addDepthLabels();

    function addDepthLabels() {
      function depthSprite(label) {
        const c = document.createElement('canvas'); c.width = 128; c.height = 48;
        const g = c.getContext('2d');
        g.font = "500 30px 'IBM Plex Mono', monospace"; g.textAlign = 'center'; g.textBaseline = 'middle';
        g.fillStyle = 'rgba(6, 21, 25, 0.55)'; g.beginPath();
        if (g.roundRect) { g.roundRect(14, 6, 100, 36, 10); } else { g.rect(14, 6, 100, 36); }
        g.fill();
        g.fillStyle = '#8CCFEA'; g.fillText(label, 64, 25);
        const sprite = new THREE.Sprite(new THREE.SpriteMaterial({ map: new THREE.CanvasTexture(c), transparent: true, opacity: 0.85, depthWrite: false }));
        sprite.scale.set(9, 3.4, 1);
        return sprite;
      }
      [-0.35, 0.35].forEach(function (offset) {
        const anchor = [along[0] * SIZE * offset, along[1] * SIZE * offset];
        for (let d = 10; d <= Math.min(100, maxDepth - 2); d += 10) {
          let best = null, bestDistance = 1e9;
          for (let j = 2; j < N - 2; j++) {
            for (let i = 2; i < N - 2; i++) {
              const h = heights[j * N + i];
              if (Math.abs(h + d) > 0.8) { continue; }
              const x = -HALF + i * CELL, y = -HALF + j * CELL;
              const distance = Math.hypot(x - anchor[0], y - anchor[1]) + Math.abs((x - anchor[0]) * along[0] + (y - anchor[1]) * along[1]) * 2;
              if (distance < bestDistance) { bestDistance = distance; best = [x, y]; }
            }
          }
          if (best) {
            const sprite = depthSprite(format(text.depth, [d]));
            sprite.position.copy(toWorld(best[0], best[1], -d + 1.4));
            scene.add(sprite);
          }
        }
      });
    }

    const tealMat = new THREE.MeshStandardMaterial({ color: 0x7FE3D6, emissive: 0x1e6f68, roughness: 0.45, metalness: 0.1 });
    const woodMat = new THREE.MeshStandardMaterial({ color: 0xB88A5A, emissive: 0x2a1a0c, roughness: 0.8 });
    const signalMat = new THREE.MeshStandardMaterial({ color: 0xFF9A4D, emissive: 0x5a2a08, roughness: 0.5 });
    const hullMat = new THREE.MeshStandardMaterial({ color: 0xD8E6E2, emissive: 0x1d2e2c, roughness: 0.7 });
    const rockMat = new THREE.MeshStandardMaterial({ color: 0x55615F, emissive: 0x0d1312, roughness: 0.9 });
    addSketchObjects();

    function addSketchObjects() {
      function pointMesh(kind) {
        const g = new THREE.Group();
        if (kind === 'Sculpture') {
          const base = new THREE.Mesh(new THREE.CylinderGeometry(1.2, 1.5, 0.8, 16), hullMat); base.position.y = 0.4; g.add(base);
          const fig = new THREE.Mesh(new THREE.CapsuleGeometry(0.55, 2.2, 6, 12), tealMat); fig.position.y = 2.3; g.add(fig);
          const head = new THREE.Mesh(new THREE.SphereGeometry(0.5, 16, 12), tealMat); head.position.y = 4.0; g.add(head);
        } else if (kind === 'Wreck') {
          const hull = new THREE.Mesh(new THREE.SphereGeometry(1, 20, 10, 0, Math.PI * 2, Math.PI / 2, Math.PI / 2), hullMat);
          hull.scale.set(4.2, 1.3, 1.6); hull.rotation.z = Math.PI; hull.position.y = 1.0; g.add(hull);
          g.rotation.set(0.12, 0.9, 0.18);
        } else if (kind === 'TreeTrunk') {
          const t = new THREE.Mesh(new THREE.CylinderGeometry(0.6, 0.8, 9, 10), woodMat); t.rotation.z = Math.PI / 2; t.position.y = 0.7; g.add(t);
          const br = new THREE.Mesh(new THREE.CylinderGeometry(0.2, 0.35, 3.4, 8), woodMat); br.position.set(1.5, 1.8, 0.4); br.rotation.z = -0.8; g.add(br);
          g.rotation.y = 0.6;
        } else if (kind === 'Danger') {
          const m = new THREE.Mesh(new THREE.OctahedronGeometry(1.3), signalMat); m.position.y = 1.6; g.add(m);
        } else if (kind === 'Boulder') {
          const m = new THREE.Mesh(new THREE.DodecahedronGeometry(2.2), rockMat); m.scale.set(1.2, 0.8, 1); m.position.y = 1.2; g.add(m);
        } else if (kind === 'Object') {
          const m = new THREE.Mesh(new THREE.BoxGeometry(3.2, 1.4, 1.8), hullMat); m.position.y = 0.7; m.rotation.y = 0.4; g.add(m);
        } else {
          const m = new THREE.Mesh(new THREE.IcosahedronGeometry(1.1), tealMat); m.position.y = 1.4; g.add(m);
        }
        return g;
      }
      function label(feature) { return feature.label || kinds[feature.kind] || feature.kind; }

      sketch.forEach(function (feature) {
        const points = feature.points.map(function (p) { return local(p.lat, p.lng); });
        if (feature.kind === 'Route' || points.length === 0) { return; }
        if (points.length === 1) {
          const p = points[0];
          const ground = heightAt(p[0], p[1]);
          const y = feature.depth != null ? -feature.depth : ground;
          const mesh = pointMesh(feature.kind);
          mesh.position.copy(toWorld(p[0], p[1], y));
          scene.add(mesh);
          const estimated = feature.depth == null;
          const top = toWorld(p[0], p[1], y + 7);
          scene.add(new THREE.Line(new THREE.BufferGeometry().setFromPoints([toWorld(p[0], p[1], y + 1), top]),
            new THREE.LineBasicMaterial({ color: estimated ? 0xFF9A4D : 0x7FE3D6, transparent: true, opacity: 0.7 })));
          const depth = ground < 0 || feature.depth != null
            ? (estimated ? '<i class="est" title="' + escapeHtml(text.estimatedTitle) + '">≈ ' + Math.round(-ground) + ' m</i>' : '<i>' + escapeHtml(feature.depth) + ' m</i>')
            : '';
          const dot = feature.kind === 'Danger' || estimated ? '#FF9A4D' : '#7FE3D6';
          addLabel(top, '<span class="dot" style="background:' + dot + '"></span>' + escapeHtml(label(feature)) + depth);
          return;
        }
        const draped = points.map(function (p) { return toWorld(p[0], p[1], heightAt(p[0], p[1]) + 0.5); });
        const isArea = feature.kind === 'PlantField' || feature.kind === 'MooringField';
        if (isArea) { draped.push(draped[0].clone()); }
        const color = feature.kind === 'PlantField' ? 0x2F8F55 : feature.kind === 'MooringField' ? 0x2F6BC4 : feature.kind === 'Line' ? 0xB8740F : 0xDCE8E5;
        const curve = new THREE.CatmullRomCurve3(draped, false, 'catmullrom', 0.1);
        scene.add(new THREE.Mesh(new THREE.TubeGeometry(curve, Math.max(16, draped.length * 8), isArea ? 0.3 : 0.45, 6, false),
          new THREE.MeshBasicMaterial({ color: color, transparent: true, opacity: 0.75 })));
        const mid = draped[Math.floor(draped.length / 2)];
        addLabel(mid.clone().add(new THREE.Vector3(0, 5, 0)), '<span class="dot" style="background:#' + color.toString(16).padStart(6, '0') + '"></span>' + escapeHtml(label(feature)));
      });
    }

    addEntries();

    function addEntries() {
      entries.forEach(function (entry) {
        const p = local(entry.lat, entry.lng);
        const shore = heightAt(p[0], p[1]) >= 0 ? p : nearestShore(p[0], p[1], 40);
        const g = new THREE.Group();
        let top;
        if (shore) {
          const y = heightAt(shore[0], shore[1]);
          const pole = new THREE.Mesh(new THREE.CylinderGeometry(0.3, 0.3, 9, 8), hullMat); pole.position.y = 4.5; g.add(pole);
          g.add(alphaFlag(8.6, 1.6));
          g.position.copy(toWorld(shore[0], shore[1], y));
          top = toWorld(shore[0], shore[1], y + 12);
        } else {
          const buoy = new THREE.Mesh(new THREE.SphereGeometry(0.9, 16, 10), signalMat); buoy.scale.y = 0.7; g.add(buoy);
          const pole = new THREE.Mesh(new THREE.CylinderGeometry(0.12, 0.12, 3.6, 8), hullMat); pole.position.y = 1.8; g.add(pole);
          g.add(alphaFlag(3, 0.55));
          g.position.copy(toWorld(p[0], p[1], 0));
          top = toWorld(p[0], p[1], 5.5);
        }
        g.rotation.y = Math.atan2(along[1], along[0]);
        scene.add(g);
        addLabel(top, '<span class="dot" style="background:#7FE3D6"></span>' + escapeHtml(entry.name || text.entry));
      });
    }

    /** Taucherflagge «Alpha»: weiss am Mast, blau mit Schwalbenschwanz. */
    function alphaFlag(top, scale) {
      const k = scale || 1, w = 3.6 * k, h = 2.4 * k;
      const flag = new THREE.Group();
      const white = new THREE.Shape([new THREE.Vector2(0, 0), new THREE.Vector2(w / 2, 0), new THREE.Vector2(w / 2, h), new THREE.Vector2(0, h)]);
      const blue = new THREE.Shape([new THREE.Vector2(w / 2, 0), new THREE.Vector2(w, 0), new THREE.Vector2(w * 0.75, h / 2), new THREE.Vector2(w, h), new THREE.Vector2(w / 2, h)]);
      flag.add(new THREE.Mesh(new THREE.ShapeGeometry(white), new THREE.MeshBasicMaterial({ color: 0xF4F7F6, side: THREE.DoubleSide })));
      flag.add(new THREE.Mesh(new THREE.ShapeGeometry(blue), new THREE.MeshBasicMaterial({ color: 0x1E4FA3, side: THREE.DoubleSide })));
      flag.position.set(0.2, top - h, 0);
      return flag;
    }

    function nearestShore(x, y, radius) {
      let best = null, bestDistance = radius;
      for (let dy = -radius; dy <= radius; dy += CELL) {
        for (let dx = -radius; dx <= radius; dx += CELL) {
          const distance = Math.hypot(dx, dy);
          if (distance <= bestDistance && Math.abs(x + dx) <= HALF && Math.abs(y + dy) <= HALF && heightAt(x + dx, y + dy) >= 0.3) {
            best = [x + dx, y + dy]; bestDistance = distance;
          }
        }
      }
      return best;
    }

    const route = routeAlongGround();

    /** Die Route der Skizze 1.5 bis 2 m über Grund, geglättet, an der Oberfläche höchstens bis 0.3 m. */
    function routeAlongGround() {
      const routeFeature = sketch.find(function (f) { return f.kind === 'Route' && f.points.length > 1; });
      if (routeFeature) {
        const pts2 = routeFeature.points.map(function (p) { const l = local(p.lat, p.lng); return new THREE.Vector3(l[0], l[1], 0); });
        const flat = new THREE.CatmullRomCurve3(pts2, false, 'centripetal');
        const SAMPLES = 400, rough = [];
        for (let s = 0; s <= SAMPLES; s++) {
          const p = flat.getPoint(s / SAMPLES);
          rough.push([p.x, p.y, Math.min(-0.3, heightAt(p.x, p.y) + 1.8)]);
        }
        for (let pass = 0; pass < 6; pass++) {
          for (let s = 1; s < rough.length - 1; s++) { rough[s][2] = (rough[s - 1][2] + rough[s][2] * 2 + rough[s + 1][2]) / 4; }
        }
        rough.forEach(function (r) { r[2] = Math.min(r[2], Math.min(-0.3, heightAt(r[0], r[1]) + 1.2)); });
        const curve = new THREE.CatmullRomCurve3(rough.map(function (r) { return toWorld(r[0], r[1], r[2]); }));
        let length = 0; const cum = [0];
        for (let s = 1; s < rough.length; s++) {
          length += Math.hypot(rough[s][0] - rough[s - 1][0], rough[s][1] - rough[s - 1][1], rough[s][2] - rough[s - 1][2]);
          cum.push(length);
        }
        return { rough: rough, curve: curve, length: length, cum: cum, samples: SAMPLES };
      }
      return null;
    }

    const ARROWS = 26;
    let routeMat = null, arrows = null, diver = null, legs = [], halo = null;
    const v3 = new THREE.Vector3(), dummy = new THREE.Object3D(), ahead = new THREE.Vector3(), up = new THREE.Vector3(0, 1, 0), mouth = new THREE.Vector3(0, 0.7, 0);
    if (route) {
      addRouteTrail();
      addDiver();
    }

    function addRouteTrail() {
      routeMat = new THREE.ShaderMaterial({
        uniforms: { uTime: uniforms.uTime, uHead: { value: 0 } }, transparent: true,
        vertexShader: 'varying vec2 vUv; void main(){ vUv = uv; gl_Position = projectionMatrix * modelViewMatrix * vec4(position,1.0); }',
        fragmentShader: 'uniform float uTime; uniform float uHead; varying vec2 vUv; void main(){ float f = fract(vUv.x * 70.0 - uTime * 2.2); float dash = smoothstep(0.0, 0.15, f) * (1.0 - smoothstep(0.45, 0.6, f)); float trail = vUv.x <= uHead ? exp(-(uHead - vUv.x) * 14.0) : 0.0; vec3 c = mix(vec3(1.0, 0.42, 0.10), vec3(1.0, 0.86, 0.62), trail); gl_FragColor = vec4(min(vec3(1.0), c * (0.75 + dash * 0.35 + trail * 0.5)), 0.85 + dash * 0.15); }'
      });
      scene.add(new THREE.Mesh(new THREE.TubeGeometry(route.curve, 700, 0.55, 10, false), routeMat));
      scene.add(new THREE.Mesh(new THREE.TubeGeometry(route.curve, 700, 2.4, 14, false), new THREE.ShaderMaterial({
        uniforms: { uTime: uniforms.uTime, uHead: routeMat.uniforms.uHead }, transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, side: THREE.DoubleSide,
        vertexShader: 'varying vec2 vUv; varying vec3 vN; varying vec3 vV; void main(){ vUv = uv; vec4 mv = modelViewMatrix * vec4(position,1.0); vN = normalize(normalMatrix * normal); vV = normalize(-mv.xyz); gl_Position = projectionMatrix * mv; }',
        fragmentShader: 'uniform float uTime; uniform float uHead; varying vec2 vUv; varying vec3 vN; varying vec3 vV; void main(){ float rim = pow(abs(dot(vN, vV)), 1.6); float pulse = 0.0; for (int k = 0; k < 3; k++) { float p = fract(uTime * 0.09 + float(k) / 3.0); pulse += exp(-pow((vUv.x - p) * 22.0, 2.0)); } float trail = vUv.x <= uHead ? exp(-(uHead - vUv.x) * 10.0) : 0.0; float a = rim * (0.16 + pulse * 0.55 + trail * 0.8); gl_FragColor = vec4(vec3(1.0, 0.5, 0.18) * a, a); }'
      })));
      scene.add(new THREE.Line(new THREE.BufferGeometry().setFromPoints(route.rough.map(function (r) { return toWorld(r[0], r[1], heightAt(r[0], r[1]) + 0.15); })),
        new THREE.LineBasicMaterial({ color: 0xFF9A4D, transparent: true, opacity: 0.35 })));
      const arrowGeo = new THREE.ConeGeometry(0.9, 2.4, 3); arrowGeo.rotateX(Math.PI / 2);
      arrows = new THREE.InstancedMesh(arrowGeo, new THREE.MeshBasicMaterial({ color: 0xFFE3C2, transparent: true, opacity: 0.9, blending: THREE.AdditiveBlending, depthWrite: false }), ARROWS);
      scene.add(arrows);
    }

    function addDiver() {
      diver = new THREE.Group();
      const suit = new THREE.MeshStandardMaterial({ color: 0x1B2E33, roughness: 0.55, emissive: 0x060c0d });
      const suitLight = new THREE.MeshStandardMaterial({ color: 0x2C4A50, roughness: 0.5, emissive: 0x08110f });
      const accent = new THREE.MeshStandardMaterial({ color: 0xFF9A4D, emissive: 0x6a3008, roughness: 0.4 });
      const steel = new THREE.MeshStandardMaterial({ color: 0xC9D6D3, metalness: 0.6, roughness: 0.3, emissive: 0x1a2220 });
      const glass = new THREE.MeshStandardMaterial({ color: 0x9FE9DF, emissive: 0x2a6a62, metalness: 0.4, roughness: 0.15 });
      const limb = function (r, len, mat) { const m = new THREE.Mesh(new THREE.CapsuleGeometry(r, len, 6, 10), mat); m.rotation.x = Math.PI / 2; m.position.z = -len / 2; return m; };
      const torso = new THREE.Mesh(new THREE.CapsuleGeometry(0.21, 0.62, 8, 14), suit); torso.rotation.x = Math.PI / 2; torso.scale.set(1.3, 1, 0.9); torso.position.set(0, 0, 0.32); diver.add(torso);
      const jacket = new THREE.Mesh(new THREE.CapsuleGeometry(0.23, 0.38, 8, 14), suitLight); jacket.rotation.x = Math.PI / 2; jacket.scale.set(1.35, 1.05, 0.9); jacket.position.set(0, 0.02, 0.46); diver.add(jacket);
      const tank = new THREE.Mesh(new THREE.CapsuleGeometry(0.1, 0.52, 6, 12), accent); tank.rotation.x = Math.PI / 2; tank.position.set(0, 0.25, 0.36); diver.add(tank);
      const valve = new THREE.Mesh(new THREE.CylinderGeometry(0.04, 0.04, 0.1, 8), steel); valve.rotation.x = Math.PI / 2; valve.position.set(0, 0.25, 0.73); diver.add(valve);
      const neck = new THREE.Mesh(new THREE.CylinderGeometry(0.08, 0.09, 0.12, 10), suit); neck.rotation.x = Math.PI / 2; neck.position.set(0, 0.03, 0.8); diver.add(neck);
      const head = new THREE.Mesh(new THREE.SphereGeometry(0.14, 18, 14), suit); head.scale.set(1, 1.05, 1.15); head.position.set(0, 0.06, 0.94); diver.add(head);
      const mask = new THREE.Mesh(new THREE.BoxGeometry(0.22, 0.1, 0.06), glass); mask.position.set(0, 0.06, 1.08); mask.rotation.x = -0.5; diver.add(mask);
      const strap = new THREE.Mesh(new THREE.TorusGeometry(0.145, 0.012, 6, 24), suitLight); strap.position.set(0, 0.08, 0.95); strap.rotation.x = Math.PI / 2 - 0.5; diver.add(strap);
      const reg = new THREE.Mesh(new THREE.SphereGeometry(0.045, 10, 8), steel); reg.position.set(0, -0.06, 1.06); diver.add(reg);
      [-1, 1].forEach(function (side) {
        const shoulder = new THREE.Group(); shoulder.position.set(side * 0.29, -0.02, 0.66);
        shoulder.add(limb(0.06, 0.3, suit));
        const elbow = new THREE.Group(); elbow.position.z = -0.36; shoulder.add(elbow);
        elbow.add(limb(0.05, 0.26, suit));
        const hand = new THREE.Mesh(new THREE.SphereGeometry(0.055, 10, 8), suitLight); hand.position.z = -0.32; elbow.add(hand);
        shoulder.rotation.set(0.35, side * 0.25, 0); elbow.rotation.set(-0.2, -side * 0.6, 0);
        diver.add(shoulder);
      });
      const finShape = new THREE.Shape();
      finShape.moveTo(-0.07, 0); finShape.lineTo(0.07, 0); finShape.lineTo(0.17, -0.55); finShape.quadraticCurveTo(0, -0.5, -0.17, -0.55); finShape.lineTo(-0.07, 0);
      const finGeo = new THREE.ShapeGeometry(finShape); finGeo.rotateX(-Math.PI / 2);
      const finMat = new THREE.MeshStandardMaterial({ color: 0xFF9A4D, side: THREE.DoubleSide, emissive: 0x4a2006, roughness: 0.45 });
      [-1, 1].forEach(function (side) {
        const hip = new THREE.Group(); hip.position.set(side * 0.12, -0.02, 0);
        hip.add(limb(0.085, 0.38, suit));
        const knee = new THREE.Group(); knee.position.z = -0.44; hip.add(knee);
        knee.add(limb(0.07, 0.36, suit));
        const foot = new THREE.Group(); foot.position.z = -0.46; knee.add(foot);
        foot.add(new THREE.Mesh(finGeo, finMat));
        diver.add(hip); legs.push({ hip: hip, knee: knee, side: side });
      });
      const lamp = new THREE.PointLight(0xFFE2C4, 1.6, 22, 2); lamp.position.set(0, 0, 3); diver.add(lamp);
      diver.scale.setScalar(4.2);
      scene.add(diver);
      halo = new THREE.Mesh(new THREE.RingGeometry(3.0, 3.6, 48), new THREE.MeshBasicMaterial({ color: 0xFF9A4D, transparent: true, opacity: 0.6, side: THREE.DoubleSide, depthWrite: false }));
      scene.add(halo);
    }

    function particles(max, vertex, fragment, extra) {
      const positions = new Float32Array(max * 3), life = new Float32Array(max).fill(-1);
      const geo = new THREE.BufferGeometry();
      geo.setAttribute('position', new THREE.BufferAttribute(positions, 3));
      geo.setAttribute('life', new THREE.BufferAttribute(life, 1));
      if (extra) { geo.setAttribute('size', new THREE.BufferAttribute(extra, 1)); }
      const mat = new THREE.ShaderMaterial({ transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, uniforms: { uScale: { value: 300 } }, vertexShader: vertex, fragmentShader: fragment });
      const points = new THREE.Points(geo, mat); points.frustumCulled = false; scene.add(points);
      return { positions: positions, life: life, geo: geo, mat: mat, next: 0, max: max };
    }
    const bubbleSize = new Float32Array(260), bubbleSeed = new Float32Array(260);
    const bubbles = particles(260,
      'attribute float size; attribute float life; varying float vLife; uniform float uScale; void main(){ vLife = life; vec4 mv = modelViewMatrix * vec4(position,1.0); gl_PointSize = life < 0.0 ? 0.0 : size * uScale / -mv.z; gl_Position = projectionMatrix * mv; }',
      'varying float vLife; void main(){ vec2 p = gl_PointCoord - 0.5; float r = length(p); if (r > 0.5) discard; float ring = smoothstep(0.32, 0.46, r) * (1.0 - smoothstep(0.46, 0.5, r)); float hl = smoothstep(0.16, 0.0, length(p - vec2(-0.14, -0.14))); float a = (ring * 0.9 + hl * 0.8 + 0.08) * (1.0 - smoothstep(0.85, 1.0, vLife)); gl_FragColor = vec4(vec3(0.75, 0.97, 0.94), a); }',
      bubbleSize);
    const wake = particles(160,
      'attribute float life; varying float vL; uniform float uScale; void main(){ vL = life; vec4 mv = modelViewMatrix * vec4(position,1.0); gl_PointSize = life < 0.0 ? 0.0 : (1.0 - life) * 1.4 * uScale / -mv.z; gl_Position = projectionMatrix * mv; }',
      'varying float vL; void main(){ float r = length(gl_PointCoord - 0.5); if (r > 0.5) discard; float a = (1.0 - r * 2.0) * (1.0 - vL); gl_FragColor = vec4(vec3(1.0, 0.7, 0.35) * a, a); }');
    function emitBubbles(origin, count) {
      for (let c = 0; c < count; c++) {
        const k = bubbles.next; bubbles.next = (bubbles.next + 1) % bubbles.max;
        bubbles.positions[k * 3] = origin.x + (Math.random() - 0.5) * 0.5;
        bubbles.positions[k * 3 + 1] = origin.y + Math.random() * 0.4;
        bubbles.positions[k * 3 + 2] = origin.z + (Math.random() - 0.5) * 0.5;
        bubbleSize[k] = 0.25 + Math.random() * 0.45; bubbles.life[k] = 0; bubbleSeed[k] = Math.random() * 10;
      }
    }
    function emitWake(p) {
      const k = wake.next; wake.next = (wake.next + 1) % wake.max;
      wake.positions[k * 3] = p.x + (Math.random() - 0.5) * 1.2; wake.positions[k * 3 + 1] = p.y + (Math.random() - 0.5) * 1.2; wake.positions[k * 3 + 2] = p.z + (Math.random() - 0.5) * 1.2;
      wake.life[k] = 0;
    }
    const ripples = [];
    for (let r = 0; r < 10; r++) {
      const m = new THREE.Mesh(new THREE.RingGeometry(0.8, 1.0, 32), new THREE.MeshBasicMaterial({ color: 0xBFF5EE, transparent: true, opacity: 0, side: THREE.DoubleSide, depthWrite: false }));
      m.rotation.x = -Math.PI / 2; m.userData.t = -1; scene.add(m); ripples.push(m);
    }
    let rippleNext = 0;

    const hudDepth = viewer.querySelector('[data-3d-depth]');
    const hudMeta = viewer.querySelector('[data-3d-meta]');
    const profile = stage.querySelector('[data-3d-profile]');
    let profileMarker = null, profileDot = null, px = null, py = null;
    drawProfile();

    function drawProfile() {
      if (route) {
        const maxRoute = Math.max.apply(null, route.rough.map(function (r) { return -r[2]; }));
        const scaleMax = Math.max(10, Math.ceil(maxRoute / 10) * 10);
        px = function (s) { return route.cum[s] / route.length * 1000; };
        py = function (d) { return 8 + d / scaleMax * 132; };
        const svg = profile.querySelector('svg');
        const ns = 'http://www.w3.org/2000/svg';
        let path = 'M0,' + py(0) + ' ';
        route.rough.forEach(function (r, s) { path += 'L' + px(s).toFixed(1) + ',' + py(-r[2]).toFixed(1) + ' '; });
        for (let d = 0; d <= scaleMax; d += 10) {
          const l = document.createElementNS(ns, 'line');
          l.setAttribute('x1', 0); l.setAttribute('x2', 1000); l.setAttribute('y1', py(d)); l.setAttribute('y2', py(d));
          l.setAttribute('class', 'grid'); svg.appendChild(l);
        }
        const area = document.createElementNS(ns, 'path'); area.setAttribute('d', path + 'L1000,8 L0,8 Z'); area.setAttribute('class', 'area'); svg.appendChild(area);
        const line = document.createElementNS(ns, 'path'); line.setAttribute('d', path); line.setAttribute('class', 'line'); svg.appendChild(line);
        profileMarker = document.createElementNS(ns, 'line'); profileMarker.setAttribute('y1', 0); profileMarker.setAttribute('y2', 150); profileMarker.setAttribute('class', 'marker'); svg.appendChild(profileMarker);
        profileDot = document.createElementNS(ns, 'circle'); profileDot.setAttribute('r', 5); profileDot.setAttribute('class', 'dot'); svg.appendChild(profileDot);
        profile.querySelector('[data-3d-profile-meta]').textContent = format(text.profileMeta, [Math.round(route.length), Math.round(maxRoute)]);
        profile.hidden = false;
        viewer.querySelector('[data-3d-hud]').hidden = false;
      } else {
        viewer.querySelectorAll('[data-route-only]').forEach(function (el) { el.hidden = true; });
      }
    }

    const target = new THREE.Vector3(down[0] * 10, -Math.min(20, maxDepth / 3), -down[1] * 10);
    const views = {
      oblique: { pos: new THREE.Vector3(down[0] * 150 - along[0] * 55, 85, -(down[1] * 150 - along[1] * 55)), target: target },
      wall: { pos: new THREE.Vector3(down[0] * 110, -Math.min(30, maxDepth / 2), -down[1] * 110), target: new THREE.Vector3(-down[0] * 5, -Math.min(22, maxDepth / 3), down[1] * 5) },
      top: { pos: new THREE.Vector3(0, SIZE * 1.45, 1), target: new THREE.Vector3(0, -20, 0) }
    };
    let fly = null;
    function setView(name, instant) {
      const base = views[name];
      const factor = Math.pow(Math.max(1, 1.3 / (camera.aspect || 1.3)), 0.75);
      const v = { pos: base.target.clone().add(base.pos.clone().sub(base.target).multiplyScalar(factor)), target: base.target };
      viewer.querySelectorAll('[data-view]').forEach(function (b) { b.setAttribute('aria-pressed', String(b.dataset.view === name)); });
      if (instant || reduceMotion) { camera.position.copy(v.pos); controls.target.copy(v.target); controls.update(); return; }
      fly = { from: camera.position.clone(), fromT: controls.target.clone(), to: v.pos, toT: v.target, t: 0 };
    }
    viewer.querySelectorAll('[data-view]').forEach(function (b) { b.onclick = function () { setView(b.dataset.view); }; });

    let playing = !reduceMotion && !!route;
    let progress = reduceMotion ? 0.35 : 0;
    const btnPlay = viewer.querySelector('[data-3d-play]');
    function syncPlay() { btnPlay.textContent = playing ? text.pause : text.play; btnPlay.setAttribute('aria-pressed', String(playing)); }
    btnPlay.onclick = function () { playing = !playing; syncPlay(); };
    syncPlay();
    const btnSpin = viewer.querySelector('[data-3d-spin]');
    btnSpin.onclick = function () { controls.autoRotate = !controls.autoRotate; btnSpin.setAttribute('aria-pressed', String(controls.autoRotate)); };
    const hint = viewer.querySelector('[data-3d-hint]');
    let nudgeTimer = 0;
    /** Ohne Ctrl oder ⌘ scrollt das Mausrad die Seite (Entscheid Marc, 28.11.2026). */
    function zoomOnlyWithModifier(event) {
      if (!event.ctrlKey && !event.metaKey) {
        event.stopImmediatePropagation();
        nudgeHint();
      }
    }
    function nudgeHint() {
      hint.classList.remove('gone');
      hint.classList.add('nudge');
      clearTimeout(nudgeTimer);
      nudgeTimer = setTimeout(function () { hint.classList.remove('nudge'); }, 1600);
    }
    canvas.onpointerdown = function () { hint.classList.add('gone'); canvas.onpointerdown = null; };

    function resize() {
      const w = canvas.clientWidth, h = canvas.clientHeight;
      if (!w || !h) { return; }
      renderer.setSize(w, h, false);
      camera.aspect = w / h; camera.updateProjectionMatrix();
      bubbles.mat.uniforms.uScale.value = h * 0.9; wake.mat.uniforms.uScale.value = h * 0.9;
    }
    const resizeObserver = new ResizeObserver(resize);
    resizeObserver.observe(canvas);
    resize();
    setView('oblique', true);

    let visible = true;
    const intersection = new IntersectionObserver(function (items) { visible = items[0].isIntersecting; });
    intersection.observe(viewer);

    let last = performance.now(), breath = 0, stopped = false, running = false;
    function frame(now) {
      if (stopped) { return; }
      if (!document.body.contains(canvas)) {
        dispose();
        return;
      }
      if (stage.dataset.mode !== '3d') {
        running = false;
        return;
      }
      requestAnimationFrame(frame);
      const dt = Math.max(0, Math.min(0.05, (now - last) / 1000)); last = now;
      if (!visible || document.hidden) { return; }
      if (!reduceMotion) { uniforms.uTime.value += dt; }
      if (fly) {
        fly.t = Math.min(1, fly.t + dt / 1.4);
        const e = fly.t < 0.5 ? 4 * fly.t * fly.t * fly.t : 1 - Math.pow(-2 * fly.t + 2, 3) / 2;
        camera.position.lerpVectors(fly.from, fly.to, e); controls.target.lerpVectors(fly.fromT, fly.toT, e);
        if (fly.t >= 1) { fly = null; }
      }
      controls.update();

      if (route) {
        updateRoute(now, dt);
      }

      updateParticles(now, dt);
      placeLabels();
      renderer.render(scene, camera);
    }
    resume();
    return { dispose: dispose, resume: resume };

    function resume() {
      if (running || stopped) { return; }
      running = true;
      last = performance.now();
      requestAnimationFrame(frame);
    }

    function dispose() {
      stopped = true;
      resizeObserver.disconnect();
      intersection.disconnect();
      controls.dispose();
      scene.traverse(function (object) {
        if (object.geometry) { object.geometry.dispose(); }
        [].concat(object.material || []).forEach(function (material) {
          Object.keys(material).forEach(function (key) { if (material[key] && material[key].isTexture) { material[key].dispose(); } });
          material.dispose();
        });
      });
      if (uniforms.uSat.value) { uniforms.uSat.value.dispose(); }
      renderer.dispose();
      labelsEl.replaceChildren();
      profile.querySelector('svg').replaceChildren();
      viewer.querySelectorAll('[data-view]').forEach(function (b) { b.onclick = null; });
      btnPlay.onclick = null; btnSpin.onclick = null; canvas.onpointerdown = null; canvas.removeEventListener('wheel', zoomOnlyWithModifier, { capture: true });
      clearTimeout(nudgeTimer);
    }

    function updateRoute(now, dt) {
      if (playing) { progress = (progress + dt * 5.5 / route.length) % 1; }
      const u = Math.min(0.9999, Math.max(0, progress));
      const p = route.curve.getPointAt(u), tg = route.curve.getTangentAt(u);
      diver.position.copy(p);
      diver.lookAt(v3.copy(p).add(tg));
      legs.forEach(function (leg) {
        const phase = reduceMotion ? 0 : now / 210 + (leg.side > 0 ? Math.PI : 0);
        leg.hip.rotation.x = Math.sin(phase) * 0.28;
        leg.knee.rotation.x = 0.25 + Math.sin(phase - 0.9) * 0.3;
      });
      routeMat.uniforms.uHead.value = u;
      for (let k = 0; k < ARROWS; k++) {
        const ua = (k / ARROWS + uniforms.uTime.value * 0.012) % 1;
        const ap = route.curve.getPointAt(ua), at = route.curve.getTangentAt(ua);
        dummy.position.copy(ap); dummy.lookAt(ahead.copy(ap).add(at));
        dummy.scale.setScalar(0.6 + 0.4 * Math.sin(ua * Math.PI)); dummy.updateMatrix(); arrows.setMatrixAt(k, dummy.matrix);
      }
      arrows.instanceMatrix.needsUpdate = true;
      halo.position.set(p.x, heightAt(p.x, -p.z) + 0.25, p.z);
      halo.lookAt(ahead.copy(halo.position).add(up));
      const hs = 1 + 0.35 * Math.sin(now / 300); halo.scale.setScalar(reduceMotion ? 1 : hs);
      halo.material.opacity = reduceMotion ? 0.5 : 0.65 - 0.35 * (hs - 0.65);
      if (playing && !reduceMotion) {
        breath += dt;
        if (breath > 3.2) { breath = 0; emitBubbles(v3.copy(p).add(mouth).addScaledVector(tg, 2.0), 14); }
        else if (Math.random() < dt * 3) { emitBubbles(v3.copy(p).add(mouth).addScaledVector(tg, 2.0), 1); }
        if (Math.random() < dt * 40) { emitWake(v3.copy(p).addScaledVector(tg, -3.5)); }
      }
      hudDepth.innerHTML = (-p.y).toFixed(1) + ' <small>m</small>';
      hudMeta.textContent = format(text.progress, [Math.round(u * route.length), Math.round(route.length)]);
      const mx = u * 1000; profileMarker.setAttribute('x1', mx); profileMarker.setAttribute('x2', mx);
      profileDot.setAttribute('cx', mx); profileDot.setAttribute('cy', py(-p.y));
    }

    function updateParticles(now, dt) {
      for (let k = 0; k < bubbles.max; k++) {
        if (bubbles.life[k] < 0) { continue; }
        const y = bubbles.positions[k * 3 + 1] + (2.6 + bubbleSize[k] * 2.2) * dt;
        bubbles.positions[k * 3 + 1] = y;
        bubbles.positions[k * 3] += Math.sin(now / 260 + bubbleSeed[k]) * dt * 0.6;
        bubbles.positions[k * 3 + 2] += Math.cos(now / 300 + bubbleSeed[k]) * dt * 0.6;
        bubbleSize[k] *= 1 + dt * 0.03;
        bubbles.life[k] = Math.min(0.999, Math.max(0, 1 + y / 2.5));
        if (y >= -0.05) {
          if (Math.random() < 0.25) { const m = ripples[rippleNext]; rippleNext = (rippleNext + 1) % ripples.length; m.position.set(bubbles.positions[k * 3], 0.05, bubbles.positions[k * 3 + 2]); m.userData.t = 0; }
          bubbles.life[k] = -1;
        }
      }
      for (let k = 0; k < wake.max; k++) {
        if (wake.life[k] < 0) { continue; }
        wake.life[k] += dt / 1.8; wake.positions[k * 3 + 1] += dt * 0.4;
        if (wake.life[k] >= 1) { wake.life[k] = -1; }
      }
      ['position', 'life', 'size'].forEach(function (a) { if (bubbles.geo.attributes[a]) { bubbles.geo.attributes[a].needsUpdate = true; } });
      wake.geo.attributes.position.needsUpdate = true; wake.geo.attributes.life.needsUpdate = true;
      ripples.forEach(function (m) {
        if (m.userData.t < 0) { return; }
        m.userData.t += dt;
        const t = m.userData.t / 1.6;
        m.scale.setScalar(1 + t * 4); m.material.opacity = Math.max(0, 0.6 * (1 - t));
        if (t >= 1) { m.userData.t = -1; m.material.opacity = 0; }
      });
    }

    /** Etiketten auf den Bildschirm projizieren, verdeckte ausblenden. */
    function placeLabels() {
      const w = canvas.clientWidth, h = canvas.clientHeight;
      const placed = [];
      labelItems
        .map(function (item) { v3.copy(item.world).project(camera); return { item: item, x: (v3.x + 1) / 2 * w, y: (1 - v3.y) / 2 * h, z: v3.z }; })
        .sort(function (a, b) { return a.z - b.z; })
        .forEach(function (l) {
          const el = l.item.el;
          el.style.left = l.x + 'px';
          el.style.top = l.y + 'px';
          const inView = l.z < 1 && l.x > -40 && l.x < w + 40 && l.y > 0 && l.y < h + 40;
          if (!l.item.width) { l.item.width = el.offsetWidth; l.item.height = el.offsetHeight; }
          const box = { left: l.x - l.item.width / 2, right: l.x + l.item.width / 2, top: l.y - l.item.height, bottom: l.y };
          const covered = placed.some(function (b) { return box.left < b.right && box.right > b.left && box.top < b.bottom && box.bottom > b.top; });
          el.style.opacity = inView && !covered ? '1' : '0';
          if (inView && !covered) { placed.push(box); }
        });
    }
  }

  async function open(stage, state) {
    const status = stage.querySelector('[data-3d-status]');
    stage.dataset.mode = '3d';
    if (state.built) {
      state.resume();
      return;
    }
    state.built = true;
    status.textContent = JSON.parse(stage.dataset.texts).loading;
    status.hidden = false;
    try {
      const origin = { e: parseFloat(stage.dataset.e), n: parseFloat(stage.dataset.n) };
      const size = (parseInt(stage.dataset.points, 10) - 1) * parseFloat(stage.dataset.cell);
      const results = await Promise.all([loadLibraries(stage), loadTerrain(stage), loadAerial(origin, size, 768)]);
      if (stage.__site3d !== state) {
        return;
      }
      const view = build(stage, results[1], results[2]);
      state.dispose = view.dispose;
      state.resume = view.resume;
      status.hidden = true;
    } catch (e) {
      state.built = false;
      status.textContent = JSON.parse(stage.dataset.texts).failed;
      console.error('3D view failed', e);
    }
  }

  // Der Zustand hängt am Element. Nach Enhanced Navigation (auch auf denselben Platz: Tag, Sprache) baut die Ansicht neu auf,
  // weil Blazor Etiketten, HUD und Profil an das HTML des Servers angleicht. Bibliothek und Raster kommen dann aus dem Cache.
  function init() {
    document.querySelectorAll('[data-3d-stage]').forEach(function (stage) {
      if (stage.__site3d) {
        stage.__site3d.dispose();
      }
      const state = { built: false, webGl: webGlAvailable(), dispose: function () {}, resume: function () {} };
      stage.__site3d = state;
      stage.dataset.mode = 'map';
      if (!state.webGl) {
        return;
      }
      const sticker = stage.querySelector('[data-3d-open]');
      const back = stage.querySelector('[data-3d-close]');
      sticker.hidden = false;
      sticker.onclick = function () { open(stage, state); };
      back.onclick = function () { stage.dataset.mode = 'map'; sticker.focus(); };
    });
  }

  document.addEventListener('DOMContentLoaded', function () {
    init();
    if (window.Blazor) {
      window.Blazor.addEventListener('enhancedload', init);
    }
  });
})();
