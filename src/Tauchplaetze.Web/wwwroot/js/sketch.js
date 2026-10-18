// Tauchskizze (dive-site.md, «Tauchskizze»): zeichnet Symbole, Linien und Flächen im Design der Seite.
// Die öffentliche Karte und der Editor im CMS nutzen dieselbe Darstellung.
window.tauchplaetzeSketch = (function () {
  function token(name, fallback) {
    return getComputedStyle(document.documentElement).getPropertyValue(name).trim() || fallback;
  }

  function escapeHtml(text) {
    const span = document.createElement('span');
    span.textContent = text;
    return span.innerHTML;
  }

  // Symbole als kleines SVG, 24 × 24. Farben über currentColor und Klassen in base.css.
  const symbols = {
    Sculpture: '<path d="M12 3a2.5 2.5 0 1 1 0 5 2.5 2.5 0 0 1 0-5Zm-3 6h6l-1 6h-4Zm-2 7h10v3H7Z"/>',
    TreeTrunk: '<rect x="3" y="9" width="18" height="6" rx="3"/><circle cx="18" cy="12" r="2" class="sketch-cut"/>',
    Wreck: '<path d="M3 11h18l-3 6H6Zm6-5h5v5H9Z"/>',
    Object: '<rect x="5" y="7" width="14" height="10" rx="2"/>',
    Boulder: '<path d="M4 16c0-5 4-9 8-9 5 0 8 4 8 8 0 2-1 3-3 3H6c-1 0-2-1-2-2Z"/>',
    Highlight: '<path d="m12 3 2.6 5.6 6.1.7-4.5 4.2 1.2 6L12 16.6 6.6 19.5l1.2-6L3.3 9.3l6.1-.7Z"/>',
    Danger: '<path d="M12 3 22 20H2Z"/><path d="M12 9v5M12 16.5v.5" class="sketch-cut" stroke-width="2" stroke-linecap="round"/>'
  };

  const lineStyles = {
    Wall: function () { return { color: token('--color-sketch-wall', '#3B4B49'), weight: 6, opacity: 0.95, lineCap: 'butt' }; },
    Edge: function () { return { color: token('--color-sketch-wall', '#3B4B49'), weight: 3, dashArray: '8 6', opacity: 0.9 }; },
    Route: function () { return { color: token('--color-primary', '#7FE3D6'), weight: 3, dashArray: '2 8', lineCap: 'round', opacity: 1 }; },
    Line: function () { return { color: token('--color-sketch-line', '#B8740F'), weight: 2, opacity: 1 }; },
    PlantField: function () { return { color: token('--color-sketch-plants', '#2F8F55'), weight: 1.5, fillOpacity: 0.25, opacity: 0.9 }; }
  };

  const shapes = { Wall: 'line', Edge: 'line', Route: 'line', Line: 'line', PlantField: 'area' };

  function shapeOf(kind) {
    return shapes[kind] || 'point';
  }

  function label(feature, kindNames) {
    const name = feature.label || kindNames[feature.kind] || feature.kind;
    const depth = feature.depth != null ? ' <span class="map-label-kind">· ' + feature.depth + ' m</span>' : '';
    return escapeHtml(name) + depth;
  }

  function icon(kind, selected) {
    return L.divIcon({
      className: 'sketch-symbol sketch-' + kind.toLowerCase() + (selected ? ' is-selected' : ''),
      html: '<svg viewBox="0 0 24 24" width="22" height="22" aria-hidden="true">' + (symbols[kind] || symbols.Object) + '</svg>',
      iconSize: [28, 28],
      iconAnchor: [14, 14]
    });
  }

  // Ein Layer pro Element. options.onSelect und options.selectedId nutzt nur der Editor.
  function layerFor(feature, kindNames, options) {
    const latlngs = feature.points.map(function (p) { return [p.lat, p.lng]; });
    const selected = options && options.selectedId === feature.id;
    let layer;

    if (shapeOf(feature.kind) === 'point') {
      layer = L.marker(latlngs[0], { icon: icon(feature.kind, selected), keyboard: false });
    } else if (shapeOf(feature.kind) === 'area') {
      layer = L.polygon(latlngs, Object.assign(lineStyles[feature.kind](), selected ? { weight: 3 } : {}));
    } else {
      layer = L.polyline(latlngs, Object.assign(lineStyles[feature.kind](), selected ? { weight: 7 } : {}));
    }

    if (feature.label || feature.depth != null || shapeOf(feature.kind) === 'point') {
      layer.bindTooltip(label(feature, kindNames), {
        permanent: true,
        direction: shapeOf(feature.kind) === 'point' ? 'right' : 'center',
        offset: shapeOf(feature.kind) === 'point' ? [12, 0] : [0, 0],
        className: 'map-label sketch-label'
      });
    }

    if (options && options.onSelect) {
      layer.on('click', function (event) {
        L.DomEvent.stopPropagation(event);
        options.onSelect(feature.id);
      });
    }

    return layer;
  }

  // Öffentliche Karte: Skizze als Overlay «Skizze», standardmässig an.
  function addTo(map, element, control) {
    const features = JSON.parse(element.dataset.sketch || '[]');
    if (features.length === 0) {
      return null;
    }

    const kindNames = JSON.parse(element.dataset.sketchKinds || '{}');
    const group = L.featureGroup(features.map(function (f) { return layerFor(f, kindNames); })).addTo(map);
    if (control) {
      control.addOverlay(group, element.dataset.layerSketch || 'Skizze');
    }

    return group;
  }

  return { addTo: addTo, layerFor: layerFor, shapeOf: shapeOf };
})();
