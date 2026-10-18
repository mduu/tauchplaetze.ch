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
