using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Duckov.Modding;
using HarmonyLib;
using UnityEngine;
using ItemStatsSystem;
using Newtonsoft.Json;
using System.Globalization;

namespace DuckovCheatGUI
{
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        public static GUIRenderer Renderer;
        public static string CacheFilePath;
        public static string ConfigFilePath;
        private Harmony harmony;
        private bool guiInitialized = false;

        private void OnEnable()
        {
            UnityEngine.Debug.Log("=================================");
            UnityEngine.Debug.Log("CheatGUI Mod v0.3.0 已加载！");

            // 设置文件路径（使用当前DLL文件所在目录）
            string dllDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            CacheFilePath = Path.Combine(dllDirectory, "ItemCache.json");
            ConfigFilePath = Path.Combine(dllDirectory, "Config.json");

            // 确保目录存在
            if (!Directory.Exists(dllDirectory))
            {
                Directory.CreateDirectory(dllDirectory);
            }

            UnityEngine.Debug.Log($"DLL目录: {dllDirectory}");
            UnityEngine.Debug.Log($"缓存文件: {CacheFilePath}");
            UnityEngine.Debug.Log($"配置文件: {ConfigFilePath}");
            UnityEngine.Debug.Log("=================================");

            try
            {
                // 初始化本地化系统
                LocalizationManager.Initialize();
                
                this.harmony = new Harmony("com.dandan.duckov.cheatgui");
                // Only patch if you need other patches - remove this line if not needed
                // this.harmony.PatchAll(Assembly.GetExecutingAssembly());

                UnityEngine.Debug.Log("[成功] Mod 初始化完成");
                UnityEngine.Debug.Log("进入游戏后按 Home 键打开菜单");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"初始化失败: {e}");
            }
        }

        private void Update()
        {
            // Initialize GUI once
            if (!guiInitialized)
            {
                try
                {
                    UnityEngine.Debug.Log("[>>] 创建GUI GameObject...");

                    GameObject guiObject = new GameObject("CheatGUI_Renderer");
                    DontDestroyOnLoad(guiObject);
                    Renderer = guiObject.AddComponent<GUIRenderer>();

                    UnityEngine.Debug.Log("[成功] GUI 创建成功！");
                    guiInitialized = true;
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"创建GUI失败: {e}");
                }
            }

            // Check for toggle key
            if (Input.GetKeyDown(KeyCode.Home) || Input.GetKeyDown(KeyCode.Backslash))
            {
                Renderer?.ToggleWindow();
            }
        }

        private void OnDisable()
        {
            if (harmony != null)
            {
                this.harmony.UnpatchAll("com.dandan.duckov.cheatgui");
            }
            UnityEngine.Debug.Log("CheatGUI Mod 已卸载");
        }
    }



    public class GUIRenderer : MonoBehaviour
    {
        // ============ Window & UI Settings ============
        private bool showWindow = false;
        private Rect windowRect = new Rect(50, 50, 750, 700);
        private Texture2D backgroundTexture;

        // ============ Tab System ============
        private int currentTab = 0;
        private string[] tabs = { "[物品]", "[作弊]", "[设置]" };

        // ============ Language Selection ============
        private int selectedLanguageIndex = 0;
        private string[] availableLanguages = { "zh-CN", "en-US" };
        private string[] languageDisplayNames = { "中文", "English" };
        private bool showLanguageDropdown = false;

        // ============ Item Management ============
        private string searchText = "";
        private string itemIdInput = "";
        private string itemCountInput = "1";
        private Vector2 itemScrollPosition = Vector2.zero;
        private List<ItemInfo> allItems = new List<ItemInfo>();
        private List<ItemInfo> searchResults = new List<ItemInfo>();
        private bool itemsLoaded = false;
        private bool isScanning = false;
        private DateTime cacheTime = DateTime.MinValue;

        // ============ Pagination ✨ NEW ============
        private int currentPage = 0;
        private int itemsPerPage = 10;
        private int totalPages => Mathf.Max(1, Mathf.CeilToInt((float)searchResults.Count / itemsPerPage));

        // ============ Features ============
        private bool teleportEnabled = false;

        // ============ UI Scale ============
        private float uiScale = 1.0f;
        private const float MIN_SCALE = 0.5f;
        private const float MAX_SCALE = 2.0f;
        private float baseWindowWidth = 750f;
        private float baseWindowHeight = 700f;

        // ============ Colors & Styles ============
        private readonly Color colorHeader = new Color(0.2f, 0.8f, 1f);
        private readonly Color colorSuccess = new Color(0.3f, 1f, 0.3f);
        private readonly Color colorWarning = new Color(1f, 0.8f, 0.2f);
        private readonly Color colorError = new Color(1f, 0.3f, 0.3f);
        private readonly Color colorMuted = new Color(0.7f, 0.7f, 0.7f);
        private readonly Color colorWindowTitle = new Color(1f, 1f, 1f); // Light blue-white for window title

        // ============ UI Dimensions (Uniform) ============
        private const float STANDARD_BUTTON_HEIGHT = 35f;
        private const float STANDARD_INPUT_HEIGHT = 35f;
        private const float STANDARD_BUTTON_WIDTH = 80f;
        private const float SMALL_BUTTON_WIDTH = 60f;
        private const float SMALL_BUTTON_HEIGHT = 30f;
        private const float MEDIUM_BUTTON_WIDTH = 100f;
        private const float LARGE_BUTTON_WIDTH = 130f;
        private const float TAB_BUTTON_HEIGHT = 50f;
        private const float SECTION_HEADER_HEIGHT = 35f;
        private const float STATUS_MESSAGE_HEIGHT = 30f;

        // ============ Font Sizes (Uniform) ============
        private const int FONT_SIZE_SMALL = 20;
        private const int FONT_SIZE_MEDIUM = 21;
        private const int FONT_SIZE_LARGE = 24;
        private const int FONT_SIZE_HEADER = 26;

        private void Update()
        {
            // 检测鼠标中键按下，且传送功能已启用
            if (teleportEnabled && Input.GetKeyDown(KeyCode.Mouse2))
            {
                CheatMove();
            }
        }

        // 更新本地化字符串
        private void UpdateLocalizedStrings()
        {
            tabs[0] = LocalizationManager.GetString("tab_items");
            tabs[1] = LocalizationManager.GetString("tab_cheats");
            tabs[2] = LocalizationManager.GetString("tab_settings");
            
            // 更新语言选择数组
            UpdateLanguageArrays();
        }

        // 更新语言数组
        private void UpdateLanguageArrays()
        {
            var languages = LocalizationManager.GetAvailableLanguages();
            availableLanguages = languages;
            languageDisplayNames = new string[languages.Length];
            
            for (int i = 0; i < languages.Length; i++)
            {
                // 动态获取语言显示名称
                languageDisplayNames[i] = GetLanguageDisplayName(languages[i]);
            }
            
            // 更新当前选择的语言索引
            string currentLang = LocalizationManager.GetCurrentLanguage();
            for (int i = 0; i < availableLanguages.Length; i++)
            {
                if (availableLanguages[i] == currentLang)
                {
                    selectedLanguageIndex = i;
                    break;
                }
            }
        }

        // 获取语言显示名称
        private string GetLanguageDisplayName(string languageCode)
        {
            // 根据语言代码设置显示名称
            switch (languageCode)
            {
                case "zh-CN":
                    return "中文";
                case "en-US":
                    return "English";
                case "ja-JP":
                    return "日本語";
                case "ko-KR":
                    return "한국어";
                case "fr-FR":
                    return "Français";
                case "de-DE":
                    return "Deutsch";
                case "es-ES":
                    return "Español";
                case "ru-RU":
                    return "Русский";
                case "it-IT":
                    return "Italiano";
                case "pt-BR":
                    return "Português (BR)";
                case "pt-PT":
                    return "Português (PT)";
                default:
                    // 尝试从系统获取语言显示名称
                    try
                    {
                        var culture = new CultureInfo(languageCode);
                        return culture.DisplayName;
                    }
                    catch
                    {
                        return languageCode; // 使用语言代码作为显示名称
                    }
            }
        }

        public void ToggleWindow()
        {
            showWindow = !showWindow;

            if (showWindow)
            {
                // 初始化背景纹理（如果还没有）
                if (backgroundTexture == null)
                {
                    InitializeBackground();
                }

                // 首次打开时加载缓存和配置
                if (!itemsLoaded)
                {
                    LoadItemsFromCache();
                }
                LoadConfig();

                // 更新本地化字符串
                UpdateLocalizedStrings();

                UnityEngine.Debug.Log("[菜单] 菜单打开");
            }
            else
            {
                showLanguageDropdown = false; // 关闭菜单时也关闭下拉列表
                UnityEngine.Debug.Log("[菜单] 菜单关闭");
            }
        }

        // 初始化背景纹理 ✨ NEW
        private void InitializeBackground()
        {
            backgroundTexture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            backgroundTexture.SetPixel(0, 0, new Color(0.15f, 0.15f, 0.15f, 0.8f));
            backgroundTexture.Apply();
        }

        // 加载配置 ✨ NEW
        private void LoadConfig()
        {
            try
            {
                if (File.Exists(ModBehaviour.ConfigFilePath))
                {
                    string json = File.ReadAllText(ModBehaviour.ConfigFilePath);
                    var config = JsonConvert.DeserializeObject<GUIConfig>(json);

                    uiScale = Mathf.Clamp(config.UIScale, MIN_SCALE, MAX_SCALE);
                    ApplyScale();

                    // 设置语言（只有在配置文件中明确设置了语言时才覆盖系统检测的语言）
                    if (!string.IsNullOrEmpty(config.Language))
                    {
                        LocalizationManager.SetLanguage(config.Language);
                        UnityEngine.Debug.Log($"[配置] 使用配置文件中的语言: {config.Language}");
                    }
                    else
                    {
                        UnityEngine.Debug.Log("[配置] 未设置语言，使用系统检测的语言");
                    }

                    UnityEngine.Debug.Log(LocalizationManager.GetString("config_loaded", uiScale));
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"{LocalizationManager.GetString("load_config_failed")}: {e.Message}");
            }
        }

        // 保存配置 ✨ NEW
        private void SaveConfig()
        {
            try
            {
                var config = new GUIConfig
                {
                    UIScale = uiScale,
                    Language = LocalizationManager.GetCurrentLanguage()
                };

                string json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(ModBehaviour.ConfigFilePath, json);

                UnityEngine.Debug.Log(LocalizationManager.GetString("config_saved", uiScale));
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{LocalizationManager.GetString("save_config_failed")}: {e.Message}");
            }
        }

        // 应用缩放 ✨ NEW
        private void ApplyScale()
        {
            windowRect.width = baseWindowWidth * uiScale;
            windowRect.height = baseWindowHeight * uiScale;

            // 确保窗口不超出屏幕
            if (windowRect.xMax > Screen.width)
            {
                windowRect.x = Screen.width - windowRect.width;
            }
            if (windowRect.yMax > Screen.height)
            {
                windowRect.y = Screen.height - windowRect.height;
            }

            // 确保窗口在屏幕内
            windowRect.x = Mathf.Max(0, windowRect.x);
            windowRect.y = Mathf.Max(0, windowRect.y);
        }

        // 从缓存加载物品
        private void LoadItemsFromCache()
        {
            try
            {
                if (File.Exists(ModBehaviour.CacheFilePath))
                {
                    UnityEngine.Debug.Log(LocalizationManager.GetString("loading_from_cache"));

                    string json = File.ReadAllText(ModBehaviour.CacheFilePath);
                    var cache = JsonConvert.DeserializeObject<ItemCache>(json);

                    allItems = cache.Items;
                    cacheTime = cache.CacheTime;

                    UnityEngine.Debug.Log(LocalizationManager.GetString("loaded_from_cache", allItems.Count));
                    UnityEngine.Debug.Log(LocalizationManager.GetString("cache_time_format", cacheTime));
                    itemsLoaded = true;
                }
                else
                {
                    UnityEngine.Debug.Log(LocalizationManager.GetString("cache_not_found"));
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{LocalizationManager.GetString("cache_load_failed")}: {e.Message}");
            }
        }

        // 扫描并保存到缓存（增强版 - 支持MOD物品）
        private void ScanAndCacheItems()
        {
            isScanning = true;
            allItems.Clear();

            try
            {
                UnityEngine.Debug.Log(LocalizationManager.GetString("scanning_items"));

                HashSet<int> addedIds = new HashSet<int>();
                int mainGameCount = 0;
                int modItemCount = 0;

                // 方法1: 从 ItemAssetsCollection 扫描主游戏物品
                try
                {
                    if (ItemAssetsCollection.Instance != null)
                    {
                        var allItemEntries = ItemAssetsCollection.Instance.entries;
                        UnityEngine.Debug.Log(LocalizationManager.GetString("main_game_items", allItemEntries.Count));

                        foreach (var itemEntry in allItemEntries)
                        {
                            try
                            {
                                if (itemEntry.prefab != null)
                                {
                                    int typeId = itemEntry.typeID;

                                    if (addedIds.Contains(typeId))
                                        continue;

                                    addedIds.Add(typeId);

                                    ItemInfo info = new ItemInfo
                                    {
                                        id = typeId,
                                        name = itemEntry.prefab.DisplayName ?? $"物品{typeId}",
                                        description = itemEntry.prefab.Description ?? "",
                                        value = itemEntry.prefab.Value,
                                        maxStack = itemEntry.prefab.MaxStackCount,
                                        weight = itemEntry.prefab.UnitSelfWeight,
                                        isMod = false
                                    };

                                    allItems.Add(info);
                                    mainGameCount++;
                                }
                            }
                            catch (Exception e)
                            {
                                UnityEngine.Debug.LogWarning($"{LocalizationManager.GetString("main_game_item_failed")}: {e.Message}");
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"{LocalizationManager.GetString("scan_main_game_failed")}: {e}");
                }

                // 方法2: 通过反射从 dynamicDic 扫描MOD物品
                try
                {
                    UnityEngine.Debug.Log(LocalizationManager.GetString("scanning_mod_items"));

                    var dynamicDicField = typeof(ItemAssetsCollection).GetField("dynamicDic",
                        BindingFlags.NonPublic | BindingFlags.Static);

                    if (dynamicDicField != null)
                    {
                        var dynamicDic = dynamicDicField.GetValue(null) as Dictionary<int, ItemAssetsCollection.DynamicEntry>;

                        if (dynamicDic != null && dynamicDic.Count > 0)
                        {
                            UnityEngine.Debug.Log(LocalizationManager.GetString("found_mod_items", dynamicDic.Count));

                            foreach (var kvp in dynamicDic)
                            {
                                try
                                {
                                    int modItemTypeId = kvp.Key;
                                    ItemAssetsCollection.DynamicEntry modItemEntry = kvp.Value;

                                    if (modItemEntry != null && modItemEntry.prefab != null)
                                    {
                                        if (addedIds.Contains(modItemTypeId))
                                            continue;

                                        addedIds.Add(modItemTypeId);

                                        ItemInfo info = new ItemInfo
                                        {
                                            id = modItemTypeId,
                                            name = "[MOD] " + (modItemEntry.prefab.DisplayName ?? $"MOD物品{modItemTypeId}"),
                                            description = modItemEntry.prefab.Description ?? "",
                                            value = modItemEntry.prefab.Value,
                                            maxStack = modItemEntry.prefab.MaxStackCount,
                                            weight = modItemEntry.prefab.UnitSelfWeight,
                                            isMod = true
                                        };

                                        allItems.Add(info);
                                        modItemCount++;
                                    }
                                }
                                catch (Exception e)
                                {
                                    UnityEngine.Debug.LogWarning($"{LocalizationManager.GetString("mod_item_failed")} (ID:{kvp.Key}): {e.Message}");
                                }
                            }
                        }
                        else
                        {
                            UnityEngine.Debug.Log(LocalizationManager.GetString("no_mod_items"));
                        }
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning(LocalizationManager.GetString("reflection_failed"));
                    }
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"{LocalizationManager.GetString("scan_mod_failed")}: {e}");
                }

                // 方法3: 备用方法 - 使用 Resources.FindObjectsOfTypeAll (可能找不到所有物品)
                if (allItems.Count == 0)
                {
                    UnityEngine.Debug.Log(LocalizationManager.GetString("backup_scan"));

                    Item[] allItemComponents = Resources.FindObjectsOfTypeAll<Item>();
                    UnityEngine.Debug.Log(LocalizationManager.GetString("found_components", allItemComponents.Length));

                    foreach (Item item in allItemComponents)
                    {
                        try
                        {
                            // 跳过场景实例
                            if (item.gameObject.scene.name != null)
                                continue;

                            int typeId = item.TypeID;

                            if (addedIds.Contains(typeId))
                                continue;

                            addedIds.Add(typeId);

                            ItemInfo info = new ItemInfo
                            {
                                id = typeId,
                                name = item.DisplayName ?? $"物品{typeId}",
                                description = item.Description ?? "",
                                value = item.Value,
                                maxStack = item.MaxStackCount,
                                weight = item.UnitSelfWeight,
                                isMod = false
                            };

                            allItems.Add(info);
                        }
                        catch (Exception e)
                        {
                            UnityEngine.Debug.LogWarning($"{LocalizationManager.GetString("backup_item_failed")}: {e.Message}");
                        }
                    }
                }

                // 排序
                allItems = allItems.OrderBy(i => i.id).ToList();

                // 保存到缓存
                SaveItemsToCache();

                UnityEngine.Debug.Log("=================================");
                UnityEngine.Debug.Log(LocalizationManager.GetString("scan_complete"));
                UnityEngine.Debug.Log(LocalizationManager.GetString("main_game_count", mainGameCount));
                UnityEngine.Debug.Log(LocalizationManager.GetString("mod_count", modItemCount));
                UnityEngine.Debug.Log(LocalizationManager.GetString("total_count", allItems.Count));
                UnityEngine.Debug.Log("=================================");

                itemsLoaded = true;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{LocalizationManager.GetString("scan_failed")}: {e}");
            }
            finally
            {
                isScanning = false;
            }
        }

        // 保存到缓存文件
        private void SaveItemsToCache()
        {
            try
            {
                var cache = new ItemCache
                {
                    Items = allItems,
                    CacheTime = DateTime.Now
                };

                string json = JsonConvert.SerializeObject(cache, Formatting.Indented);
                File.WriteAllText(ModBehaviour.CacheFilePath, json);
                cacheTime = cache.CacheTime;

                UnityEngine.Debug.Log(LocalizationManager.GetString("cache_saved", allItems.Count));
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{LocalizationManager.GetString("save_cache_failed")}: {e.Message}");
            }
        }

        // ============ HELPER: Style Methods ============
        private GUIStyle CreateLabelStyle(int fontSize, Color color)
        {
            return new GUIStyle(GUI.skin.label) { fontSize = fontSize, normal = { textColor = color } };
        }

        private GUIStyle CreateButtonStyle(int fontSize)
        {
            return new GUIStyle(GUI.skin.button) { fontSize = fontSize };
        }

        private GUIStyle CreateBoxStyle(int fontSize, Color color)
        {
            return new GUIStyle(GUI.skin.box) { fontSize = fontSize, normal = { textColor = color } };
        }

        private GUIStyle CreateTextFieldStyle(int fontSize)
        {
            return new GUIStyle(GUI.skin.textField) { fontSize = fontSize };
        }

        private GUIStyle CreateWindowTitleStyle(Color titleColor)
        {
            var style = new GUIStyle(GUI.skin.window);
            style.normal.textColor = titleColor;
            return style;
        }

        private Texture2D CreateColorTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        // ============ UI Optimization Methods ============
        private float CalculateTextWidth(string text, int fontSize)
        {
            var style = CreateLabelStyle(fontSize, Color.white);
            return style.CalcSize(new GUIContent(text)).x;
        }

        private float GetDynamicLabelWidth(string text, int fontSize, float minWidth = 50f, float maxWidth = 200f)
        {
            float textWidth = CalculateTextWidth(text, fontSize);
            return Mathf.Clamp(textWidth + 10f, minWidth, maxWidth); // Add 10px padding
        }

        private GUIStyle CreateFlexibleLabelStyle(int fontSize, Color color, bool wordWrap = false)
        {
            var style = new GUIStyle(GUI.skin.label) 
            { 
                fontSize = fontSize, 
                normal = { textColor = color },
                wordWrap = wordWrap,
                clipping = TextClipping.Overflow
            };
            return style;
        }

        private void DrawSectionHeader(string title)
        {
            var style = CreateBoxStyle(FONT_SIZE_HEADER, colorHeader);
            GUILayout.Label($">> {title}", style, GUILayout.Height(SECTION_HEADER_HEIGHT));
            GUILayout.Space(5);
        }

        private void DrawHorizontalSeparator(float height = 2)
        {
            var rect = GUILayoutUtility.GetRect(baseWindowWidth, height);
            GUI.Box(rect, "", CreateBoxStyle(FONT_SIZE_SMALL, colorMuted));
            GUILayout.Space(3);
        }

        private void OnGUI()
        {
            if (!showWindow) return;

            // 处理点击外部关闭下拉菜单
            if (showLanguageDropdown && Event.current.type == EventType.MouseDown)
            {
                showLanguageDropdown = false;
            }

            // 应用UI缩放
            Matrix4x4 originalMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1));

            // Create custom window style with colored title
            var windowStyle = CreateWindowTitleStyle(colorWindowTitle);
            Rect scaledRect = GUILayout.Window(123456, windowRect, DrawWindow, $"{LocalizationManager.GetString("menu_title")} [{uiScale:F1}x]", windowStyle);
            windowRect = scaledRect;

            GUI.matrix = originalMatrix;
        }

        private void DrawWindow(int windowID)
        {
            // Apply background texture
            if (backgroundTexture != null)
            {
                GUI.DrawTexture(new Rect(0, 0, windowRect.width, windowRect.height), backgroundTexture);
            }

            GUILayout.BeginVertical();

            // Modern Tab Navigation
            DrawTabNavigation();
            DrawHorizontalSeparator();

            // Content Area
            switch (currentTab)
            {
                case 0:
                    DrawItemSpawnTab();
                    break;
                case 1:
                    DrawPlayerCheatTab();
                    break;
                case 2:
                    DrawSettingsTab();
                    break;
            }

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, baseWindowWidth, 20));
        }

        private void DrawTabNavigation()
        {
            GUILayout.BeginHorizontal(GUILayout.Height(TAB_BUTTON_HEIGHT));

            for (int i = 0; i < tabs.Length; i++)
            {
                var isActive = currentTab == i;
                var style = new GUIStyle(GUI.skin.button)
                {
                    fontSize = FONT_SIZE_LARGE,
                    normal = { textColor = isActive ? colorSuccess : Color.white }
                };

                if (GUILayout.Button(tabs[i], style, GUILayout.ExpandWidth(true), GUILayout.Height(TAB_BUTTON_HEIGHT)))
                {
                    currentTab = i;
                }
            }

            GUILayout.EndHorizontal();
        }

        private void DrawItemSpawnTab()
        {
            DrawSectionHeader(LocalizationManager.GetString("quick_spawn"));

            // Quick Spawn Section
            GUILayout.BeginHorizontal(GUILayout.Height(STANDARD_BUTTON_HEIGHT));
            
            // Dynamic width for Item ID label
            string itemIdLabel = LocalizationManager.GetString("item_id") + ":";
            float itemIdLabelWidth = GetDynamicLabelWidth(itemIdLabel, FONT_SIZE_MEDIUM, 60f, 120f);
            GUILayout.Label(itemIdLabel, CreateFlexibleLabelStyle(FONT_SIZE_MEDIUM, Color.white), GUILayout.Width(itemIdLabelWidth));
            
            // Item ID input field
            itemIdInput = GUILayout.TextField(itemIdInput, CreateTextFieldStyle(FONT_SIZE_MEDIUM), GUILayout.Height(STANDARD_INPUT_HEIGHT), GUILayout.Width(STANDARD_BUTTON_WIDTH));
            
            // Dynamic width for Count label
            string countLabel = LocalizationManager.GetString("count") + ":";
            float countLabelWidth = GetDynamicLabelWidth(countLabel, FONT_SIZE_MEDIUM, 40f, 80f);
            GUILayout.Label(countLabel, CreateFlexibleLabelStyle(FONT_SIZE_MEDIUM, Color.white), GUILayout.Width(countLabelWidth));
            
            // Count input field
            itemCountInput = GUILayout.TextField(itemCountInput, CreateTextFieldStyle(FONT_SIZE_MEDIUM), GUILayout.Height(STANDARD_INPUT_HEIGHT), GUILayout.Width(STANDARD_BUTTON_WIDTH));
            
            // Dynamic width for Spawn button
            string spawnButtonText = LocalizationManager.GetString("spawn");
            float spawnButtonWidth = GetDynamicLabelWidth(spawnButtonText, FONT_SIZE_MEDIUM, 60f, 120f);
            if (GUILayout.Button(spawnButtonText, CreateButtonStyle(FONT_SIZE_MEDIUM), GUILayout.Width(spawnButtonWidth), GUILayout.Height(STANDARD_BUTTON_HEIGHT)))
            {
                SpawnItemById();
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            DrawSectionHeader(LocalizationManager.GetString("item_search"));

            // Search Section
            GUILayout.BeginHorizontal(GUILayout.Height(STANDARD_BUTTON_HEIGHT));
            
            // Dynamic width for Search label
            string searchLabel = LocalizationManager.GetString("search") + ":";
            float searchLabelWidth = GetDynamicLabelWidth(searchLabel, FONT_SIZE_MEDIUM, 40f, 100f);
            GUILayout.Label(searchLabel, CreateFlexibleLabelStyle(FONT_SIZE_MEDIUM, Color.white), GUILayout.Width(searchLabelWidth));
            
            // Search input field (flexible width)
            string newSearch = GUILayout.TextField(searchText, CreateTextFieldStyle(FONT_SIZE_MEDIUM), GUILayout.Height(STANDARD_INPUT_HEIGHT), GUILayout.ExpandWidth(true));
            if (newSearch != searchText)
            {
                searchText = newSearch;
                currentPage = 0;
                PerformSearch();
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(8);

            // Status & Results
            if (isScanning)
            {
                GUILayout.Label(LocalizationManager.GetString("scanning_status"), CreateBoxStyle(FONT_SIZE_MEDIUM, colorWarning), GUILayout.Height(STATUS_MESSAGE_HEIGHT));
            }
            else if (!itemsLoaded)
            {
                GUILayout.Label(LocalizationManager.GetString("not_loaded_status"), CreateBoxStyle(FONT_SIZE_MEDIUM, colorError), GUILayout.Height(STATUS_MESSAGE_HEIGHT));
            }
            else
            {
                // Pagination Info - Use flexible layout to prevent wrapping
                GUILayout.BeginHorizontal();
                
                // Found items status - flexible width
                string foundItemsText = LocalizationManager.GetString("found_items_status", searchResults.Count);
                var foundItemsStyle = CreateBoxStyle(FONT_SIZE_MEDIUM, colorSuccess);
                foundItemsStyle.wordWrap = false;
                foundItemsStyle.clipping = TextClipping.Overflow;
                GUILayout.Label(foundItemsText, foundItemsStyle, GUILayout.Height(STATUS_MESSAGE_HEIGHT), GUILayout.ExpandWidth(true));
                
                // Page info - fixed width to prevent wrapping
                string pageInfoText = LocalizationManager.GetString("page_info", currentPage + 1, totalPages);
                float pageInfoWidth = GetDynamicLabelWidth(pageInfoText, FONT_SIZE_MEDIUM, 80f, 150f);
                GUILayout.Label(pageInfoText, CreateFlexibleLabelStyle(FONT_SIZE_MEDIUM, colorHeader), GUILayout.Height(STATUS_MESSAGE_HEIGHT), GUILayout.Width(pageInfoWidth));
                
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(8);
            // Pagination Controls
            if (itemsLoaded && searchResults.Count > 0)
            {
                GUILayout.Space(5);
            }
            // Items List (Paginated)
            if (itemsLoaded)
            {
                itemScrollPosition = GUILayout.BeginScrollView(itemScrollPosition, GUILayout.ExpandHeight(true));

                // Calculate range for current page
                int startIndex = currentPage * itemsPerPage;
                int endIndex = Mathf.Min(startIndex + itemsPerPage, searchResults.Count);

                for (int i = startIndex; i < endIndex; i++)
                {
                    DrawItemCard(searchResults[i]);
                }

                GUILayout.EndScrollView();
            }
            // Bottom Pagination Controls
            if (itemsLoaded && searchResults.Count > 0)
            {
                GUILayout.Space(5);
                DrawPaginationControls();
            }
        }

        private void DrawItemCard(ItemInfo item)
        {
            GUILayout.BeginVertical(GUI.skin.box);

            // Item Header
            GUILayout.BeginHorizontal();
            GUILayout.Label($"ID: {item.id}", CreateLabelStyle(FONT_SIZE_SMALL, colorHeader), GUILayout.Width(70));
            GUILayout.Label(item.name, CreateLabelStyle(FONT_SIZE_SMALL, Color.white));
            GUILayout.EndHorizontal();

            // Description
            if (!string.IsNullOrEmpty(item.description))
            {
                var descStyle = CreateLabelStyle(FONT_SIZE_SMALL, colorMuted);
                descStyle.wordWrap = true;
                float maxWidth = baseWindowWidth - 30;
                GUILayout.Label(item.description, descStyle, GUILayout.MaxWidth(maxWidth / uiScale));
            }

            // Properties & Action Buttons - Split into two rows to prevent wrapping
            GUILayout.BeginVertical();
            
            // Properties row
            GUILayout.BeginHorizontal(GUILayout.Height(SMALL_BUTTON_HEIGHT));
            string propertiesText = $"{LocalizationManager.GetString("value")}: {item.value} | {LocalizationManager.GetString("weight")}: {item.weight:F1}kg | {LocalizationManager.GetString("stack")}: {item.maxStack}";
            var propertiesStyle = CreateFlexibleLabelStyle(FONT_SIZE_SMALL, colorMuted);
            propertiesStyle.wordWrap = false;
            propertiesStyle.clipping = TextClipping.Overflow;
            GUILayout.Label(propertiesText, propertiesStyle, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            
            // Action buttons row
            GUILayout.BeginHorizontal(GUILayout.Height(SMALL_BUTTON_HEIGHT));
            var btnStyle = CreateButtonStyle(FONT_SIZE_SMALL);
            if (GUILayout.Button("x1", btnStyle, GUILayout.Width(SMALL_BUTTON_WIDTH), GUILayout.Height(SMALL_BUTTON_HEIGHT)))
                SpawnItem(item.id, 1);
            if (GUILayout.Button("x10", btnStyle, GUILayout.Width(SMALL_BUTTON_WIDTH), GUILayout.Height(SMALL_BUTTON_HEIGHT)))
                SpawnItem(item.id, 10);
            if (GUILayout.Button("x99", btnStyle, GUILayout.Width(SMALL_BUTTON_WIDTH), GUILayout.Height(SMALL_BUTTON_HEIGHT)))
                SpawnItem(item.id, 99);
            if (GUILayout.Button($"x{item.maxStack}", btnStyle, GUILayout.Width(SMALL_BUTTON_WIDTH), GUILayout.Height(SMALL_BUTTON_HEIGHT)))
                SpawnItem(item.id, item.maxStack);
            GUILayout.EndHorizontal();
            
            GUILayout.EndVertical();

            GUILayout.EndVertical();
            GUILayout.Space(5);
        }

        private void DrawPlayerCheatTab()
        {
            DrawSectionHeader(LocalizationManager.GetString("player_abilities"));

            // Invincibility Button
            var invincibleStyle = CreateButtonStyle(FONT_SIZE_LARGE);
            if (GUILayout.Button(LocalizationManager.GetString("invincible_mode"), invincibleStyle, GUILayout.Height(STANDARD_BUTTON_HEIGHT)))
            {
                ToggleInvincible();
            }

            GUILayout.Space(10);

            // Teleport Toggle
            DrawSectionHeader(LocalizationManager.GetString("teleport_function"));

            string teleportButtonText = teleportEnabled
                ? "✓ " + LocalizationManager.GetString("teleport_enabled")
                : "✗ " + LocalizationManager.GetString("teleport_disabled");

            var teleportStyle = CreateButtonStyle(FONT_SIZE_LARGE);
            if (teleportEnabled)
                teleportStyle.normal.textColor = colorSuccess;
            else
                teleportStyle.normal.textColor = colorError;

            if (GUILayout.Button($"{LocalizationManager.GetString("teleport_to_cursor")}  [{teleportButtonText}]", teleportStyle, GUILayout.Height(STANDARD_BUTTON_HEIGHT)))
            {
                teleportEnabled = !teleportEnabled;
                UnityEngine.Debug.Log(LocalizationManager.GetString("teleport_toggled", teleportEnabled ? LocalizationManager.GetString("teleport_enabled_text") : LocalizationManager.GetString("teleport_disabled_text")));
            }

            if (teleportEnabled)
            {
                GUILayout.Space(8);
                var tipsStyle = CreateBoxStyle(FONT_SIZE_SMALL, colorWarning);
                tipsStyle.wordWrap = true;
                GUILayout.Label(LocalizationManager.GetString("teleport_tip"), tipsStyle, GUILayout.Height(STANDARD_BUTTON_HEIGHT));
            }

            GUILayout.Space(15);

            // Important Notice
            DrawSectionHeader(LocalizationManager.GetString("important_notice"));
            var warningStyle = CreateBoxStyle(FONT_SIZE_SMALL, colorWarning);
            warningStyle.wordWrap = true;
            GUILayout.Label(LocalizationManager.GetString("scene_warning"), warningStyle, GUILayout.Height(500));
        }

        private void DrawSettingsTab()
        {
            GUILayout.BeginVertical(GUILayout.ExpandHeight(true));

            // Statistics Section
            DrawSectionHeader(LocalizationManager.GetString("statistics"));
            var mainGameCount = allItems.Count(i => !i.isMod);
            var modCount = allItems.Count(i => i.isMod);

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(LocalizationManager.GetString("loaded_items") + $": {allItems.Count} " + LocalizationManager.GetString("items"), CreateLabelStyle(FONT_SIZE_MEDIUM, colorSuccess));
            GUILayout.Label(LocalizationManager.GetString("game_items") + $": {mainGameCount} " + LocalizationManager.GetString("items"), CreateLabelStyle(FONT_SIZE_SMALL, colorHeader));
            GUILayout.Label(LocalizationManager.GetString("mod_items") + $": {modCount} " + LocalizationManager.GetString("items"), CreateLabelStyle(FONT_SIZE_SMALL, colorHeader));
            GUILayout.Label(LocalizationManager.GetString("cache_time") + $": {(cacheTime != DateTime.MinValue ? cacheTime.ToString("yyyy-MM-dd HH:mm:ss") : LocalizationManager.GetString("not_cached"))}", CreateLabelStyle(FONT_SIZE_SMALL, colorMuted));
            GUILayout.Label($"{LocalizationManager.GetString("fps")}: {(int)(1f / Time.deltaTime)}", CreateLabelStyle(FONT_SIZE_SMALL, Color.yellow));
            GUILayout.EndVertical();

            GUILayout.Space(12);

            // UI Scale Section
            DrawSectionHeader(LocalizationManager.GetString("ui_scale"));

            GUILayout.BeginHorizontal(GUILayout.Height(STANDARD_BUTTON_HEIGHT));
            
            // Dynamic width for current scale label
            string currentScaleText = $"{LocalizationManager.GetString("current_scale")}: {uiScale:F2}×";
            float currentScaleWidth = GetDynamicLabelWidth(currentScaleText, FONT_SIZE_MEDIUM, 60f, 120f);
            GUILayout.Label(currentScaleText, CreateFlexibleLabelStyle(FONT_SIZE_MEDIUM, colorSuccess), GUILayout.Width(currentScaleWidth));

            var btnStyle = CreateButtonStyle(FONT_SIZE_SMALL);
            if (GUILayout.Button("100%", btnStyle, GUILayout.Height(STANDARD_BUTTON_HEIGHT)))
            {
                uiScale = 1.0f;
                ApplyScale();
                SaveConfig();
            }
            if (GUILayout.Button("125%", btnStyle, GUILayout.Height(STANDARD_BUTTON_HEIGHT)))
            {
                uiScale = 1.25f;
                ApplyScale();
                SaveConfig();
            }
            if (GUILayout.Button("150%", btnStyle, GUILayout.Height(STANDARD_BUTTON_HEIGHT)))
            {
                uiScale = 1.5f;
                ApplyScale();
                SaveConfig();
            }
            if (GUILayout.Button("200%", btnStyle, GUILayout.Height(STANDARD_BUTTON_HEIGHT)))
            {
                uiScale = 2.0f;
                ApplyScale();
                SaveConfig();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(12);

            // Language Selection Section
            DrawSectionHeader(LocalizationManager.GetString("language"));
            
            GUILayout.BeginHorizontal(GUILayout.Height(STANDARD_BUTTON_HEIGHT));
            
            // Dynamic width for current language label
            string currentLangLabel = $"{LocalizationManager.GetString("current")}:";
            float currentLangWidth = GetDynamicLabelWidth(currentLangLabel, FONT_SIZE_MEDIUM, 60f, 100f);
            GUILayout.Label(currentLangLabel, CreateFlexibleLabelStyle(FONT_SIZE_MEDIUM, colorSuccess), GUILayout.Width(currentLangWidth));
            
            // Language Dropdown Button
            string currentDisplayName = selectedLanguageIndex < languageDisplayNames.Length ? 
                languageDisplayNames[selectedLanguageIndex] : "Unknown";
            
            if (GUILayout.Button($"{currentDisplayName} ▼", CreateButtonStyle(FONT_SIZE_SMALL), GUILayout.Height(STANDARD_BUTTON_HEIGHT), GUILayout.Width(240)))
            {
                showLanguageDropdown = !showLanguageDropdown;
            }
            
            // Reset to System Language Button
            string systemLang = LocalizationManager.DetectSystemLanguage();
            string currentLang = LocalizationManager.GetCurrentLanguage();
            bool isSystemLanguage = systemLang == currentLang;
            
            var resetButtonStyle = CreateButtonStyle(FONT_SIZE_SMALL);
            if (isSystemLanguage)
            {
                resetButtonStyle.normal.textColor = colorMuted;
            }
            
            if (GUILayout.Button(LocalizationManager.GetStringInSystemLanguage("reset_to_system_language"), resetButtonStyle, GUILayout.Width(300), GUILayout.Height(STANDARD_BUTTON_HEIGHT)))
            {
                if (!isSystemLanguage)
                {
                    LocalizationManager.SetLanguage(systemLang);
                    UpdateLocalizedStrings();
                    SaveConfig();
                    showLanguageDropdown = false;
                    UnityEngine.Debug.Log(LocalizationManager.GetStringInSystemLanguage("language_reset_to_system", systemLang));
                }
                else
                {
                    UnityEngine.Debug.Log(LocalizationManager.GetString("already_using_system_language"));
                }
            }
            
            GUILayout.EndHorizontal();
            
            // Language Dropdown List
            if (showLanguageDropdown)
            {
                // 创建下拉列表背景
                var dropdownStyle = new GUIStyle(GUI.skin.box);
                dropdownStyle.normal.background = CreateColorTexture(new Color(0.1f, 0.1f, 0.1f, 0.95f));
                
                GUILayout.BeginVertical(dropdownStyle);
                
                for (int i = 0; i < availableLanguages.Length; i++)
                {
                    bool isSelected = i == selectedLanguageIndex;
                    var itemStyle = new GUIStyle(GUI.skin.button);
                    itemStyle.fontSize = FONT_SIZE_SMALL;
                    itemStyle.padding = new RectOffset(10, 10, 5, 5);
                    
                    if (isSelected)
                    {
                        itemStyle.normal.textColor = colorSuccess;
                        itemStyle.normal.background = CreateColorTexture(colorSuccess * 0.2f);
                        itemStyle.hover.textColor = colorSuccess;
                        itemStyle.hover.background = CreateColorTexture(colorSuccess * 0.3f);
                    }
                    else
                    {
                        itemStyle.normal.textColor = Color.white;
                        itemStyle.normal.background = CreateColorTexture(Color.clear);
                        itemStyle.hover.textColor = colorHeader;
                        itemStyle.hover.background = CreateColorTexture(colorHeader * 0.1f);
                    }
                    
                    if (GUILayout.Button(languageDisplayNames[i], itemStyle, GUILayout.Height(STANDARD_BUTTON_HEIGHT)))
                    {
                        selectedLanguageIndex = i;
                        LocalizationManager.SetLanguage(availableLanguages[selectedLanguageIndex]);
                        UpdateLocalizedStrings();
                        SaveConfig();
                        showLanguageDropdown = false;
                        UnityEngine.Debug.Log($"[语言] 已切换到: {languageDisplayNames[selectedLanguageIndex]}");
                    }
                }
                
                GUILayout.EndVertical();
            }

            GUILayout.Space(12);

            // System Language Info Section (Debug)
            DrawSectionHeader(LocalizationManager.GetString("system_language_info"));
            
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(LocalizationManager.GetSystemLanguageInfo(), CreateLabelStyle(FONT_SIZE_SMALL, colorMuted));
            GUILayout.Label($"{LocalizationManager.GetString("current_language")}: {LocalizationManager.GetCurrentLanguage()}", CreateLabelStyle(FONT_SIZE_SMALL, colorSuccess));
            GUILayout.Label($"{LocalizationManager.GetString("supported_languages")}: {string.Join(", ", LocalizationManager.GetAvailableLanguages())}", CreateLabelStyle(FONT_SIZE_SMALL, colorHeader));
            GUILayout.Label($"{LocalizationManager.GetString("cache_file")}: {ModBehaviour.CacheFilePath}", CreateLabelStyle(FONT_SIZE_SMALL, colorMuted));
            GUILayout.Label($"{LocalizationManager.GetString("config_file")}: {ModBehaviour.ConfigFilePath}", CreateLabelStyle(FONT_SIZE_SMALL, colorMuted));
            GUILayout.EndVertical();

            GUILayout.Space(12);

            // Cache Management Section
            DrawSectionHeader(LocalizationManager.GetString("cache_management"));

            if (GUILayout.Button(LocalizationManager.GetString("rescan_items"), btnStyle, GUILayout.Height(STANDARD_BUTTON_HEIGHT)))
            {
                ScanAndCacheItems();
            }

            GUILayout.Space(5);

            if (GUILayout.Button(LocalizationManager.GetString("delete_cache"), btnStyle, GUILayout.Height(STANDARD_BUTTON_HEIGHT)))
            {
                try
                {
                    if (File.Exists(ModBehaviour.CacheFilePath))
                    {
                        File.Delete(ModBehaviour.CacheFilePath);
                        UnityEngine.Debug.Log(LocalizationManager.GetString("cache_deleted"));
                        itemsLoaded = false;
                        allItems.Clear();
                        searchResults.Clear();
                    }
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError(LocalizationManager.GetString("delete_failed") + ": " + e.Message);
                }
            }

            GUILayout.Space(5);

            if (GUILayout.Button(LocalizationManager.GetString("open_cache_folder"), btnStyle, GUILayout.Height(STANDARD_BUTTON_HEIGHT)))
            {
                try
                {
                    System.Diagnostics.Process.Start(Path.GetDirectoryName(ModBehaviour.CacheFilePath));
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError(LocalizationManager.GetString("open_failed") + ": " + e.Message);
                }
            }

            GUILayout.Space(12);

            // Debug Section
            DrawSectionHeader(LocalizationManager.GetString("debug"));

            if (GUILayout.Button(LocalizationManager.GetString("output_item_list"), btnStyle, GUILayout.Height(STANDARD_BUTTON_HEIGHT)))
            {
                UnityEngine.Debug.Log(LocalizationManager.GetString("item_list_debug"));
                foreach (var item in allItems.Take(20))
                {
                    UnityEngine.Debug.Log(LocalizationManager.GetString("item_format", item.id, item.name, item.isMod));
                }
            }

            GUILayout.Space(5);

            if (GUILayout.Button(LocalizationManager.GetString("output_mod_items"), btnStyle, GUILayout.Height(STANDARD_BUTTON_HEIGHT)))
            {
                var modItems = allItems.Where(i => i.isMod).ToList();
                UnityEngine.Debug.Log(LocalizationManager.GetString("mod_items_debug", modItems.Count));
                foreach (var item in modItems)
                {
                    UnityEngine.Debug.Log(LocalizationManager.GetString("mod_item_format", item.id, item.name));
                }
            }

            GUILayout.EndVertical();
        }
        // ✨ NEW: Pagination Controls
        private void DrawPaginationControls()
        {
            GUILayout.BeginHorizontal(GUILayout.Height(STANDARD_BUTTON_HEIGHT));

            var btnStyle = CreateButtonStyle(FONT_SIZE_MEDIUM);
            var disabledStyle = CreateButtonStyle(FONT_SIZE_MEDIUM);
            disabledStyle.normal.textColor = colorMuted;

            // First Page Button
            GUI.enabled = currentPage > 0;
            if (GUILayout.Button("<<", currentPage > 0 ? btnStyle : disabledStyle,
                GUILayout.Width(SMALL_BUTTON_WIDTH), GUILayout.Height(STANDARD_BUTTON_HEIGHT)))
            {
                currentPage = 0;
                itemScrollPosition = Vector2.zero;
            }

            // Previous Page Button
            if (GUILayout.Button("<", currentPage > 0 ? btnStyle : disabledStyle,
                GUILayout.Width(SMALL_BUTTON_WIDTH), GUILayout.Height(STANDARD_BUTTON_HEIGHT)))
            {
                currentPage--;
                itemScrollPosition = Vector2.zero;
            }
            GUI.enabled = true;

            GUILayout.FlexibleSpace();

            // Items Per Page Selector - Dynamic width
            string perPageLabel = LocalizationManager.GetString("per_page") + ":";
            float perPageLabelWidth = GetDynamicLabelWidth(perPageLabel, FONT_SIZE_SMALL, 40f, 80f);
            GUILayout.Label(perPageLabel, CreateFlexibleLabelStyle(FONT_SIZE_SMALL, Color.white), GUILayout.Width(perPageLabelWidth));

            var smallBtnStyle = CreateButtonStyle(FONT_SIZE_SMALL);
            if (GUILayout.Button("10", itemsPerPage == 10 ? CreateButtonStyle(FONT_SIZE_SMALL) : smallBtnStyle,
                GUILayout.Width(SMALL_BUTTON_WIDTH), GUILayout.Height(SMALL_BUTTON_HEIGHT)))
            {
                itemsPerPage = 10;
                currentPage = 0;
            }
            if (GUILayout.Button("20", itemsPerPage == 20 ? CreateButtonStyle(FONT_SIZE_SMALL) : smallBtnStyle,
                GUILayout.Width(SMALL_BUTTON_WIDTH), GUILayout.Height(SMALL_BUTTON_HEIGHT)))
            {
                itemsPerPage = 20;
                currentPage = 0;
            }
            if (GUILayout.Button("50", itemsPerPage == 50 ? CreateButtonStyle(FONT_SIZE_SMALL) : smallBtnStyle,
                GUILayout.Width(SMALL_BUTTON_WIDTH), GUILayout.Height(SMALL_BUTTON_HEIGHT)))
            {
                itemsPerPage = 50;
                currentPage = 0;
            }

            GUILayout.FlexibleSpace();

            // Next Page Button
            GUI.enabled = currentPage < totalPages - 1;
            if (GUILayout.Button(">", currentPage < totalPages - 1 ? btnStyle : disabledStyle,
                GUILayout.Width(SMALL_BUTTON_WIDTH), GUILayout.Height(SMALL_BUTTON_HEIGHT)))
            {
                currentPage++;
                itemScrollPosition = Vector2.zero;
            }

            // Last Page Button
            if (GUILayout.Button(">>", currentPage < totalPages - 1 ? btnStyle : disabledStyle,
                GUILayout.Width(SMALL_BUTTON_WIDTH), GUILayout.Height(SMALL_BUTTON_HEIGHT)))
            {
                currentPage = totalPages - 1;
                itemScrollPosition = Vector2.zero;
            }
            GUI.enabled = true;

            GUILayout.EndHorizontal();
        }
        private void PerformSearch()
        {
            searchResults.Clear();
            currentPage = 0;

            if (string.IsNullOrWhiteSpace(searchText))
            {
                searchResults = allItems.ToList();
            }
            else
            {
                string lowerSearch = searchText.ToLower();

                // 先按ID搜索
                if (int.TryParse(searchText, out int searchId))
                {
                    searchResults = allItems.Where(i => i.id == searchId).ToList();
                }

                // 如果ID搜索没结果，按名称搜索
                if (searchResults.Count == 0)
                {
                    searchResults = allItems
                        .Where(i => i.name.ToLower().Contains(lowerSearch) ||
                                    i.description.ToLower().Contains(lowerSearch))
                        .ToList();
                }
            }

        }

        private void SpawnItemById()
        {
            try
            {
                if (int.TryParse(itemIdInput, out int itemId) &&
                    int.TryParse(itemCountInput, out int count))
                {
                    SpawnItem(itemId, count);
                }
                else
                {
                    UnityEngine.Debug.LogWarning(LocalizationManager.GetString("invalid_number"));
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{LocalizationManager.GetString("spawn_error")}: {e.Message}");
            }
        }

        private void SpawnItem(int itemId, int count)
        {
            try
            {
                if (CheatingManager.Instance != null)
                {
                    CheatingManager.Instance.CreateItem(itemId, count);

                    var item = allItems.FirstOrDefault(i => i.id == itemId);
                    string name = item != null ? item.name : $"ID:{itemId}";

                    UnityEngine.Debug.Log(LocalizationManager.GetString("spawn_success", name, count));
                }
                else
                {
                    UnityEngine.Debug.LogWarning(LocalizationManager.GetString("cheat_manager_not_ready"));
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{LocalizationManager.GetString("spawn_failed")}: {e.Message}");
            }
        }

        private void ToggleInvincible()
        {
            try
            {
                if (CheatingManager.Instance != null)
                {
                    CheatingManager.Instance.ToggleInvincible();
                    UnityEngine.Debug.Log(LocalizationManager.GetString("invincible_toggled"));
                }
                else
                {
                    UnityEngine.Debug.LogWarning(LocalizationManager.GetString("cheat_manager_not_ready"));
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{LocalizationManager.GetString("spawn_error")}: {e.Message}");
            }
        }

        private void CheatMove()
        {
            try
            {
                if (CheatingManager.Instance != null)
                {
                    CheatingManager.Instance.CheatMove();
                    UnityEngine.Debug.Log(LocalizationManager.GetString("teleport_success"));
                }
                else
                {
                    UnityEngine.Debug.LogWarning(LocalizationManager.GetString("cheat_manager_not_ready"));
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"{LocalizationManager.GetString("spawn_error")}: {e.Message}");
            }
        }
    }

    // 物品信息
    [Serializable]
    public class ItemInfo
    {
        public int id;
        public string name;
        public string description;
        public int value;
        public int maxStack;
        public float weight;
        public bool isMod;
    }

    // 缓存结构
    [Serializable]
    public class ItemCache
    {
        public List<ItemInfo> Items;
        public DateTime CacheTime;
    }

    // 配置结构 ✨ NEW
    [Serializable]
    public class GUIConfig
    {
        public float UIScale = 1.0f;
        public string Language = "zh-CN";
    }

    // 本地化系统
    public static class LocalizationManager
    {
        private static Dictionary<string, string> currentLanguage = new Dictionary<string, string>();
        private static Dictionary<string, Dictionary<string, string>> allLanguages = new Dictionary<string, Dictionary<string, string>>();
        private static string currentLangCode = "zh-CN";
        private static string localizationFilePath;

        public static void Initialize()
        {
            // 使用DLL文件所在目录
            string dllDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            localizationFilePath = Path.Combine(dllDirectory, "Localization.json");
            UnityEngine.Debug.Log($"[本地化] 本地化文件路径: {localizationFilePath}");
            LoadLanguages();
            
            // 检测系统语言并设置为默认语言
            string systemLanguage = DetectSystemLanguage();
            if (allLanguages.ContainsKey(systemLanguage))
            {
                currentLangCode = systemLanguage;
                UnityEngine.Debug.Log($"[本地化] 检测到系统语言: {systemLanguage}");
            }
            else
            {
                UnityEngine.Debug.Log($"[本地化] 系统语言 {systemLanguage} 不支持，使用默认语言: {currentLangCode}");
            }
            
            SetLanguage(currentLangCode);
        }

        public static void LoadLanguages()
        {
            try
            {
                if (File.Exists(localizationFilePath))
                {
                    string json = File.ReadAllText(localizationFilePath);
                    allLanguages = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(json);
                }
                else
                {
                    CreateDefaultLanguages();
                    SaveLanguages();
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"加载本地化文件失败: {e.Message}");
                CreateDefaultLanguages();
            }
        }

        public static void CreateDefaultLanguages()
        {
            allLanguages = new Dictionary<string, Dictionary<string, string>>
            {
                ["zh-CN"] = new Dictionary<string, string>
                {
                    // 通用
                    ["ok"] = "确定",
                    ["cancel"] = "取消",
                    ["close"] = "关闭",
                    ["save"] = "保存",
                    ["load"] = "加载",
                    ["search"] = "搜索",
                    ["scan"] = "扫描",
                    ["delete"] = "删除",
                    ["open"] = "打开",
                    ["refresh"] = "刷新",
                    ["settings"] = "设置",
                    ["language"] = "语言",
                    ["scale"] = "缩放",
                    ["page"] = "页",
                    ["items"] = "物品",
                    ["count"] = "数量",
                    ["value"] = "价值",
                    ["weight"] = "重量",
                    ["stack"] = "堆叠",
                    ["description"] = "描述",
                    ["status"] = "状态",
                    ["success"] = "成功",
                    ["error"] = "错误",
                    ["warning"] = "警告",
                    ["info"] = "信息",
                    ["debug"] = "调试",
                    ["statistics"] = "统计",
                    ["cache"] = "缓存",
                    ["management"] = "管理",
                    ["folder"] = "文件夹",
                    ["file"] = "文件",
                    ["mod"] = "MOD",
                    ["game"] = "游戏",
                    ["total"] = "总计",
                    ["current"] = "当前",
                    ["previous"] = "上一页",
                    ["next"] = "下一页",
                    ["first"] = "首页",
                    ["last"] = "末页",
                    ["per_page"] = "每页",
                    ["found"] = "找到",
                    ["loading"] = "加载中",
                    ["scanning"] = "扫描中",
                    ["not_loaded"] = "未加载",
                    ["please_scan"] = "请进入设置页面扫描物品",
                    ["generating"] = "生成",
                    ["generated"] = "已生成",
                    ["spawn"] = "生成",
                    ["spawn_item"] = "生成物品",
                    ["item_id"] = "物品ID",
                    ["item_count"] = "物品数量",
                    ["quick_spawn"] = "快速生成",
                    ["item_search"] = "物品搜索",
                    ["player_abilities"] = "玩家能力",
                    ["invincible_mode"] = "无敌模式",
                    ["teleport_function"] = "传送功能",
                    ["teleport_to_cursor"] = "传送到光标位置",
                    ["teleport_enabled"] = "已开启",
                    ["teleport_disabled"] = "已关闭",
                    ["teleport_tip"] = "按鼠标中键（滚轮）传送到光标指向的位置",
                    ["important_notice"] = "重要提示",
                    ["scene_warning"] = "某些功能需要在游戏场景中才能使用。请确保您在游戏中。",
                    ["ui_scale"] = "界面缩放",
                    ["current_scale"] = "当前",
                    ["cache_management"] = "缓存管理",
                    ["rescan_items"] = "重新扫描物品",
                    ["delete_cache"] = "删除缓存",
                    ["open_cache_folder"] = "打开缓存文件夹",
                    ["output_item_list"] = "输出物品列表",
                    ["output_mod_items"] = "输出MOD物品",
                    ["loaded_items"] = "已加载物品",
                    ["game_items"] = "游戏物品",
                    ["mod_items"] = "MOD物品",
                    ["cache_time"] = "缓存时间",
                    ["not_cached"] = "未缓存",
                    ["fps"] = "FPS",
                    ["menu_title"] = "Duckov Cheat Menu",
                    ["tab_items"] = "[物品]",
                    ["tab_cheats"] = "[作弊]",
                    ["tab_settings"] = "[设置]",
                    ["mod_loaded"] = "CheatGUI Mod v0.3.0 已加载！",
                    ["cache_file"] = "缓存文件",
                    ["config_file"] = "配置文件",
                    ["mod_initialized"] = "[成功] Mod 初始化完成",
                    ["press_home"] = "进入游戏后按 Home 键打开菜单",
                    ["init_failed"] = "初始化失败",
                    ["gui_created"] = "[>>] 创建GUI GameObject...",
                    ["gui_success"] = "[成功] GUI 创建成功！",
                    ["gui_failed"] = "创建GUI失败",
                    ["menu_opened"] = "[菜单] 菜单打开",
                    ["menu_closed"] = "[菜单] 菜单关闭",
                    ["mod_unloaded"] = "CheatGUI Mod 已卸载",
                    ["loading_from_cache"] = "[加载] 从缓存加载物品列表...",
                    ["loaded_from_cache"] = "[成功] 从缓存加载 {0} 个物品",
                    ["cache_time_format"] = "缓存时间: {0:yyyy-MM-dd HH:mm:ss}",
                    ["cache_not_found"] = "[警告] 缓存文件不存在，需要扫描物品",
                    ["cache_load_failed"] = "加载缓存失败",
                    ["scanning_items"] = "[扫描] 开始扫描游戏物品...",
                    ["main_game_items"] = "[主游戏] 找到 {0} 个物品条目",
                    ["main_game_item_failed"] = "读取主游戏物品失败",
                    ["scan_main_game_failed"] = "扫描主游戏物品失败",
                    ["scanning_mod_items"] = "[MOD] 开始扫描MOD物品...",
                    ["found_mod_items"] = "[MOD] 找到 {0} 个MOD物品条目",
                    ["mod_item_failed"] = "读取MOD物品失败",
                    ["no_mod_items"] = "[MOD] 未找到MOD物品或dynamicDic为空",
                    ["reflection_failed"] = "[MOD] 无法通过反射获取 ItemAssetsCollection.dynamicDic 字段",
                    ["scan_mod_failed"] = "扫描MOD物品失败",
                    ["backup_scan"] = "[备用] 使用Resources.FindObjectsOfTypeAll扫描...",
                    ["found_components"] = "[备用] 找到 {0} 个 Item 组件",
                    ["backup_item_failed"] = "备用方法读取物品失败",
                    ["scan_complete"] = "[成功] 扫描完成！",
                    ["main_game_count"] = "主游戏物品: {0} 个",
                    ["mod_count"] = "MOD物品: {0} 个",
                    ["total_count"] = "总计: {0} 个物品",
                    ["scan_failed"] = "扫描失败",
                    ["cache_saved"] = "[成功] 缓存已保存: {0} 个物品",
                    ["save_cache_failed"] = "保存缓存失败",
                    ["config_loaded"] = "[配置] 已加载 UI缩放: {0:F1}x",
                    ["load_config_failed"] = "加载配置失败",
                    ["config_saved"] = "[配置] 已保存 UI缩放: {0:F1}x",
                    ["save_config_failed"] = "保存配置失败",
                    ["invalid_number"] = "[警告] 请输入有效数字",
                    ["spawn_error"] = "错误",
                    ["spawn_success"] = "[成功] 生成 {0} x{1}",
                    ["cheat_manager_not_ready"] = "[警告] CheatingManager 未就绪",
                    ["spawn_failed"] = "生成错误",
                    ["invincible_toggled"] = "[成功] 无敌模式切换",
                    ["teleport_success"] = "[成功] 传送成功",
                    ["teleport_toggled"] = "[成功] 传送功能 {0}",
                    ["teleport_enabled_text"] = "已开启",
                    ["teleport_disabled_text"] = "已关闭",
                    ["cache_deleted"] = "[成功] 缓存文件已删除",
                    ["delete_failed"] = "删除失败",
                    ["open_failed"] = "打开失败",
                    ["item_list_debug"] = "=== 物品列表（前20个）===",
                    ["mod_items_debug"] = "=== MOD物品列表（共{0}个）===",
                    ["item_format"] = "ID:{0} | {1} | MOD:{2}",
                    ["mod_item_format"] = "ID:{0} | {1}",
                    ["scanning_status"] = "* 正在扫描物品...",
                    ["not_loaded_status"] = "! 未加载 - 请进入设置页面扫描物品",
                    ["found_items_status"] = "[OK] 找到 {0} 个物品",
                    ["page_info"] = "第 {0}/{1} 页"
                },
                ["en-US"] = new Dictionary<string, string>
                {
                    // Common
                    ["ok"] = "OK",
                    ["cancel"] = "Cancel",
                    ["close"] = "Close",
                    ["save"] = "Save",
                    ["load"] = "Load",
                    ["search"] = "Search",
                    ["scan"] = "Scan",
                    ["delete"] = "Delete",
                    ["open"] = "Open",
                    ["refresh"] = "Refresh",
                    ["settings"] = "Settings",
                    ["language"] = "Language",
                    ["scale"] = "Scale",
                    ["page"] = "Page",
                    ["items"] = "Items",
                    ["count"] = "Count",
                    ["value"] = "Value",
                    ["weight"] = "Weight",
                    ["stack"] = "Stack",
                    ["description"] = "Description",
                    ["status"] = "Status",
                    ["success"] = "Success",
                    ["error"] = "Error",
                    ["warning"] = "Warning",
                    ["info"] = "Info",
                    ["debug"] = "Debug",
                    ["statistics"] = "Statistics",
                    ["cache"] = "Cache",
                    ["management"] = "Management",
                    ["folder"] = "Folder",
                    ["file"] = "File",
                    ["mod"] = "MOD",
                    ["game"] = "Game",
                    ["total"] = "Total",
                    ["current"] = "Current",
                    ["previous"] = "Previous",
                    ["next"] = "Next",
                    ["first"] = "First",
                    ["last"] = "Last",
                    ["per_page"] = "Per Page",
                    ["found"] = "Found",
                    ["loading"] = "Loading",
                    ["scanning"] = "Scanning",
                    ["not_loaded"] = "Not Loaded",
                    ["please_scan"] = "Please go to Settings page to scan items",
                    ["generating"] = "Generating",
                    ["generated"] = "Generated",
                    ["spawn"] = "Spawn",
                    ["spawn_item"] = "Spawn Item",
                    ["item_id"] = "Item ID",
                    ["item_count"] = "Item Count",
                    ["quick_spawn"] = "Quick Spawn",
                    ["item_search"] = "Item Search",
                    ["player_abilities"] = "Player Abilities",
                    ["invincible_mode"] = "Invincible Mode",
                    ["teleport_function"] = "Teleport Function",
                    ["teleport_to_cursor"] = "Teleport to Cursor Position",
                    ["teleport_enabled"] = "Enabled",
                    ["teleport_disabled"] = "Disabled",
                    ["teleport_tip"] = "Press middle mouse button to teleport to cursor position",
                    ["important_notice"] = "Important Notice",
                    ["scene_warning"] = "Some features require you to be in the game scene. Please make sure you are in the game.",
                    ["ui_scale"] = "UI Scale",
                    ["current_scale"] = "Current",
                    ["cache_management"] = "Cache Management",
                    ["rescan_items"] = "Rescan Items",
                    ["delete_cache"] = "Delete Cache",
                    ["open_cache_folder"] = "Open Cache Folder",
                    ["output_item_list"] = "Output Item List",
                    ["output_mod_items"] = "Output MOD Items",
                    ["loaded_items"] = "Loaded Items",
                    ["game_items"] = "Game Items",
                    ["mod_items"] = "MOD Items",
                    ["cache_time"] = "Cache Time",
                    ["not_cached"] = "Not Cached",
                    ["fps"] = "FPS",
                    ["menu_title"] = "Duckov Cheat Menu",
                    ["tab_items"] = "[Items]",
                    ["tab_cheats"] = "[Cheats]",
                    ["tab_settings"] = "[Settings]",
                    ["mod_loaded"] = "CheatGUI Mod v0.3.0 Loaded!",
                    ["cache_file"] = "Cache File",
                    ["config_file"] = "Config File",
                    ["mod_initialized"] = "[Success] Mod initialization complete",
                    ["press_home"] = "Press Home key in game to open menu",
                    ["init_failed"] = "Initialization failed",
                    ["gui_created"] = "[>>] Creating GUI GameObject...",
                    ["gui_success"] = "[Success] GUI created successfully!",
                    ["gui_failed"] = "Failed to create GUI",
                    ["menu_opened"] = "[Menu] Menu opened",
                    ["menu_closed"] = "[Menu] Menu closed",
                    ["mod_unloaded"] = "CheatGUI Mod unloaded",
                    ["loading_from_cache"] = "[Loading] Loading item list from cache...",
                    ["loaded_from_cache"] = "[Success] Loaded {0} items from cache",
                    ["cache_time_format"] = "Cache time: {0:yyyy-MM-dd HH:mm:ss}",
                    ["cache_not_found"] = "[Warning] Cache file not found, need to scan items",
                    ["cache_load_failed"] = "Failed to load cache",
                    ["scanning_items"] = "[Scanning] Starting to scan game items...",
                    ["main_game_items"] = "[Main Game] Found {0} item entries",
                    ["main_game_item_failed"] = "Failed to read main game item",
                    ["scan_main_game_failed"] = "Failed to scan main game items",
                    ["scanning_mod_items"] = "[MOD] Starting to scan MOD items...",
                    ["found_mod_items"] = "[MOD] Found {0} MOD item entries",
                    ["mod_item_failed"] = "Failed to read MOD item",
                    ["no_mod_items"] = "[MOD] No MOD items found or dynamicDic is empty",
                    ["reflection_failed"] = "[MOD] Unable to get ItemAssetsCollection.dynamicDic field through reflection",
                    ["scan_mod_failed"] = "Failed to scan MOD items",
                    ["backup_scan"] = "[Backup] Using Resources.FindObjectsOfTypeAll to scan...",
                    ["found_components"] = "[Backup] Found {0} Item components",
                    ["backup_item_failed"] = "Backup method failed to read item",
                    ["scan_complete"] = "[Success] Scan complete!",
                    ["main_game_count"] = "Main game items: {0}",
                    ["mod_count"] = "MOD items: {0}",
                    ["total_count"] = "Total: {0} items",
                    ["scan_failed"] = "Scan failed",
                    ["cache_saved"] = "[Success] Cache saved: {0} items",
                    ["save_cache_failed"] = "Failed to save cache",
                    ["config_loaded"] = "[Config] Loaded UI scale: {0:F1}x",
                    ["load_config_failed"] = "Failed to load config",
                    ["config_saved"] = "[Config] Saved UI scale: {0:F1}x",
                    ["save_config_failed"] = "Failed to save config",
                    ["invalid_number"] = "[Warning] Please enter valid numbers",
                    ["spawn_error"] = "Error",
                    ["spawn_success"] = "[Success] Spawned {0} x{1}",
                    ["cheat_manager_not_ready"] = "[Warning] CheatingManager not ready",
                    ["spawn_failed"] = "Spawn error",
                    ["invincible_toggled"] = "[Success] Invincible mode toggled",
                    ["teleport_success"] = "[Success] Teleport successful",
                    ["teleport_toggled"] = "[Success] Teleport function {0}",
                    ["teleport_enabled_text"] = "enabled",
                    ["teleport_disabled_text"] = "disabled",
                    ["cache_deleted"] = "[Success] Cache file deleted",
                    ["delete_failed"] = "Delete failed",
                    ["open_failed"] = "Open failed",
                    ["item_list_debug"] = "=== Item List (First 20) ===",
                    ["mod_items_debug"] = "=== MOD Items List (Total {0}) ===",
                    ["item_format"] = "ID:{0} | {1} | MOD:{2}",
                    ["mod_item_format"] = "ID:{0} | {1}",
                    ["scanning_status"] = "* Scanning items...",
                    ["not_loaded_status"] = "! Not loaded - Please go to Settings page to scan items",
                    ["found_items_status"] = "[OK] Found {0} items",
                    ["page_info"] = "Page {0}/{1}"
                }
            };
        }

        public static void SaveLanguages()
        {
            try
            {
                string json = JsonConvert.SerializeObject(allLanguages, Formatting.Indented);
                File.WriteAllText(localizationFilePath, json);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"保存本地化文件失败: {e.Message}");
            }
        }

        public static void SetLanguage(string langCode)
        {
            if (allLanguages.ContainsKey(langCode))
            {
                currentLangCode = langCode;
                currentLanguage = allLanguages[langCode];
                UnityEngine.Debug.Log($"[本地化] 语言已切换为: {langCode}");
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[本地化] 不支持的语言代码: {langCode}");
            }
        }

        public static string GetString(string key, params object[] args)
        {
            if (currentLanguage.ContainsKey(key))
            {
                string value = currentLanguage[key];
                if (args.Length > 0)
                {
                    return string.Format(value, args);
                }
                return value;
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[本地化] 缺少翻译键: {key}");
                return key;
            }
        }

        public static string[] GetAvailableLanguages()
        {
            return allLanguages.Keys.ToArray();
        }

        public static string GetCurrentLanguage()
        {
            return currentLangCode;
        }

        // 检测系统语言
        public static string DetectSystemLanguage()
        {
            try
            {
                // 获取系统文化信息
                CultureInfo systemCulture = CultureInfo.CurrentCulture;
                string languageCode = systemCulture.Name; // 例如: "zh-CN", "en-US", "ja-JP"
                
                UnityEngine.Debug.Log($"[系统语言检测] 完整语言代码: {languageCode}");
                UnityEngine.Debug.Log($"[系统语言检测] 语言名称: {systemCulture.DisplayName}");
                UnityEngine.Debug.Log($"[系统语言检测] 英文名称: {systemCulture.EnglishName}");
                UnityEngine.Debug.Log($"[系统语言检测] 可用语言: {string.Join(", ", allLanguages.Keys)}");
                
                // 首先尝试完整语言代码 (例如: "zh-CN")
                if (allLanguages.ContainsKey(languageCode))
                {
                    UnityEngine.Debug.Log($"[系统语言检测] 找到完全匹配: {languageCode}");
                    return languageCode;
                }
                
                // 如果完整代码不支持，尝试只使用语言部分 (例如: "zh" -> "zh-CN")
                string languageOnly = systemCulture.TwoLetterISOLanguageName;
                UnityEngine.Debug.Log($"[系统语言检测] 语言代码: {languageOnly}");
                
                // 动态查找支持的语言
                string detectedLanguage = FindBestLanguageMatch(languageCode, languageOnly);
                if (detectedLanguage != null)
                {
                    UnityEngine.Debug.Log($"[系统语言检测] 找到最佳匹配: {detectedLanguage}");
                    return detectedLanguage;
                }
                
                // 如果都没找到，返回第一个可用的语言作为默认
                var availableLanguages = allLanguages.Keys.ToArray();
                if (availableLanguages.Length > 0)
                {
                    UnityEngine.Debug.Log($"[系统语言检测] 未找到匹配语言，使用第一个可用语言: {availableLanguages[0]}");
                    return availableLanguages[0];
                }
                
                UnityEngine.Debug.Log("[系统语言检测] 没有可用语言，使用默认");
                return "zh-CN"; // 最后的默认值
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[系统语言检测] 检测失败: {e.Message}");
                return "zh-CN"; // 出错时返回默认语言
            }
        }

        // 动态查找最佳语言匹配
        private static string FindBestLanguageMatch(string fullLanguageCode, string languageOnly)
        {
            // 1. 尝试查找包含相同语言代码的所有语言
            var candidates = allLanguages.Keys.Where(lang => 
                lang.StartsWith(languageOnly + "-", StringComparison.OrdinalIgnoreCase) ||
                lang.Equals(languageOnly, StringComparison.OrdinalIgnoreCase)
            ).ToArray();
            
            if (candidates.Length > 0)
            {
                UnityEngine.Debug.Log($"[语言匹配] 找到候选语言: {string.Join(", ", candidates)}");
                
                // 2. 优先选择完全匹配的语言代码
                var exactMatch = candidates.FirstOrDefault(c => 
                    c.Equals(fullLanguageCode, StringComparison.OrdinalIgnoreCase));
                if (exactMatch != null)
                {
                    return exactMatch;
                }
                
                // 3. 选择第一个候选语言
                return candidates[0];
            }
            
            // 4. 尝试模糊匹配（处理一些特殊情况）
            return FindFuzzyLanguageMatch(fullLanguageCode, languageOnly);
        }

        // 模糊语言匹配
        private static string FindFuzzyLanguageMatch(string fullLanguageCode, string languageOnly)
        {
            // 处理一些常见的语言映射
            var languageMappings = new Dictionary<string, string[]>();
            languageMappings["zh"] = new string[] { "zh-CN", "zh-TW", "zh-HK" };
            languageMappings["en"] = new string[] { "en-US", "en-GB", "en-CA", "en-AU" };
            languageMappings["ja"] = new string[] { "ja-JP" };
            languageMappings["ko"] = new string[] { "ko-KR" };
            languageMappings["fr"] = new string[] { "fr-FR", "fr-CA" };
            languageMappings["de"] = new string[] { "de-DE", "de-AT", "de-CH" };
            languageMappings["es"] = new string[] { "es-ES", "es-MX", "es-AR" };
            languageMappings["ru"] = new string[] { "ru-RU" };
            languageMappings["it"] = new string[] { "it-IT" };
            languageMappings["pt"] = new string[] { "pt-BR", "pt-PT" };
            
            if (languageMappings.ContainsKey(languageOnly.ToLower()))
            {
                var possibleLanguages = languageMappings[languageOnly.ToLower()];
                foreach (var possibleLang in possibleLanguages)
                {
                    if (allLanguages.ContainsKey(possibleLang))
                    {
                        UnityEngine.Debug.Log($"[模糊匹配] 找到语言: {possibleLang}");
                        return possibleLang;
                    }
                }
            }
            
            return null;
        }

        // 获取系统语言信息（用于调试）
        public static string GetSystemLanguageInfo()
        {
            try
            {
                CultureInfo systemCulture = CultureInfo.CurrentCulture;
                return $"{GetString("system_language")}: {systemCulture.DisplayName} ({systemCulture.Name})";
            }
            catch (Exception e)
            {
                return $"{GetString("get_system_language_failed")}: {e.Message}";
            }
        }

        // 获取系统语言下的本地化文本
        public static string GetStringInSystemLanguage(string key, params object[] args)
        {
            try
            {
                string systemLanguage = DetectSystemLanguage();
                if (allLanguages.ContainsKey(systemLanguage))
                {
                    var systemLanguageDict = allLanguages[systemLanguage];
                    if (systemLanguageDict.ContainsKey(key))
                    {
                        string value = systemLanguageDict[key];
                        if (args.Length > 0)
                        {
                            return string.Format(value, args);
                        }
                        return value;
                    }
                }
                
                // 如果系统语言中没有找到，回退到当前语言
                return GetString(key, args);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[系统语言本地化] 获取失败: {e.Message}");
                return GetString(key, args);
            }
        }
    }
}