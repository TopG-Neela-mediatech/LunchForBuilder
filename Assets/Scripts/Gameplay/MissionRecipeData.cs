using System;
using UnityEngine;

namespace tmkoc.lunchforbuilders
{
    public enum LearningRule { UniformCounting, DifferentQuantities, OrderAndCounting, SubtractionByRemoval, Memory }

    // One entry per ingredient row on the Recipe Card. sequenceOrder is only meaningful for
    // OrderAndCounting missions (Mission 3, "Order + Counting") -- everywhere else it stays -1,
    // meaning the ingredient can be added at any time.
    [Serializable]
    public class IngredientRequirement
    {
        public string ingredientId;
        [Tooltip("Shown on the Recipe Card row for this ingredient.")]
        public Sprite icon;
        [Min(1)] public int requiredCount = 1;
        [Tooltip("True if this ingredient must be dragged OUT of the station rather than added to it (Mission 4's ice cubes).")]
        public bool isRemoval;
        [Tooltip("-1 = can be added any time. 0,1,2... = must be completed as the Nth step, in order (Mission 3 only).")]
        public int sequenceOrder = -1;
    }

    [CreateAssetMenu(fileName = "Mission_New", menuName = "Count And Cook/Mission Recipe Data")]
    public class MissionRecipeData : ScriptableObject
    {
        [SerializeField] private string dishName;
        [Tooltip("Shown on the Preparation Station's content image once this mission starts -- the jug for Lemon Water, plate for Salad/Sandwich, blender for the Juice, bowl for the Fruit Bowl.")]
        [SerializeField] private Sprite stationIcon;
        [Tooltip("Swapped in on top of Station Icon the moment Serve is pressed -- the finished dish (poured lemonade, plated salad, etc.) so the player sees what they actually made.")]
        [SerializeField] private Sprite completedRecipeSprite;
        [Tooltip("The Preparation Station content anchor's resting Y position (anchoredPosition.y) once this mission's intro slide-in finishes. Different container art (jug vs plate vs blender) can sit at a different height, so this is set per mission rather than shared.")]
        [SerializeField] private float contentAnchorRestY;
        [SerializeField] private LearningRule learningRule;
        [Tooltip("What the player must add/remove to complete this dish.")]
        [SerializeField] private IngredientRequirement[] requirements;
        [Tooltip("Ingredients already sitting in the station before play starts (Mission 4's 6 starting ice cubes). Empty for every other mission.")]
        [SerializeField] private IngredientRequirement[] startingIngredients;
        [Tooltip("Memory missions only: how long the Recipe Card stays visible before flipping face-down.")]
        [SerializeField] private float memoryRevealSeconds = 5f;
        [Tooltip("Memory missions only: how long a Peek reveals the card again.")]
        [SerializeField] private float peekRevealSeconds = 3f;

        [Header("Character Reaction")]
        [Tooltip("Default/waiting state shown through the mission, and reverted back to a few seconds after a Sad reaction.")]
        [SerializeField] private Sprite hungryCharacterSprite;
        [Tooltip("Shown briefly whenever the player drags something incorrectly.")]
        [SerializeField] private Sprite sadCharacterSprite;
        [Tooltip("Shown once the recipe is complete.")]
        [SerializeField] private Sprite happyCharacterSprite;
        [Tooltip("One is picked at random and popped above the character's head whenever the Hungry state shows.")]
        [SerializeField] private Sprite[] hungryEmojis;
        [Tooltip("One is picked at random and popped above the character's head whenever the Sad state shows.")]
        [SerializeField] private Sprite[] sadEmojis;
        [Tooltip("One is picked at random and popped above the character's head whenever the Happy state shows.")]
        [SerializeField] private Sprite[] happyEmojis;

        [Header("Timer")]
        [Tooltip("How long the player has to finish this recipe once the level-start animation finishes, in seconds.")]
        [SerializeField] private float timeInSeconds = 60f;

        public string DishName => dishName;
        public Sprite StationIcon => stationIcon;
        public Sprite CompletedRecipeSprite => completedRecipeSprite;
        public float ContentAnchorRestY => contentAnchorRestY;
        public LearningRule LearningRule => learningRule;
        public IngredientRequirement[] Requirements => requirements;
        public IngredientRequirement[] StartingIngredients => startingIngredients;
        public float MemoryRevealSeconds => memoryRevealSeconds;
        public float PeekRevealSeconds => peekRevealSeconds;
        public Sprite HungryCharacterSprite => hungryCharacterSprite;
        public Sprite SadCharacterSprite => sadCharacterSprite;
        public Sprite HappyCharacterSprite => happyCharacterSprite;
        public Sprite[] HungryEmojis => hungryEmojis;
        public Sprite[] SadEmojis => sadEmojis;
        public Sprite[] HappyEmojis => happyEmojis;
        public float TimeInSeconds => timeInSeconds;
    }
}
