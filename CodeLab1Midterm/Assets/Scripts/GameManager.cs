using UnityEngine;
using TMPro;
using System.IO;

public class GameManager : MonoBehaviour
{
    const string DIR_DATA = "/Data/";
    const string FILE_HIGHSCORE = DIR_DATA + "highScore.txt";
    const string KEY_HIGHSCORE = "HighScore"; // 改用 PlayerPrefs 辅助或者保留文件逻辑

    public static GameManager instance;

    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI gameOverText;
    public TextMeshProUGUI winText;

    public float survivalTime = 0f;
    public float targetTime = 10f;
    public int currentLevel = 0;
    private bool gameOver = false;

    // 核心：使用属性，在设置 Score 时自动处理高分逻辑
    private float score;
    public float Score
    {
        get => score;
        set
        {
            score = value;
            if (score > HighScore)
            {
                HighScore = score;
            }
        }
    }

    public float HighScore
    {
        get { return LoadHighScore(); }
        set { SaveHighScore(value); }
    }

    void Awake()
    {
        if (instance == null) { 
            instance = this; 
            DontDestroyOnLoad(gameObject); 
        
            // hide game over text
            if (gameOverText != null) gameOverText.gameObject.SetActive(false);
            if (winText != null) winText.gameObject.SetActive(false);
        }
        else { 
            Destroy(gameObject); 
        }
    }

    void Update()
    {
        if (gameOver) return;

        survivalTime += Time.deltaTime;
        Score += Time.deltaTime;

        if (scoreText != null)
        {
            // Score&HighScore
            scoreText.text = "Your Score: " + Mathf.FloorToInt(Score+0.5f) + "  HighScore: " + Mathf.FloorToInt(HighScore);
        }

        if (survivalTime >= targetTime)
        {
            GoToNextLevel();
        }
    }

    public void GameOver()
    {
        if (gameOver) return; 

        gameOver = true;
    
        // display game over text
        if (gameOverText != null) 
        {
            gameOverText.gameObject.SetActive(true);
        }
    }

    void GoToNextLevel()
    {
        currentLevel++;
        if (ASCIILevelLoader.instance != null)
        {
            ASCIILevelLoader.instance.CurrentLevel = currentLevel;
        }
        
        targetTime *= 1.0f; // adjust level difficulty
        survivalTime = 0f;
    }
    
    private void SaveHighScore(float value)
    {
        string fullPath = Application.dataPath + FILE_HIGHSCORE;
        if (!Directory.Exists(Application.dataPath + DIR_DATA)) Directory.CreateDirectory(Application.dataPath + DIR_DATA);
        File.WriteAllText(fullPath, value.ToString("F0"));
    }

    private float LoadHighScore()
    {
        string fullPath = Application.dataPath + FILE_HIGHSCORE;
        return File.Exists(fullPath) ? float.Parse(File.ReadAllText(fullPath)) : 0f;
    }
}