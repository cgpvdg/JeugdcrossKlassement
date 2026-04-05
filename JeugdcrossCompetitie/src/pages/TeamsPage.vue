<script setup>
import { computed, onMounted, ref } from 'vue'
import { useCompetitionData } from '../composables/useCompetitionData'

const {
  loading,
  error,
  competitionData,
  raceColumns,
  ploegenCategorieen,
  loadData,
  formatDate,
  pointsValue,
} = useCompetitionData()

const selectedBreakdown = ref(null)

const hasBreakdown = computed(() => Boolean(selectedBreakdown.value))

function canOpenBreakdown(point) {
  return point && typeof point.punten === 'number' && Array.isArray(point.deelnemers) && point.deelnemers.length > 0
}

function participantLabel(participant, categoryLabel) {
  if (!participant) {
    return ''
  }
  const rawName = participant.naam || ''
  const source = participant.bronCategorie || ''
  const isCombinedCategory = typeof categoryLabel === 'string' && categoryLabel.includes('U20/U18')
  if (!isCombinedCategory || !source || rawName.includes('(')) {
    return rawName
  }
  return `${rawName} (${source})`
}

function openBreakdown(category, row, point) {
  if (!canOpenBreakdown(point)) {
    return
  }
  selectedBreakdown.value = {
    categoryLabel: category.categorie,
    association: row.vereniging,
    wedstrijdDatum: point.datum,
    wedstrijdNaam: point.naam || point.datum || '',
    score: point.punten,
    deelnemers: point.deelnemers,
  }
}

function closeBreakdown() {
  selectedBreakdown.value = null
}

onMounted(() => {
  loadData()
})
</script>

<template>
  <main v-if="!loading && competitionData" class="page-content">
    <section id="ploegen" class="panel">
      <h2><i class="fa-solid fa-users" /> Ploegenklassement</h2>
      <div class="info-banner">
        Groen gemarkeerde ploegen hebben zich geplaatst voor de finale en mogen eventueel aangevuld worden tot 4 lopers, indien die aan minstens 2 wedstrijden meegedaan hebben zie competitiereglement art 16.b.
      </div>
      <div class="category-stack">
        <article v-for="category in ploegenCategorieen" :key="category.categorie" class="category-card">
          <h3>{{ category.categorie }}</h3>
          <div class="table-wrap">
            <table>
              <thead>
                <tr>
                  <th class="col-center">Plaats</th>
                  <th>Vereniging</th>
                  <th v-for="race in raceColumns" :key="race.id" class="col-center">
                    {{ formatDate(race.datum) }}
                  </th>
                  <th class="col-center">Totaal</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="row in category.rows"
                  :key="`${category.categorie}-${row.vereniging}-${row.totaal}`"
                  :class="{ qualified: row.geplaatstVoorFinale }"
                >
                  <td class="col-center">{{ row.plaats ?? '-' }}</td>
                  <td>{{ row.vereniging }}</td>
                  <td v-for="point in row.wedstrijdPunten" :key="point.crossId" class="col-center">
                    <button
                      v-if="canOpenBreakdown(point)"
                      type="button"
                      class="points-link"
                      @click="openBreakdown(category, row, point)"
                    >
                      {{ pointsValue(point.punten) }}
                    </button>
                    <span v-else>{{ pointsValue(point.punten) }}</span>
                  </td>
                  <td class="col-center col-total">{{ row.totaal }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </article>
      </div>
    </section>

    <div v-if="hasBreakdown" class="modal-backdrop" @click.self="closeBreakdown">
      <section class="modal-card" role="dialog" aria-modal="true" aria-label="Ploegpunten uitleg">
        <header class="modal-head">
          <h3>Ploegpunten Uitleg</h3>
          <button type="button" class="modal-close" @click="closeBreakdown" aria-label="Sluiten">
            <i class="fa-solid fa-xmark" />
          </button>
        </header>

        <div class="modal-body">
          <p><strong>Categorie:</strong> {{ selectedBreakdown.categoryLabel }}</p>
          <p><strong>Vereniging:</strong> {{ selectedBreakdown.association }}</p>
          <p><strong>Wedstrijd:</strong> {{ formatDate(selectedBreakdown.wedstrijdDatum) }}</p>
          <p><strong>Ploegscore:</strong> {{ selectedBreakdown.score }}</p>

          <div class="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Deelnemer</th>
                  <th>Punten</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="participant in selectedBreakdown.deelnemers"
                  :key="`${participant.naam}-${participant.tijd}-${participant.punten}`"
                >
                  <td>{{ participantLabel(participant, selectedBreakdown.categoryLabel) }}</td>
                  <td>{{ participant.punten }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </section>
    </div>
  </main>

  <main v-else class="loading-state">
    <p v-if="loading">
      Gegevens worden geladen...
    </p>
    <p v-else-if="error">
      {{ error }}
    </p>
  </main>
</template>
