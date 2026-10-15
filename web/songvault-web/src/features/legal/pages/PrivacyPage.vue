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
          <td>Commentaires sur les fichiers audio : texte, position dans l'enregistrement, auteur</td>
          <td>Échanger avec les membres du groupe</td>
          <td>Contrat</td>
          <td>Tant que le fichier existe ; après la suppression de votre compte, ceux des groupes partagés restent sans votre nom</td>
        </tr>
        <tr>
          <td>Invitations : adresse invitée, rôle proposé</td>
          <td>Ajouter une personne à un groupe</td>
          <td>Intérêt légitime du groupe</td>
          <td>Avec l'historique du groupe ; effacées avec le compte qui porte cette adresse</td>
        </tr>
        <tr>
          <td>
            Lien de partage ouvert sans compte : date, fichier écouté, navigateur, et une empreinte de
            votre adresse IP (jamais l'adresse elle-même)
          </td>
          <td>Montrer au groupe qui vous a envoyé le lien s'il a été ouvert et écouté</td>
          <td>Intérêt légitime du groupe</td>
          <td>Tant que le lien existe ; effacé quand le groupe le supprime</td>
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
          <td>30 jours</td>
        </tr>
      </tbody>
    </table>

    <h2>Qui y a accès</h2>
    <p>
      Les membres de vos groupes, selon leur rôle. Nos sous-traitants, tous dans l'Union européenne :
      l'hébergeur (Microsoft Azure, en France) et Brevo, qui envoie les e-mails. Aucune donnée n'est vendue
      ni utilisée à des fins publicitaires.
    </p>

    <h2>Cookies et stockage du navigateur</h2>
    <ul>
      <li><code>songvault.auth</code> : cookie de session, indispensable pour rester connecté (7 jours au plus).</li>
      <li><code>songvault.band</code> : dernier groupe ouvert, stocké dans votre navigateur.</li>
      <li><code>songvault.afterConfirm</code> : page à rouvrir après la confirmation de votre adresse.</li>
        <li><code>songvault.share</code> : mot de passe d'un lien de partage déjà donné (12 heures au plus, pour ce lien seulement).</li>
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
            Après une suppression, vos données restent dans les sauvegardes jusqu'à leur rotation : 7 jours pour
      la base de données, 14 jours pour les fichiers.
      Les invitations que vous avez envoyées dans des groupes partagés restent dans ces groupes.
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
  td:nth-child(1)::before { content: 'Données'; }
  td:nth-child(2)::before { content: 'Pourquoi'; }
  td:nth-child(3)::before { content: 'Base légale'; }
  td:nth-child(4)::before { content: 'Combien de temps'; }
}
</style>