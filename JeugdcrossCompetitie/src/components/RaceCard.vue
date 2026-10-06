<script setup>
import { useCompetitionData } from '../composables/useCompetitionData'
defineProps({ wedstrijd: { type: Object, required: true } })
const { formatDate } = useCompetitionData()
</script>

<template>
  <article class="wedstrijd-card">
    <h4>{{ wedstrijd.titel }}</h4>
    <dl class="race-details">
      <div><dt>Datum</dt><dd>{{ wedstrijd.datum ? formatDate(wedstrijd.datum) : 'Nog te bepalen' }}</dd></div>
      <div><dt>Vereniging</dt><dd>{{ wedstrijd.vereniging || 'Nog te bepalen' }}</dd></div>
      <div><dt>Plaats</dt><dd>{{ wedstrijd.plaats || 'Nog te bepalen' }}</dd></div>
    </dl>
    <div class="wedstrijd-links">
      <a v-if="wedstrijd.verenigingUrl" :href="wedstrijd.verenigingUrl" target="_blank" rel="noopener noreferrer"><i class="fa-solid fa-globe" /> Verenigingswebsite</a>
      <a v-if="wedstrijd.tijdschemaUrl" :href="wedstrijd.tijdschemaUrl" target="_blank" rel="noopener noreferrer"><i class="fa-regular fa-clock" /> Tijdschema</a>
      <a v-if="wedstrijd.uitslagUrl" :href="wedstrijd.uitslagUrl" target="_blank" rel="noopener noreferrer"><i class="fa-solid fa-list-ol" /> Uitslag</a>
      <a v-if="wedstrijd.ploegenUitslagUrl" :href="wedstrijd.ploegenUitslagUrl" target="_blank" rel="noopener noreferrer"><i class="fa-solid fa-people-group" /> Ploegen uitslag</a>
    </div>
  </article>
</template>

<style scoped>
.wedstrijd-card {
  padding: 10px 12px;
  border-radius: 8px;
}

.wedstrijd-card h4 {
  min-height: 0;
  margin: 0 0 6px;
  font-size: .95rem;
}

.race-details {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: 3px 8px;
  font-size: .8rem;
}

.race-details div {
  margin: 0;
}

.race-details div + div {
  border-left: 1px solid var(--line);
  padding-left: 8px;
}

.race-details dt {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  overflow: hidden;
  clip-path: inset(50%);
  white-space: nowrap;
}

.race-details dd {
  margin: 0;
}

.wedstrijd-links {
  display: flex;
  flex-wrap: wrap;
  gap: 4px 12px;
  margin-top: 7px;
  font-size: .8rem;
}

.wedstrijd-links:empty {
  display: none;
}

.wedstrijd-links a {
  gap: 4px;
}
</style>
