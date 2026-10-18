// Karte mit Landeskarte von swisstopo (data-sources.md, swisstopo). Liest Plätze aus data-sites, Gewässer aus data-waters.
// Blazor ersetzt beim Seitenwechsel (enhanced navigation) den Inhalt der Elemente, nicht die Elemente selbst.
// Darum baut jeder Wechsel alle Karten neu auf.
(function () {
  const tiles = 'https://wmts.geo.admin.ch/1.0.0/ch.swisstopo.pixelkarte-farbe/default/current/3857/{z}/{x}/{y}.jpeg';
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

  function create(element) {
    const map = L.map(element, { scrollWheelZoom: false });
    L.tileLayer(tiles, { attribution: '© swisstopo', maxZoom: 18 }).addTo(map);

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
})();
