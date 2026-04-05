<script setup>
import { computed, onMounted } from 'vue'
import { useCompetitionData } from '../composables/useCompetitionData'

const {
  loading,
  error,
  competitionData,
  siteContent,
  loadData,
  formatDate,
} = useCompetitionData()

const inschrijfformulierLink = computed(() => {
  const configured = siteContent.value.inschrijfformulierUrl || '/docs/inschrijfformulier.xlsx'
  if (configured.startsWith('http://') || configured.startsWith('https://')) {
    return configured
  }
  const base = import.meta.env.BASE_URL || '/'
  return new URL(configured.replace(/^\//, ''), `http://local${base}`).pathname
})

const wedstrijdOverzicht = computed(() => siteContent.value.wedstrijdOverzicht || [])

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

      <div class="wedstrijd-grid">
        <article v-for="wedstrijd in wedstrijdOverzicht" :key="`${wedstrijd.titel}-${wedstrijd.datum}`" class="wedstrijd-card">
          <h3>{{ wedstrijd.titel }}</h3>
          <dl>
            <div>
              <dt>Datum</dt>
              <dd>{{ formatDate(wedstrijd.datum) }}</dd>
            </div>
            <div>
              <dt>Vereniging</dt>
              <dd>{{ wedstrijd.vereniging || '-' }}</dd>
            </div>
            <div>
              <dt>Plaats</dt>
              <dd>{{ wedstrijd.plaats || '-' }}</dd>
            </div>
          </dl>
          <div class="wedstrijd-links">
            <a :href="wedstrijd.verenigingUrl" target="_blank" rel="noopener noreferrer">
              <i class="fa-solid fa-globe" /> Verenigingswebsite
            </a>
            <a :href="wedstrijd.tijdschemaUrl" target="_blank" rel="noopener noreferrer">
              <i class="fa-regular fa-clock" /> Tijdschema
            </a>
            <a v-if="wedstrijd.uitslagUrl" :href="wedstrijd.uitslagUrl" target="_blank" rel="noopener noreferrer">
              <i class="fa-solid fa-list-ol" /> Uitslag
            </a>
            <a v-if="wedstrijd.ploegenUitslagUrl" :href="wedstrijd.ploegenUitslagUrl" target="_blank" rel="noopener noreferrer">
              <i class="fa-solid fa-people-group" /> Ploegen uitslag
            </a>
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
