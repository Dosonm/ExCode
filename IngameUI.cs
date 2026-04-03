using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class IngameUI : MonoBehaviour
{
    public static IngameUI Instance { get; private set; }

    private inventory inventory;
    private GameManager gameManager;
    private ingameInventory ingameInventory;


    [Header("Unit")]
    private UI_Slot[] UI_soldierSlots;
    [SerializeField] private List<GameObject> goldBarList;

    [SerializeField] private Transform UI_heroSlotParent;
    private UI_Slot[] UI_heroSlots;
    [SerializeField] private GameObject heroSlotA;
    [SerializeField] private GameObject heroSlotB;
    private CanvasGroup canvasGroup1;   //히어로 슬롯의 캔버스 1
    private CanvasGroup canvasGroup2;   //히어로 슬롯의 캔버스 2


    [Header("Skill")]
    [SerializeField] private List<Sprite> skillBgSprites;
    [SerializeField] private List<GameObject> skillLevelUpIcons;
    public UI_Slot[] UI_skillSlots;

    
    [Header("Other")]
    [SerializeField] private GameObject deathPopUp;
    private int timeScaleIndex = 1;
    private int skillCount = 0;
    private readonly float[] timeScaleValues = { 0f, 1f, 1.25f, 1.5f, 2f };
    private readonly string[] timeScaleTexts = { "", "x1", "x1.25", "x1.5", "x2" };


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
        gameManager = GameManager.instance;
        inventory = inventory.Instance;
        ingameInventory = ingameInventory.Instance;

        UI_soldierSlots = gameManager.UI_soldierSlots;
        UI_heroSlots = gameManager.UI_HeroSlots;
        UI_skillSlots = gameManager.UI_skillSlots;

        if (heroSlotA != null && heroSlotB != null)
        {
            canvasGroup1 = heroSlotA.GetComponent<CanvasGroup>();
            canvasGroup2 = heroSlotB.GetComponent<CanvasGroup>();
        }

        updateSlotUI(); 
    }


    #region UI Updates
    private void updateSlotUI()
    {
        // 병사 슬롯 & 골드바 업데이트
        for (int i = 0; i < UI_soldierSlots.Length; i++)
        {
            UI_soldierSlots[i].cleanUpSlot();

            if (UI_soldierSlots[i] == null) break; // 다른 컨텐츠에서도 재사용하기 위해

            bool hasUnit = i < inventory.selectedSoliders.Count;
            
            if (hasUnit)
                UI_soldierSlots[i].updateEntitySlotUI(inventory.selectedSoliders[i]);

            //골드바 (골드바: 유닛의 골드의 가시성 높이는 바)
            if (i < goldBarList.Count && goldBarList[i] != null)
            {
                goldBarList[i].SetActive(hasUnit);
            }
        }

        // 영웅 슬롯 업데이트
        for (int i = 0; i < UI_heroSlots.Length; i++)
        {
            UI_heroSlots[i].cleanUpSlot();

            if (UI_heroSlots[i] == null) continue;

            if (i < inventory.selectedheros.Count)
                UI_heroSlots[i].updateEntitySlotUI(inventory.selectedheros[i]);
        }

        // 스킬 슬롯 업데이트
        for (int i = 0; i < UI_skillSlots.Length; i++)
        {
            UI_skillSlots[i].cleanUpSlot();

            if (UI_skillSlots[i] == null) continue;

            if (i < skillManager.instance.selectedSkills.Count && skillManager.instance.selectedSkills[i] != null)
                UI_skillSlots[i].updateSkillSlotUI(skillManager.instance.selectedSkills[i].fourSkill.skillImage);
        }
    }
    #endregion

    #region Skill System
    public void LevelUpSkill(int index)
    {
        audioManager.Instance.playConsistentSfx(29);

        var targetSkill = skillManager.instance.selectedSkills[index];
        targetSkill.skillLevel++;

        int bgIndex = targetSkill.skillLevel - 2; //인덱스랑 매핑하기 위해
        if (bgIndex >= 0 && bgIndex < skillBgSprites.Count)
        {
            UI_skillSlots[index].updateSkillBg(skillBgSprites[bgIndex]);
        }

        setSkillLevelUpIcon(false);

        //스킬을 바로 찍지 않고 쌓였을 때
        if (skillCount > 0)
        {
            setSkillLevelUpIcon(true);
            skillCount--;
        }
    }

    public void setSkillLevelUpIcon(bool isShow)
    {
        //파라미터로 드러낼지 말지 결정 

        if (!isShow)    //안 드러냄
        {
            foreach (var icon in skillLevelUpIcons)
            {
                icon?.SetActive(false);
            }
            return;
        }

        //드러냄
        bool hasActiveIcon = skillLevelUpIcons.Any(icon => icon != null && icon.activeSelf);

        if (hasActiveIcon)  //그 전에 안 찍었을 때
        {
            skillCount++;
        }
        else                
        {
            var selectedSkills = skillManager.instance.selectedSkills;
            for (int i = 0; i < selectedSkills.Count; i++)
            {
                if (selectedSkills[i] != null && selectedSkills[i].skillLevel < 6)
                {
                    if (i < skillLevelUpIcons.Count) skillLevelUpIcons[i].SetActive(true);
                }
            }
        }
    }
    #endregion

    #region Other
    public void OnDeathUi()
    {
        deathPopUp.SetActive(true);
        Time.timeScale = 0;
        audioManager.Instance.pauseSkillSfx();
    }
    #endregion
    
    #region Utility Methods
    public void onlyOn(GameObject _menu) => _menu?.SetActive(true);
    public void onlyOff(GameObject _menu) => _menu?.SetActive(false);

    public void makeTimeScale0()
    {
        ingameInventory.makeTimeScale0();
    }
    public void makeTimeScaleOrigin()
    {
        ingameInventory.makeTimeScaleOrigin();
    }
    public void changeTimeScale(TextMeshProUGUI _tmp)
    {
        int maxIndex = currencyInfo.instance.iHaveGoldPass ? 4 : 3;

        timeScaleIndex++;
        if (timeScaleIndex > maxIndex)
        {
            timeScaleIndex = 1;
        }

        Time.timeScale = timeScaleValues[timeScaleIndex];
        _tmp.text = timeScaleTexts[timeScaleIndex];
        
        audioManager.Instance.UpdatePlayingSkillSfxPitch(Time.timeScale);
    }

    public void changeHeroSlot()
    {
        if (canvasGroup1 == null || canvasGroup2 == null) return;

        bool isSlotAActive = canvasGroup1.alpha == 1f;

        canvasGroup1.alpha = isSlotAActive ? 0f : 1f;
        canvasGroup1.blocksRaycasts = !isSlotAActive;

        canvasGroup2.alpha = isSlotAActive ? 1f : 0f;
        canvasGroup2.blocksRaycasts = isSlotAActive;
    }

    public void loadScene(string sceneName)
    {
        audioManager.Instance.stopEndCoroutine();
        SceneManager.LoadScene(sceneName);
    }
    #endregion
}