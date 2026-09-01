using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using System.IO.Compression;

public class RuntimeAudioLoader : MonoBehaviour
{
    public enum Language
    {
        English,
        Tamil,
        Hindi,
        Telugu,
        Malayalam,
        Bengali,
        Gujarati,
        Marathi,
        Punjabi,
        Kannada,
        Odia,

        Russian,
        French,
        Spanish,
        German


    }

    public static RuntimeAudioLoader Instance;
    public AudioSource _commonAudioSource;
    //public string audiofolderName;
    Dictionary<string, AudioClip> audioDict = new Dictionary<string, AudioClip>();
    [SerializeField] private Language selectedLanguage;
    [SerializeField] string CurentSelectedLanguage;
    [SerializeField] string CurrentCategoryName;


    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

    }

    public IEnumerator CategoryAudioDownlaodAndLoader(string currentCategory, bool isCommon = false)
    {
        CurrentCategoryName = currentCategory;
        string langStr = PlayerPrefs.GetString("PlayschoolLanguageAudio");

        if (System.Enum.TryParse(langStr, out selectedLanguage))
        {
            Debug.Log("Selected Language Enum: " + selectedLanguage);
        }
        else
        {
            Debug.LogWarning("Language not found, defaulting to English.");
            selectedLanguage = Language.English;
        }

        CurentSelectedLanguage = selectedLanguage.ToString();
        Debug.Log("Enum as String: " + CurentSelectedLanguage);
        yield return StartCoroutine(CheckDownloadExtract());
        StartCoroutine(LoadAllAudio(isCommon));
    }

    void Start()
    {

        StartCoroutine(CategoryAudioDownlaodAndLoader("common", true));
        // StartCoroutine(DownlaodAllBatchZip());

    }


    IEnumerator CheckDownloadExtract()
    {
        string bundleFolder = Path.Combine(Application.persistentDataPath, "AudioBundles");
        string categoryFolder = Path.Combine(bundleFolder, CurrentCategoryName);

        string zipPath = Path.Combine(categoryFolder, CurentSelectedLanguage + ".zip");
        string extractPath = Path.Combine(categoryFolder, CurentSelectedLanguage);

        // // ❗ If zip exists but too small → delete it
        // if (File.Exists(zipPath))
        // {
        //     FileInfo fi = new FileInfo(zipPath);
        //     if (fi.Length < 5000) // invalid zip check
        //     {
        //         Debug.LogWarning("Corrupted ZIP detected. Deleting...");
        //         File.Delete(zipPath);
        //     }
        // }

        if (!Directory.Exists(bundleFolder))
            Directory.CreateDirectory(bundleFolder);

        if (!Directory.Exists(categoryFolder))
            Directory.CreateDirectory(categoryFolder);

        if (!Directory.Exists(extractPath))
        {
            if (!File.Exists(zipPath))
            {
                yield return StartCoroutine(DownloadZip(zipPath));

                if (!File.Exists(zipPath))
                {
                    Debug.LogWarning("Zip not found on server for: " + CurentSelectedLanguage);
                    yield break;
                }

            }

            try
            {
                ZipFile.ExtractToDirectory(zipPath, extractPath);
                File.Delete(zipPath);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Extraction failed: " + e.Message);
                yield break;
            }
        }
    }

    IEnumerator DownloadZip(string savePath)
    {
        string url = $"https://d2r38fn3ydtrfq.cloudfront.net/{CurrentCategoryName}/{CurentSelectedLanguage}.zip";

        UnityWebRequest www = UnityWebRequest.Get(url);

        www.downloadHandler = new DownloadHandlerFile(savePath);

        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            if (www.result != UnityWebRequest.Result.Success || www.responseCode != 200)
            {
                //Debug.LogError($"Download failed: {www.error} | Code: {www.responseCode}");
                Debug.LogError($"Download failed: {www.error} | Audio Data Not Found Subcategory Name {CurrentCategoryName} Language : {CurentSelectedLanguage} ");

                // ❗ Delete bad file if somehow created
                if (File.Exists(savePath))
                    File.Delete(savePath);

                yield break;
            }



        }
    }

    IEnumerator LoadAllAudio(bool isCommon)
    {

        audioDict.Clear();
        //string path = Path.Combine(Application.persistentDataPath, "AudioBundles", CurentSelectedLanguage);

        string path = Path.Combine(
           Application.persistentDataPath,
           "AudioBundles",
           CurrentCategoryName,
           CurentSelectedLanguage
       );

        if (!Directory.Exists(path))
        {
            Debug.LogWarning("Audio folder missing: " + path);
            yield break;
        }

        if (!Directory.Exists(path))
        {
            Debug.LogError("Audio folder not found: " + path);
            yield break;
        }

        string[] files = Directory.GetFiles(path, "*.mp3");

        foreach (string file in files)
        {
            yield return LoadClip(file, isCommon);
        }
    }

    IEnumerator LoadClip(string filePath, bool isCommon)
    {
        UnityWebRequest www =
            UnityWebRequestMultimedia.GetAudioClip("file://" + filePath, AudioType.MPEG);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning("Clip load failed: " + filePath);
            yield break;
        }

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError(www.error);
            yield break;
        }
        AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
        string key = Path.GetFileNameWithoutExtension(filePath);
        if (isCommon)
        {
            clip.name = key;
            CommonaudioDict[key] = clip;
        }
        else
        {
            clip.name = key;
            audioDict[key] = clip;
        }

    }

    public float PlayRuntimeAudio(string key)
    {
        AudioClip clip = GetClip(key);
        if (clip == null)
        {
            Debug.Log("Your key not Found Chal Chal " + key);
            return -1;
        }
        _commonAudioSource.Stop();
        _commonAudioSource.PlayOneShot(clip);

        return clip.length;
    }
    public AudioClip GetClip(string name)
    {
        if (audioDict.ContainsKey(name))
            return audioDict[name];

        Debug.LogWarning("Audio not found: " + name);

        return null;
    }

    public AudioClip GetCommonAudioClip(string name)
    {
        if (CommonaudioDict.ContainsKey(name))
            return CommonaudioDict[name];

        Debug.LogWarning("Audio not found: " + name);

        return null;
    }

    #region  CommonAudio
    //Common
    Dictionary<string, AudioClip> CommonaudioDict = new Dictionary<string, AudioClip>();
    public void PlayNumberClip(int number)
    {

        _commonAudioSource.Stop();
        _commonAudioSource.PlayOneShot(GetCommonAudioClip(number.ToString() + ".0"));
    }

    public void PlayAlphabetClip(string Alphabet)
    {
        _commonAudioSource.Stop();
        _commonAudioSource.PlayOneShot(GetCommonAudioClip(Alphabet));
    }
    public void PlayRetryAudioClip()
    {

        _commonAudioSource.Stop();
        string randoms = "retry" + UnityEngine.Random.Range(1, 7);
        _commonAudioSource.PlayOneShot(GetCommonAudioClip(randoms));
    }

    public void PlayNextLevelAudioClip()
    {

        _commonAudioSource.Stop();
        string randoms = "nextlevel" + UnityEngine.Random.Range(1, 7);
        _commonAudioSource.PlayOneShot(GetCommonAudioClip(randoms));
    }

    public void PlaytimesupAudioClip()
    {

        _commonAudioSource.Stop();
        string randoms = "timesup" + UnityEngine.Random.Range(1, 7);
        _commonAudioSource.PlayOneShot(GetCommonAudioClip(randoms));
    }
    public void PlayCorrectAudioClip()
    {

        _commonAudioSource.Stop();
        string randoms = "correct" + UnityEngine.Random.Range(1, 7);
        _commonAudioSource.PlayOneShot(GetCommonAudioClip(randoms));
    }

    public void PlayIncorrectAudioClip()
    {

        _commonAudioSource.Stop();
        string randoms = "incorrect" + UnityEngine.Random.Range(1, 7);
        _commonAudioSource.PlayOneShot(GetCommonAudioClip(randoms));
    }

    public void StopCommonAudioSource()
    {
        _commonAudioSource.Stop();
    }


    #endregion

    public IEnumerator DownlaodAllBatchZip()
    {
        string bundleFolder = Path.Combine(Application.persistentDataPath, "AudioBundles");
        string categoryFolder = Path.Combine(bundleFolder, CurrentCategoryName);

        foreach (Language lang in System.Enum.GetValues(typeof(Language)))
        {

            CurentSelectedLanguage = lang.ToString();
            Debug.Log("Enum as String: " + CurentSelectedLanguage);

            string zipPath = Path.Combine(categoryFolder, CurentSelectedLanguage + ".zip");
            string extractPath = Path.Combine(categoryFolder, CurentSelectedLanguage);

            if (!Directory.Exists(bundleFolder))
                Directory.CreateDirectory(bundleFolder);

            if (!Directory.Exists(categoryFolder))
                Directory.CreateDirectory(categoryFolder);

            if (!Directory.Exists(extractPath))
            {
                if (!File.Exists(zipPath))
                {
                    yield return StartCoroutine(DownloadZip(zipPath));

                    if (!File.Exists(zipPath))
                    {
                        Debug.LogWarning("Zip not found on server for: " + CurentSelectedLanguage);
                        yield break;
                    }

                }

                try
                {
                    ZipFile.ExtractToDirectory(zipPath, extractPath);
                    File.Delete(zipPath);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("Extraction failed: " + e.Message);
                    yield break;
                }
            }

        }

    }

}












