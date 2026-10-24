import { nextTick, watch } from 'vue'
import { createRouter, createWebHistory, type RouteLocationNormalizedLoaded } from 'vue-router'
import SongListPage from '@/features/songs/pages/SongListPage.vue'
import { useAuthStore } from '@/features/auth/stores/authStore'
import { useBandStore } from '@/features/bands/stores/bandStore'
import { locale, t } from '@/shared/i18n'
import { resolveAuthNavigation } from './authGuard'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    // Accueil : la garde redirige vers /b/<groupe préféré>/songs
    { path: '/', name: 'home', component: SongListPage },
    // Anciens favoris (/songs, /songs/…) d'avant les groupes
    { path: '/songs/:rest(.*)*', redirect: '/' },

    // ---- Publiques ----
    {
      path: '/login',
      name: 'login',
      component: () => import('@/features/auth/pages/LoginPage.vue'),
      meta: { public: true, guestOnly: true, title: 'router.titles.login' },
    },
    {
      path: '/register',
      name: 'register',
      component: () => import('@/features/auth/pages/RegisterPage.vue'),
      meta: { public: true, guestOnly: true, title: 'router.titles.register' },
    },
        {
      path: '/check-email',
      name: 'check-email',
      component: () => import('@/features/auth/pages/CheckEmailPage.vue'),
      meta: { public: true, guestOnly: true, title: 'router.titles.checkEmail' },
    },
    {
      // Lien de l'e-mail de confirmation (#4) ; ouvert connecté aussi, après un changement d'adresse
      path: '/confirm-email',
      name: 'confirm-email',
      component: () => import('@/features/auth/pages/ConfirmEmailPage.vue'),
      meta: { public: true, title: 'router.titles.confirmEmail' },
    },
    {
      path: '/forgot-password',
      name: 'forgot-password',
      component: () => import('@/features/auth/pages/ForgotPasswordPage.vue'),
      meta: { public: true, guestOnly: true, title: 'router.titles.forgotPassword' },
    },
   {
      // Lien de l'e-mail : ouvert connecté aussi, sinon le code serait perdu dans la redirection
      path: '/reset-password',
      name: 'reset-password',
      component: () => import('@/features/auth/pages/ResetPasswordPage.vue'),
      meta: { public: true, title: 'router.titles.resetPassword' },
    },
    {
      // Lien reçu par e-mail : ouvert à tous, connecté ou non (#3)
      path: '/invite/:token',
      name: 'invite',
      component: () => import('@/features/invitations/pages/InvitePage.vue'),
      props: true,
      meta: { public: true, title: 'router.titles.invite' },
    },
    {
      // Pages légales : lisibles par tous, connecté ou non (#5)
      path: '/mentions-legales',
      name: 'legal-notice',
      component: () => import('@/features/legal/pages/LegalNoticePage.vue'),
      meta: { public: true, title: 'router.titles.legalNotice' },
    },
    {
      path: '/confidentialite',
      name: 'privacy',
      component: () => import('@/features/legal/pages/PrivacyPage.vue'),
      meta: { public: true, title: 'router.titles.privacy' },
    },
    {
      // #16 : formules et conditions de vente, lisibles avant de créer un compte
      path: '/formules',
      name: 'plans',
      component: () => import('@/features/billing/pages/PlansPage.vue'),
      meta: { public: true, title: 'router.titles.plans' },
    },
    {
      path: '/conditions-generales-de-vente',
      name: 'terms-of-sale',
      component: () => import('@/features/legal/pages/TermsOfSalePage.vue'),
      meta: { public: true, title: 'router.titles.terms' },
    },
    {
      // Lien des e-mails de notification (#12) : sans connexion, le jeton suffit
      path: '/unsubscribe',
      name: 'unsubscribe',
      component: () => import('@/features/notifications/pages/UnsubscribePage.vue'),
      meta: { public: true, title: 'router.titles.unsubscribe' },
    },
    {
      // #18 : état du service, lisible même quand l'API ne répond plus
      path: '/statut',
      name: 'status',
      component: () => import('@/features/status/pages/StatusPage.vue'),
      meta: { public: true, title: 'router.titles.status' },
    },
    {
      // Lien de partage externe (#13) : écoute seule, sans compte
      path: '/s/:token',
      name: 'shared',
      component: () => import('@/features/sharing/pages/SharedPage.vue'),
      props: true,
      meta: { public: true, title: 'router.titles.shared' },
    },
        // ---- Connexion exigée, hors groupe ----
    {
      path: '/account',
      name: 'account',
      component: () => import('@/features/auth/pages/AccountPage.vue'),
      meta: { title: 'router.titles.account' },
    },
    {
      // Aucun groupe (première connexion, ou dernier groupe quitté) : créer le premier
      path: '/bienvenue',
      name: 'welcome',
      component: () => import('@/features/bands/pages/WelcomePage.vue'),
      meta: { title: 'router.titles.welcome' },
    },

    // ---- Connexion exigée (par défaut), dans un groupe ----
    {
      path: '/b/:bandId/songs',
      name: 'songs',
      component: SongListPage,
      meta: { title: 'router.titles.songs' },
    },
    {
      path: '/b/:bandId/songs/new',
      name: 'song-create',
      component: () => import('@/features/songs/pages/SongCreatePage.vue'),
      meta: { title: 'router.titles.songCreate', write: true },
    },
    {
      path: '/b/:bandId/songs/:songId/edit',
      name: 'song-edit',
      component: () => import('@/features/songs/pages/SongEditPage.vue'),
      props: true,
      meta: { title: 'router.titles.songEdit', write: true },
    },
    {
      path: '/b/:bandId/songs/:songId/compare',
      name: 'version-compare',
      component: () => import('@/features/versions/pages/VersionComparePage.vue'),
      props: true,
      meta: { title: 'router.titles.versionCompare' },
    },
    {
      // #32 : le lecteur Guitar Pro sur une page à lui, en plein écran (ADR 0038)
      path: '/b/:bandId/songs/:songId/versions/:versionId/tablature/:fileId',
      name: 'tablature',
      component: () => import('@/features/guitarpro/pages/TablaturePage.vue'),
      props: true,
      meta: { title: 'router.titles.tablature' },
    },
    {
      path: '/b/:bandId/songs/:songId/versions/:versionId',
      name: 'version-detail',
      component: () => import('@/features/versions/pages/VersionDetailPage.vue'),
      props: true,
      meta: { title: 'router.titles.version' },
    },
    {
      path: '/b/:bandId/songs/:songId',
      name: 'song-detail',
      component: () => import('@/features/songs/pages/SongDetailPage.vue'),
      props: true,
      meta: { title: 'router.titles.songDetail' },
    },
    {
      path: '/b/:bandId/releases',
      name: 'releases',
      component: () => import('@/features/releases/pages/ReleaseListPage.vue'),
      meta: { title: 'router.titles.releases' },
    },
    {
      path: '/b/:bandId/releases/:releaseId',
      name: 'release-detail',
      component: () => import('@/features/releases/pages/ReleaseDetailPage.vue'),
      props: true,
      meta: { title: 'router.titles.release' },
    },
    {
      path: '/b/:bandId/setlists',
      name: 'setlists',
      component: () => import('@/features/setlists/pages/SetlistListPage.vue'),
      meta: { title: 'router.titles.setlists' },
    },
    {
      path: '/b/:bandId/setlists/:setlistId',
      name: 'setlist-detail',
      component: () => import('@/features/setlists/pages/SetlistDetailPage.vue'),
      props: true,
      meta: { title: 'router.titles.setlist' },
    },
    {
      path: '/b/:bandId/activity',
      name: 'activity',
      component: () => import('@/features/activity/pages/ActivityFeedPage.vue'),
      meta: { title: 'router.titles.activity' },
    },
    {
      path: '/b/:bandId/members',
      name: 'band-members',
      component: () => import('@/features/bands/pages/MembersPage.vue'),
      meta: { title: 'router.titles.members' },
    },
    {
      // #20 : corbeille ; un Guest n'y a pas accès (l'API répond 403), il retourne à la liste
      path: '/b/:bandId/trash',
      name: 'trash',
      component: () => import('@/features/trash/pages/TrashPage.vue'),
      meta: { title: 'router.titles.trash', write: true },
    },
    {
      // #19 : journal d'audit, réservé aux Owner (l'API répond 403 aux autres)
      path: '/b/:bandId/audit',
      name: 'audit',
      component: () => import('@/features/audit/pages/AuditLogPage.vue'),
      meta: { title: 'router.titles.audit', owner: true },
    },
    {
      // #16 : la page existait, sa route manquait (le lien « Facturation » ne menait nulle part)
      path: '/b/:bandId/billing',
      name: 'billing',
      component: () => import('@/features/billing/pages/BillingPage.vue'),
      meta: { title: 'router.titles.billing', owner: true },
    },
    {
      // #16 : retour de Stripe Checkout sur ?checkout=success
      path: '/b/:bandId/billing',
      name: 'billing',
      component: () => import('@/features/billing/pages/BillingPage.vue'),
      meta: { title: 'router.titles.billing', owner: true },
    },
    {
      path: '/:pathMatch(.*)*',
      name: 'not-found',
      component: () => import('@/shared/pages/NotFoundPage.vue'),
      meta: { public: true, title: 'router.titles.notFound' },
    },
  ],
})

// 1. Avant la navigation : contrôle d'accès, puis groupe actif
router.beforeEach(async (to) => {
  const auth = useAuthStore()
  await auth.ensureLoaded()
  const decision = resolveAuthNavigation(to, auth.isAuthenticated)
  if (decision !== true || to.meta.public) return decision

  const bands = useBandStore()
  await bands.ensureLoaded()
  const bandId = typeof to.params.bandId === 'string' ? to.params.bandId : null

  const preferred = bands.preferredId()
  if (!bandId) {
    // Page d'accueil sans groupe : seulement tant qu'on n'en a aucun
    if (to.name === 'welcome') return preferred ? { name: 'songs', params: { bandId: preferred } } : true
    if (to.name !== 'home') return true                 // page hors groupe : Mon compte (#4)
    // « / » : ouvre le dernier groupe utilisé (ou le premier) ; aucun groupe, l'accueil invite à en créer un
    return preferred ? { name: 'songs', params: { bandId: preferred } } : { name: 'welcome' }
  }
  if (!preferred) return { name: 'welcome' }            // lien vers un groupe alors qu'on n'en a plus aucun
  // Groupe inconnu ou dont on n'est pas membre : même page que pour une adresse inexistante
  if (!bands.has(bandId)) return { name: 'not-found', params: { pathMatch: to.path.slice(1).split('/') } }
 bands.setActive(bandId)
  // Page d'écriture ouverte par un Guest (lien partagé, favori) : retour à la liste
  if (to.meta.write && !bands.can('write')) return { name: 'songs', params: { bandId } }
  // #19 : de même pour une page d'Owner ouverte par un autre membre (meta.owner, prévu depuis #16)
  if (to.meta.owner && !bands.can('admin')) return { name: 'songs', params: { bandId } }
    if (to.meta.owner && !bands.can('admin')) return { name: 'songs', params: { bandId } }
  return true
})

// 2. Après la navigation : titre de l'onglet + focus
function setTitle(to: RouteLocationNormalizedLoaded) {
  const band = to.params.bandId ? useBandStore().active?.name : undefined
  document.title = [to.meta.title && t(to.meta.title), band, 'SongVault'].filter(Boolean).join(' · ')
}

router.afterEach((to, from, failure) => {
  if (failure) return                                   // navigation annulée (ex. « Quitter sans enregistrer ? » → Annuler)
  setTitle(to)

  // Premier chargement, ou simple changement de query (recherche) : on ne déplace pas le focus
  if (!from.matched.length || to.path === from.path) return
  void nextTick(() => document.getElementById('main-content')?.focus({ preventScroll: true }))
})

// #26 : le titre de l'onglet suit aussi un changement de langue
watch(locale, () => setTitle(router.currentRoute.value))

export default router