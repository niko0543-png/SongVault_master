<script setup lang="ts">
import { RouterLink } from 'vue-router'
import { publisher } from '../publisher'
import { locale, t } from '@/shared/i18n'
</script>

<template>
  <article class="legal">
    <h1>{{ t('legal.notice.title') }}</h1>
    <p v-if="locale === 'en'" class="muted" data-testid="french-prevails">{{ t('legal.englishNote') }}</p>

    <h2>{{ t('legal.notice.publisher') }}</h2>
    <p>
      {{ publisher.name }}<br />
      {{ publisher.address }}<br />
      {{ publisher.registration }}<br />
      {{ t('legal.notice.contact') }} <a :href="`mailto:${publisher.email}`">{{ publisher.email }}</a>
    </p>
    <p>{{ t('legal.notice.director', { director: publisher.director }) }}</p>

    <h2>{{ t('legal.notice.hosting') }}</h2>
    <p>{{ publisher.host }}</p>

    <h2>{{ t('legal.notice.content') }}</h2>
    <p>
      {{ t('legal.notice.contentText') }}
    </p>

    <h2>{{ t('legal.notice.personalData') }}</h2>
    <p>
      {{ t('legal.notice.see') }} <RouterLink :to="{ name: 'privacy' }">{{ t('legal.notice.privacyLink') }}</RouterLink>.
    </p>
  </article>
</template>

<style scoped>
.legal { max-width: 46rem; }
</style>