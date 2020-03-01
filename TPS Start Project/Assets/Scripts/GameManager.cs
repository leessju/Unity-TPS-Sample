using UnityEngine;

public class GameManager : MonoBehaviour
{
    private static GameManager instance;
    
    public static GameManager Instance
    {
        get
        {
            if (instance == null) 
                instance = FindObjectOfType<GameManager>();
            
            return instance;
        }
    }

    private int score;
    public bool isGameover { get; private set; }

    private void Awake()
    {
        // GameManager가 하나만 생겨서 관리할 수 있도록 하기 위함
        if (Instance != this) 
            Destroy(gameObject);
    }
    
    public void AddScore(int newScore)
    {
        if (!isGameover)
        {
            score += newScore;
            UIManager.Instance.UpdateScoreText(score);
        }
    }
    
    public void EndGame()
    {
        isGameover = true;
        UIManager.Instance.SetActiveGameoverUI(true);
    }
}