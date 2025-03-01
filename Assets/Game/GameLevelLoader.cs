using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameLevelLoader : MonoBehaviour
{
    //Editor only
    public LevelUI temporaryLevel;

    public Terrain terrain;
    public TerrainEdging terrainEdging;

    public Transform cameraPivot;

    [Header("Important manager & others")]
    public LevelManager levelManager;
    public GameLevelObjectives gLevelObjective;

    [Header("Panel modes")]
    public GameObject playModeScreen;
    public GameObject spectateModeScreen;

    [Header("Radar")]
    public RawImage radarImg;

    [Header("Weather/Clock")]
    public Light directionalLight;
    public Text weatherClock;
    public Image weatherIcon;

    [Header("Menu info")]
    public Text menu_mapNameBcg;
    public Text menu_mapName;
    public Text menu_userName;
    public RawImage menu_userIcon;

    //
    [Header("Tower building menu")]
    public RectTransform tower_root_ant;
    public RectTransform tower_root_antiAircraft;
    public RectTransform tower_root_plasmatic;
    public RectTransform tower_root_machineGun;
    public RectTransform tower_root_granader;

    //Backgrounds
    [Header("UI Team coloring")]
    public Image[] colorizeImages;

    public Button tower_antBcg;
    public Button tower_antiAircraftBcg;
    public Button tower_plasmaticBcg;
    public Button tower_machineGunBcg;
    public Button tower_granaderBcg;

    void Start()
    {
        StartCoroutine(loadLevel());
    }

    private IEnumerator loadLevel()
    {
        while (!GlobalList.loadFinished)
            yield return null;

        try
        {
            //Apply temporary level (for editor)
            if (LoadLevelPanel.onGoingLoadingLvl == null)
            {
                temporaryLevel.Init();
                LoadLevelPanel.onGoingLoadingLvl = temporaryLevel.header;
            }

            MapLevel.setStaticParentTrans(levelManager.transform);

            LevelData.Init();
            LevelData.environment.LinkUI(weatherClock, weatherIcon);

            //Switch main scene status
            LevelData.scene = LevelData.Scene.GAME;

            //Fill menu UI
            menu_mapNameBcg.text = LoadLevelPanel.onGoingLoadingLvl.lvlName;
            menu_mapName.text = LoadLevelPanel.onGoingLoadingLvl.lvlName;
            if (!string.IsNullOrEmpty(DiscordProfile.lastUserName))
            {
                menu_userName.text = DiscordProfile.lastUserName;
                menu_userName.color = Color.white;
                menu_userIcon.texture = DiscordProfile.lastAvatar;
            }

            //Load level - Implemented*
            Level.Header header = LoadLevelPanel.onGoingLoadingLvl;

            using (BinaryReader br = Level.OpenFileReader(header.fileLvlPath))
            {
                var (major, minor, fix) = Level.GetVersion(br);
                Level level = Level.GetMapLevelInstance(major, minor, fix);

                if (level == null)
                    throw new System.Exception("Load unsupported for version: " + major + "." + minor + "." + fix + "");


            }

            /*FileStream stream = new FileStream(lui.fileLvlPath, FileMode.Open);
            BinaryReader br = new BinaryReader(stream);

            stream.Position = LoadLevelPanel.onGoingLoadingLvl.mapLevelData_offset;
            byte[] mapLevelBytes = LevelUI.readByteSequence(br, lui.mapLevelData_length);

            br.Close();
            stream.Close();

            if (PanelLoadLevel.compareVersion(lui, 03, 06, 01))
            {
                byte[] decompressedMapBytes = CompressionManager.Decompress(mapLevelBytes);

                ML_03_05 mapLevel = PanelLoadLevel.Deserialize<ML_03_05>(decompressedMapBytes);
                mapLevel.mapWidth = (int)(mapLevel.mapWidth / 2f);
                mapLevel.mapHeight = (int)(mapLevel.mapHeight / 2f);
                mapLevel.LoadData(terrain, gameObject.transform, false);
            }
            else
            {
                throw new Exception("THIS LEVEL SHOULDN'T BE LOADED... IT'S OUTDATED OR NOT ADDED AND CONFIGURED!\nMinor: " + lui.big_patch + "_" + lui.small_patch + "_" + lui.build_code);
            }*/

            LevelData.ResizeTerrain((int)terrain.terrainData.size.x, terrainEdging, (int)terrain.terrainData.size.x, false);

            TerrainData td = terrain.terrainData;

            //Generate radar img
            GenerateRadarMinimap();

            //Reset all teamstats
            for (int i = 0; i < LevelData.units.Count; i++) //Check if team is going to somehow partipiciated in game
            {
                Unit t = LevelData.units[i];
                if (t.id == "0_commandCenter1")
                    LevelData.teamStats[t.team.id].activeTeam = true;
            }
            for (int i = 0; i < LevelData.teamStats.Length; i++)
            {
                TeamStats02_12 t = LevelData.teamStats[i];
                t.OnDeserialize();
                t.clearStats();
            }

            //Change UI Coloring
            changePlayerTeam(LoadLevelPanel.selectedTeamID);

            // Init objectives
            gLevelObjective.Init();

            //Teleport Camera to some headquarter
            cameraPivot.position = new Vector3(td.size.x / 2f, 1f, td.size.z / 2f);
            foreach (Unit unit in LevelData.units)
            {
                if (unit.id == "0_commandCenter1" && unit.team.id == LevelData.ts.teamId)
                {
                    cameraPivot.position = new Vector3(unit.transform.position.x, 1f, unit.transform.position.z);
                    break;
                }
            }

            //Initiate team drivers
            if (LoadLevelPanel.teamPlaySettings == null) //If loading directly from gamelevel scene / set default values
            {
                LoadLevelPanel.teamPlaySettings = new TeamPlaySettings[GlobalList.teams.Length];
                /*for (int i = 0; i < GlobalList.teams.Length; i++)
                {
                    LoadLevelPanel.teamPlaySettings[i] = new TeamPlaySettings(i, SingleplayerTeamSettings.DRIVER_CATEGORY_AI, "aiClassic");
                }
                LoadLevelPanel.teamPlaySettings[LevelData.ts.teamId] = new TeamPlaySettings(LevelData.ts.teamId, SingleplayerTeamSettings.DRIVER_CATEGORY_PLAYER, "playerMain");*/

                for (int i = 0; i < GlobalList.teams.Length; i++)
                {
                    //LoadLevelPanel.teamPlaySettings[i] = new TeamPlaySettings(i, SingleplayerTeamSettings.DRIVER_CATEGORY_AI, i % 2 == 0 ? "aiClassic" : "aiClassic");
                    LoadLevelPanel.teamPlaySettings[i] = new TeamPlaySettings(i, SingleplayerTeamSettings.DRIVER_CATEGORY_AI, i % 2 == 0 ? "aiClassic" : "aiAdvanced");
                }
                LoadLevelPanel.teamPlaySettings[LevelData.ts.teamId] = new TeamPlaySettings(LevelData.ts.teamId, SingleplayerTeamSettings.DRIVER_CATEGORY_PLAYER, "playerMain");
            }

            for (int i = 0; i < LoadLevelPanel.teamPlaySettings.Length; i++)
            {
                TeamPlaySettings tps = LoadLevelPanel.teamPlaySettings[i];
                GameObject newDriver = Instantiate(GlobalList.teamDrivers[tps.teamDriverCategoryId][tps.teamDriverId].gameObject, transform);
                newDriver.name = GlobalList.teams[i].name + " TeamDriver";
                newDriver.GetComponent<TeamDriver>().controllingTeam = GlobalList.teams[i];
            }

            //Unpause game
            levelManager.timeWarp_1();

            //Trigger all starting nodes
            for (int i = 0; i < LevelData.teamStats.Length; i++)
            {
                for (int j = 0; j < GameLevelObjectives.allObjectives[i].Count; j++)
                {
                    if (GameLevelObjectives.allObjectives[i][j].ser.nodeId == BarTaskTriggers.NODE_START_BASE)
                    {
                        GameLevelObjectives.allObjectives[i][j].triggerNode();
                    }
                }
            }
        } catch (Exception e)
        {
            Debug.LogException(e);
            StartCoroutine(MainMenuShowMsg("Failed to load level", e.Message));
        }
    }

    IEnumerator MainMenuShowMsg(string title, string desc)
    {
        levelManager.timeWarp_1();
        AsyncOperation operation = SceneManager.LoadSceneAsync(1, LoadSceneMode.Additive);
        
        while (!operation.isDone)
            yield return new WaitForEndOfFrame();
        
        MainMenuCanvas.ShowMessage(title, desc);

        SceneManager.UnloadSceneAsync(4);
    }

    public void changePlayerTeam(int newTeamId)
    {
        if (newTeamId == -1)
        {
            playModeScreen.SetActive(false);
            spectateModeScreen.SetActive(true);

            LevelData.ts = LevelData.teamStats[0];
        } else
        {
            playModeScreen.SetActive(true);
            spectateModeScreen.SetActive(false);

            LevelData.ts = LevelData.teamStats[newTeamId];
            setupTowerBuildMenu();
            setUIColoring();
        }
    }

    public void setupTowerBuildMenu()
    {
        float current_offset = 20.6f;
        TeamStats02_12 td = LevelData.ts;

        current_offset = checkTowerButton(tower_root_granader, td.granader, current_offset);
        current_offset = checkTowerButton(tower_root_machineGun, td.machineGun, current_offset);
        current_offset = checkTowerButton(tower_root_plasmatic, td.plasmatic, current_offset);
        current_offset = checkTowerButton(tower_root_antiAircraft, td.antiAircraft, current_offset);
        current_offset = checkTowerButton(tower_root_ant, td.ant, current_offset);
    }

    private float checkTowerButton(RectTransform rt, bool condition, float current_offset)
    {
        rt.anchoredPosition = new Vector2(current_offset, condition ? 0 : -60);
        current_offset += condition ? -144 : 0;
        return current_offset;
    }

    public void setUIColoring()
    {
        Color c = GlobalList.teams[LevelData.ts.teamId].minimapColor;

        foreach(Image i in colorizeImages)
        {
            i.color = c;
        }

        ColorBlock cb = new ColorBlock();
        cb.colorMultiplier = 1f;
        cb.normalColor = c;
        cb.highlightedColor = c;
        cb.selectedColor = c;
        cb.pressedColor = new Color(1f, 1f, 1f, 1f);

        tower_antBcg.colors = cb;
        tower_antiAircraftBcg.colors = cb;
        tower_plasmaticBcg.colors = cb;
        tower_machineGunBcg.colors = cb;
        tower_granaderBcg.colors = cb;
    }

    public void GenerateRadarMinimap()
    {
        //Generate radar img
        TerrainData td = Terrain.activeTerrain.terrainData;
        Texture2D radarTxt = new Texture2D(td.alphamapResolution, td.alphamapResolution, TextureFormat.RGBA32, false);
        float[,,] alphamaps = td.GetAlphamaps(0, 0, td.alphamapResolution, td.alphamapResolution);
        Color[] terrainColors = new Color[8] {
            new Color(0, 1f, 0, 1f), //Grass
            new Color(0.392f, 0.392f, 0.392f, 1f),
            new Color(0.4588f, 0.278f, 0.055f, 1f),
            new Color(0.89f, 0.8f, 0f, 1f),
            new Color(120, 108, 0f, 1f),
            new Color(0.471f, 0.471f, 0.471f, 1f),
            new Color(0.706f, 0.706f, 0.706f, 1f),
            new Color(0.49f, 0.49f, 0.49f, 1f)
        };

        for (int x = 0; x < radarTxt.width; x++)
        {
            for (int y = 0; y < radarTxt.height; y++)
            {
                bool isThereWater = false; //TODO minimap radar show water/land
                if (isThereWater)
                {
                    radarTxt.SetPixel(x, y, Color.cyan);
                }
                else
                {
                    for (int i = alphamaps.GetLength(2) - 1; i >= 0; i--)
                    {
                        if (alphamaps[x, y, i] > 0.1f)
                        {
                            radarTxt.SetPixel(x, y, terrainColors[i]);
                            break;
                        }
                    }
                }
            }
        }
        radarTxt.Apply();
        radarImg.texture = radarTxt;
    }
}
