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
