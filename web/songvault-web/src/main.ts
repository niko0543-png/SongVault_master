import './assets/main.css'
import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router'
import { useAuthStore } from './features/auth/stores/authStore'
import { setUnauthorizedHandler } from './shared/api/httpClient'
import { registerServiceWorker } from './app/registerServiceWorker'

const app = createApp(App)
app.use(createPinia())             // AVANT le routeur : la garde utilise un store
app.use(router)

// Session expirée pendant l'utilisation : on efface tout et on renvoie vers la connexion
const auth = useAuthStore()
setUnauthorizedHandler(() => {
  auth.clearSession()
  const current = router.currentRoute.value
  if (!current.meta.public) void router.push({ name: 'login', query: { redirect: current.fullPath } })
})

app.mount('#app')
registerServiceWorker()                 // #14 : application installable, page hors connexion