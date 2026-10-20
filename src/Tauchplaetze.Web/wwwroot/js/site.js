// Aufklappmenüs (details[data-dropdown]) schliessen bei Klick daneben und mit Escape. Ohne JavaScript schliesst ein zweiter Klick.
(function () {
  function closeAll(except) {
    document.querySelectorAll('details[data-dropdown][open]').forEach(function (menu) {
      if (menu !== except) {
        menu.removeAttribute('open');
      }
    });
  }

  document.addEventListener('click', function (event) {
    closeAll(event.target.closest('a') ? null : event.target.closest('details[data-dropdown]'));
  });

  document.addEventListener('keydown', function (event) {
    if (event.key === 'Escape') {
      const open = document.querySelector('details[data-dropdown][open]');
      closeAll(null);
      open?.querySelector('summary')?.focus();
    }
  });
})();

// Erläuterungen als Popover ([popover], geöffnet über popovertarget): neben dem Knopf statt in der Mitte,
// und mit der Maus schon beim Darüberfahren. Touch und Tastatur öffnen per Klick, Escape schliesst.
(function () {
  function place(popover, button) {
    const rect = button.getBoundingClientRect();
    const width = popover.offsetWidth;
    const height = popover.offsetHeight;
    const left = Math.min(Math.max(8, rect.right - width), window.innerWidth - width - 8);
    const below = rect.bottom + 6;
    const top = below + height > window.innerHeight - 8 ? Math.max(8, rect.top - height - 6) : below;
    popover.setAttribute('data-anchored', '');
    popover.style.left = left + 'px';
    popover.style.top = top + 'px';
  }

  document.addEventListener('toggle', function (event) {
    const popover = event.target;
    if (event.newState !== 'open' || !popover.id) {
      return;
    }

    const button = document.querySelector('[popovertarget="' + popover.id + '"]');
    if (button) {
      place(popover, button);
    }
  }, true);

  let hideTimer;
  let lastPointer = '';
  document.addEventListener('pointerdown', function (event) { lastPointer = event.pointerType; }, true);

  // Mit der Maus ist das Popover beim Klick schon offen. Der Klick soll es nicht wieder schliessen.
  document.addEventListener('click', function (event) {
    const button = event.target.closest && event.target.closest('[data-hover-popover]');
    const popover = button && document.getElementById(button.getAttribute('popovertarget'));
    if (lastPointer === 'mouse' && popover && popover.matches(':popover-open')) {
      event.preventDefault();
    }
  }, true);
  document.addEventListener('pointerover', function (event) {
    const button = event.target.closest && event.target.closest('[data-hover-popover]');
    const popover = button && document.getElementById(button.getAttribute('popovertarget'));
    if (event.pointerType !== 'mouse' || !popover) {
      return;
    }

    clearTimeout(hideTimer);
    if (!popover.matches(':popover-open')) {
      popover.showPopover();
    }

    function leave(e) {
      if (popover.contains(e.relatedTarget) || button.contains(e.relatedTarget)) {
        return;
      }

      hideTimer = setTimeout(function () {
        if (popover.matches(':popover-open')) {
          popover.hidePopover();
        }
      }, 200);
    }

    if (!popover.dataset.hoverBound) {
      popover.dataset.hoverBound = 'true';
      button.addEventListener('pointerleave', leave);
      popover.addEventListener('pointerleave', leave);
      popover.addEventListener('pointerenter', function () { clearTimeout(hideTimer); });
    }
  });
})();

// Verzeichnis: Filter und Sortierung schicken das Formular gleich ab. Ohne JavaScript bleibt der Knopf «Suchen».
(function () {
  document.addEventListener('change', function (event) {
    const form = event.target.closest && event.target.closest('form[data-autosubmit]');
    if (form && event.target.matches('input[type="checkbox"], select')) {
      form.requestSubmit();
    }
  });
})();

// «In meiner Nähe»: Standort aus dem Browser, auf zwei Dezimalen gerundet (rund 1 km), damit die URL keinen genauen Ort verrät.
(function () {
  function show() {
    if (!('geolocation' in navigator)) {
      return;
    }

    document.querySelectorAll('[data-near]').forEach(function (button) {
      button.hidden = false;
      if (button.dataset.bound) {
        return;
      }

      button.dataset.bound = 'true';
      button.addEventListener('click', function () {
        button.disabled = true;
        navigator.geolocation.getCurrentPosition(function (position) {
          const form = button.closest('form');
          const url = new URL(form.action, window.location.origin);
          new FormData(form).forEach(function (value, key) {
            if (key !== 'lat' && key !== 'lng' && key !== 'sort' && value !== '') {
              url.searchParams.append(key, value);
            }
          });
          url.searchParams.set('lat', position.coords.latitude.toFixed(2));
          url.searchParams.set('lng', position.coords.longitude.toFixed(2));
          window.location.assign(url.toString());
        }, function () {
          button.disabled = false;
        }, { maximumAge: 600000, timeout: 10000 });
      });
    });
  }

  document.addEventListener('DOMContentLoaded', function () {
    show();
    if (window.Blazor) {
      window.Blazor.addEventListener('enhancedload', show);
    }
  });
})();

// Bearbeiten-Modus (ux.md, «Bearbeiten»): Die Seite ist interaktiv, alle anderen Seiten statisch. Links laden darum die ganze Seite,
// sonst würde der Router von Blazor die statischen Seiten in der Verbindung des Editors darstellen (ohne Karten-Skripte).
(function () {
  window.addEventListener('click', function (event) {
    if (!document.querySelector('[data-edit-mode]') || event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey) {
      return;
    }

    const link = event.target.closest && event.target.closest('a[href]');
    if (!link || link.target || link.hasAttribute('download') || link.origin !== window.location.origin || link.getAttribute('href').startsWith('#')) {
      return;
    }

    event.preventDefault();
    event.stopImmediatePropagation();
    window.location.assign(link.href);
  }, true);
})();

// Warnung beim Verlassen, solange der Bearbeiten-Modus ungespeicherte Änderungen hat (data-edit-dirty).
window.tauchplaetzeEdit = (function () {
  let saved = false;

  window.addEventListener('beforeunload', function (event) {
    if (!saved && document.querySelector('[data-edit-dirty="true"]')) {
      event.preventDefault();
      event.returnValue = '';
    }
  });

  return { saved: function () { saved = true; } };
})();

// «Meinen Standort verwenden» auf der Startseite: nur auf Klick. Die Koordinate bleibt im Browser,
// gesendet wird nur der Schlüssel des nächsten Orts (data-places: «key,lat,lng;…»).
(function () {
  const item = document.querySelector('[data-locate]');
  if (!item || !navigator.geolocation) {
    return;
  }

  item.hidden = false;
  const places = item.getAttribute('data-places').split(';').map(function (entry) {
    const parts = entry.split(',');
    return { key: parts[0], lat: parseFloat(parts[1]), lng: parseFloat(parts[2]) };
  });

  function nearest(lat, lng) {
    let best = null;
    let bestDistance = Infinity;
    places.forEach(function (place) {
      const dLat = place.lat - lat;
      const dLng = (place.lng - lng) * Math.cos(lat * Math.PI / 180);
      const distance = dLat * dLat + dLng * dLng;
      if (distance < bestDistance) {
        bestDistance = distance;
        best = place;
      }
    });
    return best;
  }

  item.querySelector('button').addEventListener('click', function () {
    const error = item.querySelector('[data-locate-error]');
    error.hidden = true;
    navigator.geolocation.getCurrentPosition(function (position) {
      const place = nearest(position.coords.latitude, position.coords.longitude);
      if (place) {
        window.location.href = item.getAttribute('data-href').replace('{ort}', encodeURIComponent(place.key));
      }
    }, function () {
      error.hidden = false;
    }, { maximumAge: 600000, timeout: 10000 });
  });
})();
