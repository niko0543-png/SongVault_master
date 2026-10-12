# ADR 0010 — Confirmation d'adresse et mot de passe oublié avec les endpoints d'Identity

- Statut : accepté
- Date : 2026-10-10

## Contexte
N'importe qui peut créer un compte avec n'importe quelle adresse, et un mot de passe oublié
n'a pas de solution. Les invitations (ADR 0009) supposent pourtant que l'adresse du compte
appartient bien à la personne. `MapIdentityApi` (ADR 0004) fournit déjà les endpoints de
confirmation, de réinitialisation et de changement de mot de passe ; il lui manque l'envoi
des e-mails et la règle « pas de connexion sans adresse confirmée ».

## Décision
- **Endpoints d'Identity conservés**, sans contrôleur maison. Un adaptateur
  `IEmailSender<IdentityUser>` écrit les e-mails en français et réécrit les liens vers le
  front (`/confirm-email`, `/reset-password`), d'après `App:PublicUrl`.
- **`SignIn.RequireConfirmedEmail`** : la connexion répond 401 `NotAllowed` tant que
  l'adresse n'est pas confirmée. Les comptes créés avant cette décision sont marqués
  confirmés par la migration `ConfirmExistingEmails`.
- **Durées** : lien de confirmation valable 24 h (fournisseur de jetons dédié), lien de
  réinitialisation 2 h (`DataProtectionTokenProviderOptions.TokenLifespan`).
- **E-mails d'Identity envoyés en arrière-plan** : l'adaptateur les dépose dans une file en
  mémoire bornée (`IEmailQueue`), un `BackgroundService` les confie à `IEmailSender`.
  « Mot de passe oublié » répond 200 avec le même corps, et dans le même temps, que le
  compte existe ou non.
- **Clés Data Protection en base** (`PersistKeysToDbContext`) : cookies et liens survivent
  à un redémarrage ou à une reconstruction de l'API.
- **Sessions** : l'empreinte de sécurité est revérifiée chaque minute ; un mot de passe
  changé ou réinitialisé coupe les autres sessions dans ce délai.

## Alternatives écartées
- **Contrôleur maison pour tout le compte** : plus de code à sécuriser pour refaire ce
  qu'Identity fait déjà ; seuls les liens et les textes des e-mails devaient changer.
- **Envoi pendant la requête** : le temps de réponse de « mot de passe oublié » trahirait
  l'existence du compte (appel au fournisseur seulement pour un compte connu).
- **File persistante en base** : plus sûre (rien n'est perdu au redémarrage), mais c'est le
  rôle de la brique de tâches de fond prévue en #5 ; un e-mail perdu se redemande.
- **Demander aux comptes existants de confirmer leur adresse** : bloquerait tout le monde
  au déploiement, sans gain tant qu'il n'y a pas de vrais utilisateurs.

## Conséquences
- L'inscription ne connecte plus directement : le front affiche « Vérifiez votre boîte
  mail » ; les tests d'intégration confirment l'adresse en base.
- Sans fournisseur réel (`Email:Provider=Log`), les liens sont lisibles dans les journaux
  de l'API : à réserver au développement (ADR 0009).
- Un e-mail encore dans la file est perdu si l'API s'arrête ; l'écran permet de le
  redemander.
- Reste visible : l'inscription signale une adresse déjà utilisée, et la connexion d'un
  compte non confirmé répond `NotAllowed` avant le contrôle du mot de passe. Les deux
  révèlent l'existence d'un compte ; à reprendre si cela devient un risque (limitation de
  débit déjà en place sur `/api/auth`).
- Le premier démarrage avec les clés en base déconnecte tout le monde une fois.