// Karte mit Landeskarte von swisstopo (data-sources.md, swisstopo). Liest Plätze aus data-sites, Gewässer aus data-waters,
// den eigenen Platz einer Platzseite aus data-here und seine Einstiege aus data-points. Mit data-contours zeichnet sie Tiefenlinien.
// Blazor ersetzt beim Seitenwechsel (enhanced navigation) den Inhalt der Elemente, nicht die Elemente selbst.
// Darum baut jeder Wechsel alle Karten neu auf.
window.tauchplaetzeMap = (function () {
  const layers = {
    map: 'https://wmts.geo.admin.ch/1.0.0/ch.swisstopo.pixelkarte-farbe/default/current/3857/{z}/{x}/{y}.jpeg',
    photo: 'https://wmts.geo.admin.ch/1.0.0/ch.swisstopo.swissimage/default/current/3857/{z}/{x}/{y}.jpeg'
  };
  const storageKey = 'tauchplaetze.mapLayer';
  const labelLimit = 10;
  const labelZoom = 11;
  let maps = [];

  function escapeHtml(text) {
    const span = document.createElement('span');
    span.textContent = text;
    return span.innerHTML;
  }

  // Farben aus den Design Tokens (design-system.md), damit tokens.css die einzige Quelle bleibt.
  function token(name) {
    return getComputedStyle(document.documentElement).getPropertyValue(name).trim();
  }

  function bandColor(band) {
    return token({ high: '--color-index-high', mid: '--color-index-mid', low: '--color-index-low' }[band] || '--color-text-subtle');
  }

  // Dauerhafte Beschriftung wie ein Tooltip mit Pfeil auf den Punkt. Mit url öffnet ein Klick die Seite.
  // Plätze und Gewässer oben beschriftet, Einstiege und Orte rechts, damit sie den Platz nicht verdecken.
  function labelled(marker, item, html, side) {
    marker.bindTooltip(item.url ? '<a href="' + item.url + '">' + html + '</a>' : html, {
      permanent: true,
      direction: side ? 'right' : 'top',
      offset: side ? [side, 0] : [0, -8],
      interactive: !!item.url,
      className: 'map-label'
    });
    return item.url ? marker.on('click', function () { window.location.href = item.url; }) : marker;
  }

  function siteMarker(site) {
    const index = site.index ? ' <span class="map-label-index map-label-' + (site.band || 'none') + '">' + escapeHtml(site.index) + '</span>' : '';
    const marker = L.circleMarker([site.lat, site.lng], {
      radius: 8,
      color: token('--color-bg'),
      weight: 2,
      fillColor: bandColor(site.band),
      fillOpacity: 1
    });
    return labelled(marker, site, escapeHtml(site.name) + index);
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
    return labelled(marker, site, escapeHtml(site.name));
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
      return labelled(L.marker([point.lat, point.lng], { icon: icon, keyboard: false }), point, label, 12);
    }

    const marker = L.circleMarker([point.lat, point.lng], {
      radius: 6,
      color: token('--color-bg'),
      weight: 2,
      fillColor: token('--color-text'),
      fillOpacity: 1
    });
    return labelled(marker, point, label, 8);
  }

  // Bei vielen Punkten erscheinen die Beschriftungen erst ab Zoom 11, sonst überdecken sie sich.
  function toggleLabels(map, count) {
    map.getContainer().classList.toggle('map-labels-hidden', count > labelLimit && map.getZoom() < labelZoom);
  }

  function storedLayer() {
    try {
      return localStorage.getItem(storageKey) === 'photo' ? 'photo' : 'map';
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

  // Landeskarte und Luftbild von swisstopo mit Umschalter. Die Beschriftung kommt aus data-layer-map und data-layer-photo.
  function addBaseLayers(map, element) {
    const base = {
      map: L.tileLayer(layers.map, { attribution: '© swisstopo', maxZoom: 18 }),
      photo: L.tileLayer(layers.photo, { attribution: '© swisstopo', maxZoom: 19 })
    };
    const labels = {};
    labels[element.dataset.layerMap || 'Karte'] = base.map;
    labels[element.dataset.layerPhoto || 'Luftbild'] = base.photo;

    base[storedLayer()].addTo(map);
    map.tauchplaetzeLayers = L.control.layers(labels, null, { collapsed: false, position: 'topright' }).addTo(map);
    map.on('baselayerchange', function (event) {
      storeLayer(event.layer === base.photo ? 'photo' : 'map');
    });
  }

  // Tiefenlinien aus swissBATHY3D als GeoJSON-Kacheln von 1 km (tools/bathymetry/contours.cs, data-sources.md).
  // Erst ab Zoom 13 geladen, Beschriftung der Tiefe ab Zoom 15. Kacheln bleiben im Cache, solange die Seite offen ist.
  const contourMinZoom = 13;
  const contourLabelZoom = 15;
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
    const shown = {};
    const color = token('--color-depth-line');

    function style(feature) {
      return { color: color, weight: feature.properties.depth % 10 === 0 ? 1.6 : 0.9, opacity: 0.9, interactive: false };
    }

    function label(depth, point) {
      return L.marker([point[1], point[0]], {
        icon: L.divIcon({ className: 'map-depth-label', html: '<span>' + depth + ' m</span>', iconSize: null }),
        interactive: false,
        keyboard: false
      });
    }

    function draw(data) {
      lines.addLayer(L.geoJSON(data, { style: style, interactive: false }));
      data.features.forEach(function (feature) {
        feature.geometry.coordinates
          .filter(function (line) { return line.length >= 6; })
          .forEach(function (line) { labels.addLayer(label(feature.properties.depth, middleOf(line))); });
      });
    }

    function refresh() {
      const showLabels = map.hasLayer(lines) && map.getZoom() >= contourLabelZoom;
      if (showLabels && !map.hasLayer(labels)) {
        labels.addTo(map);
      } else if (!showLabels && map.hasLayer(labels)) {
        map.removeLayer(labels);
      }

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

    map.on('zoomend', function () { toggleLabels(map, markers.length); });
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
