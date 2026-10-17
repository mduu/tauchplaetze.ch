# SEO und Domains

Organischer Traffic ist der wichtigste Kanal. Es gibt keine Werbung und keine Akquise. Jede Tauchplatzseite und jede Seeseite ist eine eigene Landingpage.

## Domains

| Domain | Verhalten |
|---|---|
| `tauchplaetze.ch` | Hauptdomain, alle Inhalte |
| `www.tauchplaetze.ch` | 301 auf `tauchplaetze.ch` |
| `sichtweiten.ch`, `www.sichtweiten.ch` | 301 auf `https://tauchplaetze.ch/de/prognose` |
| `sichtweiten.ch/{see}` | 301 auf die Seeseite (z.B. `/de/seen/zuerichsee`), Liste der Kurzpfade in der Konfiguration |

`sichtweiten.ch` ist ein Einstieg für Suchen wie «Sichtweite Zürichsee». Es gibt keinen doppelten Inhalt. Darum nur Redirects, keine zweite Website.

Die Weiterleitungen laufen in Cloudflare (Redirect Rules). So belasten sie den App Service nicht.

## URL-Struktur

Siehe `i18n.md`. Grundsätze:

- Kurz, lesbar, ohne IDs: `/de/tauchplaetze/riff-williams-herrliberg`.
- Kleinbuchstaben, Bindestriche, keine Umlaute im Slug.
- Ein geänderter Slug erzeugt automatisch einen 301 vom alten Slug.
- Query-Parameter (`?date=…`) sind kanonisch auf die Seite ohne Parameter.

## Pro Seite

- `<title>` und `<meta name="description">` aus Ressourcen plus Daten, pro Sprache.
  - Platz: «{Name}, {Gemeinde} – Tauchplatz {See} | tauchplaetze.ch»
  - See: «Tauchplätze am {See}: Prognose, Sichtweite, Temperatur»
- `<link rel="canonical">`.
- `hreflang` für `de`, `en`, `fr`, `it` und `x-default` (= `de`).
- Open Graph und Twitter Card mit dem Hero-Foto.
- Eine `<h1>` pro Seite.

## Strukturierte Daten (JSON-LD)

- Tauchplatz: `TouristAttraction` (bzw. `Place`) mit `geo` (`GeoCoordinates`), `containedInPlace` (der See), `image`.
- See: `LakeBodyOfWater`.
- Breadcrumbs: `BreadcrumbList`.
- Organisation: `Organization` auf der Startseite.

## Sitemap und Robots

- `/sitemap.xml` als Index, darunter eine Sitemap pro Sprache. Nur veröffentlichte Plätze.
- `lastmod` aus `DiveSite.UpdatedAt` (nicht aus der täglichen Prognose, sonst ändert sich jede Seite täglich).
- `/robots.txt`: `/admin` und Query-Varianten ausschliessen.

## Performance

- Static SSR und Output Cache: Antwortzeit unter 200 ms aus dem Cache.
- Bilder responsiv (`srcset`, WebP), `loading="lazy"` ausser beim Hero.
- Schriften selbst gehostet, `font-display: swap`.
- Ziel: Core Web Vitals im grünen Bereich auf dem Handy.

## Inhalte als SEO-Hebel

- Seeseiten mit Liste der Plätze, aktueller Temperatur und Text zum See.
- Jede Platzseite enthält immer Prognose und Karte, auch in Ausbaustufe 0. So hat jede Seite eigenen, täglich aktuellen Inhalt.
- Fotos mit sprechenden Dateinamen und Alternativtexten in vier Sprachen.
