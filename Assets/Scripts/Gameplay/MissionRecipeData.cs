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
        [SerializeField] private LearningRule learningRule;
        [Tooltip("What the player must add/remove to complete this dish.")]
        [SerializeField] private IngredientRequirement[] requirements;
        [Tooltip("Ingredients already sitting in the station before play starts (Mission 4's 6 starting ice cubes). Empty for every other mission.")]
        [SerializeField] private IngredientRequirement[] startingIngredients;
        [Tooltip("Memory missions only: how long the Recipe Card stays visible before flipping face-down.")]
        [SerializeField] private float memoryRevealSeconds = 5f;
        [Tooltip("Memory missions only: how long a Peek reveals the card again.")]
        [SerializeField] private float peekRevealSeconds = 3f;

        public string DishName => dishName;
        public LearningRule LearningRule => learningRule;
        public IngredientRequirement[] Requirements => requirements;
        public IngredientRequirement[] StartingIngredients => startingIngredients;
        public float MemoryRevealSeconds => memoryRevealSeconds;
        public float PeekRevealSeconds => peekRevealSeconds;
    }
}
