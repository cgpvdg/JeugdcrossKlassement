<script setup>
import { computed, onMounted } from 'vue'
import { useCompetitionData } from '../composables/useCompetitionData'

const {
  loading,
  error,
  competitionData,
  siteContent,
  topIndividueel,
  topPloegen,
  loadData,
  formatDate,
} = useCompetitionData()

const heroImageUrl = computed(() => {
  const base = import.meta.env.BASE_URL || '/'
  return new URL('images/hero-runners.svg', `http://local${base}`).pathname
})

const reglementLink = computed(() => {
  const configured = siteContent.value.reglementUrl || '/docs/competitiereglement-v2.pdf'
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
  <main v-if="!loading && competitionData" class="content-grid">
    <section id="welkom" class="hero">
      <div class="hero-copy">
        <p class="kicker">
          WELKOM BIJ DE
        </p>
        <h1 class="hero-title">
          <span class="hero-title-top">JEUGDCROSS</span>
          <span class="hero-title-bottom">COMPETITIE</span>
        </h1>
        <p class="lead">
          {{ siteContent.welkomTekst }}
        </p>
        <div class="hero-actions">
          <RouterLink to="/individueel" class="btn-primary">
            <i class="fa-solid fa-trophy" /> Bekijk klassementen
          </RouterLink>
          <a href="#wedstrijden" class="btn-ghost">
            <i class="fa-regular fa-calendar-days" /> Wedstrijduitslagen
          </a>
        </div>
      </div>
      <div class="hero-visual">
        <img :src="heroImageUrl" alt="Rennende kinderen tijdens een crosswedstrijd">
      </div>
    </section>

    <aside class="side-column">
      <section id="wedstrijden" class="panel">
        <h2><i class="fa-regular fa-calendar-days" /> Wedstrijduitslagen</h2>
        <div class="event-list">
          <a
            v-for="event in siteContent.wedstrijduitslagen"
            :key="`${event.datum}-${event.naam}`"
            :href="event.url"
            target="_blank"
            rel="noopener noreferrer"
            class="event-item"
          >
            <span class="event-date">{{ formatDate(event.datum) }}</span>
            <span>
              <strong>{{ event.naam }}</strong>
              <small>{{ event.label || 'Bekijk op uitslagen.nl' }}</small>
            </span>
          </a>
        </div>
      </section>

      <section id="reglement" class="panel">
        <h2><i class="fa-regular fa-file-lines" /> Reglement</h2>
        <p>Bekijk hier het volledige reglement van de competitie.</p>
        <a
          class="btn-ghost full"
          :href="reglementLink"
          download="competitiereglement-v2.pdf"
        >
          {{ siteContent.reglementLabel }}
        </a>
      </section>

      <section class="panel">
        <h2><i class="fa-solid fa-circle-info" /> {{ siteContent.seizoenTitel }}</h2>
        <p>{{ siteContent.seizoenTekst }}</p>
      </section>
    </aside>

    <section class="summary-cards">
      <article class="summary-card">
        <div class="summary-head">
          <h3><i class="fa-regular fa-user" /> Individueel Klassement</h3>
          <RouterLink to="/individueel" class="summary-link">
            Bekijk alles <i class="fa-solid fa-arrow-right" />
          </RouterLink>
        </div>
        <table>
          <thead>
            <tr>
              <th>Pos</th>
              <th>Naam</th>
              <th>Categorie</th>
              <th>Punten</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="row in topIndividueel" :key="`${row.naam}-${row.categorie}`">
              <td>{{ row.plaats }}</td>
              <td>{{ row.naam }}</td>
              <td>{{ row.categorie }}</td>
              <td>{{ row.totaal }}</td>
            </tr>
          </tbody>
        </table>
      </article>

      <article class="summary-card yellow">
        <div class="summary-head">
          <h3><i class="fa-solid fa-users" /> Ploegenklassement</h3>
          <RouterLink to="/ploegen" class="summary-link">
            Bekijk alles <i class="fa-solid fa-arrow-right" />
          </RouterLink>
        </div>
        <table>
          <thead>
            <tr>
              <th>Pos</th>
              <th>Ploeg</th>
              <th>Categorie</th>
              <th>Punten</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="row in topPloegen" :key="`${row.vereniging}-${row.categorie}`">
              <td>{{ row.plaats }}</td>
              <td>{{ row.vereniging }}</td>
              <td>{{ row.categorie }}</td>
              <td>{{ row.totaal }}</td>
            </tr>
          </tbody>
        </table>
      </article>
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
