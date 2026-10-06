import { computed, ref } from 'vue'

const loading = ref(true)
const error = ref('')
const competitionData = ref(null)
const siteContent = ref({
  welkomTekst: 'Volg hier alle uitslagen, klassementen en informatie van het seizoen.',
  reglementUrl: '/docs/competitiereglement-v2.pdf',
  reglementLabel: 'Reglement downloaden (PDF)',
  inschrijfformulierUrl: '/docs/inschrijfformulier.xlsx',
  wedstrijdOverzicht: [],
  seizoenTitel: 'Seizoen 2025-2026',
  seizoenTekst: 'Volg alle updates van de Jeugdcross Competitie via deze pagina.',
})
const loaded = ref(false)

const poules = computed(() => ['Noord', 'Midden', 'Zuid'].map((naam) => {
  const source = competitionData.value?.poules?.find((poule) => poule.naam === naam)
    || (naam === 'Noord' && !competitionData.value?.poules ? competitionData.value : null)
  return { naam, wedstrijden: source?.wedstrijden || [], individueelKlassement: source?.individueelKlassement || [], ploegenKlassement: source?.ploegenKlassement || [] }
}))
const raceColumns = computed(() => poules.value.flatMap((poule) => poule.wedstrijden))
const individueleCategorieen = computed(() => poules.value.flatMap((poule) => poule.individueelKlassement.map((category) => ({ ...category, poule: poule.naam }))))
const ploegenCategorieen = computed(() => poules.value.flatMap((poule) => poule.ploegenKlassement.map((category) => ({ ...category, poule: poule.naam }))))
const wedstrijdOverzicht = computed(() => {
  const overview = siteContent.value.wedstrijdOverzicht
  if (overview && !Array.isArray(overview)) {
    return (overview.poules || []).flatMap((poule) => (poule.wedstrijden || []).map((race) => ({ ...race, poule: poule.naam })))
  }
  const configured = overview || []
  const races = poules.value.flatMap((poule) => poule.wedstrijden.map((race) => {
    const info = configured.find((item) => (item.poule || 'Noord') === poule.naam && (item.crossId === race.id || item.datum === race.datum)) || {}
    return { ...info, ...race, titel: race.naam, poule: poule.naam }
  }))
  return [...races, ...configured.filter((item) => !races.some((race) => race.poule === (item.poule || 'Noord') && race.datum === item.datum)).map((item) => ({ ...item, poule: item.poule || 'Noord' }))]
})

// The final is shared and never belongs to a pool or its ranking columns.
const finaleWedstrijd = computed(() => siteContent.value.wedstrijdOverzicht?.finale || null)
const wedstrijdGroepen = computed(() => [
  ...poules.value.map((poule) => ({
    key: poule.naam,
    titel: 'Poule ' + poule.naam,
    wedstrijden: wedstrijdOverzicht.value.filter((race) => race.poule === poule.naam),
  })),
  ...(finaleWedstrijd.value ? [{ key: 'finale', titel: 'Finale — alle poules', wedstrijden: [finaleWedstrijd.value] }] : []),
])

const topIndividueel = computed(() => {
  const rows = []
  for (const category of individueleCategorieen.value) {
    for (const row of category.rows || []) {
      if (typeof row.plaats === 'number') {
        rows.push({
          plaats: row.plaats,
          naam: row.naam,
          categorie: category.categorie,
          totaal: row.totaal,
        })
      }
    }
  }
  return rows
    .sort((a, b) => a.totaal - b.totaal || a.plaats - b.plaats)
    .slice(0, 5)
})

const topPloegen = computed(() => {
  const rows = []
  for (const category of ploegenCategorieen.value) {
    for (const row of category.rows || []) {
      if (typeof row.plaats === 'number') {
        rows.push({
          plaats: row.plaats,
          vereniging: row.vereniging,
          categorie: category.categorie,
          totaal: row.totaal,
        })
      }
    }
  }
  return rows
    .sort((a, b) => a.totaal - b.totaal || a.plaats - b.plaats)
    .slice(0, 5)
})

function formatDate(dateString) {
  if (!dateString) {
    return '-'
  }
  const date = new Date(dateString)
  if (Number.isNaN(date.getTime())) {
    return dateString
  }
  return new Intl.DateTimeFormat('nl-NL', { day: '2-digit', month: '2-digit', year: 'numeric' }).format(date)
}

function pointsValue(value) {
  return value === null || value === undefined ? '-' : value
}

async function loadData(force = false) {
  if (loaded.value && !force) {
    return
  }

  loading.value = true
  error.value = ''
  try {
    const base = import.meta.env.BASE_URL || '/'
    const competitionUrl = new URL('data/competitie-data.json', `http://local${base}`).pathname
    const contentUrl = new URL('data/site-content.json', `http://local${base}`).pathname

    const competitionResponse = await fetch(competitionUrl)
    if (!competitionResponse.ok) {
      throw new Error('Competitiedata kon niet geladen worden.')
    }
    competitionData.value = await competitionResponse.json()

    const contentResponse = await fetch(contentUrl)
    if (contentResponse.ok) {
      siteContent.value = {
        ...siteContent.value,
        ...(await contentResponse.json()),
      }
    }

    loaded.value = true
  }
  catch (err) {
    error.value = err instanceof Error ? err.message : String(err)
  }
  finally {
    loading.value = false
  }
}

export function useCompetitionData() {
  const selectedPoule = ref('Noord')
  const activePoule = computed(() => poules.value.find((poule) => poule.naam === selectedPoule.value) || poules.value[0])
  return {
    loading,
    error,
    competitionData,
    siteContent,
    poules,
    wedstrijdOverzicht,
    finaleWedstrijd,
    wedstrijdGroepen,
    selectedPoule,
    activePoule,
    selectedRaceColumns: computed(() => activePoule.value.wedstrijden),
    selectedIndividueleCategorieen: computed(() => activePoule.value.individueelKlassement),
    selectedPloegenCategorieen: computed(() => activePoule.value.ploegenKlassement),
    raceColumns,
    individueleCategorieen,
    ploegenCategorieen,
    topIndividueel,
    topPloegen,
    loadData,
    formatDate,
    pointsValue,
  }
}
