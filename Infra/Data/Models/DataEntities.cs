namespace Project.Infra.Data.Models;

/// <summary>
/// Data entity representing a character record in persistence storage.
/// </summary>
public record CharacterData(
    int Id,
    string Name,
    double Hp,
    double Power
);

/// <summary>
/// Data entity representing an item record in persistence storage.
/// </summary>
public record ItemData(
    int Id,
    string Name,
    double HpMod,
    double PowerMod
);

/// <summary>
/// Data entity representing the many-to-many relationship between characters and items.
/// </summary>
public record CharacterItems(
    int Id,
    int CharacterId,
    int ItemId
);

/// <summary>
/// Data entity representing an ability record in persistence storage.
/// </summary>
public record AbilityData(
    int Id,
    string Name,
    double Damage,
    double Heal,
    string Description
);

/// <summary>
/// Data entity representing the many-to-many relationship between characters and abilities.
/// </summary>
public record CharacterAbilities(
    int Id,
    int CharacterId,
    int AbilityId
);

/// <summary>
/// Data entity representing a node in the structured text document tree.
/// </summary>
public record TextNodeData(
    int Id,
    int? ParentId,
    string NodeType,
    string Name,
    string Content,
    int Rank,
    string? Extra = null
);
