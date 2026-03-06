using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

public class ASCIILevelLoader : MonoBehaviour
{
    //make the actual prefab in unity
    public GameObject wall;
    public GameObject player;
    public GameObject goal;
    public GameObject obstacle;
    public string fileLocation;
    string fullPath;//the full path to the level file
    private int currentLevel = 0;
    GameObject loadedLevel;

    public int CurrentLevel
    {
        set
        {
            currentLevel = value;
            LoadLevel();
        }
        get
        {
            return currentLevel;
        }
    }
    //offset for the origin of the level
    public int xOffset = 5;
    public int yOffset = 4;

    public static ASCIILevelLoader instance;
        
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (instance == null)
        {
            DontDestroyOnLoad(gameObject);
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
        //creating the full path to the file location
        fullPath = Application.dataPath + "/" + fileLocation;
        //pulls information from the file to create a new level based on file contents
        LoadLevel();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    
    //load the ASCII LEVEL
    public void LoadLevel()
    {
        Destroy(loadedLevel);
        loadedLevel = new GameObject("Level"+currentLevel);
        string fullPath = this.fullPath.Replace("<num>", currentLevel + "");
        //read the file,put each line into a slot of the 'lines' string array
        string[] lines=File.ReadAllLines(fullPath);
        Debug.Log(lines);
        //显示文件内容
        foreach (string line in lines)//遍历数组中每个元素
        {
            Debug.Log(line);
            //检查文件长度来确定x的初始偏移值
            int lengthOfLine = line.Length / 2;
            if (lengthOfLine>xOffset)
            {
                xOffset = lengthOfLine;
            }
        }
        yOffset = lines.Length / 2;//offset y based on how many lines there are in the file
        //xOffset = lines[0].Length / 2;
        
        //loop through all the lines in the file
        for(int y=0;y<lines.Length;y++)//y determines the y position in the world
        {
            string currentLineFromFile = lines[y];
            //look at every character on each line
            for (int x = 0; x < currentLineFromFile.Length; x++)//x 表示在世界中的x位置
            {
                char currentChar = currentLineFromFile[x];//get the character on from this line at x
                
                GameObject newObject= null;
                switch (currentChar)
                {
                    case'W' :
                        newObject=Instantiate<GameObject>(wall);
                        break;
                    case'P' :
                        newObject=Instantiate<GameObject>(player);
                        break;
                    case'G' :
                        newObject=Instantiate<GameObject>(goal);
                        break;
                    case'O' :
                        newObject=Instantiate<GameObject>(obstacle);
                        break;
                    default:
                        break;
                }
                if (newObject != null)
                {
                    // 这里从 Vector2 改为了 Vector3
                    // 原本的 yOffset - y 现在作为 Z 轴坐标，让关卡在地面平铺
                    newObject.transform.position = new Vector3(x - xOffset, 0, yOffset - y);
                    newObject.transform.SetParent(loadedLevel.transform);
                }
                //create a new gameObject based on the character
                //if (currentChar == 'W')
                //{
                //    GameObject newWall=Instantiate<GameObject>(wall);
                //    newWall.transform.position = new Vector2(x-xOffset, yOffset-y);
                //}
                //if (currentChar == 'P')
                //{
                //    GameObject newPlayer=Instantiate<GameObject>(Player);
                //    newPlayer.transform.position = new Vector2(x-xOffset, yOffset-y);
                //}
                
            }
            
        }
        
    }
}