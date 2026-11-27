# Story 001: Seite «Partner»

**Status:** 👷‍♂️ gebaut und deployt am 27.11.2026, aber ausgeblendet (`Partner:Visible` = `false`), bis der SUSV den Wortlaut freigibt (Entscheid Marc, 27.11.2026).

## Ziel

Besucher sehen auf einer Seite «Partner», wer das Tauchen in der Schweiz möglich macht. Der SUSV (Schweizer Unterwasser-Sport-Verband, französisch und italienisch FSSS) ist der erste Eintrag, mit Aufruf zur Mitgliedschaft.

Wunsch des SUSV (November 2026): Möglichst alle Tauchenden sollen Mitglied werden, damit der Verband seine Arbeit weiter machen kann.

## Umfang

- Seite `/de/partner`, `/en/partners`, `/fr/partenaires`, `/it/partner`. Aufbau in `ux.md`, «Partner».
- Link im Fuss zwischen «Echolot» und «Impressum». Eintrag in der Sitemap (`seo.md`).
- Eintrag SUSV: Logo mit dem Namen des Verbands in drei Sprachen, kurzer Text zur Arbeit des Verbands, Links «Mitglied werden» und «Tauchplatzförderung des SUSV» auf susv.ch in der Sprache der Seite.
- Zeile «An {n} Plätzen zeigen wir Hinweise und Projekte des SUSV.» Sie erscheint erst mit bestätigten Einträgen (Story 002).
- Einträge sind nie bezahlt (`vision.md`, «Finanzierung»).

Nicht in dieser Story: weitere Partner.

## Akzeptanzkriterien

1. Die Seite ist in vier Sprachen online, im Fuss verlinkt und in der Sitemap.
2. Der SUSV ist der erste Eintrag, mit beiden Links.
3. Ohne bestätigte Einträge des SUSV fehlt die Zeile mit der Zahl der Plätze.
4. Das Echolot hat einen Eintrag in den Neuigkeiten.

## Abhängigkeiten und offene Punkte

| Punkt | Nächster Schritt |
|---|---|
| **Wortlaut** des Eintrags. Der Text auf der Seite ist ein Entwurf in vier Sprachen (`PartnerStrings.resx`). | Marc stimmt ihn mit dem SUSV ab. |
| **Englische Links:** susv.ch hat keine englischen Seiten. | Englisch verlinkt die deutsche Fassung mit dem Zusatz «(in German)». |
| **Gegenlink:** Marc hat den SUSV um einen Link auf tauchplaetze.ch gebeten. | Antwort abwarten. |

Hängt von keiner anderen Story ab. Die Zeile mit der Zahl der Plätze (Kriterium 3) kommt mit den bestätigten Einträgen (Story 002) und ihrer Anzeige (Stories 003 und 004). Bis dahin gibt es keine bestätigten Einträge, die Zeile fehlt, und die Seite liest keine Daten.

## Umsetzung

- Seite `Features/Partner/PartnerPage.razor`, Texte in `Localization/PartnerStrings.resx`, Link im Fuss (`Footer_Partner` in `SharedStrings.resx`), Sitemap (`Sitemap.cs`).
- Logo aus `assets/partner/susv/susv-logo-mit-namen.jpg`, verkleinert auf 600 px als `wwwroot/img/partner/susv-logo.webp` und `.png`.
- Links auf susv.ch: Einzelmitglied und Tauchplatzförderung, je in der Sprache der Seite.

## Freischalten

Sobald der SUSV den Wortlaut freigibt:

1. `Partner:Visible` auf `true` setzen (`appsettings.json`). Dann erscheint die Seite, der Link im Fuss und der Eintrag in der Sitemap. Die Seite «Tauchen und Schifffahrt» verlinkt dann auf die Partnerseite statt auf susv.ch.
2. Die Neuigkeit fürs Echolot in `news.json` eintragen, mit dem Datum des Freischaltens:
   - de: «Neue Seite «Partner»» – «Die Seite «Partner» zeigt, wer das Tauchen in der Schweiz möglich macht. Der erste Partner ist der Schweizer Unterwasser-Sport-Verband SUSV.»
   - en: «New page «Partners»» – «The page «Partners» shows who makes diving in Switzerland possible. The first partner is the Swiss Underwater Sports Federation SUSV.»
   - fr: «Nouvelle page « Partenaires »» – «La page « Partenaires » montre qui rend la plongée possible en Suisse. Le premier partenaire est la Fédération Suisse de Sports Subaquatiques FSSS.»
   - it: «Nuova pagina «Partner»» – «La pagina «Partner» mostra chi rende possibile l’immersione in Svizzera. Il primo partner è la Federazione Svizzera di Sport Subacquei FSSS.»
