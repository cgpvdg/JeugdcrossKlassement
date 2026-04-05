<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import { useCompetitionData } from '../composables/useCompetitionData'

const {
  loading,
  error,
  competitionData,
  siteContent,
  individueleCategorieen,
  ploegenCategorieen,
  loadData,
  formatDate,
} = useCompetitionData()

const randomIndividueel = ref([])
const randomPloegen = ref([])

const heroImageUrl = computed(() => {
  const base = import.meta.env.BASE_URL || '/'
  return new URL('images/Cross-kids.png', `http://local${base}`).pathname
})

const reglementLink = computed(() => {
  const configured = siteContent.value.reglementUrl || '/docs/competitiereglement-v2.pdf'
  if (configured.startsWith('http://') || configured.startsWith('https://')) {
    return configured
  }
  const base = import.meta.env.BASE_URL || '/'
  return new URL(configured.replace(/^\//, ''), `http://local${base}`).pathname
})

const wedstrijduitslagen = computed(() =>
  (siteContent.value.wedstrijdOverzicht || []).map((item) => ({
    naam: item.titel,
    datum: item.datum,
    vereniging: item.vereniging || '',
    url: item.uitslagUrl || '',
    ploegUrl: item.ploegenUitslagUrl || '',
    label: 'Bekijk uitslagen.nl',
    ploegLabel: 'Ploegen uitslag',
  })),
)

function pickRandomItems(items, count = 5) {
  const shuffled = [...items]
  for (let i = shuffled.length - 1; i > 0; i -= 1) {
    const j = Math.floor(Math.random() * (i + 1))
    const temp = shuffled[i]
    shuffled[i] = shuffled[j]
    shuffled[j] = temp
  }
  return shuffled.slice(0, Math.min(count, shuffled.length))
}

function refreshRandomRankings() {
  const individueleRows = []
  for (const category of individueleCategorieen.value) {
    for (const row of category.rows || []) {
      if (typeof row.plaats === 'number') {
        individueleRows.push({
          plaats: row.plaats,
          naam: row.naam,
          categorie: category.categorie,
          totaal: row.totaal,
        })
      }
    }
  }

  const ploegenRows = []
  for (const category of ploegenCategorieen.value) {
    for (const row of category.rows || []) {
      if (typeof row.plaats === 'number') {
        ploegenRows.push({
          plaats: row.plaats,
          vereniging: row.vereniging,
          categorie: category.categorie,
          totaal: row.totaal,
        })
      }
    }
  }

  randomIndividueel.value = pickRandomItems(individueleRows, 5)
  randomPloegen.value = pickRandomItems(ploegenRows, 5)
}

onMounted(async () => {
  await loadData()
  refreshRandomRankings()
})

watch(
  () => competitionData.value,
  (value) => {
    if (value) {
      refreshRandomRankings()
    }
  },
  { immediate: true },
)
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
          <span class="hero-title-bottom">
            COMPETITIE
            <span class="hero-title-region">Regio 1 t/m 5</span>
          </span>
        </h1>
        <p class="lead">
          {{ siteContent.welkomTekst }}
        </p>
        <div class="hero-actions">
          <RouterLink to="/individueel" class="btn-primary">
            <i class="fa-solid fa-trophy" /> Individueel klassement
          </RouterLink>
          <RouterLink to="/ploegen" class="btn-primary btn-secondary">
            <i class="fa-solid fa-users" /> Ploegen klassement
          </RouterLink>
          <RouterLink to="/wedstrijden" class="btn-ghost">
            <i class="fa-regular fa-calendar-days" /> Wedstrijden
          </RouterLink>
        </div>
      </div>
      <div class="hero-visual">
        <img :src="heroImageUrl" alt="Rennende kinderen tijdens een crosswedstrijd">
      </div>
    </section>

    <aside class="side-column">
      <section id="wedstrijden" class="panel">
        <h2><i class="fa-regular fa-calendar-days" /> Wedstrijden</h2>
        <div class="event-list">
          <div
            v-for="event in wedstrijduitslagen"
            :key="`${event.datum}-${event.naam}`"
            class="event-item"
          >
            <span class="event-date">{{ formatDate(event.datum) }}</span>
            <span>
              <strong>{{ event.naam }}</strong>
              <small v-if="event.url">
                <span v-if="event.vereniging">{{ event.vereniging }}</span>
                <span v-if="event.vereniging"> | </span>
                <a :href="event.url" target="_blank" rel="noopener noreferrer">{{ event.label || 'Bekijk op uitslagen.nl' }}</a>
                <span v-if="event.ploegUrl"> | </span>
                <a v-if="event.ploegUrl" :href="event.ploegUrl" target="_blank" rel="noopener noreferrer">{{ event.ploegLabel }}</a>
              </small>
              <small v-else-if="event.vereniging || event.ploegUrl">
                <span v-if="event.vereniging">{{ event.vereniging }}</span>
                <span v-if="event.vereniging && event.ploegUrl"> | </span>
                <a v-if="event.ploegUrl" :href="event.ploegUrl" target="_blank" rel="noopener noreferrer">{{ event.ploegLabel }}</a>
              </small>
            </span>
          </div>
        </div>
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
            <tr v-for="row in randomIndividueel" :key="`${row.naam}-${row.categorie}`">
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
            <tr v-for="row in randomPloegen" :key="`${row.vereniging}-${row.categorie}`">
              <td>{{ row.plaats }}</td>
              <td>{{ row.vereniging }}</td>
              <td>{{ row.categorie }}</td>
              <td>{{ row.totaal }}</td>
            </tr>
          </tbody>
        </table>
      </article>

      <div class="summary-side">
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
