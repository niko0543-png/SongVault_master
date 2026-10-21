<script setup lang="ts">
import { t } from '@/shared/i18n'
defineProps<{ message: string }>()
defineEmits<{ retry: [] }>()
</script>

<template>
  <div class="card" role="alert" data-testid="error-state">
    <p>{{ message }}</p>
    <button type="button" class="secondary" @click="$emit('retry')">{{ t('common.actions.retry') }}</button>
  </div>
</template>
