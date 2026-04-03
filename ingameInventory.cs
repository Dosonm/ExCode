using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ingameInventory : MonoBehaviour
{
    public static ingameInventory Instance { get; private set; }

    private audioManager audioManager;
    private inventory inventory;
    public AugmentData augmentData;

    [Header("Resources & Data")]
    public float gold;
    public float defaultMaxGold;
    public float maxGold;
    public int soul;
    public int defaultMaxSoul;
    public int maxSoul;
    
    public float goldIncreaseInterval = 0.1f;
    public float goldPerInterval = 1; 
    private float augmentGoldIncrease = 1;
    
    public int DefaultMaxPopulation;
    public int MaxPopulation;   


    [Header("Data")]
    private int wave;
    private int workerLevel = 0;
    public int deathPeople = 0;
    public bool isMad;
    public bool isGoldTowerEffect;
    public bool isClearBoss = false;


    [Header("Object Pools")]
    [SerializeField] private int defaultCapacity = 20;
    [SerializeField] private Transform poolParent;

    [SerializeField] private GameObject arrowPrefab;
    private List<GameObject> arrowPool = new List<GameObject>();

    [SerializeField] private GameObject firePrefab;
    private List<GameObject> firePool = new List<GameObject>();


    [Header("UI")]
    [SerializeField] private GameObject augmentCanvas;
    [SerializeField] private TextMeshProUGUI[] infoSlots;
    [SerializeField] private List<GameObject> heroSlots;
    [SerializeField] private List<TextMeshProUGUI> workerCostTmp;


    [Header("Entities")]
    public List<teamStat> aliveEntityList = new List<teamStat>();
    public List<GameObject> aliveHeroList = new List<GameObject>();
    public List<GameObject> aliveEnemyList = new List<GameObject>();
    [HideInInspector] public int AliveEntity;
    public List<float> aliveBuffers = new List<float>();
    public List<float> aliveHealers = new List<float>();
    [SerializeField] private GameObject heroHpPrefab;


    [Header("Others")]
    [SerializeField] public GameObject snow;
    public GameObject star;
    public float currentTimeScale;
    [SerializeField] private GameObject soulTmpPrefab;

    [HideInInspector] public List<float> spawnYPositions = new List<float>();
    public Transform spawnTransform;
    

    private readonly int[] workerCosts = { 100, 200, 300, 400, 500, 700, 800, 1000 };
    private readonly float[] workerRatios = { 0, 1, 3, 6, 10, 15, 22, 30, 40 };


    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        audioManager = audioManager.Instance;
        inventory = inventory.Instance;

        float cardBonus = inventory.getCardOptValue(opt.goldPerTime);
        float ringBonus = inventory.getSpecificRingEffect(ringTypes.goldRing, 1);
        goldPerInterval = 1f + ((cardBonus + ringBonus) * 0.01f);
        
        StartCoroutine(IncreaseGoldOverTime());

        AddGold((int)inventory.getCardOptValue(opt.startGold));
        AddSoul((int)inventory.getCardOptValue(opt.startSoul));

        updateCurrencyUI();

        if (stageInfo.gameMode == GameMode.Story || stageInfo.gameMode == GameMode.Raid)
        {
            InitializePools();
        }
    }

    private void InitializePools()
    {
        for (int i = 0; i < defaultCapacity; i++)
        {
            GameObject aro = Instantiate(arrowPrefab, poolParent);
            aro.SetActive(false);
            arrowPool.Add(aro);

            GameObject fir = Instantiate(firePrefab, poolParent);
            fir.SetActive(false);
            firePool.Add(fir);
        }
    }

    #region Spawning System
    public void spawnUnit(GameObject _unit, int _cost)
    {
        audioManager.playSfx(19);
        teamStat tstat = _unit.GetComponent<teamStat>();
        
        if (tstat.esc.rank == Rank.SSS)
            audioManager.playConsistentSfx(36); //sss영웅의 고유 소리 추가

        //소환 로직
        RandomSpawn(_unit, null);
        SpendGold(_cost);
        AliveEntity++;

        //힐러, 버퍼 전용
        if (tstat.esc.unitType == unitTypes.buffer)
            aliveBuffers.Add(_unit.GetComponent<healerBufferStat>().esc.buff);
        else if (tstat.esc.unitType == unitTypes.healer)
            aliveHealers.Add(_unit.GetComponent<healerBufferStat>().esc.heal);
        
        UpdatePopulationUI();
    }

    public void spawnHero(GameObject _unit, GameObject _parent, int _myindex2)
    {
        audioManager.playSfx(20);
        int cost = _unit.GetComponent<teamStat>().esc.heroCost;

        //영웅 체력바
        GameObject heroHp = Instantiate(heroHpPrefab, _parent.transform.position, Quaternion.identity, heroSlots[_myindex2].transform);
        RectTransform rectTransform = heroHp.GetComponent<RectTransform>();
        rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, -300);

        //소환 로직
        RandomSpawn(_unit, heroHp);
        aliveHeroList.Add(_unit);
        SpendHeroCost(cost);
        AliveEntity++;

        UpdatePopulationUI();
    }

    private void RandomSpawn(GameObject _unit, GameObject _HP)
    {
        //소환될 y값 결정
        float yPos = GetAvailableSpawnY(_unit);
        spawnYPositions.Add(yPos);
        
        //소환
        Vector2 spawnPosition = new Vector2(spawnTransform.position.x, spawnTransform.position.y + yPos);
        GameObject team = Instantiate(_unit, spawnPosition, Quaternion.identity);
        teamStat ts = team.GetComponent<teamStat>();

        //소환 후 initialize
        if (ts.heroOrUnit == heroOrUnit.hero)
        {
            _HP.GetComponent<HeroHealthBar>().getHero(team);
            ts.getHeroType(_unit);
            
            int uiIndex = getUiIndex(_unit);
            if(uiIndex != -1)
            {
                GameManager.instance.UI_HeroSlots[uiIndex].getGameObj(team);
                GameManager.instance.heroInstance[uiIndex] = team;
            }
        }
        
        aliveEntityList.Add(ts);

        //죽은 유닛으로부터 y값 가져오기
        if (ts.isDead)
        {
            spawnYPositions.Remove(yPos);
        }
    }

    private float GetAvailableSpawnY(GameObject _unit)
    {
        teamStat unit = _unit.GetComponent<teamStat>();
        float center = -0.1f;
        float[] priorityOrder;

        //유닛 타입에 따른 y위치를 다르게 설정
        switch (unit.unitType)
        {
            case unitTypes.buffer:
                priorityOrder = new float[] { center - 0.5f, center - 0.4f, center - 0.6f, center - 0.3f };
                break;
            case unitTypes.healer:
                priorityOrder = new float[] { center + 0.5f, center + 0.4f, center + 0.6f, center + 0.3f };
                break;
            case unitTypes.tanker:
            case unitTypes.warrior:
            case unitTypes.ranger:
            default:
                priorityOrder = new float[] { center, center + 0.1f, center - 0.1f, center + 0.2f, center - 0.2f };
                break;
        }

        foreach (float y in priorityOrder)
        {
            if (!spawnYPositions.Contains(y))
                return y;
        }

        return priorityOrder[UnityEngine.Random.Range(0, priorityOrder.Length)] + UnityEngine.Random.Range(0.0001f, 0.01f);
    }

    public void RemoveSpawnY(float y) => spawnYPositions.Remove(y);
    #endregion

    #region Economy System
    private IEnumerator IncreaseGoldOverTime()
    {
        while (true)
        {
            yield return new WaitForSeconds(goldIncreaseInterval);
            
            float baseAmount = goldPerInterval + (RatioPerWorker() * 0.15f);
            float finalAmount = baseAmount * augmentGoldIncrease * (1f + augmentData.getCurrencyBonus());
            
            AddGold(finalAmount); 
        }
    }

    //일꾼에 비례한 골드 생산속도 계산 함수
    private float RatioPerWorker() => workerLevel < workerRatios.Length ? workerRatios[workerLevel] : 1f;

    //현재 일꾼에 비례해 증가하는 골드 생산속도 증가 비용
    private int CostPerWorker() => workerLevel < workerCosts.Length ? workerCosts[workerLevel] : 10000;

    private void changeWorkerText()
    {
        string text = workerLevel >= 8 ? "최대 레벨" : $"{CostPerWorker()}골드";
        workerCostTmp[0].text = text;
        workerCostTmp[1].text = text;
    }

    public void AddGold(float amount)
    {
        gold = Mathf.Min(gold + amount, maxGold);
        UpdateGoldUI();
    }

    public void AddSoul(int amount)
    {
        soul = Mathf.Min(soul + amount, maxSoul);
        UpdateSoulUI();
    }

    public void addMaxSoul(int _int)
    {
        maxSoul += _int;
        UpdateSoulUI();
    }

    public void changeAugmentGold(float i)
    {
        augmentGoldIncrease += augmentGoldIncrease * 0.01f * i * GoldTowerEffect(); 
    }

    private void SpendGold(int amount)
    {
        if (augmentData.isForbiddenDeal) return;
        gold -= amount;
        UpdateGoldUI();
    }

    private void SpendHeroCost(int amount)
    {
        if (augmentData.isForbiddenDeal) return;
        soul -= amount;
        UpdateSoulUI();
    }
    #endregion

    #region UI & Updates
    public void updateCurrencyUI()
    {
        UpdateSoulUI();
        UpdatePopulationUI();
    }

    private void UpdateGoldUI()
    {
        maxGold = defaultMaxGold + augmentData.getMaxGold();
        if (infoSlots[1] != null) 
            infoSlots[1].text = $"{Mathf.FloorToInt(gold)}/{maxGold}";
    }

    public void UpdateSoulUI()
    {
        maxSoul = augmentData.getMaxSoul() + defaultMaxSoul;
        if (infoSlots[2] != null) 
            infoSlots[2].text = $"{soul}/{maxSoul}";
    }

    public void UpdatePopulationUI()
    {
        MaxPopulation = DefaultMaxPopulation + augmentData.getMaxPopulation();
        if (infoSlots[3] != null) 
            infoSlots[3].text = $"{AliveEntity} / {MaxPopulation}";
    }

    public void UpdateWaveText(int _wave)
    {
        wave = _wave;
        if (infoSlots[0] != null) 
            infoSlots[0].text = wave.ToString();
    }

    public void UpgradeWorker()
    {
        if (workerLevel >= 8)
        {
            WarningMessageManager.Instance.ShowMessage("일꾼의 레벨이 최대입니다.");
            return;
        }

        int cost = CostPerWorker();
        if (gold < cost)
        {
            WarningMessageManager.Instance.ShowMessage("골드가 부족합니다.");
            return;
        }

        audioManager.playSfx(8);

        SpendGold(cost);
        workerLevel++;

        changeWorkerText();
    }
    #endregion
    
    #region Get
    private int getUiIndex(GameObject _prefab)
    {
        var heroes = inventory.Instance.selectedheros;
        for (int i = 0; i < heroes.Count; i++)
        {
            if (_prefab == heroes[i]) return i;
        }
        return -1;
    }

    public void getBossReward(BossRank _bossRank)
    {
        if (stageInfo.gameMode == GameMode.Dungeon) return;
        
        switch (_bossRank)
        {
            case BossRank.A:
                AddGold(200);
                break;
            case BossRank.Aplus:
                AddSoul(1);
                break;
            case BossRank.S:
                augmentCanvas.GetComponent<UIaugment>().changeNum(2, 1);
                StartCoroutine(AugmentActive());
                break;
            case BossRank.SS:
                AddSoul(2);
                break;
            case BossRank.SSS:
                augmentCanvas.GetComponent<UIaugment>().changeNum(2, 2);
                StartCoroutine(AugmentActive());
                break;
        }
    }

    public GameObject GetArrow(int _ballNum)
    {
        return _ballNum == 2 ? returnBall(firePool, firePrefab) : returnBall(arrowPool, arrowPrefab);
    }

    #endregion

    #region Others
    public void makeTimeScale0()
    {
        currentTimeScale = Time.timeScale;
        Time.timeScale = 0;
        
        if (audioManager == null) return;
        
        audioManager.pauseSkillSfx();
    }
    public void makeTimeScaleOrigin()
    {
        Time.timeScale = currentTimeScale;
        audioManager.resumeSkillSfx();
    }

    public void InstanceSoulPrefab()
    {
        GameObject tmp = Instantiate(soulTmpPrefab, DamageTextManager.instance.canvas.transform);
        tmp.transform.position = playerManager.instance.player.transform.position + new Vector3(1.5f, 2f, 0);
    }

    IEnumerator AugmentActive()
    {
        yield return new WaitForSeconds(1f);
        augmentCanvas.SetActive(true);
    }

    private float GoldTowerEffect() => isGoldTowerEffect ? 1.2f : 1f;

    private GameObject returnBall(List<GameObject> pool, GameObject _ball)
    {
        // 비활성화된 투사체가 있는지 확인
        foreach (GameObject ball in pool)
        {
            if (!ball.activeInHierarchy)
            {
                return ball;
            }
        }

        // 없으면 새로 생성해서 리스트에 추가
        GameObject newBall = Instantiate(_ball);
        pool.Add(newBall);
        return newBall;
    }
    #endregion
}
