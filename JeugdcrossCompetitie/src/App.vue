<script setup>
import { computed, ref, watch } from 'vue'
import { RouterLink, RouterView, useRoute } from 'vue-router'
import { useCompetitionData } from './composables/useCompetitionData'

const { siteContent } = useCompetitionData()
const route = useRoute()
const mobileMenuOpen = ref(false)

const reglementLink = computed(() => {
  const configured = siteContent.value.reglementUrl || '/docs/competitiereglement-v2.pdf'
  if (configured.startsWith('http://') || configured.startsWith('https://')) {
    return configured
  }
  const base = import.meta.env.BASE_URL || '/'
  return new URL(configured.replace(/^\//, ''), `http://local${base}`).pathname
})

const headerLogoUrl = computed(() => {
  const base = import.meta.env.BASE_URL || '/'
  return new URL('images/Logo.png', `http://local${base}`).pathname
})

function toggleMobileMenu() {
  mobileMenuOpen.value = !mobileMenuOpen.value
}

function closeMobileMenu() {
  mobileMenuOpen.value = false
}

watch(
  () => route.fullPath,
  () => {
    closeMobileMenu()
  },
)
</script>

<template>
  <div class="page-shell">
    <div class="app-card">
      <header class="topbar">
        <div class="brand">
          <div class="brand-icon">
            <img :src="headerLogoUrl" alt="Jeugdcross logo">
          </div>
          <div class="brand-title">
            <span class="brand-title-yellow">JEUGDCROSS</span>
            <span class="brand-title-white">COMPETITIE</span>
          </div>
        </div>
        <button
          type="button"
          class="menu-toggle"
          :aria-expanded="mobileMenuOpen ? 'true' : 'false'"
          aria-controls="main-nav"
          @click="toggleMobileMenu"
        >
          <i class="fa-solid fa-bars" />
        </button>
        <nav id="main-nav" class="nav-links" :class="{ 'is-open': mobileMenuOpen }">
          <RouterLink :to="{ path: '/', hash: '#welkom' }" @click="closeMobileMenu">
            <i class="fa-solid fa-house" /> Welkom
          </RouterLink>
          <a :href="reglementLink" download="competitiereglement-v2.pdf" @click="closeMobileMenu">
            <i class="fa-regular fa-file-lines" /> Reglement
          </a>
          <RouterLink to="/wedstrijden" @click="closeMobileMenu">
            <i class="fa-solid fa-flag-checkered" /> Wedstrijden
          </RouterLink>
          <RouterLink to="/individueel" @click="closeMobileMenu">
            <i class="fa-regular fa-user" /> Individueel
          </RouterLink>
          <RouterLink to="/ploegen" @click="closeMobileMenu">
            <i class="fa-solid fa-users" /> Ploegen
          </RouterLink>
        </nav>
      </header>

      <RouterView />
    </div>
  </div>
</template>
