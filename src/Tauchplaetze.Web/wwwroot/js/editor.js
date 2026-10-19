// Editoren auf Karten für alle interaktiven Seiten (Moderation und Bearbeiten-Modus), ohne MudBlazor.
// Karte für eine Koordinate: Klick setzt sie (cms.md, «Platz bearbeiten»). Blazor ruft die Funktionen über JS-Interop auf.
window.tauchplaetzeAdmin = (function () {
  const maps = new Map();

  // pan: die Koordinate kam aus den Feldern, die Karte folgt ihr.
  function show(id, latitude, longitude, dotnet, pan) {
    const element = document.getElementById(id);
    if (!element) {
      return;
    }

    let entry = maps.get(id);
    if (!entry) {
      const map = L.map(element, { scrollWheelZoom: true });
      window.tauchplaetzeMap.addBaseLayers(map, element);
      window.tauchplaetzeMap.addContours(map, element);
      const marker = L.circleMarker([latitude, longitude], { radius: 9, color: getComputedStyle(document.documentElement).getPropertyValue('--color-bg').trim(), weight: 2, fillColor: getComputedStyle(document.documentElement).getPropertyValue('--color-signal').trim(), fillOpacity: 1 }).addTo(map);
      map.on('click', function (event) {
        marker.setLatLng(event.latlng);
        dotnet.invokeMethodAsync('SetLocation', event.latlng.lat, event.latlng.lng);
      });
      entry = { map: map, marker: marker };
      maps.set(id, entry);
      map.setView([latitude, longitude], 15);
    } else {
      entry.marker.setLatLng([latitude, longitude]);
      if (pan) {
        entry.map.panTo([latitude, longitude]);
      }
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

  // Einstiege und Orte als verschiebbare Punkte. Orte an Land (Parkplatz, Toilette) als Schild wie auf der Platzseite.
  const placeSigns = { parking: 'P', toilet: 'WC' };

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
