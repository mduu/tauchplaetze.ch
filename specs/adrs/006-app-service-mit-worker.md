# ADR-006: Azure App Service mit Background Worker im selben Host

## Status

**Akzeptiert** (17.10.2026)

## Kontext

- Die Website und ein täglicher Prognose-Job müssen laufen.
- Budget unter CHF 20 pro Monat.
- Marc kennt Azure sehr gut. feldnah läuft auf App Service B1.

## Entscheidung

Wir entscheiden uns für **einen Azure App Service (Linux, B1)**. Der Prognose-Job läuft als **`BackgroundService` im Web-Host** und ruft Handler des Core auf.

## Optionen

### Option A: App Service mit BackgroundService (gewählt)

**Vorteile:**
- Ein Deployment, eine Ressource, bekannte Plattform.
- «Always On» hält den Worker wach.

**Nachteile:**
- Job und Website teilen sich die Ressourcen. Bei einem täglichen Lauf unkritisch.

### Option B: Azure Functions (Timer Trigger) zusätzlich

**Vorteile:**
- Praktisch gratis im Verbrauchsmodell.

**Nachteile:**
- Zweites Deployment, zweite Konfiguration. Für einen Job zu viel.

### Option C: Azure Container Apps mit Scale-to-Zero

**Vorteile:**
- Potenziell günstiger.

**Nachteile:**
- Kaltstarts schaden SEO und Nutzererlebnis. Job braucht eigenes Muster (Container Apps Jobs).

## Konsequenzen

### Positive Auswirkungen

- Einfacher Betrieb.

### Negative Auswirkungen / Risiken

- Skaliert die App auf mehrere Instanzen, braucht der Job eine Sperre. Wir bauen sie von Anfang an (Blob Lease).
- Wächst der Job stark (z.B. ML-Training), wandert er in eine eigene Fassade (CLI oder Container Apps Job).

## Referenzen

- `infrastructure.md`, `forecast.md`
