namespace SongVault.Domain.Songs;

/// <summary>Rôle d'un ayant droit sur l'œuvre (#22, ADR 0029). Enregistré en texte : en ajouter un ne décale rien.</summary>
public enum CreditRole { Author, Composer, Arranger, Publisher }