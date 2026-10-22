// Steuert den eigenen Reconnect-Dialog (ReconnectModal.razor). Blazor meldet die Zustände mit dem Event «components-reconnect-state-changed».
(function () {
  const modal = document.getElementById('components-reconnect-modal');
  if (!modal) {
    return;
  }

  modal.addEventListener('components-reconnect-state-changed', function (event) {
    const state = event.detail.state;
    if (state === 'show') {
      modal.showModal();
    } else if (state === 'hide') {
      modal.close();
    } else if (state === 'failed') {
      document.addEventListener('visibilitychange', retryWhenVisible);
    } else if (state === 'rejected') {
      location.reload();
    }
  });

  // Esc schliesst den Dialog nicht: Ohne Verbindung reagiert die Seite nicht.
  modal.addEventListener('cancel', function (event) {
    event.preventDefault();
  });

  document.getElementById('components-reconnect-button').addEventListener('click', retry);
  document.getElementById('components-resume-button').addEventListener('click', resume);
  document.getElementById('components-reload-button').addEventListener('click', function () {
    location.reload();
  });

  // Blazor.reconnect: true verbunden, false Server erreicht, aber Circuit weg, Exception Server nicht erreichbar.
  async function retry() {
    document.removeEventListener('visibilitychange', retryWhenVisible);
    try {
      const reconnected = await Blazor.reconnect();
      if (!reconnected) {
        const resumed = await Blazor.resumeCircuit();
        if (resumed) {
          modal.close();
        } else {
          location.reload();
        }
      }
    } catch {
      document.addEventListener('visibilitychange', retryWhenVisible);
    }
  }

  async function resume() {
    try {
      if (!await Blazor.resumeCircuit()) {
        location.reload();
      }
    } catch {
      location.reload();
    }
  }

  async function retryWhenVisible() {
    if (document.visibilityState === 'visible') {
      await retry();
    }
  }
})();
