# ADR 0033 — Interface multilingue : catalogues TypeScript, erreurs de l'API traduites à la sortie

- Statut : accepté
- Date : 2026-10-21

## Contexte
Le backlog (#26) demande l'interface en français et en anglais, avec le choix de la langue
gardé dans le compte et les messages d'erreur de l'API traduits. Aujourd'hui, environ 1 300
textes sont écrits en dur dans 68 composants et dans les scripts du front, et les messages de
l'API (exceptions du domaine, validations, `ProblemDetails`) sont en français. La vue
d'ensemble proposait vue-i18n côté front et `IStringLocalizer` avec des fichiers `.resx` côté
API.

## Décision
- **Pas de bibliothèque côté front** : une fonction `t('songs.list.title', { count })` dans
  `src/shared/i18n`, des catalogues en TypeScript (un `locale.ts` par fonctionnalité, réunis
  dans `messages.ts`). Le catalogue français fait foi ; le type `Widen<typeof fr>` oblige
  l'anglais à avoir exactement les mêmes clés, et `MessageKey` fait échouer `vue-tsc` sur une
  clé inconnue. Les pluriels passent par `Intl.PluralRules` (`{ one, other, zero? }`), les
  nombres, dates et monnaies par `Intl` avec `fr-FR` ou `en-GB` (jour avant mois). vue-i18n
  apportait la même chose avec un paquet de plus et des clés non typées.
- **Choix de la langue** : sélecteur « Français / English » dans le pied de page. Sans compte, la
  langue est gardée sur l'appareil (`localStorage`), sinon celle du navigateur si on la
  propose, sinon le français. Connecté, elle est enregistrée dans le compte
  (`PUT /api/me/locale`, revendication `locale` d'Identity, sans migration) et renvoyée par
  `GET /api/auth/me`, qui l'applique à la connexion sur un autre appareil.
- **Changer de langue recharge la page affichée** (`<main :key="locale">`) : les textes calculés
  une fois dans un `setup` suivent ainsi la langue sans réécrire chaque composant. Une saisie
  en cours dans un formulaire est perdue ; le sélecteur est en bas de page, rarement utilisé
  au milieu d'une saisie.
- **Tables de libellés** (statut → texte, rôle → texte) : `labels({ … })` rend un objet dont
  chaque lecture traduit dans la langue du moment ; les appels gardent leur forme.
- **Contrôle** : la règle `vue/no-bare-strings-in-template` (déjà dans eslint-plugin-vue) refuse
  un texte en dur dans un gabarit, avec une courte liste de symboles et de noms propres permis.
  Un test vérifie que les deux catalogues ont les mêmes clés, les mêmes `{paramètres}` et aucun
  texte vide.
- **API sans `.resx` ni `IStringLocalizer`** : les messages restent écrits en français à leur
  place. Un catalogue C# (`Localization/ErrorCatalog.cs`, un code stable, le modèle français et
  le modèle anglais de chaque message) sert à les traduire à la sortie : le filtre de
  `ProblemDetails` lit `Accept-Language` (français par défaut), traduit le titre, le détail et
  chaque erreur de validation, ajoute `code` (`common.notFound`…) et l'en-tête
  `Content-Language`. Les valeurs placées dans un message (nom de champ, identifiant) passent
  telles quelles, sauf les noms de champ, traduits par une petite table. Un test parcourt le
  code de l'API et échoue si un message levé n'a pas d'entrée dans le catalogue.
- **La culture de la requête ne change pas** : formats de date, tri et validation côté serveur
  restent ceux d'aujourd'hui ; seule la sortie des erreurs est traduite.

## Conséquences
- Les e-mails restent en français : ils partent en arrière-plan, sans requête ni langue à lire.
  Ils suivront la langue du compte quand un besoin réel apparaîtra (la revendication `locale`
  est déjà là).
- Les pages légales (mentions, confidentialité, CGV) existent en anglais avec une mention :
  seule la version française fait foi.
- Les messages par défaut des attributs `DataAnnotations` (« The Email field is required. ») ne
  changent pas : les formulaires du front valident avant l'envoi et ont leurs propres textes.
- Les deux catalogues sont dans le paquet du front (morceau `i18n` de 147 Ko, 51 Ko compressé) ;
  charger l'anglais à la demande n'en vaut pas la peine à cette taille.
- Une troisième langue demande un catalogue de plus côté front et une colonne de plus dans
  `ErrorCatalog`, sans autre changement.
- Le contenu saisi par les groupes (titres, paroles, notes, commentaires) n'est jamais traduit.