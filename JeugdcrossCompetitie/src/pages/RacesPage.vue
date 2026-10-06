<script setup>
import { computed, onMounted } from 'vue'
import RaceCard from '../components/RaceCard.vue'
import { useCompetitionData } from '../composables/useCompetitionData'

const {
  loading,
  error,
  competitionData,
  siteContent,
  wedstrijdGroepen,
  loadData,
} = useCompetitionData()

const inschrijfformulierLink = computed(() => {
  const configured = siteContent.value.inschrijfformulierUrl || '/docs/inschrijfformulier.xlsx'
  if (configured.startsWith('http://') || configured.startsWith('https://')) {
    return configured
  }
  const base = import.meta.env.BASE_URL || '/'
  return new URL(configured.replace(/^\//, ''), `http://local${base}`).pathname
})



onMounted(() => {
  loadData()
})
</script>

<template>
  <main v-if="!loading && competitionData" class="page-content">
    <section class="panel">
      <h2><i class="fa-solid fa-flag-checkered" /> Wedstrijden</h2>

      <div class="wedstrijd-info-grid">
        <article class="info-banner">
          <p><strong>Inschrijfformulier</strong></p>
          <p>
            Download hier het inschrijfformulier:
            <a :href="inschrijfformulierLink" download="inschrijfformulier.xlsx">inschrijfformulier.xlsx</a>
          </p>
        </article>

        <article class="info-banner">
          <p><strong>Coördinatoren</strong></p>
          <p>Noord : Margret van Kampen - <a href="mailto:Margret.vankampen@live.nl">Margret.vankampen@live.nl</a></p>
          <p>Midden : Gerrit Tigchelaar - <a href="mailto:tigch@ziggo.nl">tigch@ziggo.nl</a></p>
          <p>Zuid : Bernard Wouters - <a href="mailto:crosszuid@gmail.com">crosszuid@gmail.com</a></p>
        </article>
      </div>

      <div class="race-poules-grid">
      <section
        v-for="groep in wedstrijdGroepen"
        :key="groep.key"
        class="race-group"
        :class="{ 'race-group-final': groep.key === 'finale' }"
      >
        <h3>{{ groep.titel }}</h3>
        <p v-if="!groep.wedstrijden.length">Nog geen wedstrijden.</p>
        <div class="race-stack">
          <RaceCard v-for="(wedstrijd, index) in groep.wedstrijden" :key="index" :wedstrijd="wedstrijd" />
        </div>
      </section>
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

<style scoped>
.race-poules-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 1rem .75rem;
  align-items: start;
}

.race-group {
  min-width: 0;
  margin-top: 0;
}

.race-group h3 {
  margin-bottom: .5rem;
}

.race-stack {
  display: grid;
  gap: .5rem;
}

.race-group-final {
  grid-column: 1 / -1;
}

@media (max-width: 800px) {
  .race-poules-grid {
    grid-template-columns: 1fr;
  }
}
</style>
