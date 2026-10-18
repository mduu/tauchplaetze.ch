// Karte mit Landeskarte von swisstopo (data-sources.md, swisstopo). Liest Plätze aus data-sites, Gewässer aus data-waters,
// den eigenen Platz einer Platzseite aus data-here und seine Einstiege aus data-points.
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
    L.control.layers(labels, null, { collapsed: false, position: 'topright' }).addTo(map);
    map.on('baselayerchange', function (event) {
      storeLayer(event.layer === base.photo ? 'photo' : 'map');
    });
  }

  function create(element) {
    const map = L.map(element, { scrollWheelZoom: false });
    addBaseLayers(map, element);

    const markers = JSON.parse(element.dataset.sites || '[]').map(siteMarker)
      .concat(JSON.parse(element.dataset.waters || '[]').map(waterMarker))
      .concat(element.dataset.here ? [hereMarker(JSON.parse(element.dataset.here))] : [])
      .concat(JSON.parse(element.dataset.points || '[]').map(entryMarker));
    markers.forEach(function (marker) { marker.addTo(map); });

    map.on('zoomend', function () { toggleLabels(map, markers.length); });
    if (markers.length > 0) {
      map.fitBounds(L.featureGroup(markers).getBounds(), { padding: [48, 48], maxZoom: Number(element.dataset.maxZoom || 13) });
    } else {
      map.setView([Number(element.dataset.lat || 46.8), Number(element.dataset.lng || 8.2)], Number(element.dataset.zoom || 8));
    }

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

  document.addEventListener('DOMContentLoaded', function () {
    initMaps();
    if (window.Blazor) {
      window.Blazor.addEventListener('enhancedload', initMaps);
    }
  });

  return { addBaseLayers: addBaseLayers };
})();
