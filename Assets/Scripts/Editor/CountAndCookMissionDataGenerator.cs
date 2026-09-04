using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using tmkoc.lunchforbuilders;

namespace tmkoc.lunchforbuilders.Editor
{
    // Creates the 5 Count & Cook MissionRecipeData assets pre-filled with the GDD's "UPDATED"
    // healthy-dish table, so the data-driven CookingManager/RecipeCardController system has real
    // content to test against instead of empty ScriptableObjects someone has to hand-fill.
    // Only writes .asset files -- no scene or prefab changes.
    public static class CountAndCookMissionDataGenerator
    {
        private const string OutputFolder = "Assets/ScriptableObjects/CountAndCook";

        private struct ReqDef
        {
            public string id;
            public int count;
            public bool isRemoval;
            public int order;
            public ReqDef(string id, int count, bool isRemoval = false, int order = -1)
            {
                this.id = id; this.count = count; this.isRemoval = isRemoval; this.order = order;
            }
        }

        private struct MissionDef
        {
            public string assetName;
            public string dishName;
            public LearningRule rule;
            public ReqDef[] requirements;
            public ReqDef[] starting;
            public float memoryReveal;
            public float peekReveal;
        }

        [MenuItem("Count And Cook/Generate Default Mission Data")]
        public static void GenerateDefaultMissions()
        {
            if (!Directory.Exists(OutputFolder)) Directory.CreateDirectory(OutputFolder);

            var missions = new List<MissionDef>
            {
                new MissionDef
                {
                    assetName = "Mission1_StrawberryLemonWater",
                    dishName = "Strawberry Lemon Water",
                    rule = LearningRule.UniformCounting,
                    requirements = new[]
                    {
                        new ReqDef("IceCube", 6),
                        new ReqDef("LemonSlice", 6),
                        new ReqDef("Strawberry", 6),
                    },
                    starting = new ReqDef[0],
                    memoryReveal = 0f,
                    peekReveal = 0f,
                },
                new MissionDef
                {
                    assetName = "Mission2_Salad",
                    dishName = "Salad",
                    rule = LearningRule.DifferentQuantities,
                    requirements = new[]
                    {
                        new ReqDef("BroccoliPiece", 4),
                        new ReqDef("TomatoPiece", 3),
                        new ReqDef("CornPiece", 5),
                        new ReqDef("BellPepperPiece", 2),
                    },
                    starting = new ReqDef[0],
                    memoryReveal = 0f,
                    peekReveal = 0f,
                },
                new MissionDef
                {
                    assetName = "Mission3_VeggieSandwich",
                    dishName = "Veggie Sandwich",
                    rule = LearningRule.OrderAndCounting,
                    requirements = new[]
                    {
                        new ReqDef("BottomBreadSlice", 1, order: 0),
                        new ReqDef("CucumberSlice", 2, order: 1),
                        new ReqDef("TomatoSlice", 2, order: 2),
                        new ReqDef("LettuceLeaf", 3, order: 3),
                        new ReqDef("TopBreadSlice", 1, order: 4),
                    },
                    starting = new ReqDef[0],
                    memoryReveal = 0f,
                    peekReveal = 0f,
                },
                new MissionDef
                {
                    assetName = "Mission4_OrangeMangoJuice",
                    dishName = "Orange Mango Juice",
                    rule = LearningRule.SubtractionByRemoval,
                    requirements = new[]
                    {
                        new ReqDef("OrangeSlice", 8),
                        new ReqDef("MangoChunk", 5),
                        new ReqDef("IceCube", 2, isRemoval: true),
                    },
                    starting = new[]
                    {
                        new ReqDef("IceCube", 6),
                    },
                    memoryReveal = 0f,
                    peekReveal = 0f,
                },
                new MissionDef
                {
                    assetName = "Mission5_RainbowFruitBowl",
                    dishName = "Rainbow Fruit Bowl",
                    rule = LearningRule.Memory,
                    requirements = new[]
                    {
                        new ReqDef("ApplePiece", 3),
                        new ReqDef("BananaSlice", 4),
                        new ReqDef("Grapes", 5),
                        new ReqDef("StrawberryPiece", 2),
                        new ReqDef("SpoonYogurt", 1),
                    },
                    starting = new ReqDef[0],
                    memoryReveal = 5f,
                    peekReveal = 3f,
                },
            };

            foreach (var m in missions) CreateOrUpdateAsset(m);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Count & Cook: generated {missions.Count} mission assets in {OutputFolder}");
        }

        private static void CreateOrUpdateAsset(MissionDef def)
        {
            string path = $"{OutputFolder}/{def.assetName}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<MissionRecipeData>(path);
            bool isNew = asset == null;
            if (isNew) asset = ScriptableObject.CreateInstance<MissionRecipeData>();

            var so = new SerializedObject(asset);
            so.FindProperty("dishName").stringValue = def.dishName;
            so.FindProperty("learningRule").enumValueIndex = (int)def.rule;
            so.FindProperty("memoryRevealSeconds").floatValue = def.memoryReveal;
            so.FindProperty("peekRevealSeconds").floatValue = def.peekReveal;

            WriteRequirementArray(so.FindProperty("requirements"), def.requirements);
            WriteRequirementArray(so.FindProperty("startingIngredients"), def.starting);

            so.ApplyModifiedProperties();

            if (isNew) AssetDatabase.CreateAsset(asset, path);
            else EditorUtility.SetDirty(asset);
        }

        private static void WriteRequirementArray(SerializedProperty arrayProp, ReqDef[] defs)
        {
            arrayProp.arraySize = defs.Length;
            for (int i = 0; i < defs.Length; i++)
            {
                var element = arrayProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("ingredientId").stringValue = defs[i].id;
                element.FindPropertyRelative("requiredCount").intValue = defs[i].count;
                element.FindPropertyRelative("isRemoval").boolValue = defs[i].isRemoval;
                element.FindPropertyRelative("sequenceOrder").intValue = defs[i].order;
            }
        }
    }
}
