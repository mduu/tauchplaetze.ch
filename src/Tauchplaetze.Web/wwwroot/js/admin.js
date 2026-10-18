// Karte im CMS: Klick setzt die Koordinate (cms.md, «Platz bearbeiten»). Blazor ruft die Funktionen über JS-Interop auf.
window.tauchplaetzeAdmin = (function () {
  const maps = new Map();

  function show(id, latitude, longitude, dotnet) {
    const element = document.getElementById(id);
    if (!element) {
      return;
    }

    let entry = maps.get(id);
    if (!entry) {
      const map = L.map(element, { scrollWheelZoom: true });
      window.tauchplaetzeMap.addBaseLayers(map, element);
      const marker = L.circleMarker([latitude, longitude], { radius: 9, color: '#071417', weight: 2, fillColor: '#FF9A4D', fillOpacity: 1 }).addTo(map);
      map.on('click', function (event) {
        marker.setLatLng(event.latlng);
        dotnet.invokeMethodAsync('SetLocation', event.latlng.lat, event.latlng.lng);
      });
      entry = { map: map, marker: marker };
      maps.set(id, entry);
      map.setView([latitude, longitude], 15);
    } else {
      entry.marker.setLatLng([latitude, longitude]);
    }

    // Die Karte entsteht oft in einem Tab, der seine Grösse erst nach dem Rendern kennt.
    setTimeout(function () { entry.map.invalidateSize(); }, 150);
  }

  function dispose(id) {
    const entry = maps.get(id);
    if (entry) {
      entry.map.remove();
      maps.delete(id);
    }
  }

  return { show: show, dispose: dispose };
})();
