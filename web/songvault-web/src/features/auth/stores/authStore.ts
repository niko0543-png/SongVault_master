import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { authApi } from '../api/authApi'
import type { Credentials, CurrentUser } from '../types'
import { useSongsStore } from '@/features/songs/stores/songsStore'
import { usePlayerStore } from '@/features/player/stores/playerStore'

export const useAuthStore = defineStore('auth', () => {
  const user = ref<CurrentUser | null>(null)
  const isLoaded = ref(false)
  const isAuthenticated = computed(() => user.value !== null)

  let pending: Promise<void> | null = null

  /** Interroge /me une seule fois (au premier passage dans la garde). */
  function ensureLoaded(): Promise<void> {
    if (isLoaded.value) return Promise.resolve()
    pending ??= authApi.me()
      .then((me) => { user.value = me })
      .catch(() => { user.value = null })          // 401 = anonyme
      .finally(() => {
        isLoaded.value = true
        pending = null
      })
    return pending
  }

  async function login(credentials: Credentials) {
    await authApi.login(credentials)
    user.value = await authApi.me()
    isLoaded.value = true
  }

  async function register(credentials: Credentials) {
    await authApi.register(credentials)
    await login(credentials)                       // inscription réussie → connexion directe
  }

  /** Efface tout ce qui appartient à l'utilisateur, côté navigateur. */
  function clearSession() {
    user.value = null
    isLoaded.value = true
    useSongsStore().reset()
    usePlayerStore().stop()
  }

  async function logout() {
    try {
      await authApi.logout()
    } finally {
      clearSession()                               // même si l'appel échoue
    }
  }

  return { user, isLoaded, isAuthenticated, ensureLoaded, login, register, logout, clearSession }
})