using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class GameManager : MonoBehaviour       
{
    public static GameManager instance { get; private set; }


    [Header("Dependencies")]
    private skillManager skillManager;
    private inventory inventory;
    private currencyInfo currencyInfo;


    [Header("Sound")]
    [SerializeField] private Button[] btns;
    [SerializeField] private List<UI_volumeSlider> volumeSliders;


    [Header("Skill UI")]
    [SerializeField] private UnityEngine.UI.Image shotCool;
    [SerializeField] private Transform UI_skillSlotsParent;
    public UI_Slot[] UI_skillSlots;
    public List<skill> selectedSkills;
    public float skillCoolDown;


    [Header("Soldier UI")]
    
    [FormerlySerializedAs("UI_unitSlots")]
    public UI_Slot[] UI_soldierSlots;
    public List<float> soldierSpawnCoolTime = new List<float>();
    public List<float> soldierSpawnCoolTimer = new List<float>();


    [Header("Hero UI")]
    public UI_Slot[] UI_HeroSlots;
    public List<float> HeroSpawnCoolTime = new List<float>();
    public List<float> HeroSpawnCoolTimer = new List<float>();
    public List<GameObject> heroInstance;


    [Header("OtherReference")]
    [SerializeField] private GameObject tutorialObj;
    public GameObject DeathCanvas;
    public UIaugment uIaugment;


    [Header("Other")]
    public bool isGoldTowerEffectActive;
    public int ExDeathCount;
    private float spawnCoolDown;


    [Header("Constants")]
    private const float MaxSkillCooldown = 0.3f;
    private const float UnitCooldownMultiplier = 0.075f;
    private const float UnitMinCooldown = 30f;
    private const float UnitMaxCooldown = 120f;
    private const float HeroBaseCooldown = 150f;
    private const float TankerCooldownMultiplier = 1.5f;


    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    private void Start()
    {
        InitializeDependencies();
        InitializeAudio();

        CheckAndShowTutorial();
        
        InitializeSkillCooldowns();
        InitializeUnitCooldowns();
        InitializeHeroCooldowns();
    }

    private void Update()
    {
        UpdateSkillCooldownUI();
        UpdateUnitCooldownUI();
        UpdateHeroCooldownUI();
    }

    #region Initializ
    private void InitializeDependencies()
    {
        currencyInfo = currencyInfo.instance;
        inventory = inventory.Instance;
        skillManager = skillManager.instance;
        UI_skillSlots = UI_skillSlotsParent.GetComponentsInChildren<UI_Slot>();
    }

    private void InitializeAudio()
    {
        foreach (Button btn in btns)
        {
            if (btn == null) continue;
            btn.onClick.AddListener(() => audioManager.Instance.playSfx(39));
        }

        if (currencyInfo.settedVolume.Count > 0)
        {
            for (int i = 0; i < volumeSliders.Count; i++)
            {
                volumeSliders[i].sliderValue(currencyInfo.settedVolume[i]);
                volumeSliders[i].slider.value = currencyInfo.settedVolume[i];
            }
        }
    }

    private void CheckAndShowTutorial()
    {
        if (stageInfo.currentStoryChapter == 4) return; // 튜토리얼이 없는 챕터

        int chapterIndex = stageInfo.currentStoryChapter - 1;
        if (tutorialObj != null && !currencyInfo.isSeenTutorial[chapterIndex])
        {
            tutorialObj.SetActive(true);
            currencyInfo.isSeenTutorial[chapterIndex] = true;
        }
    }

    private void InitializeSkillCooldowns()
    {
        selectedSkills = inventory.selectedSkills;
        
        float rawSkillCooldown = inventory.getCardOptValue(opt.cooltime) + inventory.getSpecificRingEffect(ringTypes.coolDownRing, 0);
        skillCoolDown = Mathf.Min(rawSkillCooldown, MaxSkillCooldown*100) * 0.01f;

        if (stageInfo.gameMode == GameMode.ExPark)
        {
            skillCoolDown = MaxSkillCooldown;
        }
    }

    private void InitializeUnitCooldowns()
    {
        spawnCoolDown = inventory.getSpecificRingEffect(ringTypes.coolDownRing, 0) * 0.01f;

        foreach (var soldier in inventory.selectedSoliders)
        {
            teamStat tstat = soldier.GetComponent<teamStat>();
            float coolTime = 0f;

            if (tstat.esc != null)
            {
                coolTime = tstat.esc.cost * UnitCooldownMultiplier * 2f * GetGoldTowerMultiplier();
                coolTime = Mathf.Clamp(coolTime, UnitMinCooldown, UnitMaxCooldown) * (1f - spawnCoolDown);
            }

            soldierSpawnCoolTime.Add(coolTime);
            soldierSpawnCoolTimer.Add(0f); 
        }
    }

    private void InitializeHeroCooldowns()
    {
        heroInstance = new List<GameObject>(new GameObject[6]);

        foreach (var hero in inventory.selectedheros)
        {
            teamStat tstat = hero.GetComponent<teamStat>();
            float coolTime = 0f;

            if (tstat.esc != null)
            {
                coolTime = HeroBaseCooldown * (1f - spawnCoolDown) * GetGoldTowerMultiplier();
                if (tstat.unitType == unitTypes.tanker)
                {
                    coolTime *= TankerCooldownMultiplier;
                }
            }

            HeroSpawnCoolTime.Add(coolTime);
            HeroSpawnCoolTimer.Add(0f);
        }
    }
    #endregion

    #region UpdateCoolDown
    private void UpdateSkillCooldownUI()
    {
        if (skillManager.mainShot != null)
        {
            shotCool.fillAmount = skillManager.mainShot.cooltimer / (skillManager.mainShot.fourSkill.coolTime * (1f - skillCoolDown));
        }

        for (int i = 0; i < selectedSkills.Count; i++)
        {
            if (selectedSkills[i] == null || i >= UI_skillSlots.Length) continue;
            UI_skillSlots[i].blackBg.fillAmount = selectedSkills[i].cooltimer / (selectedSkills[i].fourSkill.coolTime * (1f - skillCoolDown));
        }
    }

    private void UpdateUnitCooldownUI()
    {
        for (int i = 0; i < inventory.selectedSoliders.Count; i++)
        {
            if (UI_soldierSlots[i] == null) continue;
            soldierSpawnCoolTimer[i] -= Time.deltaTime;
            UI_soldierSlots[i].blackBg.fillAmount = soldierSpawnCoolTimer[i] / soldierSpawnCoolTime[i];
        }
    }

    private void UpdateHeroCooldownUI()
    {
        for (int i = 0; i < inventory.selectedheros.Count; i++)
        {
            if (UI_HeroSlots[i] == null) continue;
            HeroSpawnCoolTimer[i] -= Time.deltaTime;
            UI_HeroSlots[i].blackBg.fillAmount = HeroSpawnCoolTimer[i] / HeroSpawnCoolTime[i];
        }
    }
    #endregion

    #region Get & Public Methods
    private float GetGoldTowerMultiplier() => isGoldTowerEffectActive ? 1.1f : 1f;

    public void OnDeathCanvas()
    {
        ingameInventory.Instance.makeTimeScale0();
        DeathCanvas.SetActive(true);
    }

    public void changeAllAugment(int index)
    {
        switch (index)
        {
            case 1: uIaugment.SetAttackBias(); break;
            case 2: uIaugment.SetDefenseBias(); break;
            case 3: uIaugment.SetGoldBias(); break;
            case 4: uIaugment.SetProbabilityBias(); break;
        }
    }
    #endregion
}