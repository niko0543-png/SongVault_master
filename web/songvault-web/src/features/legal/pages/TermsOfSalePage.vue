<script setup lang="ts">
import { RouterLink } from 'vue-router'
import { publisher } from '../publisher'
</script>

<!-- #16 : à faire relire avant d'ouvrir le paiement (ADR 0023) ; les crochets sont dans publisher.ts -->
<template>
  <article class="legal">
    <h1>Conditions générales de vente</h1>
    <p class="muted">Dernière mise à jour : {{ publisher.updatedAt }}</p>

    <h2>1. Vendeur</h2>
    <p>
      {{ publisher.name }}, {{ publisher.address }}, {{ publisher.registration }}.
      Contact : <a :href="`mailto:${publisher.email}`">{{ publisher.email }}</a>.
    </p>

    <h2>2. Ce qui est vendu</h2>
    <p>
      Un abonnement mensuel à une formule payante de SongVault, pour un groupe : il augmente la place
      disponible pour les fichiers du groupe et le nombre de groupes dont vous pouvez être propriétaire.
      Les formules, leurs limites et leurs prix sont sur la page <RouterLink :to="{ name: 'plans' }">Formules</RouterLink>.
    </p>

    <h2>3. Prix et paiement</h2>
    <p>
      Les prix sont indiqués en euros, toutes taxes comprises. Le paiement se fait par carte, chaque mois à la date
      anniversaire de la souscription, par l'intermédiaire de Stripe. SongVault ne reçoit ni ne conserve vos
      coordonnées bancaires. Une facture est disponible pour chaque paiement dans « Gérer l'abonnement ».
      Un changement de prix est annoncé par e-mail au moins 30 jours avant de s'appliquer ; vous pouvez résilier
      avant.
    </p>

    <h2>4. Durée et résiliation</h2>
    <p>
      L'abonnement est sans engagement et se renouvelle chaque mois. Vous pouvez le résilier à tout moment depuis la
      page Facturation du groupe, bouton « Gérer l'abonnement » : la formule reste en place jusqu'à la fin du mois
      payé, puis le groupe repasse sur la formule gratuite. Aucun fichier n'est effacé ; au-delà du quota gratuit,
      les nouveaux envois sont refusés.
    </p>

    <h2>5. Paiement refusé</h2>
    <p>
      Si un paiement échoue, les propriétaires du groupe sont prévenus par e-mail et Stripe réessaie pendant quelques
      jours. Sans paiement à l'issue de ces tentatives, l'abonnement prend fin et le groupe repasse sur la formule
      gratuite, comme pour une résiliation.
    </p>

    <h2>6. Droit de rétractation</h2>
    <p>
      Vous disposez de 14 jours après la souscription pour vous rétracter, en écrivant à
      <a :href="`mailto:${publisher.email}`">{{ publisher.email }}</a>. En souscrivant, vous demandez que le
      service commence tout de suite : en cas de rétractation, le montant correspondant aux jours déjà utilisés
      reste dû et le reste vous est remboursé sous 14 jours, sur la carte utilisée.
    </p>

    <h2>7. Service et responsabilité</h2>
    <p>
      SongVault conserve les fichiers et les données du groupe avec des sauvegardes quotidiennes, sans garantie de
      disponibilité permanente. Les contenus déposés restent la propriété de leurs auteurs ; vous garantissez
      avoir le droit de les déposer.
    </p>

    <h2>8. Données personnelles</h2>
    <p>Voir la <RouterLink :to="{ name: 'privacy' }">politique de confidentialité</RouterLink>.</p>

    <h2>9. Litiges</h2>
    <p>
      Les présentes conditions sont soumises au droit français. En cas de litige, écrivez-nous d'abord. Si vous êtes
      consommateur, vous pouvez ensuite recourir gratuitement au médiateur de la consommation :
      {{ publisher.mediator }}.
    </p>
  </article>
</template>

<style scoped>
.legal { max-width: 46rem; }
</style>