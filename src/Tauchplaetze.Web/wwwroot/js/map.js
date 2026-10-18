// Karte mit Landeskarte von swisstopo (data-sources.md, swisstopo). Liest die Plätze aus data-sites.
(function () {
  const tiles = 'https://wmts.geo.admin.ch/1.0.0/ch.swisstopo.pixelkarte-farbe/default/current/3857/{z}/{x}/{y}.jpeg';
  const colors = { high: '#7FE3D6', mid: '#4FB3A9', low: '#5E9C96', none: '#8FAAA6' };

  function escapeHtml(text) {
    const span = document.createElement('span');
    span.textContent = text;
    return span.innerHTML;
  }

  function initMaps() {
    document.querySelectorAll('[data-map]:not([data-map-ready])').forEach(function (element) {
      element.dataset.mapReady = 'true';
      const sites = JSON.parse(element.dataset.sites || '[]');
      const map = L.map(element, { scrollWheelZoom: false });
      L.tileLayer(tiles, { attribution: '© swisstopo', maxZoom: 18 }).addTo(map);

      const markers = sites.map(function (site) {
        return L.circleMarker([site.lat, site.lng], {
          radius: 9,
          color: '#071417',
          weight: 2,
          fillColor: colors[site.band] || colors.none,
          fillOpacity: 1
        }).bindPopup('<a href="' + site.url + '">' + escapeHtml(site.name) + '</a>').addTo(map);
      });

      if (markers.length > 0) {
        map.fitBounds(L.featureGroup(markers).getBounds(), { padding: [40, 40], maxZoom: 13 });
      } else {
        map.setView([Number(element.dataset.lat || 46.8), Number(element.dataset.lng || 8.2)], Number(element.dataset.zoom || 8));
      }
    });
  }

  document.addEventListener('DOMContentLoaded', function () {
    initMaps();
    if (window.Blazor) {
      window.Blazor.addEventListener('enhancedload', initMaps);
    }
  });
})();
