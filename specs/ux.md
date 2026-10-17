# UX

Die Oberfläche beantwortet zuerst die Frage «Wo soll ich tauchen gehen?». Danach liefert sie alles, was man für den Tauchgang braucht. Das Erscheinungsbild ist dunkel («Kaltwasser-Nacht»), siehe `design-system.md`. Die Mockups liegen in `design/`.

## Grundsätze

- **Antwort zuerst.** Die Startseite zeigt sofort die beste Wahl und die Rangliste. Keine Einleitung, kein Marketingtext davor.
- **Mobile first.** Viele schauen am Morgen auf dem Handy oder am Parkplatz.
- **Ehrlich.** Prognose als Indikator kennzeichnen. Datenalter zeigen. Quellen nennen.
- **Bilder tragen.** Unterwasserfotos sind das stärkste Element. Platzhalter nur, solange keine Fotos da sind.
- **Sicherheit sichtbar.** Notfallnummern und Hinweise (Schiffsverkehr, Verbote) auf jeder Platzseite.
- **Keine Konten für Besucher.** Favoriten und Standort bleiben im Browser.

## Informationsarchitektur

```
/{lang}                         Start = Prognose «Wo tauchen wir heute?»
/{lang}/prognose                (Alias der Startseite, Ziel von sichtweiten.ch)
/{lang}/tauchplaetze            Verzeichnis (Liste + Karte, Filter)
/{lang}/tauchplaetze/{slug}     Tauchplatzseite
/{lang}/gewaesser               Gewässer-Übersicht (Seen und Flüsse)
/{lang}/gewaesser/{slug}        Gewässerseite
/{lang}/ueber                   Über, Datenquellen, Impressum, Datenschutz
/admin/…                        Redaktion (ohne Sprachpräfix)
```

Pfadsegmente pro Sprache: siehe `i18n.md`.

Hauptnavigation: Prognose · Tauchplätze · Gewässer · Buddy-Gruppen. Rechts: Sprachwahl (DE · EN · FR · IT).

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

1. Hero: Foto (oder Unterwasser-Illustration), Brotkrumen «See › Gemeinde», Name gross, Tags (Wand, Wrack, Tiefe, Zugang, Schwierigkeit, ganzjährig).
2. **Tauchbedingungen:** Satz («Heute ein guter Tag.») und Index gross.
   - Temperaturprofil als Diagramm (Temperatur über Tiefe) mit markierter Sprungschicht.
   - Kacheln: Sicht-Indikator («Gut ab 16 m», mit Datum des Satellitenbilds), Anzug-Hinweis, Wind, Algen.
   - 5-Tage-Verlauf: Index, wo vorhanden. Tage im Ausblick zeigen Wind und Regen.
   - Datenstufe als kurzer Hinweis: «Profil am Platz», «für den ganzen See», «Messstation {Name}» oder «nur Wetter».
3. **Der Platz:** Bewertungen als kompakter Block (Schwierigkeit, Steilwand, UW-Leben, Wracks und Gegenstände, Strömung, Umgebung; nur bewertete Skalen, Wert immer auch als Zahl und Kurztext). Darunter «Unter Wasser»: Text, Fotos (Galerie), Fotograf mit Link.
4. **Gefahren:** Text und Bilder. Gut sichtbar, nicht eingeklappt.
5. **Einstiege und Tauchplatzskizze:** Karte mit allen Einstiegen, pro Einstieg Name, Text und Bilder. Skizze mit Tiefenlinien und Highlights. Hinweis «Skizze, nicht zur Navigation».
6. **Praktisch:** Anfahrt und Parkplatz (Text, Bilder), Umgebung (Bewertung, Text, Bilder), Restaurants und Toiletten, nächste Füllstation, Notfall.
7. **Letzte Meldungen** (Phase 1, falls vorhanden) mit Button «Sicht melden».
8. Buddy-Block für die Region.
9. Fuss mit Quellen.

Alle Angaben in den Abschnitten 3 bis 6 sind optional (siehe `dive-site.md`). Ein Abschnitt ohne Inhalt fehlt. Fehlen die Abschnitte 3 bis 6 ganz, erscheint ein freundlicher Hinweis: «Dieser Platz ist erst im Verzeichnis. Kennst du ihn? Schreib uns.» (mit Kontaktlink).

### Verzeichnis

- Liste und Karte nebeneinander (Desktop), umschaltbar (Handy).
- Filter: See, Datenstufe, Schwierigkeit (Einsteiger, Fortgeschrittene, Erfahrene), Zugang, Eigenschaften (Wand und Wrack ab Bewertung 3, Fluss …).
- Jede Zeile zeigt den heutigen Index als kleinen Wert.

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
