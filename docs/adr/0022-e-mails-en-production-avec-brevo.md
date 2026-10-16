# ADR 0022 — E-mails en production : Brevo, domaine authentifié, réglage gardé d'un déploiement à l'autre

- Statut : accepté
- Date : 2026-10-15

## Contexte
Le code d'envoi par Brevo existe depuis l'ADR 0009, et `scripts/deploy-azure.ps1` sait le
configurer depuis l'ADR 0012. Mais la production tourne avec `Email:Provider=Log` : aucun
e-mail ne part. Invitations, confirmation de compte, mot de passe oublié, export des données
et notifications (ADR 0018) en dépendent.

Deux pièges dans le script : `-EmailProvider` valait `Log` par défaut, donc un redéploiement
qui oubliait l'option coupait tous les e-mails sans rien dire ; et la clé Brevo devait être
retapée à chaque déploiement, sinon le secret repassait à vide.

Gmail et Outlook attendent un domaine d'expédition authentifié (DKIM, DMARC). Brevo ne
peut pas authentifier une adresse `@gmail.com` : envoyée par lui, elle finit souvent en
indésirables.

## Décision
- **Un domaine à nous, `songvault.eu`**, acheté chez OVHcloud (`songvault.fr` était pris),
  avec sa zone DNS chez OVHcloud, et authentifié chez Brevo :
  code Brevo, DKIM et DMARC dans le DNS du registraire. Brevo ne demande pas de SPF pour
  le domaine : l'enveloppe de l'e-mail utilise son propre domaine, qui passe SPF, et DMARC
  passe par la signature DKIM alignée sur le nôtre.
- **Expéditeur `no-reply@songvault.eu`, nom « SongVault »**. C'est un alias de la boîte
  `contact@songvault.eu` (Zimbra Starter, incluse avec le domaine chez OVHcloud), qui est
  aussi l'adresse du compte Brevo : vérification de l'expéditeur, alertes de Brevo et
  réponses égarées arrivent dans une boîte lue.
- **DMARC en `p=none`** au départ, avec les rapports vers Brevo ; passer à `p=quarantine`
  quand les rapports ne montrent plus d'envoi non signé.
- **Le script garde les réglages de l'API en place.** Sans `-EmailProvider` ni
  `-EmailFrom`, il relit `Email__Provider` et `Email__FromAddress` de `songvault-api`, et le
  secret `brevo-api-key` quand `$env:BREVO_API_KEY` n'est pas défini. Un groupe de ressources
  neuf (exercice de restauration, ADR 0013) part sur `Log`. Le script refuse Brevo avec
  l'adresse `@songvault.local` et avertit en fin de déploiement quand rien ne part.
- **La clé reste un secret du Container App**, comme le mot de passe SQL, pas un secret
  Key Vault : un seul endroit à changer, par le script.
- **`scripts/test-brevo.ps1`** vérifie la clé et l'expéditeur depuis le PC avant de
  déployer : compte, crédits restants, puis un e-mail de test.
- **Blocage des IP inconnues de Brevo gardé.** Brevo apprend les adresses qui appellent son
  API, puis bloque les nouvelles. Les adresses sortantes de `songvault-api` sont autorisées
  à la main ; le runbook dit quoi faire si elles changent.

## Alternatives écartées
- **Adresse Gmail comme expéditeur** : pas de domaine à acheter, mais pas d'authentification
  possible, et les e-mails partent en indésirables ou sont refusés.
- **Sous-domaine d'envoi (`mail.songvault.eu`)** : isole la réputation des envois, utile à
  gros volume ; ici une seule source d'e-mails, et une adresse plus simple à lire.
- **Clé dans Key Vault, lue par l'API** : rotation sans redéploiement, mais un rôle de plus
  sur l'identité de l'API et un premier déploiement plus fragile, pour une clé changée
  rarement.
- **`-EmailProvider Brevo` obligatoire à chaque déploiement** : rien de caché, mais un oubli
  coupe les e-mails, et le script de l'exercice de restauration devrait passer `Log`.
- **Désactiver le blocage des IP chez Brevo** : plus de souci si les adresses sortantes
  d'Azure changent, mais une clé volée servirait de n'importe où.
- **Azure Communication Services ou Mailjet** : possibles ; Brevo est déjà codé, en France,
  avec un palier gratuit.

## Conséquences
- Un redéploiement simple (`-ImageTag` seul) garde Brevo. Revenir aux journaux demande
  `-EmailProvider Log` explicitement.
- Le script lit la clé en clair depuis Azure à chaque déploiement : il faut les mêmes droits
  qu'aujourd'hui sur le groupe de ressources, et rien de plus.
- Le domaine coûte un abonnement annuel et doit être renouvelé : un domaine expiré coupe
  les e-mails.
- Les liens des e-mails pointent encore vers l'adresse `azurecontainerapps.io` du site ; un
  nom de domaine pour le site est un autre travail.
- Le palier gratuit de Brevo limite le nombre d'e-mails par jour : un jour de beaucoup de
  notifications, les suivants sont refusés et notés en erreur dans les journaux.