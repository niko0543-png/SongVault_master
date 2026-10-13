<script setup lang="ts">
import { RouterLink } from 'vue-router'
import { publisher } from '../publisher'
</script>

<template>
  <article class="legal">
    <h1>Politique de confidentialité</h1>
    <p class="muted">Dernière mise à jour : {{ publisher.updatedAt }}</p>

    <h2>Qui traite vos données</h2>
    <p>
      {{ publisher.name }}, {{ publisher.address }}, responsable du traitement.
      Contact : <a :href="`mailto:${publisher.email}`">{{ publisher.email }}</a>.
    </p>

    <h2>Ce que nous traitons, et pourquoi</h2>
    <table>
      <caption class="visually-hidden">Données traitées, finalité, base légale et durée de conservation</caption>
      <thead>
        <tr><th scope="col">Données</th><th scope="col">Pourquoi</th><th scope="col">Base légale</th><th scope="col">Combien de temps</th></tr>
      </thead>
      <tbody>
        <tr>
          <td>Adresse e-mail, mot de passe (stocké sous forme d'empreinte, jamais lisible)</td>
          <td>Créer et protéger votre compte</td>
          <td>Contrat</td>
          <td>Tant que le compte existe, puis 30 jours après une demande de suppression</td>
        </tr>
        <tr>
          <td>Groupes, rôles, morceaux, versions, paroles, notes et fichiers</td>
          <td>Fournir le service aux membres du groupe</td>
          <td>Contrat</td>
          <td>Tant que le groupe existe ; un groupe dont vous êtes le seul membre est effacé avec votre compte</td>
        </tr>
        <tr>
          <td>Invitations : adresse invitée, rôle proposé</td>
          <td>Ajouter une personne à un groupe</td>
          <td>Intérêt légitime du groupe</td>
          <td>Avec l'historique du groupe ; effacées avec le compte qui porte cette adresse</td>
        </tr>
        <tr>
          <td>Archive de vos données</td>
          <td>Vous remettre vos données</td>
          <td>Obligation légale</td>
          <td>7 jours</td>
        </tr>
        <tr>
          <td>Journaux techniques (date, requête, erreurs)</td>
          <td>Sécurité et diagnostic</td>
          <td>Intérêt légitime</td>
          <td>[6 mois, à confirmer]</td>
        </tr>
      </tbody>
    </table>

    <h2>Qui y a accès</h2>
    <p>
      Les membres de vos groupes, selon leur rôle. Nos sous-traitants, tous dans l'Union européenne :
      l'hébergeur ([à compléter]) et Brevo, qui envoie les e-mails. Aucune donnée n'est vendue
      ni utilisée à des fins publicitaires.
    </p>

    <h2>Cookies et stockage du navigateur</h2>
    <ul>
      <li><code>songvault.auth</code> : cookie de session, indispensable pour rester connecté (7 jours au plus).</li>
      <li><code>songvault.band</code> : dernier groupe ouvert, stocké dans votre navigateur.</li>
      <li><code>songvault.afterConfirm</code> : page à rouvrir après la confirmation de votre adresse.</li>
    </ul>
    <p>Aucun cookie de publicité ni de mesure d'audience : aucun consentement n'est donc demandé.</p>

    <h2>Vos droits</h2>
    <p>
      Vous pouvez accéder à vos données, les corriger, les faire effacer, en limiter l'usage, vous y opposer
      et les récupérer. Depuis <RouterLink :to="{ name: 'account' }">Mon compte</RouterLink> : exporter vos
      données et supprimer votre compte. Pour toute autre demande, écrivez à
      <a :href="`mailto:${publisher.email}`">{{ publisher.email }}</a> : réponse sous un mois.
      Vous pouvez aussi saisir la CNIL (<a href="https://www.cnil.fr/fr/plaintes" rel="noopener">cnil.fr</a>).
    </p>
    <p>
      Après une suppression, vos données restent dans les sauvegardes jusqu'à leur rotation ([durée à fixer]).
      Les invitations que vous avez envoyées dans des groupes partagés restent dans ces groupes.
    </p>
  </article>
</template>

<style scoped>
.legal { max-width: 46rem; }
table { width: 100%; border-collapse: collapse; margin: .5rem 0 1rem; font-size: .95rem; }
th, td { text-align: left; vertical-align: top; padding: .5rem; border-bottom: 1px solid var(--border); }
</style>