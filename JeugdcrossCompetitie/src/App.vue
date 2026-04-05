<script setup>
import { computed } from 'vue'
import { RouterLink, RouterView } from 'vue-router'
import { useCompetitionData } from './composables/useCompetitionData'

const { siteContent } = useCompetitionData()

const reglementLink = computed(() => {
  const configured = siteContent.value.reglementUrl || '/docs/competitiereglement-v2.pdf'
  if (configured.startsWith('http://') || configured.startsWith('https://')) {
    return configured
  }
  const base = import.meta.env.BASE_URL || '/'
  return new URL(configured.replace(/^\//, ''), `http://local${base}`).pathname
})
</script>

<template>
  <div class="page-shell">
    <div class="app-card">
      <header class="topbar">
        <div class="brand">
          <div class="brand-icon">
            JC
          </div>
          <div class="brand-title">
            JEUGDCROSS COMPETITIE
          </div>
        </div>
        <nav class="nav-links">
          <RouterLink :to="{ path: '/', hash: '#welkom' }">
            <i class="fa-solid fa-house" /> Welkom
          </RouterLink>
          <a :href="reglementLink" download="competitiereglement-v2.pdf">
            <i class="fa-regular fa-file-lines" /> Reglement
          </a>
          <RouterLink to="/wedstrijden">
            <i class="fa-solid fa-flag-checkered" /> Wedstrijden
          </RouterLink>
          <RouterLink to="/individueel">
            <i class="fa-regular fa-user" /> Individueel
          </RouterLink>
          <RouterLink to="/ploegen">
            <i class="fa-solid fa-users" /> Ploegen
          </RouterLink>
        </nav>
      </header>

      <RouterView />
    </div>
  </div>
</template>
