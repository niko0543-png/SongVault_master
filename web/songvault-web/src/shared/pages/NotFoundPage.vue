<script setup lang="ts">
import { RouterLink } from 'vue-router'
import { t } from '@/shared/i18n'
</script>

<template>
  <section class="card">
    <h1>{{ t('shared.notFound.title') }}</h1>
    <p>{{ t('shared.notFound.text') }}</p>
    <RouterLink to="/">{{ t('shared.notFound.back') }}</RouterLink>
  </section>
</template>
