# SEO und Domains

Organischer Traffic ist der wichtigste Kanal. Es gibt keine Werbung und keine Akquise. Jede Tauchplatzseite und jede Gewässerseite ist eine eigene Landingpage.

## Domains

| Domain | Verhalten |
|---|---|
| `tauchplaetze.ch` | Hauptdomain, alle Inhalte |
| `www.tauchplaetze.ch` | 301 auf `tauchplaetze.ch` |
| `sichtweiten.ch`, `www.sichtweiten.ch` | 301 auf `https://tauchplaetze.ch/de` (Prognose) |
| `sichtweiten.ch/{see}` | 301 auf die Gewässerseite (z.B. `/de/gewaesser/zuerichsee`), Liste der Kurzpfade in der Konfiguration |

`sichtweiten.ch` ist ein Einstieg für Suchen wie «Sichtweite Zürichsee». Es gibt keinen doppelten Inhalt. Darum nur Redirects, keine zweite Website.

Die Weiterleitungen macht die App (`HostRedirects`, Konfiguration `HostRedirects` in `appsettings.json`): Aliase mit Pfad und Query auf den Host aus `PublicSite:BaseUri`, Kurzpfade von `sichtweiten.ch` auf die Gewässerseite, sonst auf die Prognose (`/de`). GET und HEAD bekommen 301, andere Methoden 308. Andere Hosts (z.B. `*.azurewebsites.net` für Health Checks) laufen unverändert. Entscheid Marc, 21.10.2026: DNS bei Hostpoint, Cloudflare später (`infrastructure.md`).

## URL-Struktur

Siehe `i18n.md`. Grundsätze:

- Kurz, lesbar, ohne IDs: `/de/tauchplaetze/riff-williams-herrliberg`.
- Kleinbuchstaben, Bindestriche, keine Umlaute im Slug.
- Ein geänderter Slug erzeugt automatisch einen 301 vom alten Slug.
- Reserviert: `neu`, `new`, `nouveau`, `nuovo` (Seite «Neuer Platz», `ReservedSlugs`). Ein neuer oder importierter Platz bekommt dann `-2`, ein Slug von Hand ergibt `InvalidSlug`. Der Sprachwechsel übersetzt diese Seite wie ein Segment.
- Die Startseite kennt `?tag=…`, `?ort=…` und `?km=…`. `?ort=schweiz` löscht den gemerkten Ort (Cookie `tp_area`, `ux.md`). Alle sind kanonisch auf `/{lang}`.
- Query-Parameter (`?date=…`, Suche und Filter im Verzeichnis `?q=…&f=…&page=…`) sind kanonisch auf die Seite ohne Parameter. Das Canonical aller Seiten ist die Adresse ohne Query, das Verzeichnis also `/{lang}/tauchplaetze`. Suchergebnisse sind keine eigenen Landingpages.
- Absolute Adressen in `canonical` und `hreflang` kommen aus `PublicSite:BaseUri`, nie aus der Anfrage (hinter Ingress und Cloudflare stimmen Schema und Host der Anfrage nicht), immer ohne Query-String.

## Pro Seite

- `<title>` und `<meta name="description">` aus Ressourcen plus Daten, pro Sprache (umgesetzt 24.10.2026, `SeoText`). Die Beschreibung hat höchstens 160 Zeichen: Sätze kommen nur dazu, solange sie passen.
  - Platz: Titel «{Name} – Tauchplatz {Gewässer} · tauchplaetze.ch». Beschreibung «Tauchplatz {Name}, {Gemeinde} {Kanton}, {Gewässer}.», dazu mit Prognose «Heute: Tauch-Index {x}/10, Sicht ca. {s} m in Ufernähe, Sprungschicht {a}–{b} m.» (nur vorhandene und plausible Werte), sonst «Tägliche Tauchprognose, Karte und Einstiege.». Die Beschreibung ändert täglich; der Output Cache läuft vor Mitternacht ab.
  - Gewässer: Titel «{Gewässer}: Tauchen und Sichtweite heute · tauchplaetze.ch». Beschreibung mit der Zahl der Plätze und dem besten Tauch-Index von heute.
  - Startseite, Verzeichnis, Gewässerliste: Beschreibungen mit den Suchbegriffen Tauchen Schweiz, Tauchplätze, Sichtweite, Tauchprognose.
- `<link rel="canonical">` auf die eigene Adresse in der Sprache der Seite: Hauptdomain aus `PublicSite:BaseUri`, ohne Query, in Kleinbuchstaben. Gesetzt zentral in `HreflangLinks` zusammen mit `hreflang`, also nur auf indexierbaren Seiten. Seiten mit `noindex` (Konto, Versionen, Bearbeiten, Neu, Melden, `/admin`, `/platform`, 404, 410) und die Weiterleitung `/` haben kein Canonical (22.10.2026: Search Console zeigte «User-declared canonical: None»).
- `hreflang` für `de`, `en`, `fr`, `it` und `x-default` (= `de`).
- Open Graph und Twitter Card zentral in `HreflangLinks` (umgesetzt 24.10.2026), nur auf indexierbaren Seiten: `og:type` (`article` für Platz und Gewässer, sonst `website`), `og:site_name`, `og:title` (Titel ohne Markenname), `og:description` (= Meta-Description), `og:url` (= Canonical), `og:locale` (`de_CH`, `fr_CH`, `it_CH`, `en_GB`) mit `og:locale:alternate`, `og:image` mit Breite, Höhe und Alt-Text, `twitter:card` = `summary_large_image`. Bild: das Titelbild des Platzes (Bildgrösse ab 1200 px, sonst die grösste), ohne Titelbild das Standardbild `img/og-default.png` (1200 × 630, Logo mit «Wo tauchen wir heute?»).
- Eine `<h1>` pro Seite.

## Strukturierte Daten (JSON-LD)

Umgesetzt am 24.10.2026 (`StructuredData`, Komponente `JsonLd`), als `<script type="application/ld+json">` im Kopf, nur auf indexierbaren Seiten.

- Tauchplatz: `TouristAttraction` mit `name`, `description` (= Meta-Description), `url`, `geo` (`GeoCoordinates`, 5 Dezimalen), `address` (`addressLocality` = Gemeinde, `addressRegion` = Kanton, `addressCountry` = `CH`), `containedInPlace` (das Gewässer mit Typ und URL), `image` (Titelbild, falls vorhanden).
- Gewässer: `LakeBodyOfWater` (See) bzw. `RiverBodyOfWater` (Fluss) mit `geo`.
- Breadcrumbs: `BreadcrumbList` – Platz: tauchplaetze.ch › Gewässer › Platz; Gewässer: tauchplaetze.ch › Gewässer.
- Startseite: `WebSite` (Name, URL der Sprache, `inLanguage`, Beschreibung) und `Organization` (Name, URL, Logo, keine Personendaten).
- Sicherheit: `System.Text.Json` mit dem Standard-Encoder maskiert `<`, `>` und `&`; ein Name kann das `<script>` nicht schliessen (Test). Razor rendert kein `<script>` aus dem Markup einer Komponente, darum gibt `JsonLd` das ganze Element als Fragment aus.

## Sitemap und Robots

- `/sitemap.xml` als Index, darunter `/sitemap-{sprache}.xml` pro Sprache (Endpoint der App, `Sitemap`, Query `GetSitemapEntries`). Enthalten: Startseite, Verzeichnis, Gewässerliste, «Über», jeder veröffentlichte Platz und jedes Gewässer mit mindestens einem veröffentlichten Platz. Jede Adresse nennt ihre Fassungen in allen vier Sprachen und `x-default` (= `de`) als `xhtml:link rel="alternate"`. Die Antwort bleibt eine Stunde im Speicher der App. Nicht enthalten: Entwürfe, archivierte Plätze, alte Slugs, Konto-, Redaktions-, Versions- und Bearbeiten-Seiten.
- `lastmod` aus `DiveSite.UpdatedAt`, beim Gewässer die jüngste Änderung seiner Plätze (nicht aus der täglichen Prognose, sonst ändert sich jede Seite täglich). Die festen Seiten haben kein `lastmod`.
- `/robots.txt` (Endpoint der App, `RobotsTxt`): sperrt `/admin`, `/platform`, `/api`, die Konto-Seiten (`/{Lang}/account/`), «Neuer Platz», Bearbeiten, Versionen und «Tauchgang melden» in allen vier Sprachen sowie Query-Varianten (`/*?`). Am Schluss steht `Sitemap: {PublicSite:BaseUri}/sitemap.xml`.
- Die Seiten der Versionen (`…/versionen`) haben `noindex` und stehen nicht in der Sitemap (Datenschutz, `cms.md`). Ebenso der Bearbeiten-Modus (`…/bearbeiten`) und «Neuen Platz vorschlagen» (`…/neu`), beide nur angemeldet.

## Performance

- Static SSR mit Output Cache (`architecture.md`, «Caching»), Ziel: Antwortzeit unter 200 ms aus dem Cache. Ein Crawler mit mehr als 60 Seiten pro Minute bekommt 429 mit `Retry-After` (`infrastructure.md`, «Rate Limit Seiten»).
- Bilder responsiv (`srcset`, WebP), `loading="lazy"` ausser beim Hero.
- Schriften selbst gehostet, `font-display: swap`.
- Ziel: Core Web Vitals im grünen Bereich auf dem Handy.

## Inhalte als SEO-Hebel

- Gewässerseiten mit Liste der Plätze, aktueller Temperatur und Text zum See.
- Jede Platzseite enthält immer Prognose und Karte, auch in Ausbaustufe 0. So hat jede Seite eigenen, täglich aktuellen Inhalt.
- Fotos mit sprechenden Dateinamen und Alternativtexten in vier Sprachen.
