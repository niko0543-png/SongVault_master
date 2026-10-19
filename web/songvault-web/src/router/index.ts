import { nextTick } from 'vue'
import { createRouter, createWebHistory } from 'vue-router'
import SongListPage from '@/features/songs/pages/SongListPage.vue'
import { useAuthStore } from '@/features/auth/stores/authStore'
import { useBandStore } from '@/features/bands/stores/bandStore'
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
      meta: { public: true, guestOnly: true, title: 'Connexion' },
    },
    {
      path: '/register',
      name: 'register',
      component: () => import('@/features/auth/pages/RegisterPage.vue'),
      meta: { public: true, guestOnly: true, title: 'Créer un compte' },
    },
        {
      path: '/check-email',
      name: 'check-email',
      component: () => import('@/features/auth/pages/CheckEmailPage.vue'),
      meta: { public: true, guestOnly: true, title: 'Vérifiez votre boîte mail' },
    },
    {
      // Lien de l'e-mail de confirmation (#4) ; ouvert connecté aussi, après un changement d'adresse
      path: '/confirm-email',
      name: 'confirm-email',
      component: () => import('@/features/auth/pages/ConfirmEmailPage.vue'),
      meta: { public: true, title: "Confirmation de l'adresse" },
    },
    {
      path: '/forgot-password',
      name: 'forgot-password',
      component: () => import('@/features/auth/pages/ForgotPasswordPage.vue'),
      meta: { public: true, guestOnly: true, title: 'Mot de passe oublié' },
    },
   {
      // Lien de l'e-mail : ouvert connecté aussi, sinon le code serait perdu dans la redirection
      path: '/reset-password',
      name: 'reset-password',
      component: () => import('@/features/auth/pages/ResetPasswordPage.vue'),
      meta: { public: true, title: 'Nouveau mot de passe' },
    },
    {
      // Lien reçu par e-mail : ouvert à tous, connecté ou non (#3)
      path: '/invite/:token',
      name: 'invite',
      component: () => import('@/features/invitations/pages/InvitePage.vue'),
      props: true,
      meta: { public: true, title: 'Invitation' },
    },
    {
      // Pages légales : lisibles par tous, connecté ou non (#5)
      path: '/mentions-legales',
      name: 'legal-notice',
      component: () => import('@/features/legal/pages/LegalNoticePage.vue'),
      meta: { public: true, title: 'Mentions légales' },
    },
    {
      path: '/confidentialite',
      name: 'privacy',
      component: () => import('@/features/legal/pages/PrivacyPage.vue'),
      meta: { public: true, title: 'Politique de confidentialité' },
    },
    {
      // #16 : formules et conditions de vente, lisibles avant de créer un compte
      path: '/formules',
      name: 'plans',
      component: () => import('@/features/billing/pages/PlansPage.vue'),
      meta: { public: true, title: 'Formules' },
    },
    {
      path: '/conditions-generales-de-vente',
      name: 'terms-of-sale',
      component: () => import('@/features/legal/pages/TermsOfSalePage.vue'),
      meta: { public: true, title: 'Conditions générales de vente' },
    },
    {
      // Lien des e-mails de notification (#12) : sans connexion, le jeton suffit
      path: '/unsubscribe',
      name: 'unsubscribe',
      component: () => import('@/features/notifications/pages/UnsubscribePage.vue'),
      meta: { public: true, title: 'Se désinscrire' },
    },
    {
      // #18 : état du service, lisible même quand l'API ne répond plus
      path: '/statut',
      name: 'status',
      component: () => import('@/features/status/pages/StatusPage.vue'),
      meta: { public: true, title: 'État du service' },
    },
    {
      // Lien de partage externe (#13) : écoute seule, sans compte
      path: '/s/:token',
      name: 'shared',
      component: () => import('@/features/sharing/pages/SharedPage.vue'),
      props: true,
      meta: { public: true, title: 'Écoute partagée' },
    },
        // ---- Connexion exigée, hors groupe ----
    {
      path: '/account',
      name: 'account',
      component: () => import('@/features/auth/pages/AccountPage.vue'),
      meta: { title: 'Mon compte' },
    },

    // ---- Connexion exigée (par défaut), dans un groupe ----
    {
      path: '/b/:bandId/songs',
      name: 'songs',
      component: SongListPage,
      meta: { title: 'Morceaux' },
    },
    {
      path: '/b/:bandId/songs/new',
      name: 'song-create',
      component: () => import('@/features/songs/pages/SongCreatePage.vue'),
      meta: { title: 'Nouveau morceau' , write: true },
    },
    {
      path: '/b/:bandId/songs/:songId/edit',
      name: 'song-edit',
      component: () => import('@/features/songs/pages/SongEditPage.vue'),
      props: true,
      meta: { title: 'Modifier le morceau' , write: true },
    },
    {
      path: '/b/:bandId/songs/:songId/compare',
      name: 'version-compare',
      component: () => import('@/features/versions/pages/VersionComparePage.vue'),
      props: true,
      meta: { title: 'Comparer des versions' },
    },
    {
      path: '/b/:bandId/songs/:songId/versions/:versionId',
      name: 'version-detail',
      component: () => import('@/features/versions/pages/VersionDetailPage.vue'),
      props: true,
      meta: { title: 'Version' },
    },
    {
      path: '/b/:bandId/songs/:songId',
      name: 'song-detail',
      component: () => import('@/features/songs/pages/SongDetailPage.vue'),
      props: true,
      meta: { title: 'Détail du morceau' },
    },
    {
      path: '/b/:bandId/releases',
      name: 'releases',
      component: () => import('@/features/releases/pages/ReleaseListPage.vue'),
      meta: { title: 'Sorties' },
    },
    {
      path: '/b/:bandId/releases/:releaseId',
      name: 'release-detail',
      component: () => import('@/features/releases/pages/ReleaseDetailPage.vue'),
      props: true,
      meta: { title: 'Sortie' },
    },
       {
      path: '/b/:bandId/activity',
      name: 'activity',
      component: () => import('@/features/activity/pages/ActivityFeedPage.vue'),
      meta: { title: 'Activité' },
    },
    {
      path: '/b/:bandId/members',
      name: 'band-members',
      component: () => import('@/features/bands/pages/MembersPage.vue'),
      meta: { title: 'Membres' },
    },
    {
      // #20 : corbeille ; un Guest n'y a pas accès (l'API répond 403), il retourne à la liste
      path: '/b/:bandId/trash',
      name: 'trash',
      component: () => import('@/features/trash/pages/TrashPage.vue'),
      meta: { title: 'Corbeille', write: true },
    },
    {
      // #19 : journal d'audit, réservé aux Owner (l'API répond 403 aux autres)
      path: '/b/:bandId/audit',
      name: 'audit',
      component: () => import('@/features/audit/pages/AuditLogPage.vue'),
      meta: { title: 'Journal d’audit', owner: true },
    },
    {
      // #16 : la page existait, sa route manquait (le lien « Facturation » ne menait nulle part)
      path: '/b/:bandId/billing',
      name: 'billing',
      component: () => import('@/features/billing/pages/BillingPage.vue'),
      meta: { title: 'Facturation', owner: true },
    },
    {
      // #16 : retour de Stripe Checkout sur ?checkout=success
      path: '/b/:bandId/billing',
      name: 'billing',
      component: () => import('@/features/billing/pages/BillingPage.vue'),
      meta: { title: 'Facturation', owner: true },
    },
    {
      path: '/:pathMatch(.*)*',
      name: 'not-found',
      component: () => import('@/shared/pages/NotFoundPage.vue'),
      meta: { public: true, title: 'Page introuvable' },
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

  if (!bandId) {
    if (to.name !== 'home') return true                 // page hors groupe : Mon compte (#4)
    // « / » : ouvre le dernier groupe utilisé (ou le premier)
    const preferred = bands.preferredId()
    return preferred ? { name: 'songs', params: { bandId: preferred } } : true
  }
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
router.afterEach((to, from, failure) => {
  if (failure) return                                   // navigation annulée (ex. « Quitter sans enregistrer ? » → Annuler)
  const band = to.params.bandId ? useBandStore().active?.name : undefined
  document.title = [to.meta.title, band, 'SongVault'].filter(Boolean).join(' · ')

  // Premier chargement, ou simple changement de query (recherche) : on ne déplace pas le focus
  if (!from.matched.length || to.path === from.path) return
  void nextTick(() => document.getElementById('main-content')?.focus({ preventScroll: true }))
})

export default router