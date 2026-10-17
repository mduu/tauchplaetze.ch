# Ubiquitous Language

Diese Liste legt die Fachbegriffe fest. Code verwendet den englischen Begriff. Die Oberfläche verwendet die Übersetzungen dieser Tabelle. Claude Code nimmt diese Begriffe beim Übersetzen der `.resx`-Dateien. Weicht ein Begriff ab, wird zuerst diese Liste angepasst.

## Kernbegriffe

| Deutsch (UI) | Code | English (UI) | Français (UI) | Italiano (UI) | Bedeutung |
|---|---|---|---|---|---|
| Tauchplatz | `DiveSite` | dive site | site de plongée | sito d'immersione | Ein Ort, an dem man taucht. Kann See oder Fluss sein. |
| See | `Lake` | lake | lac | lago | Gewässer, dem ein Tauchplatz angehört. Flüsse sind ebenfalls `Lake` mit `WaterBodyType = River`. |
| Gewässer | `WaterBody` | water body | plan d'eau | specchio d'acqua | Oberbegriff für See und Fluss (nur in Texten, im Code `Lake`). |
| Tauchgang | `Dive` | dive | plongée | immersione | Ein einzelner Tauchgang. |
| Prognose | `Forecast` | forecast | prévision | previsione | Berechnete Bedingungen pro Platz und Tag. |
| Tauch-Index | `DiveIndex` | dive index | indice de plongée | indice d'immersione | Zahl von 0 bis 10. Fasst die Bedingungen zusammen. |
| Tauchbedingungen | `Conditions` | dive conditions | conditions de plongée | condizioni d'immersione | Alle Werte einer Prognose zusammen. |
| Sprungschicht | `Thermocline` | thermocline | thermocline | termoclino | Schicht mit starkem Temperaturabfall. Darunter ist die Sicht oft besser. |
| Sichtweite | `Visibility` | visibility | visibilité | visibilità | Wie weit man unter Wasser sieht. |
| Sicht-Indikator | `VisibilityIndicator` | visibility indicator | indicateur de visibilité | indicatore di visibilità | Geschätzte Sicht aus Daten. Keine Messung. |
| Secchi-Tiefe | `SecchiDepth` | Secchi depth | profondeur de Secchi | profondità di Secchi | Gemessene Sichttiefe mit Secchi-Scheibe. |
| Oberflächentemperatur | `SurfaceTemperature` | surface temperature | température de surface | temperatura superficiale | Wassertemperatur bei 0 m. |
| Temperatur auf X m | `TemperatureAtDepth` | temperature at X m | température à X m | temperatura a X m | Wassertemperatur in einer Tiefe. |
| Temperaturprofil | `TemperatureProfile` | temperature profile | profil de température | profilo di temperatura | Temperatur über alle Tiefen. |
| Algen | `Algae` | algae | algues | alghe | Algenaktivität aus Satellitendaten. |
| Zufluss | `Inflow` | inflow | affluent | immissario | Fluss, der in den See mündet. |
| Abfluss (Menge) | `Discharge` | discharge | débit | portata | Wassermenge eines Flusses (BAFU). |
| Meldung | `Report` | report | signalement | segnalazione | Beobachtung einer Person nach einem Tauchgang. |
| Ausbaustufe | `ContentLevel` | completeness | niveau de détail | livello di dettaglio | Wie vollständig ein Tauchplatz erfasst ist (0 bis 2). |
| Revision | `Revision` | revision | révision | revisione | Gespeicherte Version eines Tauchplatzes. |
| Partnerplatz | `PartnerListing` | partner | partenaire | partner | Bezahlter Eintrag eines Tauchshops bei einem Platz. |
| Buddy-Gruppe | `BuddyGroup` | buddy group | groupe de binômes | gruppo buddy | Verlinkte WhatsApp-Gruppe einer Region. |

## Tauchplatz-Details

| Deutsch (UI) | Code | English (UI) | Français (UI) | Italiano (UI) |
|---|---|---|---|---|
| Einstieg | `EntryPoint` | entry point | point d'entrée | punto d'entrata |
| Ausstieg | `ExitPoint` | exit point | point de sortie | punto d'uscita |
| Uferzugang | `ShoreAccess` | shore access | accès depuis la rive | accesso da riva |
| Bootstauchgang | `BoatDive` | boat dive | plongée depuis un bateau | immersione da barca |
| Felswand / Steilwand | `Wall` | wall | tombant | parete |
| Wrack | `Wreck` | wreck | épave | relitto |
| Flusstauchgang | `RiverDive` | river dive | plongée en rivière | immersione in fiume |
| Maximaltiefe | `MaxDepth` | maximum depth | profondeur maximale | profondità massima |
| Schwierigkeit | `Difficulty` | difficulty | difficulté | difficoltà |
| Einsteiger (Schwierigkeit 1–2) | `Beginner` | beginner | débutant | principiante |
| Fortgeschrittene (Schwierigkeit 3) | `Advanced` | advanced | avancé | avanzato |
| Erfahrene (Schwierigkeit 4–5) | `Expert` | experienced | expérimenté | esperto |
| Bewertung | `Rating` | rating | évaluation | valutazione |
| UW-Leben | `MarineLife` | underwater life | vie aquatique | vita subacquea |
| Wracks und Gegenstände | `Wrecks` | wrecks and objects | épaves et objets | relitti e oggetti |
| Strömung | `Current` | current | courant | corrente |
| Umgebung | `Surroundings` | surroundings | environs | dintorni |
| Anfahrt | `Approach` | getting there | accès | come arrivare |
| Unter Wasser | `Underwater` | underwater | sous l'eau | sott'acqua |
| Gefahren | `Hazards` | hazards | dangers | pericoli |
| Restaurants und Toiletten | `Facilities` | restaurants and toilets | restaurants et toilettes | ristoranti e servizi igienici |
| Parkierung | `Parking` | parking | parking | parcheggio |
| Tauchverbot | `DivingBan` | diving ban | interdiction de plongée | divieto d'immersione |
| Schiffsverkehr | `BoatTraffic` | boat traffic | trafic de bateaux | traffico di battelli |
| Taucherflagge | `DiveFlag` | dive flag | pavillon de plongée | bandiera subacquea |
| Boje | `Buoy` | buoy | bouée | boa |
| Trockentauchanzug | `Drysuit` | drysuit | combinaison étanche | muta stagna |
| Nasstauchanzug | `Wetsuit` | wetsuit | combinaison humide | muta umida |
| Füllstation | `FillingStation` | filling station | station de gonflage | stazione di ricarica |
| Tauchshop | `DiveShop` | dive shop | magasin de plongée | negozio di subacquea |
| Tauchskizze | `SiteSketch` | site sketch | croquis du site | schizzo del sito |
| Notfall | `Emergency` | emergency | urgence | emergenza |

## Seen (amtliche Namen pro Sprache)

| Deutsch | English | Français | Italiano |
|---|---|---|---|
| Zürichsee | Lake Zurich | lac de Zurich | lago di Zurigo |
| Genfersee | Lake Geneva | Léman | lago Lemano |
| Vierwaldstättersee | Lake Lucerne | lac des Quatre-Cantons | lago dei Quattro Cantoni |
| Bodensee | Lake Constance | lac de Constance | lago di Costanza |
| Thunersee | Lake Thun | lac de Thoune | lago di Thun |
| Brienzersee | Lake Brienz | lac de Brienz | lago di Brienz |
| Neuenburgersee | Lake Neuchâtel | lac de Neuchâtel | lago di Neuchâtel |
| Bielersee | Lake Biel | lac de Bienne | lago di Bienne |
| Walensee | Lake Walen | lac de Walenstadt | lago di Walenstadt |
| Zugersee | Lake Zug | lac de Zoug | lago di Zugo |
| Lago Maggiore | Lake Maggiore | lac Majeur | Lago Maggiore |
| Luganersee | Lake Lugano | lac de Lugano | Ceresio (lago di Lugano) |

Seenamen sind Daten in der Tabelle `Lakes` (eine Spalte pro Sprache), keine Ressourcen.

## Schreibregeln für Übersetzungen

- Französisch: Schweizer Gebrauch. «Septante», «huitante», «nonante» nur in ausgeschriebenen Zahlen. Zahlen sonst in Ziffern.
- Italienisch: Schweizer Gebrauch (Tessin). Kurze, direkte Formulierungen.
- Englisch: britische Schreibweise (metres, colour).
- Temperaturen immer mit Einheit: `14.2 °C` (DE/EN/IT), `14,2 °C` (FR). Die Kultur bestimmt das Dezimalzeichen.
- Tiefen in Metern: `25 m`.
