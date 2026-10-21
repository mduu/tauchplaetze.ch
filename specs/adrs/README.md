# Architecture Decision Records

Jeder Entscheid steht in einer eigenen Datei. Vorlage: [`_template.md`](_template.md).

| Thema | Entscheid | ADR |
|---|---|---|
| Frontend | Blazor Web App, öffentliche Seiten Static SSR, Admin Interactive Server | [001](001-blazor-ssr-und-interactive-server.md) |
| Code-Struktur | Vertical Slices, Commands und Queries getrennt, Core plus Fassade | [002](002-vertical-slices-und-cqs.md) |
| Datenbank | Azure SQL Database, Medien in Blob Storage | [003](003-azure-sql-free.md) |
| Prognose | Zuerst regelbasierter Tauch-Index, ML-Modell erst mit Labels | [004](004-prognose-regelbasiert-zuerst.md) |
| Sprachen | Deutsch Standard, dazu EN, FR, IT ab dem ersten Release | [005](005-mehrsprachigkeit.md) |
| Hosting | Azure App Service (Linux B1), Background Worker im selben Host | [006](006-app-service-mit-worker.md) |
| Wetter und Abfluss | Open-Meteo (Prognose), MeteoSchweiz SwissMetNet (Messung), BAFU über existenz.ch | [007](007-wetter-und-abflussquellen.md) |
| Inhalte | Alle können beitragen, Moderatoren geben frei. Versionen wie im Wiki, Inhalt in Blob Storage | [008](008-beitraege-mit-freigabe-und-versionen.md) |
| Fotos | SkiaSharp und MetadataExtractor, Bildgrössen nur als WebP ohne Metadaten, Original privat | [009](009-fotos-mit-skiasharp-und-webp.md) |
| Mails | Storage Queue mit Backoff und Dead-Letter, einheitliches Layout mit Textfassung | [010](010-mails-ueber-storage-queue.md) |
| Inhalt von Prod nach lokal | ZIP-Paket nur mit öffentlichen Inhalten, Import lokal ohne Löschen | [011](011-inhalt-von-prod-nach-lokal.md) |
| Mailversand in Prod | SMTP eines Schweizer Anbieters mit STARTTLS | [012](012-mail-ueber-hostpoint-smtp.md) |
| Design | Dunkles Farbschema («Kaltwasser-Nacht») | [design-system.md](../design-system.md) |
