// Karte mit Landeskarte von swisstopo (data-sources.md, swisstopo). Liest Plätze aus data-sites, Gewässer aus data-waters,
// den eigenen Platz einer Platzseite aus data-here und seine Einstiege aus data-points. Mit data-contours zeichnet sie Tiefenlinien.
// Blazor ersetzt beim Seitenwechsel (enhanced navigation) den Inhalt der Elemente, nicht die Elemente selbst.
// Darum baut jeder Wechsel alle Karten neu auf.
window.tauchplaetzeMap = (function () {
  const layers = {
    map: 'https://wmts.geo.admin.ch/1.0.0/ch.swisstopo.pixelkarte-farbe/default/current/3857/{z}/{x}/{y}.jpeg',
    grey: 'https://wmts.geo.admin.ch/1.0.0/ch.swisstopo.pixelkarte-grau/default/current/3857/{z}/{x}/{y}.jpeg',
    photo: 'https://wmts.geo.admin.ch/1.0.0/ch.swisstopo.swissimage/default/current/3857/{z}/{x}/{y}.jpeg'
  };
  const storageKey = 'tauchplaetze.mapLayer';
  // Namen der Plätze erst ab Zoom 13 dauerhaft, darunter nur Punkte mit Name beim Antippen oder Darüberfahren.
  const siteLabelZoom = 13;
  let maps = [];

  // Auch für Attribute: innerHTML maskiert keine Anführungszeichen.
  function escapeHtml(text) {
    const span = document.createElement('span');
    span.textContent = text;
    return span.innerHTML.replace(/"/g, '&quot;').replace(/'/g, '&#39;');
  }

  // Farben aus den Design Tokens (design-system.md), damit tokens.css die einzige Quelle bleibt.
  function token(name) {
    return getComputedStyle(document.documentElement).getPropertyValue(name).trim();
  }

  function bandColor(band) {
    return token({ high: '--color-index-high', mid: '--color-index-mid', low: '--color-index-low' }[band] || '--color-text-subtle');
  }

  // Beschriftung wie ein Tooltip mit Pfeil auf den Punkt. Plätze und Gewässer oben beschriftet, Einstiege und Orte rechts,
  // damit sie den Platz nicht verdecken. priority entscheidet, welche Beschriftung bei Überdeckung bleibt.
  // Mit minZoom ist sie erst ab diesem Zoom dauerhaft, darunter erscheint sie beim Antippen oder Darüberfahren.
  // Ein Klick auf den Punkt öffnet die Seite, wenn die Beschriftung sichtbar ist, sonst zuerst die Beschriftung.
  function labelled(marker, item, html, side, priority, minZoom) {
    const content = item.url ? '<a href="' + escapeHtml(item.url) + '">' + html + '</a>' : html;
    marker.options.labelPriority = priority || 0;
    marker.options.labelMinZoom = minZoom || 0;

    function bind(permanent) {
      marker.unbindTooltip();
      marker.bindTooltip(content, {
        permanent: permanent,
        direction: side ? 'right' : 'top',
        offset: side ? [side, 0] : [0, -8],
        interactive: !!item.url,
        className: 'map-label'
      });
      marker.labelPermanent = permanent;
      if (permanent && marker._map) {
        marker.openTooltip();
      }
    }

    marker.updateLabel = function (zoom) {
      const permanent = zoom >= marker.options.labelMinZoom;
      if (marker.labelPermanent !== permanent) {
        bind(permanent);
      }
    };
    bind(!minZoom);

    return item.url ? marker.on('click', function () {
      const element = marker.getTooltip() && marker.getTooltip().getElement();
      const visible = marker.labelPermanent && element && !element.classList.contains('map-label-covered');
      if (visible) {
        window.location.href = item.url;
      } else {
        marker.openTooltip();
      }
    }) : marker;
  }

  function overlaps(a, b) {
    return a.left < b.right + 2 && b.left < a.right + 2 && a.top < b.bottom + 2 && b.top < a.bottom + 2;
  }

  // Blendet Beschriftungen aus, die eine wichtigere überdecken würden: zuerst der eigene Platz, dann der bessere Index,
  // die Tiefen zuletzt. Läuft nach jeder Bewegung, sobald der Browser gezeichnet hat.
  function declutter(map) {
    const labels = [];
    map.eachLayer(function (layer) {
      const tooltip = layer.getTooltip && layer.getTooltip();
      const element = tooltip && tooltip.getElement && tooltip.getElement();
      if (element && layer.labelPermanent !== false && tooltip.options.permanent && element.classList.contains('map-label')) {
        labels.push({ element: element, priority: layer.options.labelPriority || 0 });
      } else if (layer.options && layer.options.depthLabel && layer.getElement && layer.getElement()) {
        labels.push({ element: layer.getElement().firstChild, priority: -1 });
      }
    });

    labels.forEach(function (label) { label.element.classList.remove('map-label-covered'); });
    labels.sort(function (a, b) { return b.priority - a.priority; });
    const kept = [];
    labels.forEach(function (label) {
      const box = label.element.getBoundingClientRect();
      if (box.width === 0) {
        return;
      }

      if (kept.some(function (other) { return overlaps(box, other); })) {
        label.element.classList.add('map-label-covered');
      } else {
        kept.push(box);
      }
    });
  }

  function watchLabels(map) {
    let pending = false;
    function schedule() {
      if (pending) {
        return;
      }

      pending = true;
      requestAnimationFrame(function () {
        pending = false;
        declutter(map);
      });
    }

    map.on('zoomend', function () {
      const zoom = map.getZoom();
      const markers = [];
      map.eachLayer(function (layer) {
        if (layer.updateLabel) {
          markers.push(layer);
        }
      });
      markers.forEach(function (marker) { marker.updateLabel(zoom); });
    });
    map.on('layeradd', function (event) {
      if (event.layer.updateLabel) {
        event.layer.updateLabel(map.getZoom());
      }
    });
    map.on('zoomend moveend layeradd tooltipclose', schedule);
    map.tauchplaetzeDeclutter = schedule;
  }

  function siteMarker(site) {
    const band = ['high', 'mid', 'low'].includes(site.band) ? site.band : 'none';
    const index = site.index ? ' <span class="map-label-index map-label-' + band + '">' + escapeHtml(site.index) + '</span>' : '';
    const marker = L.circleMarker([site.lat, site.lng], {
      radius: 8,
      color: token('--color-bg'),
      weight: 2,
      fillColor: bandColor(site.band),
      fillOpacity: 1
    });
    return labelled(marker, site, escapeHtml(site.name) + index, 0, site.index ? parseFloat(site.index) : 0, siteLabelZoom);
  }

  function waterMarker(water) {
    const marker = L.circleMarker([water.lat, water.lng], {
      radius: 6,
      color: token('--color-bg'),
      weight: 2,
      fillColor: token('--color-primary'),
      fillOpacity: 1
    });
    return labelled(marker, water, escapeHtml(water.name));
  }

  function hereMarker(site) {
    const marker = L.circleMarker([site.lat, site.lng], {
      radius: 10,
      color: token('--color-bg'),
      weight: 3,
      fillColor: token('--color-primary'),
      fillOpacity: 1
    });
    return labelled(marker, site, escapeHtml(site.name), 0, 1000);
  }

  // Orte an Land (Parkplatz, Toilette) als Schild mit Kürzel, Einstiege als Punkt.
  const placeSigns = { parking: 'P', toilet: 'WC' };

  function entryMarker(point) {
    const name = point.name ? escapeHtml(point.name) + ' ' : '';
    const label = name + '<span class="map-label-kind">' + escapeHtml(point.kind) + '</span>';
    if (placeSigns[point.type]) {
      const icon = L.divIcon({
        className: 'map-place map-place-' + point.type,
        html: '<span>' + placeSigns[point.type] + '</span>',
        iconSize: [24, 24],
        iconAnchor: [12, 12]
      });
      return labelled(L.marker([point.lat, point.lng], { icon: icon, keyboard: false }), point, label, 12, 50);
    }

    const marker = L.circleMarker([point.lat, point.lng], {
      radius: 6,
      color: token('--color-bg'),
      weight: 2,
      fillColor: token('--color-text'),
      fillOpacity: 1
    });
    return labelled(marker, point, label, 8, 50);
  }

  // Standard ist die Landeskarte. Wer auf die graue Karte oder das Luftbild umschaltet, behält die Wahl.
  function storedLayer() {
    try {
      const stored = localStorage.getItem(storageKey);
      return stored === 'photo' || stored === 'grey' ? stored : 'map';
    } catch (e) {
      return 'map';
    }
  }

  function storeLayer(name) {
    try {
      localStorage.setItem(storageKey, name);
    } catch (e) {
      // Ohne Storage gilt beim nächsten Laden wieder die Landeskarte.
    }
  }

  // Das Luftbild deckt nur die Schweiz mit Rand ab, ausserhalb liefert swisstopo weisse Kacheln.
  // Mit dem Luftbild lässt sich darum nur so weit herauszoomen, dass der Ausschnitt in der Abdeckung bleibt.
  const swissBounds = L.latLngBounds([[45.15, 5.0], [48.85, 11.2]]);
  const swissMinZoom = 7;

  function limitZoom(map, photo) {
    const size = map.getSize();
    const inside = photo && size.x > 0 && size.y > 0 ? map.getBoundsZoom(swissBounds, true) : swissMinZoom;
    map.setMinZoom(Math.max(swissMinZoom, inside));
  }

  // Landeskarte, graue Landeskarte und Luftbild von swisstopo mit Umschalter.
  // Die Beschriftung kommt aus data-layer-map, data-layer-grey und data-layer-photo.
  function addBaseLayers(map, element) {
    const base = {
      map: L.tileLayer(layers.map, { attribution: '© swisstopo', maxZoom: 18 }),
      grey: L.tileLayer(layers.grey, { attribution: '© swisstopo', maxZoom: 18 }),
      photo: L.tileLayer(layers.photo, { attribution: '© swisstopo', maxZoom: 19 })
    };
    const labels = {};
    labels[element.dataset.layerMap || 'Karte'] = base.map;
    labels[element.dataset.layerGrey || 'Karte grau'] = base.grey;
    labels[element.dataset.layerPhoto || 'Luftbild'] = base.photo;

    map.setMinZoom(swissMinZoom);
    map.setMaxBounds(swissBounds);
    map.options.maxBoundsViscosity = 1;
    base[storedLayer()].addTo(map);
    // Die Grösse der Karte steht beim ersten Laden oft noch nicht fest. Darum bei jeder Grössenänderung und nach jeder Bewegung neu prüfen.
    map.on('resize moveend', function () {
      limitZoom(map, map.hasLayer(base.photo));
    });
    if (window.ResizeObserver) {
      const observer = new ResizeObserver(function () { map.invalidateSize(); });
      observer.observe(map.getContainer());
      map.on('unload', function () { observer.disconnect(); });
    }
    map.tauchplaetzeLayers = L.control.layers(labels, null, { collapsed: false, position: 'topright' }).addTo(map);
    map.on('baselayerchange', function (event) {
      storeLayer(Object.keys(base).find(function (name) { return base[name] === event.layer; }) || 'map');
      limitZoom(map, event.layer === base.photo);
    });
    watchLabels(map);
  }

  // Tiefenlinien aus swissBATHY3D als GeoJSON-Kacheln von 1 km (tools/bathymetry/contours.cs, data-sources.md).
  // Erst ab Zoom 13 geladen, Beschriftung der Tiefe ab Zoom 15, bis Zoom 16 nur jede zweite Linie (10, 20, 30 m).
  // Kacheln bleiben im Cache, solange die Seite offen ist.
  const contourMinZoom = 13;
  const contourLabelZoom = 15;
  const contourAllLabelsZoom = 17;
  const contourTiles = {};
  let contourIndex = null;

  function loadJson(url) {
    if (!contourTiles[url]) {
      contourTiles[url] = fetch(url)
        .then(function (response) { return response.ok ? response.json() : null; })
        .catch(function () { return null; });
    }

    return contourTiles[url];
  }

  function boundsOf(box) {
    return L.latLngBounds([box[1], box[0]], [box[3], box[2]]);
  }

  function middleOf(line) {
    return line[Math.floor(line.length / 2)];
  }

  function addContours(map, element) {
    const lake = element.dataset.contours;
    if (!lake) {
      return null;
    }

    const base = element.dataset.contoursBase || '/bathymetry/';
    const lines = L.layerGroup();
    const labels = L.layerGroup();
    const minorLabels = L.layerGroup();
    const shown = {};
    const color = token('--color-depth-line');

    function style(feature) {
      return { color: color, weight: feature.properties.depth % 10 === 0 ? 1.6 : 0.9, opacity: 0.9, interactive: false };
    }

    function label(depth, point) {
      return L.marker([point[1], point[0]], {
        icon: L.divIcon({ className: 'map-depth-label', html: '<span>' + depth + ' m</span>', iconSize: null }),
        interactive: false,
        keyboard: false,
        depthLabel: true
      });
    }

    function draw(data) {
      lines.addLayer(L.geoJSON(data, { style: style, interactive: false }));
      data.features.forEach(function (feature) {
        feature.geometry.coordinates
          .filter(function (line) { return line.length >= 6; })
          .forEach(function (line) {
            const group = feature.properties.depth % 10 === 0 ? labels : minorLabels;
            group.addLayer(label(feature.properties.depth, middleOf(line)));
          });
      });
    }

    function show(group, visible) {
      if (visible && !map.hasLayer(group)) {
        group.addTo(map);
      } else if (!visible && map.hasLayer(group)) {
        map.removeLayer(group);
      }
    }

    function refresh() {
      show(labels, map.hasLayer(lines) && map.getZoom() >= contourLabelZoom);
      show(minorLabels, map.hasLayer(lines) && map.getZoom() >= contourAllLabelsZoom);

      if (!map.hasLayer(lines) || map.getZoom() < contourMinZoom) {
        return;
      }

      contourIndex = contourIndex || loadJson(base + 'index.json');
      contourIndex.then(function (index) {
        const view = map.getBounds().pad(0.3);
        Object.keys((index && index.lakes) || {}).forEach(function (slug) {
          const entry = index.lakes[slug];
          if ((lake !== 'auto' && lake !== slug) || !view.intersects(boundsOf(entry.bbox))) {
            return;
          }

          Object.keys(entry.tiles).forEach(function (name) {
            const key = slug + '/' + name;
            if (shown[key] || !view.intersects(boundsOf(entry.tiles[name]))) {
              return;
            }

            shown[key] = true;
            loadJson(base + key + '.json').then(function (data) {
              if (data) {
                draw(data);
              }
            });
          });
        });
      });
    }

    const attribution = element.dataset.contoursAttribution || 'swissBATHY3D © swisstopo';
    map.on('overlayadd', function (event) {
      if (event.layer === lines) {
        map.attributionControl.addAttribution(attribution);
        refresh();
      }
    });
    map.on('overlayremove', function (event) {
      if (event.layer === lines) {
        map.attributionControl.removeAttribution(attribution);
        map.removeLayer(labels);
        map.removeLayer(minorLabels);
      }
    });
    map.on('moveend', refresh);

    lines.addTo(map);
    map.attributionControl.addAttribution(attribution);
    if (map.tauchplaetzeLayers) {
      map.tauchplaetzeLayers.addOverlay(lines, element.dataset.layerContours || 'Tiefenlinien');
    }

    map.whenReady(refresh);
    return lines;
  }

  function create(element) {
    const map = L.map(element, { scrollWheelZoom: false });
    addBaseLayers(map, element);
    addContours(map, element);

    const markers = JSON.parse(element.dataset.sites || '[]').map(siteMarker)
      .concat(JSON.parse(element.dataset.waters || '[]').map(waterMarker))
      .concat(element.dataset.here ? [hereMarker(JSON.parse(element.dataset.here))] : [])
      .concat(JSON.parse(element.dataset.points || '[]').map(entryMarker));
    if (element.dataset.cluster && L.markerClusterGroup) {
      const cluster = L.markerClusterGroup({
        showCoverageOnHover: false,
        maxClusterRadius: 48,
        iconCreateFunction: function (group) {
          return L.divIcon({ className: 'map-cluster', html: '<span>' + group.getChildCount() + '</span>', iconSize: [36, 36] });
        }
      });
      markers.forEach(function (marker) { cluster.addLayer(marker); });
      cluster.addTo(map);
    } else {
      markers.forEach(function (marker) { marker.addTo(map); });
    }

    const sketch = window.tauchplaetzeSketch ? window.tauchplaetzeSketch.addTo(map, element, map.tauchplaetzeLayers) : null;
    if (sketch) {
      markers.push(sketch);
    }

    const padding = Number(element.dataset.padding || 48);
    map.tauchplaetzeFit = function () {
      if (markers.length === 1 && markers[0].getLatLng && element.dataset.singleZoom) {
        map.setView(markers[0].getLatLng(), Number(element.dataset.singleZoom));
      } else if (markers.length > 0) {
        map.fitBounds(L.featureGroup(markers).getBounds(), { padding: [padding, padding], maxZoom: Number(element.dataset.maxZoom || 13) });
      } else {
        map.setView([Number(element.dataset.lat || 46.8), Number(element.dataset.lng || 8.2)], Number(element.dataset.zoom || 8));
      }
    };
    map.tauchplaetzeFit();

    return map;
  }

  function initMaps() {
    maps.forEach(function (map) {
      try {
        map.remove();
      } catch (e) {
        console.warn('Karte liess sich nicht entfernen', e);
      }
    });
    maps = Array.from(document.querySelectorAll('[data-map]')).map(create);
  }

  // Umschalter Liste/Karte im Verzeichnis (schmal): Eine Karte, die versteckt war, kennt ihre Grösse erst nach dem Umschalten.
  document.addEventListener('change', function (event) {
    if (event.target.matches && event.target.matches('[data-map-view]')) {
      maps.forEach(function (map) {
        map.invalidateSize();
        map.tauchplaetzeFit();
      });
    }
  });

  document.addEventListener('DOMContentLoaded', function () {
    initMaps();
    if (window.Blazor) {
      window.Blazor.addEventListener('enhancedload', initMaps);
    }
  });

  return { addBaseLayers: addBaseLayers, addContours: addContours };
})();
