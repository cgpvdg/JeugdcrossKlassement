import { computed, ref } from 'vue'

const loading = ref(true)
const error = ref('')
const competitionData = ref(null)
const siteContent = ref({
  welkomTekst: 'Volg hier alle uitslagen, klassementen en informatie van het seizoen.',
  reglementUrl: '/docs/competitiereglement-v2.pdf',
  reglementLabel: 'Reglement downloaden (PDF)',
  wedstrijduitslagen: [],
  seizoenTitel: 'Seizoen 2025-2026',
  seizoenTekst: 'Volg alle updates van de Jeugdcross Competitie via deze pagina.',
})
const loaded = ref(false)

const raceColumns = computed(() => competitionData.value?.wedstrijden || [])
const individueleCategorieen = computed(() => competitionData.value?.individueelKlassement || [])
const ploegenCategorieen = computed(() => competitionData.value?.ploegenKlassement || [])

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
  return {
    loading,
    error,
    competitionData,
    siteContent,
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
