# ADR 0026 — Double authentification et journal d'audit : TOTP d'Identity, historique de connexion, journal en ajout seul

- Statut : accepté
- Date : 2026-10-19

## Contexte
Le backlog (#19) demande la double authentification par application (TOTP), des codes de secours,
un historique des connexions et un journal d'audit par groupe (« qui a supprimé quoi »), avec trois
critères : la double authentification s'active et se désactive, l'Owner consulte le journal, le
journal ne se modifie pas depuis l'application.
ASP.NET Core Identity (ADR 0010) sait déjà tout du TOTP : clé par compte, vérification du code,
codes de secours dans `AspNetUserTokens`, et `/api/auth/login` (MapIdentityApi) accepte
`twoFactorCode` et `twoFactorRecoveryCode`. Mais sa route `/api/auth/manage/2fa` désactive la double
authentification sans redemander le mot de passe, et rien n'écrit d'historique.
Le fil d'activité (ADR 0017) raconte déjà les suppressions de morceaux et de fichiers, à tous les
membres ; il oublie les auteurs d'un compte purgé et ne couvre ni les membres ni les liens.

## Décision
- **TOTP d'Identity, sans paquet ajouté** : `AccountSecurityController` (`/api/me/two-factor…`)
  prépare la clé (`setup`), l'active après un premier code juste (`enable`, qui renvoie 10 codes de
  secours montrés une seule fois), la désactive et renouvelle les codes contre le mot de passe
  (`CheckPasswordSignInAsync` avec verrouillage : 5 mots de passe faux bloquent le compte quelques
  minutes, comme la connexion). La clé s'affiche par groupes de 4 caractères avec son lien
  `otpauth://`, que l'application ouvre directement sur téléphone ; pas de QR code, qui demanderait
  une bibliothèque de plus.
- **Connexion en deux temps, même route** : le front envoie e-mail et mot de passe ; s'il reçoit
  `401 RequiresTwoFactor`, il demande le code et renvoie les trois. Un code faux compte comme un
  mot de passe faux (verrouillage). Après une connexion avec code, le navigateur est reconnu
  14 jours (cookie `songvault.2fa`, `HttpOnly`, `SameSite=Strict`) ; changer de mot de passe ou de
  réglage l'oublie (empreinte de sécurité), et « Redemander le code ici » aussi.
- **Routes d'Identity fermées** : `/api/auth/manage/2fa` et `POST /api/auth/manage/info` répondent
  404 (filtre sur le groupe MapIdentityApi). Le mot de passe se change par `POST /api/me/password`,
  qui garde la session ouverte (`RefreshSignInAsync`) : se reconnecter après le changement
  demanderait maintenant un code.
- **Historique de connexion** (`AccountEvents`) : écrit par `AuditingSignInManager`, sous-classe du
  `SignInManager` d'Identity, et par les actions de `AccountSecurityController`. Connexions
  (mot de passe seul, avec code, avec code de secours), mot de passe faux, code refusé,
  verrouillage, mot de passe changé, double authentification activée ou désactivée, codes
  renouvelés. Ni IP complète ni navigateur exact : le réseau (/24 en IPv4, /48 en IPv6) et
  « Firefox sur Windows ». Une adresse inconnue n'écrit rien. Visible par le compte seul
  (« Mon compte », 50 dernières lignes), gardé 90 jours, effacé avec le compte (cascade).
- **Journal d'audit par groupe** (`AuditEntries`) : écrit dans les handlers par `AuditLog`, dans la
  même transaction que l'action, comme le fil d'activité. Morceau, fichier, commentaire et sortie
  supprimés ; lien de partage créé, coupé, supprimé ; invitation envoyée, annulée, acceptée ; rôle
  changé, membre retiré, départ (y compris par suppression du compte) ; groupe renommé ; fichier
  bloqué par l'antivirus (auteur : SongVault). Il garde les titres et noms d'alors, l'adresse
  invitée masquée (`n***@exemple.fr`), jamais le texte d'un commentaire ni un jeton. Auteur et
  compte visé sont des identifiants sans clé étrangère : un compte purgé devient « Compte
  supprimé ».
- **Lecture réservée aux Owner** : `GET /api/bands/{id}/audit`, page par page avec le curseur du fil
  d'activité ; page « Journal » dans le menu des Owner.
- **Non modifiable depuis l'application**, à trois niveaux : aucune route ni méthode qui modifie ou
  supprime une ligne ; `SaveChangesAsync` refuse une ligne modifiée ou supprimée ; un déclencheur
  SQL (`TR_AuditEntries_ReadOnly`) refuse tout `UPDATE` et tout `DELETE`, sauf quand le groupe
  n'existe plus (cascade de la purge d'un compte) ou que la ligne a plus d'un an.
- **Rétention** : `RetentionWorker` efface chaque jour le journal de plus d'un an et l'historique de
  plus de 90 jours.

## Conséquences
- Un compte qui perd son téléphone et ses codes de secours ne peut plus se connecter seul :
  réinitialiser son mot de passe ne suffit pas. La procédure (vérifier l'identité, puis
  `TwoFactorEnabled = 0` en base) est dans le runbook.
- Le déclencheur ne protège pas d'un administrateur de la base, qui peut le désactiver : le critère
  est « non modifiable depuis l'application ». Les sauvegardes (ADR 0013) gardent l'état des
  7 derniers jours.
- Une nouvelle action sensible doit appeler `AuditLog` dans son handler, et sa phrase être ajoutée
  au front (`features/audit/describe.ts`).
- L'historique de connexion n'entre pas encore dans l'archive des données du compte ; il est
  consultable dans « Mon compte ».
- `ActivityCursor` sert aux deux listes : même format de curseur, même tri (date, puis numéro).