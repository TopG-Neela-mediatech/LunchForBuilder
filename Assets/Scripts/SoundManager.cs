using System;
using UnityEngine;

namespace tmkoc.lunchforbuilders
{
    // Every key below is a "voiceover_title" from the CountAndCook.xlsx sheet -- RuntimeAudioLoader
    // looks clips up by that exact string. Same shape as BuildABot's SoundManager.
    public class SoundManager : MonoBehaviour
    {
        [SerializeField] private CountAndCookAudioMapper audioMapper;
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private SFXData[] sfxData;

        public void PlayBGM()
        {
            if (bgmSource == null || bgmSource.isPlaying) return;
            bgmSource.Play();
        }

        public void StopBGM() => bgmSource?.Stop();

        public void StopAllExceptBGM() => sfxSource?.Stop();

        public void PlaySFX(sfxEnum e)
        {
            AudioClip clip = Array.Find(sfxData, x => x.sfxEnum == e)?.audioClip;
            if (clip != null)
            {
                if (sfxSource.isPlaying)
                {
                    sfxSource.Stop();
                }
                sfxSource.PlayOneShot(clip);
            }
            else
            {
                Debug.Log("Clip Not Assigned or Found");
            }
        }

        // ---- Storyboard (3 slides) ----
        public float PlayStorySlide(int slideIndex)
        {
            if (audioMapper.storySlides == null || slideIndex < 0 || slideIndex >= audioMapper.storySlides.Length) return -1f;
            return RuntimeAudioLoader.Instance != null ? RuntimeAudioLoader.Instance.PlayRuntimeAudio(audioMapper.storySlides[slideIndex]) : -1f;
        }

        // ---- Per-mission intro ("Let's make some Strawberry Lemon Water!") ----
        public float PlayMissionIntro(int missionIndex) => PlayFromArray(audioMapper.missionIntros, missionIndex);

        // ---- Per-mission outro ("Yay! The ... is ready!") -- CookingManager waits out the
        // returned clip length before reporting the mission complete, so the win panel only pops up
        // once this line has actually finished. ----
        public float PlayMissionOutro(int missionIndex) => PlayFromArray(audioMapper.missionOutros, missionIndex);

        // ---- Per-mission lose ("Oh no, time's up! Let's try making ... again!") -- fires the
        // instant the timer hits zero, immediately (no delay) alongside the lose panel. ----
        public float PlayMissionLose(int missionIndex) => PlayFromArray(audioMapper.missionLose, missionIndex);

        // ---- Final game outro (whole game complete) -- only reachable in the PLAYSCHOOL_MAIN
        // build path, right before handing off to the main app's own end-of-game panel. ----
        public float PlayFinalOutro() => RuntimeAudioLoader.Instance != null ? RuntimeAudioLoader.Instance.PlayRuntimeAudio(audioMapper.finalOutro) : -1f;

        // ---- Ingredient name callout ("Ice Cube!", "Lemon Slice!" ...) -- played the instant the
        // player picks up (starts dragging) any ingredient, add or remove alike. ----
        public float PlayIngredientName(string ingredientId)
        {
            if (audioMapper.ingredientNames == null || RuntimeAudioLoader.Instance == null) return -1f;
            foreach (var entry in audioMapper.ingredientNames)
            {
                if (entry.ingredientId == ingredientId) return RuntimeAudioLoader.Instance.PlayRuntimeAudio(entry.key);
            }
            return -1f;
        }

        // ---- Idle nudge (random variant), repeats for as long as the player stays idle ----
        public float PlayIdleNudge()
        {
            if (audioMapper.idleNudges == null || audioMapper.idleNudges.Length == 0 || RuntimeAudioLoader.Instance == null) return -1f;
            int rand = UnityEngine.Random.Range(0, audioMapper.idleNudges.Length);
            return RuntimeAudioLoader.Instance.PlayRuntimeAudio(audioMapper.idleNudges[rand]);
        }

        // ---- Correct / Incorrect spoken reinforcement -- these come from the shared "common" audio
        // bundle every Playschool game already downloads (RuntimeAudioLoader.Start() loads it as
        // category "common"), not this game's own xlsx, so they're pulled directly rather than
        // added as new CountAndCook-specific lines. Each call's own Stop()-then-PlayOneShot on the
        // shared _commonAudioSource is what keeps every VO line (name callout, correct/incorrect,
        // idle nudge, intro/outro) from ever overlapping another. ----
        public void PlayCorrectReinforcement() => RuntimeAudioLoader.Instance?.PlayCorrectAudioClip();
        public void PlayIncorrectReinforcement() => RuntimeAudioLoader.Instance?.PlayIncorrectAudioClip();

        // Numbers/keys are 0-indexed by mission (Mission 1 = index 0 ... Mission 5 = index 4).
        private float PlayFromArray(string[] keys, int index)
        {
            if (keys == null || index < 0 || index >= keys.Length || RuntimeAudioLoader.Instance == null) return -1f;
            return RuntimeAudioLoader.Instance.PlayRuntimeAudio(keys[index]);
        }
    }

    [Serializable]
    public class SFXData
    {
        public AudioClip audioClip;
        public sfxEnum sfxEnum;
    }

    public enum sfxEnum
    {
        None,
        Correct,
        Incorrect
    }

    [Serializable]
    public class IngredientVoiceEntry
    {
        public string ingredientId;
        public string key;
    }

    [Serializable]
    public class CountAndCookAudioMapper
    {
        [Header("Storyboard (3 slides)")]
        public string[] storySlides = { "story1", "story2", "story3" };

        [Header("Per-Mission Intro (index 0 = Mission 1 ... index 4 = Mission 5)")]
        public string[] missionIntros =
        {
            "mission1_intro", "mission2_intro", "mission3_intro", "mission4_intro", "mission5_intro"
        };

        [Header("Per-Mission Outro")]
        public string[] missionOutros =
        {
            "mission1_outro", "mission2_outro", "mission3_outro", "mission4_outro", "mission5_outro"
        };

        [Header("Per-Mission Lose (timer ran out)")]
        public string[] missionLose =
        {
            "mission1_lose", "mission2_lose", "mission3_lose", "mission4_lose", "mission5_lose"
        };

        [Header("Final Game Outro (whole game complete)")]
        public string finalOutro = "final_outro";

        [Header("Ingredient Name Callouts (matches IngredientRequirement.ingredientId)")]
        public IngredientVoiceEntry[] ingredientNames =
        {
            new IngredientVoiceEntry { ingredientId = "IceCube", key = "ingredient_IceCube" },
            new IngredientVoiceEntry { ingredientId = "LemonSlice", key = "ingredient_LemonSlice" },
            new IngredientVoiceEntry { ingredientId = "Strawberry", key = "ingredient_Strawberry" },
            new IngredientVoiceEntry { ingredientId = "BroccoliPiece", key = "ingredient_BroccoliPiece" },
            new IngredientVoiceEntry { ingredientId = "TomatoPiece", key = "ingredient_TomatoPiece" },
            new IngredientVoiceEntry { ingredientId = "CornPiece", key = "ingredient_CornPiece" },
            new IngredientVoiceEntry { ingredientId = "BellPepperPiece", key = "ingredient_BellPepperPiece" },
            new IngredientVoiceEntry { ingredientId = "BottomBreadSlice", key = "ingredient_BottomBreadSlice" },
            new IngredientVoiceEntry { ingredientId = "CucumberSlice", key = "ingredient_CucumberSlice" },
            new IngredientVoiceEntry { ingredientId = "TomatoSlice", key = "ingredient_TomatoSlice" },
            new IngredientVoiceEntry { ingredientId = "LettuceLeaf", key = "ingredient_LettuceLeaf" },
            new IngredientVoiceEntry { ingredientId = "TopBreadSlice", key = "ingredient_TopBreadSlice" },
            new IngredientVoiceEntry { ingredientId = "OrangeSlice", key = "ingredient_OrangeSlice" },
            new IngredientVoiceEntry { ingredientId = "MangoChunk", key = "ingredient_MangoChunk" },
            new IngredientVoiceEntry { ingredientId = "ApplePiece", key = "ingredient_ApplePiece" },
            new IngredientVoiceEntry { ingredientId = "BananaSlice", key = "ingredient_BananaSlice" },
            new IngredientVoiceEntry { ingredientId = "Grapes", key = "ingredient_Grapes" },
            new IngredientVoiceEntry { ingredientId = "StrawberryPiece", key = "ingredient_StrawberryPiece" },
            new IngredientVoiceEntry { ingredientId = "SpoonYogurt", key = "ingredient_SpoonYogurt" },
        };

        [Header("Idle Nudge (random variant)")]
        public string[] idleNudges = { "idle_nudge_1", "idle_nudge_2", "idle_nudge_3" };
    }
}
