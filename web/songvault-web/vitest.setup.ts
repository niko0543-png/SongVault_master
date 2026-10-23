import { setLocale } from '@/shared/i18n'

// #26 : jsdom annonce un navigateur anglais ; les tests vérifient les textes français, sauf s'ils changent de langue
setLocale('fr')