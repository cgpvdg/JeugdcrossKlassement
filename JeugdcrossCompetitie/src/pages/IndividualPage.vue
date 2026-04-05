<script setup>
import { onMounted } from 'vue'
import { useCompetitionData } from '../composables/useCompetitionData'

const {
  loading,
  error,
  competitionData,
  raceColumns,
  individueleCategorieen,
  loadData,
  formatDate,
  pointsValue,
} = useCompetitionData()

onMounted(() => {
  loadData()
})
</script>

<template>
  <main v-if="!loading && competitionData" class="page-content">
    <section id="individueel" class="panel">
      <h2><i class="fa-regular fa-user" /> Individueel Klassement</h2>
      <div class="info-banner">
        Groen gemarkeerde atleten hebben zich geplaatst voor de finale.
      </div>
      <div class="category-stack">
        <article v-for="category in individueleCategorieen" :key="category.categorie" class="category-card">
          <h3>{{ category.categorie }}</h3>
          <div class="table-wrap">
            <table>
              <thead>
                <tr>
                  <th class="col-center">Plaats</th>
                  <th>Naam</th>
                  <th>Vereniging</th>
                  <th v-for="race in raceColumns" :key="race.id" class="col-center">
                    {{ formatDate(race.datum) }}
                  </th>
                  <th class="col-center">Bonus</th>
                  <th class="col-center">Totaal</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="row in category.rows"
                  :key="`${category.categorie}-${row.naam}-${row.vereniging}-${row.totaal}`"
                  :class="{ qualified: row.geplaatstVoorFinale }"
                >
                  <td class="col-center">{{ row.plaats ?? '-' }}</td>
                  <td>{{ row.naam }}</td>
                  <td>{{ row.vereniging }}</td>
                  <td v-for="point in row.wedstrijdPunten" :key="point.crossId" class="col-center">
                    {{ pointsValue(point.punten) }}
                  </td>
                  <td class="col-center">{{ row.bonus }}</td>
                  <td class="col-center col-total">{{ row.totaal }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </article>
      </div>
    </section>
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
