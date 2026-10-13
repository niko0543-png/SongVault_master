<script setup lang="ts">
import { RouterLink } from 'vue-router'
import { publisher } from '../publisher'
</script>

<template>
  <article class="legal">
    <h1>Mentions légales</h1>

    <h2>Éditeur</h2>
    <p>
      {{ publisher.name }}<br />
      {{ publisher.address }}<br />
      {{ publisher.registration }}<br />
      Contact : <a :href="`mailto:${publisher.email}`">{{ publisher.email }}</a>
    </p>
    <p>Directeur de la publication : {{ publisher.director }}</p>

    <h2>Hébergement</h2>
    <p>{{ publisher.host }}</p>

    <h2>Contenus déposés</h2>
    <p>
      Les morceaux, paroles, notes et fichiers déposés restent la propriété de leurs auteurs.
      SongVault ne les utilise que pour les conserver et les montrer aux membres du groupe concerné.
    </p>

    <h2>Données personnelles</h2>
    <p>
      Voir la <RouterLink :to="{ name: 'privacy' }">politique de confidentialité</RouterLink>.
    </p>
  </article>
</template>

<style scoped>
.legal { max-width: 46rem; }
</style>