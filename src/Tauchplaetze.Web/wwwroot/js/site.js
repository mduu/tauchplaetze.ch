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

    button.addEventListener('pointerout', leave, { once: true });
    popover.addEventListener('pointerout', leave, { once: true });
    popover.addEventListener('pointerover', function () { clearTimeout(hideTimer); }, { once: true });
  });
})();
