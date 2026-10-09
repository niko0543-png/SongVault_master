# ADR 0004 — Authentification par cookie plutôt que par jeton JWT

- Statut : accepté
- Date : 2026-10-09 (décision prise en semaine 7, consignée a posteriori)

## Contexte
L'API n'est consommée que par le front Vue de SongVault. En production, nginx sert le front
et relaie `/api` vers l'API : le navigateur ne voit **qu'une seule origine**. En
développement, le proxy Vite reproduit la même situation. Il n'y a ni application mobile,
ni client tiers.

## Décision
- ASP.NET Core Identity (`MapIdentityApi`) avec un **cookie** d'authentification.
- Cookie `HttpOnly` (illisible par JavaScript), `SameSite=Strict` (jamais envoyé depuis un
  autre site, ce qui couvre le CSRF), `Secure` selon le protocole de la requête.
- Une requête non authentifiée reçoit **401** (pas de redirection HTML vers une page de
  connexion).
- Toutes les routes des contrôleurs exigent une connexion
  (`MapControllers().RequireAuthorization()`) ; `/api/auth` est limité en débit.
- Chaque requête de données filtre par propriétaire ; une ressource d'un autre utilisateur
  renvoie **404**, pas 403, pour ne pas révéler son existence.

## Alternatives écartées
- **JWT stocké dans `localStorage`** : lisible par n'importe quel script de la page, donc
  volé en cas de faille XSS ; révocation difficile avant expiration.
- **JWT en mémoire avec jeton de rafraîchissement** : sûr, mais demande un mécanisme de
  rafraîchissement côté client et serveur, sans bénéfice pour un seul client sur la même
  origine.
- **Fournisseur externe (Entra ID, Auth0…)** : dépendance externe et configuration
  disproportionnées pour un projet de démonstration ; reste possible plus tard.

## Conséquences
- Le front n'a aucun jeton à stocker ni à joindre : le navigateur envoie le cookie seul.
- La déconnexion supprime réellement la session côté navigateur.
- L'API et le front doivent rester sur la même origine (ou des sous-domaines configurés
  avec soin). Un futur client mobile demanderait un mode jeton en complément.
