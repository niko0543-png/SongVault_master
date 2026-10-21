<script setup lang="ts">
import { RouterLink } from 'vue-router'
import { publisher } from '../publisher'
import { locale, t } from '@/shared/i18n'
</script>

<!-- #16 : à faire relire avant d'ouvrir le paiement (ADR 0023) ; les crochets sont dans publisher.ts -->
<template>
  <article class="legal">
    <h1>{{ t('legal.terms.title') }}</h1>
    <p v-if="locale === 'en'" class="muted" data-testid="french-prevails">{{ t('legal.englishNote') }}</p>
    <p class="muted">{{ t('legal.terms.updated', { updatedAt: publisher.updatedAt }) }}</p>

    <h2>{{ t('legal.terms.seller') }}</h2>
    <p>
      {{ publisher.name }}, {{ publisher.address }}, {{ publisher.registration }}.
      {{ t('legal.notice.contact') }} <a :href="`mailto:${publisher.email}`">{{ publisher.email }}</a>.
    </p>

    <h2>{{ t('legal.terms.what') }}</h2>
    <p>
      {{ t('legal.terms.whatText') }} <RouterLink :to="{ name: 'plans' }">{{ t('legal.terms.plansLink') }}</RouterLink>.
    </p>

    <h2>{{ t('legal.terms.price') }}</h2>
    <p>
      {{ t('legal.terms.priceText') }}
    </p>

    <h2>{{ t('legal.terms.duration') }}</h2>
    <p>
      {{ t('legal.terms.durationText') }}
    </p>

    <h2>{{ t('legal.terms.declined') }}</h2>
    <p>
      {{ t('legal.terms.declinedText') }}
    </p>

    <h2>{{ t('legal.terms.withdrawal') }}</h2>
    <p>
      {{ t('legal.terms.withdrawalBefore') }}
      <a :href="`mailto:${publisher.email}`">{{ publisher.email }}</a>{{ t('legal.terms.withdrawalAfter') }}
    </p>

    <h2>{{ t('legal.terms.service') }}</h2>
    <p>
      {{ t('legal.terms.serviceText') }}
    </p>

    <h2>{{ t('legal.terms.personalData') }}</h2>
    <p>{{ t('legal.terms.see') }} <RouterLink :to="{ name: 'privacy' }">{{ t('legal.terms.privacyLink') }}</RouterLink>.</p>

    <h2>{{ t('legal.terms.disputes') }}</h2>
    <p>
      {{ t('legal.terms.disputesText', { mediator: publisher.mediator }) }}
    </p>
  </article>
</template>

<style scoped>
.legal { max-width: 46rem; }
</style>