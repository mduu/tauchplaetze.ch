# UX

Die Oberfläche beantwortet zuerst die Frage «Wo soll ich tauchen gehen?». Danach liefert sie alles, was man für den Tauchgang braucht. Das Erscheinungsbild ist dunkel («Kaltwasser-Nacht»), siehe `design-system.md`. Die Mockups liegen in `design/`.

## Grundsätze

- **Antwort zuerst.** Die Startseite zeigt sofort die beste Wahl und die Rangliste. Keine Einleitung, kein Marketingtext davor.
- **Mobile first.** Viele schauen am Morgen auf dem Handy oder am Parkplatz.
- **Ehrlich.** Prognose als Indikator kennzeichnen. Datenalter zeigen. Quellen nennen.
- **Bilder tragen.** Unterwasserfotos sind das stärkste Element. Platzhalter nur, solange keine Fotos da sind.
- **Sicherheit sichtbar.** Notfallnummern und Hinweise (Schiffsverkehr, Verbote) auf jeder Platzseite.
- **Keine Konten für Besucher.** Favoriten und Standort bleiben im Browser. Ein Konto braucht nur, wer beiträgt.

## Informationsarchitektur

```
/{lang}                         Start = Prognose «Wo tauchen wir heute?»
/{lang}/prognose                (Alias der Startseite, Ziel von sichtweiten.ch)
/{lang}/tauchplaetze            Verzeichnis (Liste + Karte, Filter)
/{lang}/tauchplaetze/{slug}     Tauchplatzseite
/{lang}/tauchplaetze/{slug}/versionen   Versionen des Platzes (noindex)
/{lang}/tauchplaetze/{slug}/bearbeiten  Bearbeiten-Modus (angemeldet, noindex)
/{lang}/tauchplaetze/neu        Neuen Platz vorschlagen (angemeldet, noindex)
/{lang}/gewaesser               Gewässer-Übersicht (Seen und Flüsse)
/{lang}/gewaesser/{slug}        Gewässerseite
/{lang}/ueber                   Über, Datenquellen, Impressum, Datenschutz
/{lang}/site/{slug}             kurzer Link auf einen Platz (Mails, CMS), 301 auf den Pfad der Sprache
/{lang}/account/…               Konto: Profil, Meine Vorschläge, Meine Daten, Konto löschen (noindex)
/admin/…                        Moderation (Moderatoren und Admins, ohne Sprachpräfix)
/platform/…                     Plattform: Konten, Prognose, Mail-Queue (nur Admins, ohne Sprachpräfix)
```

Pfadsegmente pro Sprache: siehe `i18n.md`.

Hauptnavigation: Prognose · Tauchplätze · Gewässer · Buddy-Gruppen. Rechts: Konto (angemeldet als Aufklappmenü mit Profil, Meine Vorschläge, für Moderatoren «Moderation», für Admins «Plattform» und Abmelden, sonst «Anmelden») und Sprachwahl als Aufklappmenü (Knopf mit dem Kürzel der aktuellen Sprache, darunter Kürzel und Name jeder Sprache). Das Menü ist ein `<details>` und geht ohne JavaScript; mit JavaScript schliesst es bei Klick daneben und mit Escape. Bis 1000 px Breite stehen Logo, Konto und Sprache in der ersten Zeile und die Navigation darunter (auf dem Handy seitlich scrollbar); bis 720 px zeigt das Konto nur das Symbol.

## Seiten

### Startseite «Wo tauchen wir heute?»

Mockup: `design/start.dc.html`.

1. Kopf: Logo, Navigation, Sprachwahl.
2. Titel als Frage, abhängig vom gewählten Tag («… heute?», «… am Sonntag?»).
3. Kurzer Satz, woher die Prognose kommt, mit Link «So entsteht die Prognose» auf die Seite «Über» (`#prognose`).
4. Tagesauswahl als Pillen (heute plus 4 Tage). Tage ohne Index tragen den Zusatz «Ausblick». Umkreis-Auswahl («25 km ab Zürich · Ort wählen»), mit gewähltem Ort darunter die Pillen 15, 25 und 40 km (Standard 25 km; 60 km war für die Schweiz kein Umkreis mehr, Marc 19.10.2026). Ohne gewählten Ort: «ganze Schweiz · Ort wählen».
5. **Beste Wahl** (grosse Karte über die ganze Breite; breit links der Text, rechts der Querschnitt, schmal der Querschnitt oben):
   - Label «Beste Wahl für heute» bzw. «… für Dienstag», darunter der Satz «Der Platz mit dem höchsten Index in der Schweiz» bzw. «… im Umkreis». So ist klar, dass die Karte die Nummer 1 der Rangliste zeigt.
   - Seequerschnitt als Illustration mit Oberflächentemperatur, Temperatur auf 20 m und Sprungschicht.
   - Name, Ort, See, Tauch-Index gross.
   - Kacheln: Oberfläche, auf 20 m, Sprungschicht, Wind.
   - Begründung in einem Satz.
   - Aktionen: «Zum Tauchplatz», «Buddy für {See} finden».
6. **Rangliste im Umkreis** (unter der Beste Wahl, über die ganze Breite, breit in zwei Spalten): Rang, Name (Link), Ort · See, Balken, Index, Begründung. Die ersten 10 Plätze offen, der Rest hinter «Alle {n} Plätze anzeigen».
   - Solange weniger als 80 % der Plätze einen Index haben, steht über der Liste «Prognose bisher für Rhein, Zugersee und Zürichsee. Weitere Gewässer folgen.» Die Gewässer liefert der Core (`GetBestSites`), der Hinweis wächst und verschwindet mit dem Ausbau.
   - Mit gewähltem Ort folgen unter der Rangliste die Plätze im Umkreis ohne Index, gedämpft und ohne Rang («noch keine Prognose»), nächster zuerst, höchstens 10, dazu «und {n} weitere Plätze ohne Prognose».
7. Hinweis: «Der Index ist ein Indikator …».
8. Seen im Überblick: Kachel pro See mit Temperatur, Mini-Verlauf der Klarheit und Trendtext.
9. Buddy-Block (Akzentfarbe): Links zu den WhatsApp-Gruppen.
10. Fuss: Datenquellen, Hinweis «Tauche nie allein», Notfallnummern, Links «Über · Impressum · Datenschutz».

Die vier Schritte «Keine Meldungen nötig. Der See verrät es selbst.» stehen seit dem 18.10.2026 auf der Seite «Über» (Entscheid Marc): Die Startseite gibt zuerst die Antwort, die Erklärung ist für Neue.

Tagesauswahl und Umkreis ändern die URL (`?tag=2026-10-20`, `?ort=zuerich`, `&km=15`; ohne `km` gilt 25, ein anderer Wert als 15, 25 oder 40 ebenfalls). Alte Links ohne `km` funktionieren weiter. Die Seite bleibt Static SSR.

### Über

Pfad `/de/ueber`, `/en/about`, `/fr/a-propos`, `/it/chi-siamo`. Nicht in der Hauptnavigation, sondern im Fuss («Über · Impressum · Datenschutz») und als Link von der Startseite. Abschnitte mit Ankern:

1. **So entsteht die Prognose** (`#prognose`): die vier Schritte (Seemodell, Satellit, Wetter und Zuflüsse, Community als Bonus), die Datenstufen 3 bis 1 kurz erklärt, Hinweis «Der Index ist ein Indikator».
2. **Datenquellen** (`#quellen`): Eawag Alplakes, MeteoSchweiz über Open-Meteo, BAFU Hydrodaten über existenz.ch, swisstopo (Landeskarte, Luftbild, swissBATHY3D), OpenStreetMap (ODbL), je mit Link und Lizenz (`data-sources.md`).
3. **Impressum** (`#impressum`): Betreiber (Name, Adresse) aus `Operator:*` (`infrastructure.md`), Kontakt über das Formular, Haftungshinweis. Fehlen Name oder Adresse: «Angaben folgen».
4. **Kontakt** (`#kontakt`): Formular mit Name (optional), E-Mail für die Antwort (Pflicht), Bezug (optional) und Nachricht (10–4000 Zeichen). Der Command `SendContactMessage` schickt sie per Mail an `Operator:Email`, Antwort an die Absenderin (Reply-To), Betreff «tauchplaetze.ch Kontakt: …». Die Nachricht wird nicht gespeichert. Nach dem Senden Redirect mit «Danke!» (`?kontakt=gesendet`). Plätze der Stufe 0 verlinken mit «Schreib uns.» auf das Formular, Bezug vorausgefüllt (`?bezug={Name}`).
   - **Schutz:** Honeypot (Feld «Website», per CSS ausserhalb des Bildschirms, `aria-hidden`, `tabindex=-1`), Zeitfalle (Timestamp mit Data Protection signiert, unter 3 s oder über 6 h ungültig), Rate Limit 5 POST pro Stunde und Client (nur im Arbeitsspeicher, `infrastructure.md`). Nach einem Fehler bleibt der gesendete Timestamp gültig, damit ein zweiter Versuch nicht als «zu schnell» gilt. Honeypot oder zu schnell: Die Seite meldet «gesendet», sendet aber nichts.
5. **Datenschutz** (`#datenschutz`): Besuch, Speicherort, Konto, Beiträge und Fotos, Kontaktformular, Standort und Karten, Routen, Rechte («Meine Daten», «Konto löschen»), Verweis auf das Formular.

Die E-Mail-Adresse des Betreibers erscheint nirgends auf der Website, auch nicht verschleiert (Entscheid Marc, 18.10.2026).

`canonical` der Seite ist immer `/{lang}/{segment}` ohne Parameter (`?bezug`, `?kontakt`). Die Seite ist vom künftigen Output Cache ausgenommen: Das Formular trägt ein Antiforgery-Token und einen Timestamp pro Aufruf.

### Tauchplatzseite

Mockup: `design/tauchplatz.dc.html`.

1. Hero: Titelbild (`HeroMediaId`, mit «Titelbild: {Fotograf}» oben rechts und Verlauf nach unten für die Lesbarkeit) oder die Unterwasser-Illustration, Brotkrumen «See › Gemeinde», Name gross, darunter «auch bekannt als …» (falls vorhanden), Tags (Wand, Wrack, Tiefe, Zugang, Schwierigkeit, ganzjährig).
2. **Hinweise** (falls gültig): Banner direkt unter dem Hero, schwerster zuerst. Farbe nach Art, Art und Enddatum als Label («Gesperrt · bis 01.04.2027»), darunter der Text. Siehe `dive-site.md`, «Hinweise».
3. **Tauchbedingungen:** Satz («Heute ein guter Tag.») und Index gross.
   - Temperaturprofil als Diagramm (Temperatur über Tiefe) mit markierter Sprungschicht.
   - Kacheln: Sicht-Indikator (Stufe für die Ufernähe; mit Sentinel-2 «Satellit (Sentinel-2) vom 15.10.: ca. 5.4 m in Ufernähe», mit Sentinel-3 «Satellit vom 05.10.: ca. 6.3 m in der Seemitte, in Ufernähe ca. 4.4 m»; dazu «besser ab 18 m»), Wind, Algen, bei Seen mit 3D-Modell Strömung («ruhig» mit Pfeil in Fliessrichtung, «2 cm/s nach Nordosten · 5–30 m»; nur mit Werten). Kein Anzug-Hinweis: In der Schweiz taucht man fast immer trocken.
   - 5-Tage-Verlauf: Index, wo vorhanden. Tage im Ausblick zeigen Wind und Regen.
   - **Flussplätze** (Entscheid 19.10.2026): kein Temperaturprofil, keine Kachel «Algen», keine Texte zum See. Die Sicht-Kachel sagt «Am Fluss gibt es keinen Satellitenwert. Viel Abfluss macht das Wasser oft trüb.» Die Quellenzeile nennt nur MeteoSchweiz, DWD über Open-Meteo und BAFU. Misst die Station die Wassertemperatur, steht eine Kachel «Wassertemperatur» mit Wert, Station und Messzeit («16.6 °C · Station Rhein – Rheinfelden · gemessen 18:10», nach 2 h «Messwert veraltet»). Welche Blöcke gelten, liefert der Core (`SiteForecast.HasDepthProfile`, `HasSatellite`, `RankedSite.HasDepthProfile`). Auf der Startseite zeigt «Beste Wahl» an einem Fluss den Querschnitt ohne Temperaturen.
   - An Flussplätzen die Kachel «Abfluss» über die ganze Breite der Kacheln (`forecast.md`, «Abfluss»): Einordnung (sehr gering, gering, mittel, stark; Farbe wie der Index, wenig Abfluss ist gut) und Wert «312 m³/s», mit «i» und Legende (vier Stufen mit Grenzen in m³/s, Herkunft der Schwellen). Darunter der Trend «↗ steigt · +14 m³/s in 3 Stunden», die Station mit Uhrzeit des Messwerts («Station Rhein – Rheinfelden · gemessen 15:40», nach 2 h «Messwert veraltet»), die Kurve der letzten 48 Stunden mit dem Median gestrichelt und die Tagesmittel der letzten 7 Tage als Balken. Ohne Prognose steht die Kachel unter dem Hinweis «Prognose folgt», dann mit eigener Quellenzeile «Daten: BAFU».
   - «Wetterverlauf» in derselben Kachel unter den fünf Tagen, kompakt wie bei MeteoSchweiz, im Raster von 2 Stunden: Wettersymbol nur beim ersten Schritt und bei jedem Wechsel, dazwischen eine gepunktete Linie. Wind (Pfeil in Windrichtung, km/h) alle 6 Stunden. Sonne in Minuten pro Stunde, Temperaturlinie und Regenbalken (Mittel über 2 Stunden). Nacht dunkler, vergangene Stunden schattiert, Linie bei «jetzt». Wird die Kachel zu schmal, scrollt die Grafik in der Kachel, nie die Seite. Textalternative: Zusammenfassung pro Tag (Temperatur, Regen, Wind). Nur, wenn der Lauf Stundenwerte hat.
   - Kacheln im Raster: drei je Zeile, bei zwei oder vier Kacheln zwei je Zeile, damit nie eine Kachel allein steht; der Abfluss über die ganze Breite; schmal (bis 520 px) untereinander.
   - Anordnung auf dem Desktop: links oben die Kacheln, darunter das Temperaturprofil (ohne Profil nur die Kacheln), rechts die fünf Tage mit dem Wetterverlauf. Schmal: Kacheln, Profil, fünf Tage.
   - Quellen mit Links, nur einmal unter dem Block (Entscheid Marc, 19.10.2026): «Daten: Eawag Alplakes, MeteoSchweiz und DWD über Open-Meteo, BAFU». Eawag Alplakes deckt auch die Strömung ab. Unter dem Wetterverlauf steht keine eigene Quellenzeile. Text und Adressen aus den Ressourcen (Adresse von MeteoSchweiz und BAFU in der Sprache der Seite), Links öffnen in einem neuen Tab.
   - Datenstufe als kurzer Hinweis: «Profil am Platz», «für den ganzen See», «Messstation {Name}» oder «nur Wetter».
4. **Der Platz:** Bewertungen als kompakter Block (eine Kachel, eine Zeile pro Skala, `design-system.md`), darunter zweispaltig (Entscheid Marc, 19.10.2026): links «Unter Wasser» (`UnderwaterDescription`), rechts «Gefahren» und darunter «Umgebung» (`SurroundingsInfo`); schmal untereinander in dieser Reihenfolge. Die Titel der Texte im Stil der Kachel-Labels (Mono, klein, Grossbuchstaben, Cyan; «Gefahren» in Orange) (Schwierigkeit, Steilwand, UW-Leben, Wracks und Gegenstände, Strömung, Umgebung; nur bewertete Skalen, Wert immer auch als Zahl und Kurztext). Pro Skala ein «i»: Es zeigt die Legende der Skala (alle fünf Stufen mit Bedeutung aus `dive-site.md`, die aktuelle hervorgehoben). Mit der Maus beim Darüberfahren, auf Touch und mit der Tastatur per Klick, Escape oder Klick daneben schliesst. Ohne Bewertung fehlt der Block. Darunter **Fotos**: Galerie nach Abschnitt (Unter Wasser, Über Wasser, Anfahrt und Parkplatz, Einstiege, Gefahren), Raster mit Kacheln ab 280 px, `srcset` mit allen Bildgrössen, `loading=lazy`, Alternativtext. Unter jedem Foto Legende, «Foto: {Fotograf}» (mit Link, falls erfasst) und Aufnahmetiefe. Ein Klick öffnet die grösste Bildgrösse in einem neuen Tab. Ohne öffentliche Fotos fehlt der Abschnitt.
5. **Gefahren:** im Abschnitt «Der Platz» rechts oben (siehe 4): Text (`HazardsInfo`), Bilder in der Galerie. Gut sichtbar, nicht eingeklappt. Bei `HasDivingBan` zuerst eine Hinweisbox «Tauchverbot» mit `DivingBanNote`.
6. **Lage und Einstiege:** Karte (Landeskarte, graue Landeskarte oder Luftbild) mit allen Einstiegen und Orten, beschriftet mit Name und Art. Sind Einstiege oder Orte erfasst, fehlt der Platzpunkt: Sie sind genauer und überdecken sich so nicht mit ihm. Ohne sie zeigt die Karte den Platz (Zoom 17, sonst passend auf alle Punkte bis Zoom 18). Die Karte erscheint immer (Titel ohne Einstiege «Lage»). Sie steht immer als eigener Abschnitt über die ganze Breite unter den Tauchbedingungen, 540 px hoch, schmal 360 px. Darunter die Einstiege als Liste. Auf der Karte liegen die Ebenen «Tiefenlinien» (swissBATHY3D, ab Zoom 13) und «Skizze» (falls vorhanden), beide an und abschaltbar. Mit Skizze steht darunter «Skizze, nicht zur Navigation» (`dive-site.md`, «Tauchskizze»). Später: pro Einstieg Text und Bilder.
7. **Praktisch:** Kachel «Anfahrt» (EN «Getting there», FR «Accès», IT «Arrivo»; Entscheid Marc, 19.10.2026): Koordinaten, Links auf die Landeskarte, zu Google Maps und Apple Karten (die meisten navigieren damit zum Platz) und darunter der Text «Anfahrt und Parkplatz» (`ApproachInfo`); Bilder zur Anfahrt in der Galerie, Umgebung (Bewertung, Text, Bilder), Restaurants und Toiletten (Text unter den Kacheln), nächste Füllstation, Notfall (Sanität 144, Rega 1414, Polizei 117 als drei Chips nebeneinander, Nummer gross, Name klein, je `tel:`-Link und mindestens 44 px hoch; dazu der Hinweis: bei Tauchunfall Rega 1414 mit Stichwort «Tauchunfall», die Rega verbindet mit dem Tauchmediziner von DAN Suisse. Quelle: Factsheet DAN Suisse und Rega, BAG, 06.07.2025, in der Kachel verlinkt (Italienisch eigene Fassung, sonst Deutsch). Die internationale Nummer von DAN Europe entfällt, weil in der Schweiz die Rega die Hotline ist).
8. **Letzte Meldungen** (Phase 1, falls vorhanden) mit Button «Sicht melden».
9. Buddy-Block für die Region.
10. Fuss mit Quellen, «Zuletzt geändert am …», Links «Versionen» und «Platz bearbeiten» (Bearbeiten-Modus; ohne Anmeldung zuerst zur Anmeldung und danach zurück).

Alle Angaben in den Abschnitten 4 bis 7 sind optional (siehe `dive-site.md`). Ein Abschnitt ohne Inhalt fehlt, ausser der Karte in Abschnitt 6. Texte kommen in der Sprache der Seite, pro Feld sonst auf Deutsch mit dem Hinweis «Dieser Text ist nur auf Deutsch verfügbar.» (`i18n.md`). Fehlen die Abschnitte 4 bis 7 ganz (keine Bewertung, kein Text, kein Foto, kein Einstieg, keine Skizze, kein Tauchverbot; `DiveSiteDetails.IsDirectoryOnly`), erscheint ein freundlicher Hinweis: «Dieser Platz ist erst im Verzeichnis. Kennst du ihn? Schreib uns.» (mit Kontaktlink).

Nach dem Speichern im Bearbeiten-Modus zeigt die Platzseite unter dem Hero ein Banner: für Mitwirkende «Danke! Dein Vorschlag ist bei der Moderation …», für Moderatoren «Gespeichert und online.» (`?gespeichert=vorschlag|online`).

### Bearbeiten-Modus

Wer einen Platz ändern will, bleibt im Design der Website (Entscheid Marc, 19.10.2026: «mehr inline, die UX ändert nicht komplett»).

- Pfad `/{lang}/tauchplaetze/{slug}/bearbeiten`, nur angemeldet, `noindex`. Kopf, Fuss, Farben und Kacheln der Website.
- Leiste oben, klebt beim Scrollen: «Bearbeitungsmodus · Deine Änderungen gehen an die Moderation» (Moderatoren: «… sind sofort online»), Knöpfe «Abbrechen» und «Vorschlag senden» bzw. «Speichern und freigeben». Unterer Rand in `--color-signal`.
- Abschnitte in der Reihenfolge der Platzseite:
  1. **Name und Ort:** Name, «Auch bekannt als», Gemeinde, Kanton. Das Gewässer steht nur zur Info, es ändert die Moderation.
  2. **Der Platz:** maximale Tiefe, Zugang, Eigenschaften als Chips, die sechs Bewertungen als Stufen 1 bis 5 oder «keine», jede mit dem «i» und der Legende aus `dive-site.md`.
  3. **Texte:** Tabs DE, EN, FR, IT mit Unter Wasser, Gefahren, Anfahrt, Umgebung, Einrichtungen.
  4. **Lage und Einstiege:** die grosse Karte mit Tiefenlinien. Darüber die Werkzeuge als Knöpfe in Gruppen: Einstiege und Orte (Ein- und Ausstieg, Einstieg, Ausstieg, Parkplatz, Toilette …), Skizze: Symbole, Linien, Flächen. Ein Werkzeug wählen und in die Karte klicken setzt einen Punkt; für die Tastatur setzt «In der Kartenmitte setzen» ihn in die Mitte (die Karte verschiebt man mit den Pfeiltasten); Linien und Flächen enden mit «Fertig» oder Doppelklick, Escape bricht ab. Punkte lassen sich ziehen. Darunter zwei Listen («Einstiege und Orte», «Tauchskizze»): Wählen klappt das Element auf, mit Art, Koordinate (Breite und Länge als Alternative zum Klick), Name und Beschreibung bzw. Tiefe und Beschriftung pro Sprache (Tabs). Einstiege lassen sich nach oben und unten schieben (der erste ist der Hauptzugang). «Entfernen» löscht das Element; Fotos eines entfernten Einstiegs verlieren ihren Einstieg. Alles geht mit der Tastatur. Die Knöpfe ↑ und ↓ nennen das Element im `aria-label`.
  5. **Fotos:** Hochladen mit Fotograf (Vorgabe: Anzeigename), Link, Abschnitt, Tiefe und der Bestätigung der Rechte; der Knopf «Fotos wählen» ist erst danach aktiv. Die Fotos der Version als Liste mit Vorschau (private über den geschützten Endpunkt), Abschnitt, bei «Einstieg» der Einstieg, Titelbild (Radio), Reihenfolge, «Aus der Version nehmen». Eigene private Fotos (Moderation: alle) haben «Angaben» (Fotograf, Link, Tiefe, Legende und Alternativtext pro Sprache, sofort gespeichert) und «Löschen» mit Rückfrage. Darunter weitere Fotos des Platzes zum Hinzufügen.
  6. **Nur Moderation** (nur für Moderatoren an Flussplätzen): die drei Schwellen des Abflusses in m³/s («Gering ab», «Mittel ab», «Stark ab»), Dezimalzahl mit Punkt oder Komma. Leer: Die Einordnung kommt aus dem Median.
  7. **Zu deiner Änderung:** Kommentar für die Moderation.
- Hat die Person dort einen offenen Vorschlag, steht oben der Hinweis mit Link auf «Meine Vorschläge».
- Speichern nutzt `ProposeChange` wie der Editor. Fehler stehen oben, «keine Änderung» als Hinweis. Danach die Platzseite mit Banner.
- Alle Links laden die ganze Seite, weil die anderen Seiten statisch sind.

### Neuer Platz

- Pfad `/{lang}/tauchplaetze/neu`, nur angemeldet, `noindex`. Einstieg: Satz «Fehlt ein Platz? Neuen Platz vorschlagen» im Kopf des Verzeichnisses (für alle sichtbar, ohne Anmeldung zuerst zur Anmeldung) und Eintrag im Kontomenü.
- Felder: Name, Gewässer, Gemeinde, Kanton; Koordinate per Klick in die Karte (mit Tiefenlinien) oder als Breite und Länge (die Karte folgt); Kommentar für die Moderation. Ein Platz im Verzeichnis braucht nicht mehr (siehe `vision.md`, «Verzeichnis zuerst»).
- Die Koordinate ist Pflicht: Ohne Klick oder Eingabe sendet die Seite nicht und zeigt den Fehler am Feld «Lage». Der Core lehnt einen Vorschlag ohne Koordinate mit `MissingLocation` ab. Eine Prüfung der Distanz zum Gewässer gibt es bewusst nicht: Ein Gewässer hat nur einen Mittelpunkt, und bei Flüssen und grossen Seen sagt die Distanz dazu nichts. Die Moderation prüft die Lage.
- Sobald die Koordinate steht, zeigt die Seite öffentliche Plätze im Umkreis von 300 m mit Distanz und Link (neuer Tab), damit niemand einen Platz doppelt vorschlägt.
- Speichern nutzt `ProposeDiveSite`. Mitwirkende landen auf «Meine Vorschläge» mit «Danke! Dein Vorschlag … ist eingereicht.», Moderatoren im Editor des neuen Platzes.

### Verzeichnis

Die Seite «Tauchplätze» ist der **Finder**: «Welcher Platz passt zu mir, oder wo ist der Platz XY?» Sie grenzt sich ab von der Prognose («Wo tauche ich heute?», Rangliste nach Index) und von der Gewässer-Übersicht («Was ist los an meinem See?», Stöbern nach See).

- **Kopf:** Titel, «{n} Plätze an {m} Gewässern», ein Satz zur Suche.
- **Suche:** ein Feld für Name, weitere Namen, Gemeinde und Gewässer (alle Sprachen, ohne Gross-/Kleinschreibung, Umlaute und Akzente: «zurich» findet «Zürichsee»). Ein GET-Formular, die Seite bleibt Static SSR.
- **In meiner Nähe:** Knopf nur mit JavaScript und Standort im Browser. Die Koordinate kommt auf zwei Dezimalen gerundet (rund 1 km) in die URL (`lat`, `lng`). Die Liste sortiert dann nach Distanz und zeigt sie. Ein Chip «In deiner Nähe ×» entfernt den Standort.
- **Eigenschaften als Chips:** Steilwand (Bewertung ≥ 3), Wrack (Wracks ≥ 3), Einsteiger (Schwierigkeit ≤ 2), Ausbildung, Fluss, Höhle. Mehrere gelten zusammen. Mit JavaScript schickt ein Klick das Formular gleich ab.
- **Sortierung:** Index heute (ohne Index am Ende), alphabetisch, mit Standort Distanz.
- **Liste:** 30 Plätze pro Seite mit Name (dazu dezent die weiteren Namen), Gemeinde · Gewässer, Eigenschaften und Tiefe, Index heute, Distanz (mit Standort), Badge «Gesperrt» oder «Warnung». Seitenlinks behalten Suche und Filter.
- **Karte:** alle Treffer, nahe Plätze als Gruppe mit Anzahl (Leaflet.markercluster). Desktop rechts neben der Liste und beim Scrollen sichtbar, schmal per Umschalter «Liste | Karte».
- **Leer:** «Keine Plätze gefunden» mit Link «Suche ohne Filter».
- **Gewässer:** keine Pillen mehr. Link «Nach Gewässer stöbern» zur Gewässer-Übersicht. Alte Links mit `?gewaesser={slug}` filtern weiter auf das Gewässer und zeigen es als entfernbaren Chip.
- URL-Parameter: `q`, `f` (mehrfach: `wall`, `wreck`, `beginner`, `training`, `river`, `cave`), `sort` (`index`, `name`, `distance`), `page`, `lat`, `lng`, `gewaesser`. Ungültige Werte (z.B. `page=abc`, `lat=x`, ein unbekanntes Gewässer) gelten als nicht gesetzt, nie als Fehler.
- **Grenze:** Die Suche lädt alle öffentlichen Plätze in den Speicher und filtert dort (`FindDiveSites`). Das reicht bis etwa 2000 Plätze. Darüber kommen Suche, Filter und Paging in die SQL-Query.
- **Später:** Output Cache für die Seite ohne Parameter (Tag `forecast`, wie die Startseite).

### Gewässer-Übersicht

Use-Case: stöbern nach See («Was ist los an meinem See?»). Die Suche nach Eigenschaften und Nähe gehört zu «Tauchplätze».

- **Gruppen** (Regel im Core, `GetWaterBodyGroups`):
  1. «Die grossen Seen»: Seen mit mindestens 8 veröffentlichten Plätzen, als grosse Kacheln (Art, Name, Zahl der Plätze).
  2. Die übrigen Seen nach Grossregion des BFS, Name der Region als Überschrift (Genferseeregion, Espace Mittelland, Nordwestschweiz, Zürich, Ostschweiz, Zentralschweiz, Tessin). Die Region eines Sees ist die des Kantons mit den meisten seiner Plätze, bei Gleichstand das Kantonskürzel alphabetisch zuerst.
  3. «Flüsse»: alle Flüsse.
  4. «Weitere Gewässer»: Seen ohne Platz mit Kanton.
  Leere Gruppen fehlen. In jeder Gruppe nach Zahl der Plätze, dann nach Name.
- Ausser den grossen Seen stehen die Gewässer als kompakte Liste (Name, Zahl der Plätze), auf breiten Bildschirmen in zwei Spalten.
- Oben eine Sprungnavigation zu den Gruppen (Anker `#largelakes`, `#region-ticino` …).
- Zwei Spalten ab 960 px: links die Gruppen, rechts die Karte mit allen beschrifteten Gewässern. Die Karte bleibt beim Scrollen sichtbar. Schmal: zuerst die Gruppen, darunter die Karte.
- Ein Klick auf eine Beschriftung öffnet die Gewässerseite.

### Gewässerseite

- Kopf mit See-Name und aktueller Oberflächentemperatur.
- Temperaturverlauf und Klarheitstrend.
- Alle Plätze des Sees mit Index, sortierbar.
- Karte mit allen Plätzen.

### Moderation (`/admin`)

- Nur Moderatoren und Admins. Eigenes Layout mit Seitenleiste, gleiche dunkle Farbwelt (MudBlazor).
- Navigation: Übersicht, Plätze, Vorschläge, für Moderatoren ohne Admin-Rolle «Konten» (sperren), dazu «Meine Vorschläge» (führt ins Profil der Website) und für Admins «Plattform».
- Platzliste mit Ausbaustufe als Badge. Schnellerfassung mit Karte.
- Details in `cms.md`.

### Meine Vorschläge (Profil)

- Im Profil der Website (`/{lang}/account/proposals`), Navigation des Kontos. Static SSR.
- Pro Vorschlag: Platz (Link auf die Platzseite, wenn öffentlich), Nummer, Status, eingereicht und geprüft am, eigener Kommentar, bei Ablehnung die Begründung in `--color-signal`. Link «Vergleich» auf die Versionen.
- «Zurückziehen» bei wartenden: ein Formular für die Liste, der Knopf trägt die Version.

### Plattform (`/platform`)

- Nur Admins. MudBlazor im selben Theme, aber Navigation oben (Übersicht, Konten, Prognose, Mail-Queue, Inhalt) und ein oranger Strich unter dem Kopf, damit der Bereich nicht mit der Moderation verwechselt wird. Links «Moderation» und «Zur Website».
- Kopf von Plattform und Moderation: Logo wie auf der Website, daneben «tauchplaetze.ch · Plattform» bzw. «· Moderation».
- Navigation: eine Zeile, vertikal zentriert, gleiche Abstände, kein Umbruch (auf schmalen Bildschirmen seitlich scrollbar). Der aktive Eintrag ist fett mit Unterstrich in `--color-signal`.
- Übersicht: vier Kacheln (Konten mit Zahl der gesperrten, offene Vorschläge, letzter Prognose-Lauf, Mail-Queue mit Dead-Letter).
- Liefert der Core nichts (zum Beispiel ohne Recht), zeigt jede Seite eine Meldung statt eines Ladebalkens.
- **Prognose:** Kacheln letzter Lauf (Status als farbiger Chip, Zeit, während des Laufs Fortschritt «40 von 318 Plätzen, 120 Calls bisher»), nächster Lauf nach Zeitplan, Bilanz der letzten 7 Tage (erfolgreich, Teilerfolg, fehlgeschlagen), mittlere Dauer. Darunter die Läufe als Tabelle (Start, Dauer, Status, Calls, Fehler), 20 pro Seite, Schalter «Nur Läufe mit Fehlern». Die Zahl der Fehler klappt die Gruppen auf («alplakes · 3 × Timeout»), jede Gruppe mit den betroffenen Keys und den vollständigen Meldungen. Läuft ein Lauf, lädt die Seite alle 5 s neu, sonst nicht.
- **Konten:** Liste mit Rollen und Sperre; der Name führt auf die Detailseite mit Angaben links und rechts Rollen (Schalter) und Aktionen (Knöpfe, gefährliche in Rot, mit Rückfrage). Details in `cms.md`, «Konten auf der Plattform».
- **Mail-Queue:** Zwei Kacheln mit Zählern (in der Queue, Dead-Letter, rot umrandet, sobald nicht leer), Knöpfe «Alle erneut senden» und «Alle löschen», darunter die Dead-Letter-Mails als aufklappbare Liste mit Details und Knöpfen pro Mail. Löschen fragt nach.

## Zustände

| Zustand | Darstellung |
|---|---|
| Prognose veraltet (> 30 h) | Hinweis mit Datenalter über der Rangliste |
| Kein Index für einen Platz (Datenstufe 1 ohne Satellit) | Wind und Regen ohne Index, Hinweis «nur Wetterdaten» |
| Tag im Ausblick | Wind und Regen ohne Index, Zusatz «Ausblick» |
| Satellitenwert älter als 14 Tage | Klarheit «unbekannt», Datum des letzten Bilds |
| Prognose ausgeschaltet (Datenstufe 0) | kein Bedingungsblock, nur Dokumentation |
| Keine Plätze im Umkreis | Vorschlag, den Umkreis zu vergrössern |
| Text nur auf Deutsch | dezenter Hinweis über dem Text |
| Automatisch übersetzt | dezenter Hinweis «automatisch übersetzt» |
| Platz archiviert | 410 oder Weiterleitung zur Gewässerseite |
| Platz gesperrt (Hinweis `Closure`) | Banner in Korallenrot oben auf der Platzseite, Badge im Verzeichnis, nicht in der Rangliste |

## Barrierefreiheit

- Kontrast mindestens 4.5:1 für Text, 3:1 für grosse Schrift und Bedienelemente.
- Echte Elemente: `<button>`, `<a href>`, `<input>` mit `<label>`.
- Diagramme mit Textalternative (`role="img"` und `aria-label` aus Ressourcen) und den Werten zusätzlich als Text.
- Farbe nie als einzige Information: Index immer auch als Zahl.
- Touch-Ziele mindestens 44 px.
- Tastaturbedienung vollständig. Sichtbarer Fokus.
- `prefers-reduced-motion` respektieren.

## Ton der Texte

- Du-Form in allen Sprachen, wo üblich (DE: du, FR: tu bzw. neutral, IT: tu, EN: neutral).
- Kurz, freundlich, von Tauchern für Taucher.
- Keine Übertreibungen bei der Prognose («gute Bedingungen erwartet», nicht «perfekte Sicht garantiert»).
