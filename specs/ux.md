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
/{lang}/seen                    Seen-Übersicht
/{lang}/seen/{lake}             Seeseite
/{lang}/ueber                   Über, Datenquellen, Impressum, Datenschutz
/admin/…                        Redaktion (ohne Sprachpräfix)
```

Pfadsegmente pro Sprache: siehe `i18n.md`.

Hauptnavigation: Prognose · Tauchplätze · Seen · Buddy-Gruppen. Rechts: Sprachwahl (DE · EN · FR · IT).

## Seiten

### Startseite «Wo tauchen wir heute?»

Mockup: `design/start.dc.html`.

1. Kopf: Logo, Navigation, Sprachwahl.
2. Titel als Frage, abhängig vom gewählten Tag («… heute?», «… am Sonntag?»).
3. Kurzer Satz, woher die Prognose kommt.
4. Tagesauswahl als Pillen (heute plus folgende Tage). Umkreis-Auswahl («60 km ab Zürich · ändern»).
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

1. Hero: Foto (oder Unterwasser-Illustration), Brotkrumen «See › Gemeinde», Name gross, Tags (Wand, Tiefe, Zugang, Schwierigkeit, ganzjährig).
2. **Tauchbedingungen:** Satz («Heute ein guter Tag.») und Index gross.
   - Temperaturprofil als Diagramm (Temperatur über Tiefe) mit markierter Sprungschicht.
   - Kacheln: Sicht-Indikator («Gut ab 16 m»), Anzug-Hinweis, Wind, Algen.
   - 5-Tage-Verlauf des Index.
3. **Der Platz:** Beschreibung, Fotos (Galerie), Fotograf mit Link.
4. **Tauchplatzskizze:** Einstieg, Tiefenlinien, Highlights. Hinweis «Skizze, nicht zur Navigation».
5. **Praktisch:** Parkierung, Einstieg, Achtung (Schiffsverkehr, Verbote), nächste Füllstation (Partner), Notfall.
6. **Letzte Meldungen** (Phase 1, falls vorhanden) mit Button «Sicht melden».
7. Buddy-Block für die Region.
8. Fuss mit Quellen.

Je nach Ausbaustufe (siehe `cms.md`) fehlen Abschnitte 3 bis 5. Dann erscheint ein freundlicher Hinweis: «Dieser Platz ist erst im Verzeichnis. Kennst du ihn? Schreib uns.» (mit Kontaktlink).

### Verzeichnis

- Liste und Karte nebeneinander (Desktop), umschaltbar (Handy).
- Filter: See, Schwierigkeit, Zugang, Eigenschaften (Wand, Wrack, Fluss …).
- Jede Zeile zeigt den heutigen Index als kleinen Wert.

### Seeseite

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
| Keine Prognose für einen Platz | «Keine Prognose» statt Index, Begründung (z.B. See ohne Modell) |
| Keine Plätze im Umkreis | Vorschlag, den Umkreis zu vergrössern |
| Text nur auf Deutsch | dezenter Hinweis über dem Text |
| Automatisch übersetzt | dezenter Hinweis «automatisch übersetzt» |
| Platz archiviert | 410 oder Weiterleitung zur Seeseite |

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
