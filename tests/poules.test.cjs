const { test } = require('node:test')
const assert = require('node:assert/strict')
const fs = require('node:fs')
const vue = require('../JeugdcrossKlassement/node_modules/vue')

function loadRankings() {
  const source = fs.readFileSync('JeugdcrossKlassement/src/App.vue', 'utf8').split('<script setup>')[1].split('</script>')[0]
    .replace(/^import[\s\S]*?from ['"][^'"]+['"]\s*$/gm, '')
    .replace(/onMounted\(init\)/g, '')
  const api = new Function('computed', 'markRaw', 'onMounted', 'ref', 'shallowRef', 'CATEGORY_ORDER', 'normalizeParticipantKey', source + `
    let exported
    downloadTextFile = (name, content) => { exported = JSON.parse(content) }
    showSuccess = () => {}
    return { allCrosses, allResults, selectedPoule, standingsPerCategory, teamStandingsPerCategory, canHighlightFinalists,
      exportData: () => { exportCompetitionOverview(); return exported } }
  `)(vue.computed, vue.markRaw, () => {}, vue.ref, vue.shallowRef, ['Jongens U16'], (value) => value.toLowerCase())
  return api
}

test('individual and team scores, race columns and finals remain within their pool', () => {
  const api = loadRankings()
  api.allCrosses.value = ['Noord', 'Midden', 'Zuid'].flatMap((poule) => [1, 2, 3].map((n) => ({ id: poule + n, poule, name: poule + n, date: '2026-01-0' + n, association: 'Club' })))
  api.allResults.value = api.allCrosses.value.filter((race) => race.id !== 'Zuid3').flatMap((race) => [1, 2, 3].map((n) => ({ crossId: race.id, category: 'Jongens U16', participantName: 'Loper ' + n, participantKey: 'loper ' + n, association: 'Club', points: race.poule === 'Noord' ? 1 : 8, time: '1:00' })))
  const north = api.standingsPerCategory.value.get('Jongens U16')[0]
  assert.equal(north.starts, 3)
  assert.equal(north.total, -1)
  assert.equal(north.pointsPerCross.length, 3)
  api.selectedPoule.value = 'Midden'
  assert.equal(api.standingsPerCategory.value.get('Jongens U16')[0].total, 13)
  assert.equal(api.teamStandingsPerCategory.value.get('Jongens U16')[0].pointsPerCross.length, 3)
  assert.equal(api.teamStandingsPerCategory.value.get('Jongens U16')[0].total, 48)
  api.selectedPoule.value = 'Zuid'
  assert.equal(api.standingsPerCategory.value.get('Jongens U16')[0].starts, 2)
  assert.equal(api.canHighlightFinalists.value, false)
  const exported = api.exportData()
  assert.equal(exported.version, 2)
  assert.equal(exported.poules.length, 3)
  assert.equal(exported.poules[0].individueelKlassement[0].rows[0].totaal, -1)
  assert.equal(exported.poules[1].individueelKlassement[0].rows[0].totaal, 13)
  assert.equal(exported.poules[2].individueelKlassement[0].rows[0].geplaatstVoorFinale, false)
  assert.equal(api.selectedPoule.value, 'Zuid')
  for (const pool of exported.poules) {
    assert.equal(pool.wedstrijden.length, 3)
    assert.ok(pool.individueelKlassement[0].rows[0].wedstrijdPunten.every((point) => point.crossId.startsWith(pool.naam)))
  }
})

test('website selects Noord by default and loads all pools and their race links', async () => {
  const source = fs.readFileSync('JeugdcrossCompetitie/src/composables/useCompetitionData.js', 'utf8')
    .replace(/^import.*$/gm, '').replace('export function useCompetitionData', 'function useCompetitionData')
    .replaceAll('import.meta.env.BASE_URL', "'/'")
  const data = { version: 2, poules: ['Noord', 'Midden', 'Zuid'].map((naam) => ({ naam, wedstrijden: [{ id: naam, naam: 'Cross ' + naam, datum: '2026-01-01' }], individueelKlassement: [{ categorie: 'U16', rows: [{ naam, plaats: 1 }] }], ploegenKlassement: [] })) }
  const fetch = async (url) => ({ ok: true, json: async () => url.includes('competitie-data') ? JSON.parse(JSON.stringify(data)) : { wedstrijdOverzicht: [{ poule: 'Midden', datum: '2026-01-01', uitslagUrl: 'https://example.com/midden' }] } })
  const api = new Function('computed', 'ref', 'fetch', source + '\nreturn useCompetitionData()')(vue.computed, vue.ref, fetch)
  await api.loadData()
  assert.equal(api.selectedPoule.value, 'Noord')
  assert.equal(api.selectedRaceColumns.value[0].id, 'Noord')
  assert.equal(api.individueleCategorieen.value.length, 3)
  assert.equal(api.wedstrijdOverzicht.value.length, 3)
  assert.equal(api.wedstrijdOverzicht.value.find((race) => race.poule === 'Midden').uitslagUrl, 'https://example.com/midden')
  api.selectedPoule.value = 'Zuid'
  assert.equal(api.selectedIndividueleCategorieen.value[0].rows[0].naam, 'Zuid')
  assert.equal(api.selectedRaceColumns.value[0].id, 'Zuid')
  delete data.poules
  data.wedstrijden = [{ id: 'legacy' }]
  await api.loadData(true)
  api.selectedPoule.value = 'Noord'
  assert.equal(api.selectedRaceColumns.value[0].id, 'legacy')
  api.selectedPoule.value = 'Midden'
  assert.equal(api.selectedRaceColumns.value.length, 0)
})

test('site planning contains three races per pool and one shared final', async () => {
  const content = JSON.parse(fs.readFileSync('JeugdcrossCompetitie/public/data/site-content.json', 'utf8'))
  const data = JSON.parse(fs.readFileSync('JeugdcrossCompetitie/public/data/competitie-data.json', 'utf8'))
  const source = fs.readFileSync('JeugdcrossCompetitie/src/composables/useCompetitionData.js', 'utf8')
    .replace(/^import.*$/gm, '').replace('export function useCompetitionData', 'function useCompetitionData')
    .replaceAll('import.meta.env.BASE_URL', "'/'")
  const fetch = async (url) => ({ ok: true, json: async () => url.includes('competitie-data') ? data : content })
  const api = new Function('computed', 'ref', 'fetch', source + '\nreturn useCompetitionData()')(vue.computed, vue.ref, fetch)
  await api.loadData()
  assert.equal(api.error.value, '')
  assert.deepEqual(api.wedstrijdGroepen.value.map((group) => group.key), ['Noord', 'Midden', 'Zuid', 'finale'])
  assert.deepEqual(api.wedstrijdGroepen.value.map((group) => group.wedstrijden.length), [3, 3, 3, 1])
  assert.equal(api.wedstrijdOverzicht.value.length, 9)
  assert.equal(api.finaleWedstrijd.value.datum, '2027-03-06')
  assert.equal(api.finaleWedstrijd.value.poule, undefined)
  assert.equal(api.wedstrijdOverzicht.value.some((race) => race.titel.includes('Finale')), false)
  assert.equal(api.raceColumns.value.some((race) => race.datum === '2027-03-06'), false)
  assert.equal(api.wedstrijdGroepen.value[0].wedstrijden[0].vereniging, 'NOVA')
  assert.equal(api.wedstrijdGroepen.value[1].wedstrijden[0].datum, '2026-11-21')
  assert.equal(api.wedstrijdGroepen.value[3].wedstrijden[0].ploegenUitslagUrl, content.wedstrijdOverzicht.finale.ploegenUitslagUrl)
})
