import { createRouter, createWebHistory } from 'vue-router'
import HomePage from './pages/HomePage.vue'
import IndividualPage from './pages/IndividualPage.vue'
import TeamsPage from './pages/TeamsPage.vue'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    { path: '/', name: 'home', component: HomePage },
    { path: '/individueel', name: 'individueel', component: IndividualPage },
    { path: '/ploegen', name: 'ploegen', component: TeamsPage },
  ],
  scrollBehavior(to) {
    if (to.hash) {
      return { el: to.hash, behavior: 'smooth' }
    }
    return { top: 0 }
  },
})

export default router
