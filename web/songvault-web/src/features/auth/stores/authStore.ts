import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { authApi } from '../api/authApi'
import type { Credentials, CurrentUser } from '../types'
import { useSongsStore } from '@/features/songs/stores/songsStore'
import { usePlayerStore } from '@/features/player/stores/playerStore'
import { useBandStore } from '@/features/bands/stores/bandStore' 
import { isLocale, setLocale, type Locale } from '@/shared/i18n'

export const useAuthStore = defineStore('auth', () => {
  const user = ref<CurrentUser | null>(null)
  const isLoaded = ref(false)
  const isAuthenticated = computed(() => user.value !== null)

  let pending: Promise<void> | null = null

    /** #26 : la langue enregistrée sur le compte l'emporte sur celle de l'appareil. */
  function signedIn(me: CurrentUser) {
    user.value = me
    if (isLocale(me.locale)) setLocale(me.locale)
  }

  /** #26 : change la langue tout de suite, puis l'enregistre sur le compte si l'on est connecté. */
  async function changeLocale(next: Locale) {
    setLocale(next)
    if (!user.value) return
    await authApi.saveLocale(next)
    user.value = { ...user.value, locale: next }
  }


  /** Interroge /me une seule fois (au premier passage dans la garde). */
  function ensureLoaded(): Promise<void> {
    if (isLoaded.value) return Promise.resolve()
    pending ??= authApi.me()
      .then(signedIn)
      .catch(() => { user.value = null })          // 401 = anonyme
      .finally(() => {
        isLoaded.value = true
        pending = null
      })
    return pending
  }

  async function login(credentials: Credentials) {
    await authApi.login(credentials)
    signedIn(await authApi.me())
    isLoaded.value = true
  }

 /** Crée le compte. La connexion attend la confirmation de l'adresse (#4, ADR 0010). */
  async function register(credentials: Credentials) {
    await authApi.register(credentials)
  }

  /** Efface tout ce qui appartient à l'utilisateur, côté navigateur. */
  function clearSession() {
    user.value = null
    isLoaded.value = true
    useSongsStore().reset()
    usePlayerStore().stop()
    useBandStore().reset()
  }

  async function logout() {
    try {
      await authApi.logout()
    } finally {
      clearSession()                               // même si l'appel échoue
    }
  }

  return { user, isLoaded, isAuthenticated, ensureLoaded, login, register, logout, clearSession, changeLocale }
})