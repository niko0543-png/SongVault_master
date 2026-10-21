<script setup lang="ts">
import { t } from '@/shared/i18n'

defineProps<{ message?: string }>()
</script>

<template>
  <p class="muted" role="status" aria-live="polite" data-testid="loading-state">{{ message ?? t('shared.loading') }}</p>
</template>
