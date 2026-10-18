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
/{lang}/gewaesser               Gewässer-Übersicht (Seen und Flüsse)
/{lang}/gewaesser/{slug}        Gewässerseite
/{lang}/ueber                   Über, Datenquellen, Impressum, Datenschutz
/{lang}/site/{slug}             kurzer Link auf einen Platz (Mails, CMS), 301 auf den Pfad der Sprache
/admin/…                        Redaktion (ohne Sprachpräfix)
```

Pfadsegmente pro Sprache: siehe `i18n.md`.

Hauptnavigation: Prognose · Tauchplätze · Gewässer · Buddy-Gruppen. Rechts: Konto (angemeldet als Aufklappmenü mit Profil, Redaktion und Abmelden, sonst «Anmelden») und Sprachwahl als Aufklappmenü (Knopf mit dem Kürzel der aktuellen Sprache, darunter Kürzel und Name jeder Sprache). Das Menü ist ein `<details>` und geht ohne JavaScript; mit JavaScript schliesst es bei Klick daneben und mit Escape. Bis 1000 px Breite stehen Logo, Konto und Sprache in der ersten Zeile und die Navigation darunter (auf dem Handy seitlich scrollbar); bis 720 px zeigt das Konto nur das Symbol.

## Seiten

### Startseite «Wo tauchen wir heute?»

Mockup: `design/start.dc.html`.

1. Kopf: Logo, Navigation, Sprachwahl.
2. Titel als Frage, abhängig vom gewählten Tag («… heute?», «… am Sonntag?»).
3. Kurzer Satz, woher die Prognose kommt.
4. Tagesauswahl als Pillen (heute plus 4 Tage). Tage ohne Index tragen den Zusatz «Ausblick». Umkreis-Auswahl («60 km ab Zürich · ändern»). Ohne gewählten Ort: «ganze Schweiz · Ort wählen».
5. **Beste Wahl** (grosse Karte):
   - Seequerschnitt als Illustration mit Oberflächentemperatur, Temperatur auf 20 m und Sprungschicht.
   - Name, Ort, See, Tauch-Index gross.
   - Kacheln: Oberfläche, auf 20 m, Sprungschicht, Wind.
   - Begründung in einem Satz.
   - Aktionen: «Zum Tauchplatz», «Buddy für {See} finden».
6. **Rangliste im Umkreis:** Rang, Name (Link), Ort · See, Balken, Index, Begründung.
7. Hinweis: «Der Index ist ein Indikator …».
8. «Keine Meldungen nötig. Der See verrät es selbst.»: vier Schritte (Seemodell, Satellit, Wetter und Zuflüsse, Community als Bonus).
9. Seen im Überblick: Kachel pro See mit Temperatur, Mini-Verlauf der Klarheit und Trendtext.
10. Buddy-Block (Akzentfarbe): Links zu den WhatsApp-Gruppen.
11. Fuss: Datenquellen, Hinweis «Tauche nie allein», Notfallnummern.

Tagesauswahl und Umkreis ändern die URL (`?date=`, `?lat=&lng=&r=`). Die Seite bleibt Static SSR.

### Tauchplatzseite

Mockup: `design/tauchplatz.dc.html`.

1. Hero: Titelbild (`HeroMediaId`, mit «Titelbild: {Fotograf}» oben rechts und Verlauf nach unten für die Lesbarkeit) oder die Unterwasser-Illustration, Brotkrumen «See › Gemeinde», Name gross, Tags (Wand, Wrack, Tiefe, Zugang, Schwierigkeit, ganzjährig).
2. **Hinweise** (falls gültig): Banner direkt unter dem Hero, schwerster zuerst. Farbe nach Art, Art und Enddatum als Label («Gesperrt · bis 01.04.2027»), darunter der Text. Siehe `dive-site.md`, «Hinweise».
3. **Tauchbedingungen:** Satz («Heute ein guter Tag.») und Index gross.
   - Temperaturprofil als Diagramm (Temperatur über Tiefe) mit markierter Sprungschicht.
   - Kacheln: Sicht-Indikator («Gut ab 16 m», mit Datum des Satellitenbilds), Wind, Algen. Kein Anzug-Hinweis: In der Schweiz taucht man fast immer trocken.
   - 5-Tage-Verlauf: Index, wo vorhanden. Tage im Ausblick zeigen Wind und Regen.
   - «Wetterverlauf» in derselben Kachel unter den fünf Tagen, kompakt wie bei MeteoSchweiz, im Raster von 2 Stunden: Wettersymbol nur beim ersten Schritt und bei jedem Wechsel, dazwischen eine gepunktete Linie. Wind (Pfeil in Windrichtung, km/h) alle 6 Stunden. Sonne in Minuten pro Stunde, Temperaturlinie und Regenbalken (Mittel über 2 Stunden). Nacht dunkler, vergangene Stunden schattiert, Linie bei «jetzt». Wird die Kachel zu schmal, scrollt die Grafik in der Kachel, nie die Seite. Textalternative: Zusammenfassung pro Tag (Temperatur, Regen, Wind). Nur, wenn der Lauf Stundenwerte hat.
   - Anordnung auf dem Desktop: links oben die drei Kacheln, darunter das Temperaturprofil (ohne Profil nur die Kacheln), rechts die fünf Tage mit dem Wetterverlauf. Schmal: Kacheln, Profil, fünf Tage.
   - Quellen mit Links: unter dem Wetterverlauf «Wetter: MeteoSchweiz … über Open-Meteo», unter dem Block «Daten: Eawag Alplakes, MeteoSchweiz über Open-Meteo, BAFU». Text und Adressen aus den Ressourcen (Adresse von MeteoSchweiz und BAFU in der Sprache der Seite), Links öffnen in einem neuen Tab.
   - Datenstufe als kurzer Hinweis: «Profil am Platz», «für den ganzen See», «Messstation {Name}» oder «nur Wetter».
4. **Der Platz:** Bewertungen als kompakter Block (Schwierigkeit, Steilwand, UW-Leben, Wracks und Gegenstände, Strömung, Umgebung; nur bewertete Skalen, Wert immer auch als Zahl und Kurztext). Pro Skala ein «i»: Es zeigt die Legende der Skala (alle fünf Stufen mit Bedeutung aus `dive-site.md`, die aktuelle hervorgehoben). Mit der Maus beim Darüberfahren, auf Touch und mit der Tastatur per Klick, Escape oder Klick daneben schliesst. Ohne Bewertung fehlt der Block. Darunter **Fotos**: Galerie nach Abschnitt (Unter Wasser, Über Wasser, Anfahrt und Parkplatz, Einstiege, Gefahren), Raster mit Kacheln ab 280 px, `srcset` mit allen Bildgrössen, `loading=lazy`, Alternativtext. Unter jedem Foto Legende, «Foto: {Fotograf}» (mit Link, falls erfasst) und Aufnahmetiefe. Ein Klick öffnet die grösste Bildgrösse in einem neuen Tab. Ohne öffentliche Fotos fehlt der Abschnitt.
5. **Gefahren:** Text und Bilder. Gut sichtbar, nicht eingeklappt.
6. **Lage und Einstiege:** Karte (Landeskarte oder Luftbild) mit allen Einstiegen und Orten, beschriftet mit Name und Art. Sind Einstiege oder Orte erfasst, fehlt der Platzpunkt: Sie sind genauer und überdecken sich so nicht mit ihm. Ohne sie zeigt die Karte den Platz (Zoom 17, sonst passend auf alle Punkte bis Zoom 18). Die Karte erscheint immer (Titel ohne Einstiege «Lage»). Sie steht immer als eigener Abschnitt über die ganze Breite unter den Tauchbedingungen, 540 px hoch, schmal 360 px. Darunter die Einstiege als Liste. Auf der Karte liegen die Ebenen «Tiefenlinien» (swissBATHY3D, ab Zoom 13) und «Skizze» (falls vorhanden), beide an und abschaltbar. Mit Skizze steht darunter «Skizze, nicht zur Navigation» (`dive-site.md`, «Tauchskizze»). Später: pro Einstieg Text und Bilder.
7. **Praktisch:** Koordinaten mit Link auf die Landeskarte, «Hinfahren» mit Routenlinks zu Google Maps und Apple Karten (die meisten navigieren damit zum Platz), Anfahrt und Parkplatz (Text, Bilder), Umgebung (Bewertung, Text, Bilder), Restaurants und Toiletten, nächste Füllstation, Notfall.
8. **Letzte Meldungen** (Phase 1, falls vorhanden) mit Button «Sicht melden».
9. Buddy-Block für die Region.
10. Fuss mit Quellen, «Zuletzt geändert am …», Links «Versionen» und «Platz bearbeiten» (führt zur Anmeldung).

Alle Angaben in den Abschnitten 4 bis 7 sind optional (siehe `dive-site.md`). Ein Abschnitt ohne Inhalt fehlt, ausser der Karte in Abschnitt 6. Fehlen die Abschnitte 4 bis 7 ganz, erscheint ein freundlicher Hinweis: «Dieser Platz ist erst im Verzeichnis. Kennst du ihn? Schreib uns.» (mit Kontaktlink).

### Verzeichnis

- Liste und Karte nebeneinander (Desktop), umschaltbar (Handy).
- Filter: See, Datenstufe, Schwierigkeit (Einsteiger, Fortgeschrittene, Erfahrene), Zugang, Eigenschaften (Wand und Wrack ab Bewertung 3, Fluss …).
- Jede Zeile zeigt den heutigen Index als kleinen Wert.
- Gilt heute eine Sperrung oder Warnung, trägt die Zeile ein Badge «Gesperrt» bzw. «Warnung».

### Gewässer-Übersicht

Use-Case: stöbern nach See («Was ist los an meinem See?»). Die Suche nach Eigenschaften und Nähe gehört zu «Tauchplätze».

- **Gruppen** (Regel im Core, `GetWaterBodyGroups`):
  1. «Die grossen Seen»: Seen mit mindestens 8 veröffentlichten Plätzen, als grosse Kacheln (Art, Name, Zahl der Plätze).
  2. Die übrigen Seen nach Grossregion des BFS (Genferseeregion, Espace Mittelland, Nordwestschweiz, Zürich, Ostschweiz, Zentralschweiz, Tessin). Die Region eines Sees ist die des Kantons mit den meisten seiner Plätze, bei Gleichstand das Kantonskürzel alphabetisch zuerst.
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

### Redaktion (Admin)

- Eigenes Layout mit Seitenleiste, gleiche dunkle Farbwelt.
- Platzliste mit Ausbaustufe als Badge. Schnellerfassung mit Karte.
- Details in `cms.md`.

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
