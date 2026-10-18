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

// Verschleierte E-Mail-Adressen (ObfuscatedEmail.razor): Der Link entsteht erst im Browser aus den rückwärts geschriebenen Teilen.
(function () {
  function reverse(text) {
    return text.split('').reverse().join('');
  }

  function revealEmails() {
    document.querySelectorAll('a.obfuscated-email[data-email-user]:not([href])').forEach(function (link) {
      const address = reverse(link.dataset.emailUser) + '@' + reverse(link.dataset.emailDomain);
      link.href = 'mail' + 'to:' + address;
      link.textContent = address;
    });
  }

  document.addEventListener('DOMContentLoaded', function () {
    revealEmails();
    if (window.Blazor) {
      window.Blazor.addEventListener('enhancedload', revealEmails);
    }
  });
})();
