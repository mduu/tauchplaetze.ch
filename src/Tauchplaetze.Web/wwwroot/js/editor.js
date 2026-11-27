// Editoren auf Karten für alle interaktiven Seiten (Moderation und Bearbeiten-Modus), ohne MudBlazor.
// Karte für eine Koordinate: Klick setzt sie (cms.md, «Platz bearbeiten»). Blazor ruft die Funktionen über JS-Interop auf.
window.tauchplaetzeAdmin = (function () {
  const maps = new Map();

  // Ohne Koordinate (null) zeigt die Karte die Schweiz ohne Punkt, bis jemand klickt.
  // pan: die Koordinate kam aus den Feldern, die Karte folgt ihr.
  function show(id, latitude, longitude, dotnet, pan) {
    const element = document.getElementById(id);
    if (!element) {
      return;
    }

    const located = latitude != null && longitude != null;
    let entry = maps.get(id);
    if (!entry) {
      const map = L.map(element, { scrollWheelZoom: true });
      window.tauchplaetzeMap.addBaseLayers(map, element);
      window.tauchplaetzeMap.addContours(map, element);
      entry = { map: map, marker: null };
      maps.set(id, entry);
      map.on('click', function (event) {
        place(entry, event.latlng);
        dotnet.invokeMethodAsync('SetLocation', event.latlng.lat, event.latlng.lng);
      });
      map.setView(located ? [latitude, longitude] : [46.8, 8.2], located ? 15 : 8);
    } else if (located && pan) {
      entry.map.panTo([latitude, longitude]);
    }

    if (located) {
      place(entry, [latitude, longitude]);
    }

    // Die Karte entsteht oft in einem Tab, der seine Grösse erst nach dem Rendern kennt.
    setTimeout(function () { entry.map.invalidateSize(); }, 150);
  }

  function place(entry, latlng) {
    if (entry.marker) {
      entry.marker.setLatLng(latlng);
      return;
    }

    const style = getComputedStyle(document.documentElement);
    entry.marker = L.circleMarker(latlng, { radius: 9, color: style.getPropertyValue('--color-bg').trim(), weight: 2, fillColor: style.getPropertyValue('--color-signal').trim(), fillOpacity: 1 }).addTo(entry.map);
  }

  function dispose(id) {
    const entry = maps.get(id);
    if (entry) {
      entry.map.remove();
      maps.delete(id);
    }
  }

  // Karte zur Prüfung eines Eintrags des SUSV (Story 002): Punkt des SUSV, Umkreis der Vorschläge und die Plätze.
  // Gewählte Plätze leuchten. Ein Klick auf einen Platz wählt ihn an oder ab.
  function showReview(id, latitude, longitude, radius, pointLabel, sites, dotnet) {
    const element = document.getElementById(id);
    if (!element) {
      return;
    }

    let entry = maps.get(id);
    if (entry && entry.map.getContainer() !== element) {
      dispose(id);
      entry = null;
    }

    const style = getComputedStyle(document.documentElement);
    const color = function (name) { return style.getPropertyValue(name).trim(); };
    if (!entry) {
      const map = L.map(element, { scrollWheelZoom: false });
      window.tauchplaetzeMap.addBaseLayers(map, element);
      window.tauchplaetzeMap.addContours(map, element);
      L.circle([latitude, longitude], { radius: radius, color: color('--color-text-subtle'), weight: 1, dashArray: '4 4', fill: false, interactive: false }).addTo(map);
      L.circleMarker([latitude, longitude], { radius: 9, color: color('--color-bg'), weight: 2, fillColor: color('--color-notice-info'), fillOpacity: 1 })
        .bindTooltip(label(pointLabel), { permanent: true, direction: 'top', offset: [0, -10], className: 'map-label' })
        .addTo(map);
      entry = { map: map, sites: L.layerGroup().addTo(map) };
      maps.set(id, entry);

      const points = [[latitude, longitude]].concat(sites.map(function (s) { return [s.lat, s.lng]; }));
      if (points.length > 1) {
        map.fitBounds(L.latLngBounds(points).pad(0.3), { maxZoom: 17 });
      } else {
        map.setView([latitude, longitude], 16);
      }
    }

    entry.sites.clearLayers();
    sites.forEach(function (site) {
      L.circleMarker([site.lat, site.lng], {
        radius: 7,
        color: site.selected ? color('--color-bg') : color('--color-text-subtle'),
        weight: 2,
        fillColor: site.selected ? color('--color-signal') : color('--color-bg'),
        fillOpacity: 1
      })
        .bindTooltip(label(site.name), { permanent: true, direction: 'right', offset: [10, 0], className: 'map-label' })
        .on('click', function () { dotnet.invokeMethodAsync('Toggle', site.id); })
        .addTo(entry.sites);
    });

    setTimeout(function () { entry.map.invalidateSize(); }, 150);
  }

  // Namen kommen von Mitwirkenden und vom SUSV: nur als Text ins DOM, nie als HTML.
  function label(text) {
    const span = document.createElement('span');
    span.textContent = text;
    return span;
  }

  return { show: show, showReview: showReview, dispose: dispose };
})();

// Editor der Tauchskizze und der Einstiege (dive-site.md, «Tauchskizze», «Einstiege und Orte»).
// Blazor hält die Elemente, die Karte zeigt sie und meldet Klicks und Verschiebungen.
window.tauchplaetzeSketchEditor = (function () {
  const editors = new Map();

  // Gibt zurück, ob die Karte steht. Ohne Element (Seite schon weg) false, damit Blazor nicht aufräumen muss.
  function init(id, latitude, longitude, dotnet, kindNames) {
    const element = document.getElementById(id);
    if (!element) {
      return false;
    }

    // MudTabs baut das Panel beim Tab-Wechsel neu. Ein alter Editor mit derselben Id gehört dann zu einem entfernten Element.
    const existing = editors.get(id);
    if (existing && existing.map.getContainer() === element) {
      return true;
    }

    if (existing) {
      dispose(id);
    }

    const map = L.map(element, { scrollWheelZoom: true, doubleClickZoom: false });
    window.tauchplaetzeMap.addBaseLayers(map, element);
    window.tauchplaetzeMap.addContours(map, element);

    L.circleMarker([latitude, longitude], { radius: 5, color: getComputedStyle(document.documentElement).getPropertyValue('--color-text-subtle').trim(), weight: 2, fill: false }).addTo(map);
    map.setView([latitude, longitude], 18);

    const editor = { map: map, dotnet: dotnet, kindNames: kindNames, layers: L.layerGroup().addTo(map), handles: L.layerGroup().addTo(map), entries: L.layerGroup().addTo(map), draft: null, tool: null, features: [], selectedId: null };
    editors.set(id, editor);

    map.on('click', function (event) { onClick(editor, event.latlng); });
    map.on('dblclick', function () { finish(id); });
    setTimeout(function () { map.invalidateSize(); }, 150);
    return true;
  }

  function onClick(editor, latlng) {
    if (!editor.tool) {
      editor.dotnet.invokeMethodAsync('Select', null);
      return;
    }

    const point = { lat: round(latlng.lat), lng: round(latlng.lng) };
    if (editor.tool.shape === 'entry') {
      editor.dotnet.invokeMethodAsync('AddEntry', editor.tool.kind, point.lat, point.lng);
      stopTool(editor);
      return;
    }

    if (editor.tool.shape === 'point') {
      editor.dotnet.invokeMethodAsync('Add', editor.tool.kind, [point]);
      stopTool(editor);
      return;
    }

    editor.draft = editor.draft || [];
    const last = editor.draft[editor.draft.length - 1];
    if (last && last.lat === point.lat && last.lng === point.lng) {
      return;
    }

    editor.draft.push(point);
    drawDraft(editor);
  }

  function stopTool(editor) {
    editor.tool = null;
    editor.map.getContainer().classList.remove('is-drawing');
  }

  // Für die Tastatur: setzt das gewählte Werkzeug in der Mitte der Karte, wie ein Klick dort. Die Karte verschiebt man mit den Pfeiltasten.
  function placeAtCenter(id) {
    const editor = editors.get(id);
    if (editor && editor.tool) {
      onClick(editor, editor.map.getCenter());
    }
  }

  function drawDraft(editor) {
    if (editor.draftLayer) {
      editor.map.removeLayer(editor.draftLayer);
    }

    const latlngs = editor.draft.map(function (p) { return [p.lat, p.lng]; });
    editor.draftLayer = L.polyline(latlngs, { color: getComputedStyle(document.documentElement).getPropertyValue('--color-signal').trim(), weight: 2, dashArray: '4 4' }).addTo(editor.map);
  }

  function round(value) {
    return Math.round(value * 1e6) / 1e6;
  }

  function setTool(id, kind, shape) {
    const editor = editors.get(id);
    if (!editor) {
      return;
    }

    cancel(id);
    editor.tool = kind ? { kind: kind, shape: shape } : null;
    editor.map.getContainer().classList.toggle('is-drawing', !!kind);
  }

  // Schliesst eine Linie oder Fläche ab, sobald sie genug Punkte hat (Doppelklick oder Knopf «Fertig»).
  function finish(id) {
    const editor = editors.get(id);
    if (!editor || !editor.tool || !editor.draft) {
      return;
    }

    const needed = editor.tool.shape === 'area' ? 3 : 2;
    if (editor.draft.length >= needed) {
      editor.dotnet.invokeMethodAsync('Add', editor.tool.kind, editor.draft);
    }

    setTool(id, null, null);
  }

  function cancel(id) {
    const editor = editors.get(id);
    if (!editor) {
      return;
    }

    editor.draft = null;
    if (editor.draftLayer) {
      editor.map.removeLayer(editor.draftLayer);
      editor.draftLayer = null;
    }
  }

  // Zeichnet alle Elemente neu. Das gewählte bekommt verschiebbare Griffe an jedem Punkt.
  function render(id, features, selectedId) {
    const editor = editors.get(id);
    if (!editor) {
      return;
    }

    editor.features = features;
    editor.selectedId = selectedId;
    editor.layers.clearLayers();
    editor.handles.clearLayers();

    features.forEach(function (feature) {
      window.tauchplaetzeSketch.layerFor(feature, editor.kindNames, {
        selectedId: selectedId,
        onSelect: function (featureId) { editor.dotnet.invokeMethodAsync('Select', featureId); }
      }).addTo(editor.layers);
    });

    const selected = features.find(function (f) { return f.id === selectedId; });
    if (selected) {
      selected.points.forEach(function (p, index) {
        L.marker([p.lat, p.lng], {
          draggable: true,
          icon: L.divIcon({ className: 'sketch-handle', iconSize: [14, 14], iconAnchor: [7, 7] })
        }).on('dragend', function (event) {
          const moved = selected.points.slice();
          const latlng = event.target.getLatLng();
          moved[index] = { lat: round(latlng.lat), lng: round(latlng.lng) };
          editor.dotnet.invokeMethodAsync('Move', selected.id, moved);
        }).addTo(editor.handles);
      });
    }
  }

  // Einstiege und Orte als verschiebbare Punkte. Orte an Land (Parkplatz, Toilette, Flaggenhalter) als Schild wie auf der Platzseite.
  const placeSigns = { parking: 'P', toilet: 'WC', flagholder: '<svg viewBox="-1 -1 26 20" width="26" height="20" aria-hidden="true" style="display:block"><path d="M0 0h12v18H0z" style="fill:#fff"/><path d="M12 0h12l-5 9 5 9H12z" style="fill:var(--color-place-parking)"/><path d="M0 0h24l-5 9 5 9H0z" style="fill:none;stroke:var(--color-bg);stroke-width:1.5;stroke-linejoin:round"/></svg>' };

  // Namen kommen von Mitwirkenden: nur als Text ins DOM, nie als HTML.
  function textElement(text) {
    const span = document.createElement('span');
    span.textContent = text;
    return span;
  }

  function renderEntries(id, entries, selectedId) {
    const editor = editors.get(id);
    if (!editor) {
      return;
    }

    editor.entries.clearLayers();
    entries.forEach(function (entry) {
      const sign = placeSigns[entry.type];
      const icon = L.divIcon({
        className: (sign ? 'map-place map-place-' + entry.type : 'editor-entry') + (entry.id === selectedId ? ' is-selected' : ''),
        html: sign ? '<span>' + sign + '</span>' : '',
        iconSize: sign ? [24, 24] : [16, 16],
        iconAnchor: sign ? [12, 12] : [8, 8]
      });
      L.marker([entry.lat, entry.lng], { icon: icon, draggable: true, keyboard: false })
        .bindTooltip(textElement(entry.label), { permanent: true, direction: 'right', offset: [12, 0], className: 'map-label' })
        .on('click', function (event) {
          L.DomEvent.stopPropagation(event);
          editor.dotnet.invokeMethodAsync('SelectEntry', entry.id);
        })
        .on('dragend', function (event) {
          const latlng = event.target.getLatLng();
          editor.dotnet.invokeMethodAsync('MoveEntry', entry.id, round(latlng.lat), round(latlng.lng));
        })
        .addTo(editor.entries);
    });
  }

  function dispose(id) {
    const editor = editors.get(id);
    if (editor) {
      editor.map.remove();
      editors.delete(id);
    }
  }

  document.addEventListener('keydown', function (event) {
    if (event.key === 'Escape') {
      editors.forEach(function (editor, id) {
        if (editor.tool) {
          setTool(id, null, null);
          editor.dotnet.invokeMethodAsync('ToolStopped');
        }
      });
    }
  });

  return { init: init, setTool: setTool, finish: finish, cancel: cancel, render: render, renderEntries: renderEntries, placeAtCenter: placeAtCenter, dispose: dispose };
})();
