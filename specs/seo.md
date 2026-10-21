# SEO und Domains

Organischer Traffic ist der wichtigste Kanal. Es gibt keine Werbung und keine Akquise. Jede Tauchplatzseite und jede Gewässerseite ist eine eigene Landingpage.

## Domains

| Domain | Verhalten |
|---|---|
| `tauchplaetze.ch` | Hauptdomain, alle Inhalte |
| `www.tauchplaetze.ch` | 301 auf `tauchplaetze.ch` |
| `sichtweiten.ch`, `www.sichtweiten.ch` | 301 auf `https://tauchplaetze.ch/de` (Prognose) |
| `sichtweiten.ch/{see}` | 301 auf die Gewässerseite (z.B. `/de/gewaesser/zuerichsee`), Liste der Kurzpfade in der Konfiguration |

Weitere Domains von Marc, nicht Teil der Plattform:

| Domain | Rolle |
|---|---|
| `tauchfotos.ch` | Bestehendes Fotoarchiv, Quelle für Bilder |
| `tauchvideos.ch` | Bestehendes Videoarchiv, Quelle für Videos |
| `tauchbolde.ch` | Website der eigenen Tauchgruppe |

`sichtweiten.ch` ist ein Einstieg für Suchen wie «Sichtweite Zürichsee». Es gibt keinen doppelten Inhalt. Darum nur Redirects, keine zweite Website.

Die Weiterleitungen macht die App (`HostRedirects`, Konfiguration `HostRedirects` in `appsettings.json`): Aliase mit Pfad und Query auf den Host aus `PublicSite:BaseUri`, Kurzpfade von `sichtweiten.ch` auf die Gewässerseite, sonst auf die Prognose (`/de`). GET und HEAD bekommen 301, andere Methoden 308. Andere Hosts (z.B. `*.azurewebsites.net` für Health Checks) laufen unverändert. Entscheid Marc, 21.10.2026: DNS bei Hostpoint, Cloudflare später (`infrastructure.md`).

## URL-Struktur

Siehe `i18n.md`. Grundsätze:

- Kurz, lesbar, ohne IDs: `/de/tauchplaetze/riff-williams-herrliberg`.
- Kleinbuchstaben, Bindestriche, keine Umlaute im Slug.
- Ein geänderter Slug erzeugt automatisch einen 301 vom alten Slug.
- Reserviert: `neu`, `new`, `nouveau`, `nuovo` (Seite «Neuer Platz», `ReservedSlugs`). Ein neuer oder importierter Platz bekommt dann `-2`, ein Slug von Hand ergibt `InvalidSlug`. Der Sprachwechsel übersetzt diese Seite wie ein Segment.
- Die Startseite kennt `?tag=…`, `?ort=…` und `?km=…`. `?ort=schweiz` löscht den gemerkten Ort (Cookie `tp_area`, `ux.md`). Alle sind kanonisch auf `/{lang}`.
- Query-Parameter (`?date=…`, Suche und Filter im Verzeichnis `?q=…&f=…&page=…`) sind kanonisch auf die Seite ohne Parameter. Das Verzeichnis setzt `<link rel="canonical">` auf `/{lang}/tauchplaetze`. Suchergebnisse sind keine eigenen Landingpages.
- Absolute Adressen in `canonical` und `hreflang` kommen aus `PublicSite:BaseUri`, nie aus der Anfrage (hinter Ingress und Cloudflare stimmen Schema und Host der Anfrage nicht), immer ohne Query-String.

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
- Gewässer: `LakeBodyOfWater` (See) bzw. `RiverBodyOfWater` (Fluss).
- Breadcrumbs: `BreadcrumbList`.
- Organisation: `Organization` auf der Startseite.

## Sitemap und Robots

- `/sitemap.xml` als Index, darunter eine Sitemap pro Sprache. Nur veröffentlichte Plätze.
- `lastmod` aus `DiveSite.UpdatedAt` (nicht aus der täglichen Prognose, sonst ändert sich jede Seite täglich).
- `/robots.txt`: `/admin` und Query-Varianten ausschliessen.
- Die Seiten der Versionen (`…/versionen`) haben `noindex` und stehen nicht in der Sitemap (Datenschutz, `cms.md`). Ebenso der Bearbeiten-Modus (`…/bearbeiten`) und «Neuen Platz vorschlagen» (`…/neu`), beide nur angemeldet.

## Performance

- Static SSR und Output Cache: Antwortzeit unter 200 ms aus dem Cache.
- Bilder responsiv (`srcset`, WebP), `loading="lazy"` ausser beim Hero.
- Schriften selbst gehostet, `font-display: swap`.
- Ziel: Core Web Vitals im grünen Bereich auf dem Handy.

## Inhalte als SEO-Hebel

- Gewässerseiten mit Liste der Plätze, aktueller Temperatur und Text zum See.
- Jede Platzseite enthält immer Prognose und Karte, auch in Ausbaustufe 0. So hat jede Seite eigenen, täglich aktuellen Inhalt.
- Fotos mit sprechenden Dateinamen und Alternativtexten in vier Sprachen.
