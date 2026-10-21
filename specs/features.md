# Features

Die Features sind nach Phasen gruppiert. Die Phasen stehen in `roadmap.md`. Der Slice-Name entspricht dem Ordner unter `Features/`.

## Phase MVP

### Prognose («Wo tauchen?») – Slice `Forecasts`

- Startseite mit der Frage «Wo tauchen wir heute?».
- Tagesauswahl: heute plus die 4 folgenden Tage. Tage ohne Wasserdaten sind ein «Ausblick» mit Wind und Regen, ohne Index (`forecast.md`).
- «Beste Wahl»: der Platz mit dem höchsten Tauch-Index im Umkreis.
- Rangliste der Plätze im Umkreis mit Index, Balken und Begründung in einem Satz.
- Umkreis: Ort plus Radius. Der Ort kommt aus der Auswahl der Person oder (mit Zustimmung) aus dem Browser. Gespeichert nur im Browser.
- Ohne gewählten Ort gibt es keinen Umkreis: «Beste Wahl» und Rangliste gelten für die ganze Schweiz (Entscheid Marc, 17.10.2026).
- Seen-Übersicht: Oberflächentemperatur und Klarheitstrend pro See.
- Die Rangliste zeigt nur Plätze mit Index. Jeder Platz zeigt seine Datenstufe (genau, Gewässer, Wetter).

### Tauchplatz-Verzeichnis – Slice `DiveSites`

- Liste aller Plätze, filterbar nach See, Schwierigkeit und Zugang.
- Karte mit allen Plätzen (Leaflet mit swisstopo- oder OSM-Kacheln, siehe `open-questions.md`).
- Tauchplatzseite:
  - Hero mit Foto oder Unterwasser-Illustration, Name, See, Tags.
  - Tauchbedingungen heute: Index, Temperaturprofil mit Sprungschicht, Sicht-Indikator, Wind, Algen.
  - 5-Tage-Verlauf des Index.
  - Bewertungen 1–5: Schwierigkeit, Steilwand, UW-Leben, Wracks und Gegenstände, Strömung, Umgebung (siehe `dive-site.md`).
  - Unter Wasser (Text, Fotos), Gefahren (Text, Bilder), Tauchskizze.
  - Einstiege: einer oder mehrere, mit Pin auf der Karte, Text und Bildern.
  - Praktisch: Anfahrt und Parkplatz, Umgebung, Restaurants und Toiletten, Notfallnummern. Die nächste Füllstation kommt mit Phase 1.
  - Alle Angaben sind optional. Abschnitte ohne Inhalt fehlen.
  - Link zur Buddy-Gruppe der Region.
- Ein Platz der Ausbaustufe 0 zeigt Prognose und Karte. Dazu den Hinweis «Dieser Platz ist erst im Verzeichnis».
- Ein Platz mit Datenstufe 0 (Prognose ausgeschaltet) zeigt nur die Dokumentation.
- Karte mit Tiefenlinien (swissBATHY3D), wo vorhanden.

### Gewässer – Slice `WaterBodies`

- Gewässerseite (See oder Fluss) mit allen Plätzen, Temperaturverlauf und Übersichtskarte.

### Redaktion (CMS) – Slice `Editorial`

- Mitwirken mit Freigabe: Alle mit Konto schlagen Änderungen und neue Plätze vor. Moderatoren oder Admins geben frei. Erst dann ist der Inhalt online (`cms.md`). Im MVP mit ausgeschalteter Registrierung, ab Phase 1 offen.
- «Meine Daten» im Konto: alle gespeicherten Daten mit Links zu den eigenen Beiträgen und als JSON-Download (Auskunft nach nDSG).
- Versionen wie in einem Wiki, mit Autor und Approver. Öffentliche History pro Platz, ohne Namen ausser mit Einwilligung.
- Tauchplatz erfassen mit Pflichtfeldern Name, Koordinate (Pin auf Karte), See.
- Weitere Felder nach und nach ergänzen. Ausbaustufe wird automatisch berechnet.
- Revisionen pro Platz mit Vergleich und Wiederherstellen.
- Medien hochladen (Fotos), Bildlegende und Fotograf pro Sprache.
- Details in `cms.md`.

### Prognose-Import – Slice `Forecasts` (Worker)

- Täglicher Job: Daten von Alplakes, MeteoSchweiz und BAFU holen.
- Tauch-Index pro Platz und Tag berechnen und speichern.
- Bei Ausfall einer Quelle: letzte Werte behalten, Datenalter anzeigen.
- Details in `forecast.md` und `data-sources.md`.

### Mehrsprachigkeit – querschnittlich

- DE (Standard), EN, FR, IT ab dem ersten Release. Details in `i18n.md`.

## Phase 1

- **Meldungen (Slice `Reports`):** Sicht nach dem Tauchgang mit einem Klick melden (Sicht pro Tiefenband, optional Kommentar). Ohne Konto, mit Schutz gegen Missbrauch (Rate Limit, Captcha-freie Honeypot-Lösung).
- **Füllstationen (Slice `DiveShops`):** Tauchshops und Füllstationen. Die Platzseite zeigt die nächste Füllstation nach Distanz. Kostenlos, ohne Hervorhebung. Verwaltung im CMS.
- **Buddy-Gruppen (Slice `BuddyGroups`):** WhatsApp-Gruppen pro Region verwalten und auf Platz- und Gewässerseiten verlinken.
- **Registrierung für alle:** Selbstregistrierung für Mitwirkende. Admins ernennen Moderatoren. Mails bei Freigabe oder Ablehnung.
- **Videos:** Einbindung aus tauchvideos.ch.

## Phase 2

- **Eigenes Prognosemodell (Slice `Forecasts`):** ML-Modell mit Meldungen als Labels (siehe `forecast.md`).
- **Benachrichtigungen:** «Sag mir, wenn am Walensee gute Bedingungen sind» (E-Mail oder Push über PWA).
- **PWA:** installierbar, Favoriten offline.
- **Native Apps:** später möglich, falls die Web-App trägt.

## Bewusst nicht geplant

- Forum, Chat, Logbuch, Kommentare unter Plätzen.
- Konten für Besucher. Ein Konto braucht nur, wer beiträgt.
