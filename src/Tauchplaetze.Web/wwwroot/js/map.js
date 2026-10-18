// Karte mit Landeskarte von swisstopo (data-sources.md, swisstopo). Liest Plätze aus data-sites, Gewässer aus data-waters.
// Blazor ersetzt beim Seitenwechsel (enhanced navigation) den Inhalt der Elemente, nicht die Elemente selbst.
// Darum baut jeder Wechsel alle Karten neu auf.
window.tauchplaetzeMap = (function () {
  const layers = {
    map: 'https://wmts.geo.admin.ch/1.0.0/ch.swisstopo.pixelkarte-farbe/default/current/3857/{z}/{x}/{y}.jpeg',
    photo: 'https://wmts.geo.admin.ch/1.0.0/ch.swisstopo.swissimage/default/current/3857/{z}/{x}/{y}.jpeg'
  };
  const storageKey = 'tauchplaetze.mapLayer';
  const colors = { high: '#7FE3D6', mid: '#4FB3A9', low: '#5E9C96', none: '#8FAAA6' };
  let maps = [];

  function escapeHtml(text) {
    const span = document.createElement('span');
    span.textContent = text;
    return span.innerHTML;
  }

  function siteMarker(site) {
    return L.circleMarker([site.lat, site.lng], {
      radius: 9,
      color: '#071417',
      weight: 2,
      fillColor: colors[site.band] || colors.none,
      fillOpacity: 1
    }).bindPopup('<a href="' + site.url + '">' + escapeHtml(site.name) + '</a>');
  }

  function waterMarker(water) {
    return L.circleMarker([water.lat, water.lng], {
      radius: 6,
      color: '#071417',
      weight: 2,
      fillColor: colors.high,
      fillOpacity: 1
    })
      .bindTooltip('<a href="' + water.url + '">' + escapeHtml(water.name) + '</a>', {
        permanent: true,
        direction: 'right',
        interactive: true,
        className: 'water-label'
      })
      .on('click', function () { window.location.href = water.url; });
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
      .concat(JSON.parse(element.dataset.waters || '[]').map(waterMarker));
    markers.forEach(function (marker) { marker.addTo(map); });

    if (markers.length > 0) {
      map.fitBounds(L.featureGroup(markers).getBounds(), { padding: [40, 40], maxZoom: Number(element.dataset.maxZoom || 13) });
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
