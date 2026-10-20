# Vorschlag: Zuflussstationen der Seen

Stand 20.10.2026, für `rules-1.4` (Teilwert «Zuflüsse» und Korrektur der Klarheit mit der Abflussvorhersage). Quelle: Stationsliste des BAFU über existenz.ch (`apiv1/hydro/locations`, Abruf 20.10.2026). Im Seed als Rolle `Inflow` eingetragen. Marc prüft die Liste.

Pro See zählt der höchste Quotient «Abfluss / Median der letzten 30 Tage» aller Zuflüsse.

| See | Station | Nummer | Gewässer | Begründung |
|---|---|---|---|---|
| Zürichsee | Weesen, Biäsche | 2104 | Linth | bestehend; Hauptzufluss über den Walensee |
| Zugersee | Zug, Letzi | 2477 | Lorze | bestehend; Zufluss aus dem Ägerisee |
| Bielersee | Hagneck | 2085 | Aare | Hagneckkanal, Hauptzufluss |
| Brienzersee | Brienzwiler | 2019 | Aare | Hauptzufluss am Ostende |
| Brienzersee | Gsteig | 2109 | Lütschine | Gletscherbach, trübt nach Regen bei Bönigen |
| Thunersee | Ringgenberg, Goldswil | 2457 | Aare | Abfluss des Brienzersees, Zufluss bei Interlaken |
| Thunersee | Hondrich | 2469 | Kander | Kander mit Simme, trübt stark nach Regen |
| Genfersee | Porte du Scex | 2009 | Rhône | Hauptzufluss, trägt Gletschertrübe |
| Hallwilersee | Hitzkirch, Richensee | 2416 | Aabach | Zufluss aus dem Baldeggersee |
| Greyerzersee | Broc, Château d'en bas | 2160 | Sarine | Hauptzufluss am Südende |
| Schiffenensee | Fribourg | 2119 | Sarine | Hauptzufluss |
| Lago Maggiore | Riazzino | 2068 | Ticino | Hauptzufluss bei Magadino |
| Lago Maggiore | Locarno, Solduno | 2368 | Maggia | Zufluss bei Locarno, trübt nach Gewittern |
| Luganersee | Agno | 2629 | Vedeggio | Zufluss im Westbecken |
| Luganersee | Pregassona | 2321 | Cassarate | Zufluss bei Lugano |
| Neuenburgersee | Boudry | 2480 | Areuse | Zufluss am Westufer |
| Neuenburgersee | Orbe, Le Chalet | 2378 | Orbe | Zufluss bei Yverdon |
| Vierwaldstättersee | Seedorf | 2056 | Reuss | Hauptzufluss in den Urnersee |
| Vierwaldstättersee | Buochs, Flugplatz | 2481 | Engelberger Aa | Zufluss nahe Buochs und Ennetbürgen (viele Plätze) |
| Walensee | Mollis, Linthbrücke | 2372 | Linth | Escherkanal, Hauptzufluss |
| Walensee | Mels | 2426 | Seez | Zufluss am Ostende |

## Bewusst ohne Station

- **Sarnersee:** 2102 Sarner Aa – Sarnen misst den Abfluss des Sees, nicht den Zufluss.
- **Vierwaldstättersee, Muota (2084 Ingenbohl):** möglicher dritter Zufluss bei Brunnen. Weggelassen, damit ein Zufluss am Rand nicht den ganzen See abwertet.
- **Ägerisee, Baldeggersee, Greifensee, Pfäffikersee, Sihlsee, Türlersee, Engadiner Seen, Bergseen und Stauseen:** keine BAFU-Station am Zufluss. Dort zählt weiter nur der Regen.
