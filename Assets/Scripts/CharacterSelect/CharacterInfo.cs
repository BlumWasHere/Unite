using UnityEngine;
using UnityEngine.AddressableAssets;

public enum PokemonClass : byte { Attacker, Defender, Supporter, AllRounder, Speedster }
public enum PokemonDifficulty : byte { Novice, Intermediate, Expert }
public enum PokemonRange : byte { Melee, Ranged }

[CreateAssetMenu(fileName = "Character", menuName = "Character", order = 1)]
public class CharacterInfo : ScriptableObject
{
    public string pokemonName;
    //public PokemonBase pokemon;
    public AssetReference pokemon;
    public PokemonClass pClass;

    [Space]
    public PokemonDifficulty Difficulty;
    public PokemonRange Range;
    public DamageType DamageType;

    [Space]
    public AssetReferenceGameObject model;

    public Sprite icon;
    [Tooltip("Circle portrait of the highest evolution, used by character-select map markers without loading the gameplay PokemonBase.")]
    public Sprite circleIcon;
    public Sprite portrait;

    public Sprite CircleIcon => circleIcon != null ? circleIcon : icon;

    public bool Hidden;
// I noticed that when in the overworld, some Pokemon were way too big, so I made this to try to make them more logical.
    [Tooltip("Scale multiplier applied only to the open-world companion model.")]
    [Min(0.01f)] public float companionScale = 1f;
}
