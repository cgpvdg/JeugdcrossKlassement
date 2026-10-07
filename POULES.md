# Poules

Wedstrijden en klassementen worden afzonderlijk berekend voor Noord, Midden en Zuid. Elke poule heeft maximaal drie wedstrijden. Kies bij iedere uitslagupload de poule; een bestaande wedstrijd wordt herkend aan de datum binnen die poule.

Bestaande lokale wedstrijden en oude imports zonder poule worden ingedeeld bij Noord. De bestaande openbare gegevens horen eveneens bij Noord. Midden en Zuid zijn leeg tot wedstrijden worden toegevoegd. Naamkeuzes en correcties zijn per poule opgeslagen.

De competitie-export heeft versie 2:

```json
{
  "generatedAt": "2026-10-06T00:00:00.000Z",
  "version": 2,
  "poules": [
    { "naam": "Noord", "wedstrijden": [], "individueelKlassement": [], "ploegenKlassement": [] },
    { "naam": "Midden", "wedstrijden": [], "individueelKlassement": [], "ploegenKlassement": [] },
    { "naam": "Zuid", "wedstrijden": [], "individueelKlassement": [], "ploegenKlassement": [] }
  ]
}
```

De website leest ook de oude competitie-export als Noord. De planning in site-content.json staat onder wedstrijdOverzicht.poules: elke poule heeft een naam en drie wedstrijden. De gezamenlijke finale staat apart onder wedstrijdOverzicht.finale, zonder poule. Beide wedstrijdweergaven gebruiken deze planning, ook als er nog geen uitslagen zijn. De planning bevat de wedstrijden van seizoen 2026–2027. Links die nog niet bekend zijn, blijven leeg en worden niet getoond.

Bij de competitie-export kun je één of meerdere poules selecteren. Standaard zijn alle poules geselecteerd; export zonder poules is niet mogelijk. Het JSON-formaat blijft versie 2 met uitsluitend de geselecteerde poules.

Jeugdcrossdata haalt bij een upload naar competitie-data.json eerst de huidige GitHub-versie op. Alleen de pouleobjecten met een naam die in de export voorkomt worden geheel vervangen (of toegevoegd als ze nog ontbreken). Andere poules en alle bestaande velden buiten poules blijven behouden, ook generatedAt en version. De oude inhoud wordt gearchiveerd en de samengevoegde inhoud wordt met de opgehaalde SHA geüpload. Bij ongeldige of dubbele poules wordt niets geüpload. site-content.json wordt opgehaald en bewerkt via de vaste velden in de app.

Jeugdcrossdata: competitie-data.json is uitsluitend als exportbestand uploadbaar. site-content.json wordt vanuit GitHub geladen in een formulier met vaste velden, gegroepeerd per poule en wedstrijd. Handmatige bestandsupload en opslaan als download zijn hiervoor verwijderd. Alleen bestaande scalarwaarden kunnen wijzigen; properties, arraygroottes en waardetypen blijven behouden. Voor versturen wordt de actuele GitHub-versie gecontroleerd. Andere gelijktijdige waardewijzigingen blijven behouden; wijzigingen aan dezelfde waarde of de structuur geven een fout.
