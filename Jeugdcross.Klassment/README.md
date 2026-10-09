# Jeugdcross.Klassment

Windows-app in C# / WPF (.NET 10), met WPF UI 4.3.0 en ClosedXML. Het project is toegevoegd aan `JeugdcrossKlassement.slnx`. De bestaande projecten blijven beschikbaar.

## Gebruik

1. Kies Noord, Midden of Zuid in het linkermenu.
2. Importeer een TXT-uitslagenbestand via **Uitslagen.nl importeren**, rechts bovenaan bij **Wedstrijden**. De kopregels leveren wedstrijdnaam en datum. Per poule zijn maximaal drie verschillende wedstrijddatums toegestaan. Een herimport op dezelfde datum vraagt bevestiging voordat de uitslag wordt vervangen.
3. Categorieën staan boven de kolomregel `Plaa Naam … Woonplaats/Vereniging Tijd`. Koppen zoals `U20-M, MANNEN U20` en `U18-M, MANNEN U18` worden automatisch herkend. Kolomkoppen en streepjesregels worden overgeslagen; de vaste kolomposities scheiden naam en vereniging. Wijs onbekende categorieën in het importvenster toe. Annuleren breekt de volledige import af. Tekst wordt als UTF-8 gelezen, met Windows-1252 als terugval.
4. Rond **Naamcontroles** af. Gebruik per regel **Optie A**, **Optie B** of **Verschillend**. Optie A/B behoudt de betreffende naam en vereniging. Selecteer meerdere regels met Ctrl of Shift en gebruik de gelijknamige bovenste knoppen om de keuze in één keer op de hele selectie toe te passen. Filter op **Soort** om alleen naamvarianten of controles over andere verenigingen te zien. Wijzigen van dit filter wist de selectie. Keuzes worden per poule bewaard en kunnen worden hersteld.
5. Bekijk de **Uitslagenlijst**, het **Individueel klassement** of het **Ploegenklassement**. De categoriekeuze opent standaard op de eerste categorie en bevat geen optie voor alle categorieën. Uitslagen zijn daarnaast filterbaar op wedstrijd. Weergavenamen kunnen worden aangepast. **Puntopbouw bekijken** toont twee tabellen: de ploegscore per wedstrijd met de twee meetellende scores, en de lopers met hun categorie, punten, tijd en vermelding of ze bijdragen aan de ploegscore.
6. Exporteer de gekozen poule via **Exporteren** naar Excel. Dit scherm bevat uitsluitend Excel-export. De werkmap bevat achtereenvolgens **Uitslagenlijst**, **Individuele klassement** en **Ploegen klassement**. Openstaande naamcontroles blokkeren de export.

Tijdens paginanavigatie en het wisselen van poule verschijnt **Bezig met laden…** met een voortgangsindicator. Berekeningen draaien op de achtergrond en worden hergebruikt zolang de gegevens en poule niet wijzigen. Snel opeenvolgende navigatie toont uitsluitend de laatst gekozen pagina/poule.

## Categorieën beheren

**Categorieën** staat direct onder **Wedstrijden**. Per wedstrijd zie je de categoriekop uit het TXT-bestand, de gekoppelde klassementscategorie, de naam in de applicatie en het aantal uitslagen. Selecteer een regel om de bijbehorende deelnemers, verenigingen, plaatsen en tijden te bekijken.

Wijzig **Koppelen aan** om alle uitslagen van die importcategorie binnen die wedstrijd naar een andere klassementscategorie te verplaatsen. Wijzig **Naam in applicatie** om de zichtbare naam van de gekozen klassementscategorie in de hele poule aan te passen. Klik **Opslaan en opnieuw berekenen**. Uitslagen, naamcontroles, individuele en ploegenklassementen worden opnieuw berekend. Bij een andere koppeling vervallen de eerdere naamvalidatiekeuzes voor de betrokken categorieën, zodat de gewijzigde deelnemersgroepen opnieuw kunnen worden gecontroleerd. Aangepaste weergavenamen blijven bewaard.

De nieuwe naam wordt gebruikt in filters, tabellen, ploegpuntopbouw en alle drie Excel-tabbladen. De puntentelling en gecombineerde U18/U20-ploegen volgen de gekoppelde categorie, ook als je de zichtbare naam wijzigt. Namen en mappings worden lokaal opgeslagen. Bij nieuwe imports worden bestaande mappings van dezelfde broncategorie in de poule hergebruikt; bij herimport heeft de koppeling van die wedstrijd voorrang. Bij verschillende mappings wordt om een keuze gevraagd.

Nieuwe imports bewaren de oorspronkelijke categoriekop. Oudere imports hebben deze informatie niet; hiervoor wordt de bestaande categorie als bron getoond met **Oude import: oorspronkelijke kop niet bewaard**. Ook die koppeling is aanpasbaar.

Het individuele klassement heeft naast categorie ook een **Status**-filter. Bij individueel en ploegenklassement staan lege waarden voor **Plaats**, **W1**, **W2** en **W3** altijd onderaan, zowel oplopend als aflopend.

## Berekening

- Individueel: punten zijn de oorspronkelijke plaats; de twee laagste scores tellen. Bij drie starts wordt 3 punten afgetrokken voor categorieën met maximaal 10 unieke deelnemers, anders 5. Minimaal twee starts zijn nodig voor een plaats. Gelijke totalen delen een plaats.
- Ploegen: per wedstrijd tellen de drie lopers met de laagste punten. De twee beste ploeguitslagen tellen. U18 en U20 krijgen uitsluitend voor het ploegenklassement per wedstrijd één gecombineerde rangschikking op tijd, apart voor mannen en vrouwen. De punten zijn de plaatsen in deze gecombineerde uitslag, inclusief deelnemers van andere verenigingen. Gelijke tijden delen een plaats. Individuele categoriepunten blijven behouden. Een ploeg heeft minimaal drie lopers per wedstrijd en twee volledige wedstrijdresultaten nodig voor klassering. Er is geen ploegbonus. NTB en Nederlandse Triathlon Bond zijn uitgesloten van ploegplaatsing.
- Finale: dezelfde aantallen en gelijke-scoregrenzen als de webapp. De groene finalemarkering verschijnt pas nadat alle drie wedstrijden uitslagen hebben.
- Naamvergelijking: normalisatie van accenten, hoofdletters en leestekens; Levenshtein-overeenkomst zonder spaties. Drempels: 75% binnen dezelfde vereniging en 88% over verschillende verenigingen. Samenvoegen gebeurt in twee stappen, eerst binnen verenigingen en daarna over verenigingen.

## Opslag

Automatisch lokaal in `%LOCALAPPDATA%\Jeugdcross.Klassment\competitie.json`. Opslaan gebruikt een tijdelijk bestand en bewaart de vorige versie als `.bak`. Voor overdracht naar een andere computer kan dit lokale bestand worden gekopieerd terwijl de applicatie gesloten is. Verwijderen van wedstrijden vraagt bevestiging. De installer verwijdert competitiegegevens niet bij een de-installatie. Er zijn geen JSON-export- of importknoppen in de applicatie.

## Bouwen en testen

```powershell
dotnet build Jeugdcross.Klassment/Jeugdcross.Klassment.csproj -c Release
node tests/Jeugdcross.Klassment.Tests/parity.cjs
dotnet run --project tests/Jeugdcross.Klassment.Tests -c Release
& ./Jeugdcross.Klassment/Installer/build-installer.ps1
```

Voor de vergelijkingstest moeten de bestaande Vue-afhankelijkheden in `JeugdcrossKlassement/node_modules` beschikbaar zijn. De test vergelijkt tien datasets rechtstreeks met de oorspronkelijke Vue-berekeningen en controleert daarnaast de parser, validatie, opslag, Excel en WPF-navigatie. Testbestanden en een venstervoorbeeld staan in `artifacts` en worden niet gecommit.

De installer vereist Inno Setup 6 (`ISCC_PATH` kan de compilerlocatie aangeven). Uitvoer: `artifacts/installer/Jeugdcross.Klassment-Setup-1.0.6.exe`. Dit is een self-contained Windows x64-installatie: .NET hoeft op de doelcomputer niet afzonderlijk geïnstalleerd te worden. De installatie werkt per gebruiker zonder administratorrechten. De installer is niet digitaal ondertekend.

Het icoon combineert een stopwatch en een podium; het wordt reproduceerbaar opgebouwd met `Assets/create-icon.ps1`.
