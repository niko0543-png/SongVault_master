<script setup lang="ts">
import { RouterLink } from 'vue-router'
import { publisher } from '../publisher'
import { locale, t } from '@/shared/i18n'
</script>

<template>
  <article class="legal">
    <h1>{{ t('legal.privacy.title') }}</h1>
    <p v-if="locale === 'en'" class="muted" data-testid="french-prevails">{{ t('legal.englishNote') }}</p>
    <p class="muted">{{ t('legal.privacy.updated', { updatedAt: publisher.updatedAt }) }}</p>

    <h2>{{ t('legal.privacy.who') }}</h2>
    <p>
      {{ t('legal.privacy.controller', { name: publisher.name, address: publisher.address }) }} <a :href="`mailto:${publisher.email}`">{{ publisher.email }}</a>.
    </p>

    <h2>{{ t('legal.privacy.what') }}</h2>
    <table>
      <caption class="visually-hidden">{{ t('legal.privacy.caption') }}</caption>
      <thead>
        <tr><th scope="col">{{ t('legal.privacy.colData') }}</th><th scope="col">{{ t('legal.privacy.colWhy') }}</th><th scope="col">{{ t('legal.privacy.colBasis') }}</th><th scope="col">{{ t('legal.privacy.colHowLong') }}</th></tr>
      </thead>
      <tbody>
        <tr>
          <td :data-label="t('legal.privacy.colData')">{{ t('legal.privacy.accountData') }}</td>
          <td :data-label="t('legal.privacy.colWhy')">{{ t('legal.privacy.accountWhy') }}</td>
          <td :data-label="t('legal.privacy.colBasis')">{{ t('legal.privacy.contract') }}</td>
          <td :data-label="t('legal.privacy.colHowLong')">{{ t('legal.privacy.accountHowLong') }}</td>
        </tr>
        <tr>
          <td :data-label="t('legal.privacy.colData')">{{ t('legal.privacy.bandData') }}</td>
          <td :data-label="t('legal.privacy.colWhy')">{{ t('legal.privacy.bandWhy') }}</td>
          <td :data-label="t('legal.privacy.colBasis')">{{ t('legal.privacy.contract') }}</td>
          <td :data-label="t('legal.privacy.colHowLong')">{{ t('legal.privacy.bandHowLong') }}</td>
        </tr>
        <tr>
          <td :data-label="t('legal.privacy.colData')">{{ t('legal.privacy.commentData') }}</td>
          <td :data-label="t('legal.privacy.colWhy')">{{ t('legal.privacy.commentWhy') }}</td>
          <td :data-label="t('legal.privacy.colBasis')">{{ t('legal.privacy.contract') }}</td>
          <td :data-label="t('legal.privacy.colHowLong')">{{ t('legal.privacy.commentHowLong') }}</td>
        </tr>
        <tr>
          <td :data-label="t('legal.privacy.colData')">{{ t('legal.privacy.invitationData') }}</td>
          <td :data-label="t('legal.privacy.colWhy')">{{ t('legal.privacy.invitationWhy') }}</td>
          <td :data-label="t('legal.privacy.colBasis')">{{ t('legal.privacy.bandInterest') }}</td>
          <td :data-label="t('legal.privacy.colHowLong')">{{ t('legal.privacy.invitationHowLong') }}</td>
        </tr>
        <tr>
          <td :data-label="t('legal.privacy.colData')">
            {{ t('legal.privacy.shareData') }}
          </td>
          <td :data-label="t('legal.privacy.colWhy')">{{ t('legal.privacy.shareWhy') }}</td>
          <td :data-label="t('legal.privacy.colBasis')">{{ t('legal.privacy.bandInterest') }}</td>
          <td :data-label="t('legal.privacy.colHowLong')">{{ t('legal.privacy.shareHowLong') }}</td>
        </tr>
        <tr>
          <td :data-label="t('legal.privacy.colData')">
            {{ t('legal.privacy.billingData') }}
          </td>
          <td :data-label="t('legal.privacy.colWhy')">{{ t('legal.privacy.billingWhy') }}</td>
          <td :data-label="t('legal.privacy.colBasis')">{{ t('legal.privacy.contract') }}</td>
          <td :data-label="t('legal.privacy.colHowLong')">{{ t('legal.privacy.billingHowLong') }}</td>
        </tr>
        <tr>
          <td :data-label="t('legal.privacy.colData')">
            {{ t('legal.privacy.loginData') }}
          </td>
          <td :data-label="t('legal.privacy.colWhy')">{{ t('legal.privacy.loginWhy') }}</td>
          <td :data-label="t('legal.privacy.colBasis')">{{ t('legal.privacy.securityInterest') }}</td>
          <td :data-label="t('legal.privacy.colHowLong')">{{ t('legal.privacy.loginHowLong') }}</td>
        </tr>
        <tr>
          <td :data-label="t('legal.privacy.colData')">
            {{ t('legal.privacy.auditData') }}
          </td>
          <td :data-label="t('legal.privacy.colWhy')">{{ t('legal.privacy.auditWhy') }}</td>
          <td :data-label="t('legal.privacy.colBasis')">{{ t('legal.privacy.bandInterest') }}</td>
          <td :data-label="t('legal.privacy.colHowLong')">{{ t('legal.privacy.auditHowLong') }}</td>
        </tr>
        <tr>
          <td :data-label="t('legal.privacy.colData')">{{ t('legal.privacy.exportData') }}</td>
          <td :data-label="t('legal.privacy.colWhy')">{{ t('legal.privacy.exportWhy') }}</td>
          <td :data-label="t('legal.privacy.colBasis')">{{ t('legal.privacy.legalObligation') }}</td>
          <td :data-label="t('legal.privacy.colHowLong')">{{ t('legal.privacy.sevenDays') }}</td>
        </tr>
        <tr>
          <td :data-label="t('legal.privacy.colData')">{{ t('legal.privacy.logsData') }}</td>
          <td :data-label="t('legal.privacy.colWhy')">{{ t('legal.privacy.logsWhy') }}</td>
          <td :data-label="t('legal.privacy.colBasis')">{{ t('legal.privacy.legitimateInterest') }}</td>
          <td :data-label="t('legal.privacy.colHowLong')">{{ t('legal.privacy.thirtyDays') }}</td>
        </tr>
      </tbody>
    </table>

    <h2>{{ t('legal.privacy.access') }}</h2>
   <p>
      {{ t('legal.privacy.accessText') }}
    </p>
    <h2>{{ t('legal.privacy.cookies') }}</h2>
    <ul>
      <li><code>songvault.auth</code> {{ t('legal.privacy.cookieSession') }}</li>
      <li><code>songvault.band</code> {{ t('legal.privacy.storageBand') }}</li>
      <li><code>songvault.afterConfirm</code> {{ t('legal.privacy.storageRedirect') }}</li>
      <li><code>songvault.2fa</code> {{ t('legal.privacy.cookieTwoFactor') }}</li>
        <li><code>songvault.share</code> {{ t('legal.privacy.cookieShare') }}</li>
    </ul>
    <p>{{ t('legal.privacy.noConsent') }}</p>

    <h2>{{ t('legal.privacy.rights') }}</h2>
    <p>
      {{ t('legal.privacy.rightsText') }} <RouterLink :to="{ name: 'account' }">{{ t('legal.privacy.accountLink') }}</RouterLink> {{ t('legal.privacy.rightsAccount') }}
      <a :href="`mailto:${publisher.email}`">{{ publisher.email }}</a> {{ t('legal.privacy.rightsAnswer') }}<a href="https://www.cnil.fr/fr/plaintes" rel="noopener">cnil.fr</a>).
    </p>
    <p>
            {{ t('legal.privacy.backups') }}
    </p>
  </article>
</template>

<style scoped>
.legal { max-width: 46rem; }
table { width: 100%; border-collapse: collapse; margin: .5rem 0 1rem; font-size: .95rem; }
th, td { text-align: left; vertical-align: top; padding: .5rem; border-bottom: 1px solid var(--border); }
/* #14 : téléphone. Une fiche par ligne du tableau, chaque cellule précédée de son en-tête */
@media (max-width: 600px) {
  thead { position: absolute; width: 1px; height: 1px; overflow: hidden; clip-path: inset(50%); }
  tr { display: block; padding: .5rem 0; border-bottom: 1px solid var(--border); }
  td { display: block; border: 0; padding: .15rem 0; }
  td::before { display: block; font-weight: 600; font-size: .85rem; color: var(--muted); }
  td::before { content: attr(data-label); }
}
</style>