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

namespace DuckovCheatGUI
{
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        public static GUIRenderer Renderer;
        public static string CacheFilePath;
        public static string ConfigFilePath;
        private Harmony harmony;

        private void OnEnable()
        {
            UnityEngine.Debug.Log("=================================");
            UnityEngine.Debug.Log("CheatGUI Mod v0.3.0 已加载！");
            
            // 设置缓存文件路径（使用mod目录）
            CacheFilePath = Path.Combine(Application.dataPath, "..", "DuckovCheatGUI", "ItemCache.json");
            ConfigFilePath = Path.Combine(Application.dataPath, "..", "DuckovCheatGUI", "Config.json");
            
            // 确保目录存在
            string directory = Path.GetDirectoryName(CacheFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            UnityEngine.Debug.Log($"缓存文件: {CacheFilePath}");
            UnityEngine.Debug.Log($"配置文件: {ConfigFilePath}");
            UnityEngine.Debug.Log("=================================");

            try
            {
                this.harmony = new Harmony("com.dandan.duckov.cheatgui");
                this.harmony.PatchAll(Assembly.GetExecutingAssembly());
                
                UnityEngine.Debug.Log("[成功] Harmony 补丁已应用");
                UnityEngine.Debug.Log("进入游戏后按 Home 键打开菜单");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"初始化失败: {e}");
            }
        }

        private void OnDisable()
        {
            this.harmony.UnpatchAll("com.dandan.duckov.cheatgui");
            UnityEngine.Debug.Log("CheatGUI Mod 已卸载");
        }
    }

    [HarmonyPatch(typeof(CheatingManager), "Update")]
    public static class CheatingManager_Update_Patch
    {
        private static bool initialized = false;
        private static GameObject guiObject;
        
        static void Postfix()
        {
            if (!initialized)
            {
                try
                {
                    UnityEngine.Debug.Log("[>>] 创建GUI GameObject...");
                    
                    guiObject = new GameObject("CheatGUI_Renderer");
                    UnityEngine.Object.DontDestroyOnLoad(guiObject);
                    ModBehaviour.Renderer = guiObject.AddComponent<GUIRenderer>();
                    
                    UnityEngine.Debug.Log("[成功] GUI 创建成功！");
                    initialized = true;
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"创建GUI失败: {e}");
                }
            }
            
            if (Input.GetKeyDown(KeyCode.Home) || Input.GetKeyDown(KeyCode.Backslash))
            {
                ModBehaviour.Renderer?.ToggleWindow();
            }

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
        private readonly string[] tabs = { "[物品]", "[作弊]", "[设置]" };

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

        private void Update()
        {
            // 检测鼠标中键按下，且传送功能已启用
            if (teleportEnabled && Input.GetKeyDown(KeyCode.Mouse2))
            {
                CheatMove();
            }
        }

        public void ToggleWindow()
        {
            showWindow = !showWindow;

            if (showWindow)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

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

                UnityEngine.Debug.Log("[菜单] 菜单打开");
            }
            else
            {
                UnityEngine.Debug.Log("[菜单] 菜单关闭");
            }
        }

        // 初始化背景纹理 ✨ NEW
        private void InitializeBackground()
        {
            backgroundTexture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            backgroundTexture.SetPixel(0, 0, new Color(0.15f, 0.15f, 0.15f, 1f)); // 深灰色，完全不透明
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
                    
                    UnityEngine.Debug.Log($"[配置] 已加载 UI缩放: {uiScale:F1}x");
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"加载配置失败: {e.Message}");
            }
        }

        // 保存配置 ✨ NEW
        private void SaveConfig()
        {
            try
            {
                var config = new GUIConfig
                {
                    UIScale = uiScale
                };
                
                string json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(ModBehaviour.ConfigFilePath, json);
                
                UnityEngine.Debug.Log($"[配置] 已保存 UI缩放: {uiScale:F1}x");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"保存配置失败: {e.Message}");
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
                    UnityEngine.Debug.Log("[加载] 从缓存加载物品列表...");
                    
                    string json = File.ReadAllText(ModBehaviour.CacheFilePath);
                    var cache = JsonConvert.DeserializeObject<ItemCache>(json);
                    
                    allItems = cache.Items;
                    cacheTime = cache.CacheTime;
                    
                    UnityEngine.Debug.Log($"[成功] 从缓存加载 {allItems.Count} 个物品");
                    UnityEngine.Debug.Log($"缓存时间: {cacheTime:yyyy-MM-dd HH:mm:ss}");
                    itemsLoaded = true;
                }
                else
                {
                    UnityEngine.Debug.Log("[警告] 缓存文件不存在，需要扫描物品");
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"加载缓存失败: {e.Message}");
            }
        }

        // 扫描并保存到缓存（增强版 - 支持MOD物品）
        private void ScanAndCacheItems()
        {
            isScanning = true;
            allItems.Clear();
            
            try
            {
                UnityEngine.Debug.Log("[扫描] 开始扫描游戏物品...");
                
                HashSet<int> addedIds = new HashSet<int>();
                int mainGameCount = 0;
                int modItemCount = 0;
                
                // 方法1: 从 ItemAssetsCollection 扫描主游戏物品
                try
                {
                    if (ItemAssetsCollection.Instance != null)
                    {
                        var allItemEntries = ItemAssetsCollection.Instance.entries;
                        UnityEngine.Debug.Log($"[主游戏] 找到 {allItemEntries.Count} 个物品条目");
                        
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
                                UnityEngine.Debug.LogWarning($"读取主游戏物品失败: {e.Message}");
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"扫描主游戏物品失败: {e}");
                }
                
                // 方法2: 通过反射从 dynamicDic 扫描MOD物品
                try
                {
                    UnityEngine.Debug.Log("[MOD] 开始扫描MOD物品...");
                    
                    var dynamicDicField = typeof(ItemAssetsCollection).GetField("dynamicDic", 
                        BindingFlags.NonPublic | BindingFlags.Static);
                    
                    if (dynamicDicField != null)
                    {
                        var dynamicDic = dynamicDicField.GetValue(null) as Dictionary<int, ItemAssetsCollection.DynamicEntry>;
                        
                        if (dynamicDic != null && dynamicDic.Count > 0)
                        {
                            UnityEngine.Debug.Log($"[MOD] 找到 {dynamicDic.Count} 个MOD物品条目");
                            
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
                                    UnityEngine.Debug.LogWarning($"读取MOD物品失败 (ID:{kvp.Key}): {e.Message}");
                                }
                            }
                        }
                        else
                        {
                            UnityEngine.Debug.Log("[MOD] 未找到MOD物品或dynamicDic为空");
                        }
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning("[MOD] 无法通过反射获取 ItemAssetsCollection.dynamicDic 字段");
                    }
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"扫描MOD物品失败: {e}");
                }
                
                // 方法3: 备用方法 - 使用 Resources.FindObjectsOfTypeAll (可能找不到所有物品)
                if (allItems.Count == 0)
                {
                    UnityEngine.Debug.Log("[备用] 使用Resources.FindObjectsOfTypeAll扫描...");
                    
                    Item[] allItemComponents = Resources.FindObjectsOfTypeAll<Item>();
                    UnityEngine.Debug.Log($"[备用] 找到 {allItemComponents.Length} 个 Item 组件");
                    
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
                            UnityEngine.Debug.LogWarning($"备用方法读取物品失败: {e.Message}");
                        }
                    }
                }
                
                // 排序
                allItems = allItems.OrderBy(i => i.id).ToList();
                
                // 保存到缓存
                SaveItemsToCache();
                
                UnityEngine.Debug.Log("=================================");
                UnityEngine.Debug.Log($"[成功] 扫描完成！");
                UnityEngine.Debug.Log($"主游戏物品: {mainGameCount} 个");
                UnityEngine.Debug.Log($"MOD物品: {modItemCount} 个");
                UnityEngine.Debug.Log($"总计: {allItems.Count} 个物品");
                UnityEngine.Debug.Log("=================================");
                
                itemsLoaded = true;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"扫描失败: {e}");
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
                
                UnityEngine.Debug.Log($"[成功] 缓存已保存: {allItems.Count} 个物品");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"保存缓存失败: {e.Message}");
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

        private void DrawSectionHeader(string title)
        {
            var style = CreateBoxStyle(17, colorHeader);
            GUILayout.Label($">> {title}", style, GUILayout.Height(35));
            GUILayout.Space(5);
        }

        private void DrawHorizontalSeparator(float height = 2)
        {
            var rect = GUILayoutUtility.GetRect(baseWindowWidth, height);
            GUI.Box(rect, "", CreateBoxStyle(0, colorMuted));
            GUILayout.Space(3);
        }

        private void OnGUI()
        {
            if (!showWindow) return;

            // 应用UI缩放
            Matrix4x4 originalMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1));

            Rect scaledRect = GUILayout.Window(123456, windowRect, DrawWindow, $"Duckov Cheat Menu [{uiScale:F1}x]");
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
            GUILayout.BeginHorizontal(GUILayout.Height(50));

            for (int i = 0; i < tabs.Length; i++)
            {
                var isActive = currentTab == i;
                var style = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 15,
                    normal = { textColor = isActive ? colorSuccess : Color.white }
                };

                if (GUILayout.Button(tabs[i], style, GUILayout.ExpandWidth(true), GUILayout.Height(50)))
                {
                    currentTab = i;
                }
            }

            GUILayout.EndHorizontal();
        }

        private void DrawItemSpawnTab()
        {
            DrawSectionHeader("快速生成");

            // Quick Spawn Section
            GUILayout.BeginHorizontal(GUILayout.Height(40));
            GUILayout.Label("物品ID:", CreateLabelStyle(14, Color.white), GUILayout.Width(70));
            itemIdInput = GUILayout.TextField(itemIdInput, GUILayout.Height(35), GUILayout.Width(80));
            GUILayout.Label("数量:", CreateLabelStyle(14, Color.white), GUILayout.Width(50));
            itemCountInput = GUILayout.TextField(itemCountInput, GUILayout.Height(35), GUILayout.Width(70));
            if (GUILayout.Button("生成", CreateButtonStyle(14), GUILayout.Width(90), GUILayout.Height(40)))
            {
                SpawnItemById();
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            DrawSectionHeader("物品搜索");

            // Search Section
            GUILayout.BeginHorizontal(GUILayout.Height(40));
            GUILayout.Label("搜索:", CreateLabelStyle(14, Color.white), GUILayout.Width(50));
            string newSearch = GUILayout.TextField(searchText, GUILayout.Height(35));
            if (newSearch != searchText)
            {
                searchText = newSearch;
                PerformSearch();
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(8);

            // Status & Results
            if (isScanning)
            {
                GUILayout.Label("* 正在扫描物品...", CreateBoxStyle(14, colorWarning), GUILayout.Height(30));
            }
            else if (!itemsLoaded)
            {
                GUILayout.Label("! 未加载 - 请进入设置页面扫描物品", CreateBoxStyle(14, colorError), GUILayout.Height(30));
            }
            else
            {
                GUILayout.Label($"[OK] 找到 {searchResults.Count} 个物品", CreateBoxStyle(14, colorSuccess), GUILayout.Height(30));
            }

            GUILayout.Space(8);

            // Items List
            if (itemsLoaded)
            {
                itemScrollPosition = GUILayout.BeginScrollView(itemScrollPosition, GUILayout.ExpandHeight(true));

                foreach (var item in searchResults)
                {
                    DrawItemCard(item);
                }

                GUILayout.EndScrollView();
            }
        }

        private void DrawItemCard(ItemInfo item)
        {
            GUILayout.BeginVertical(GUI.skin.box);

            // Item Header
            GUILayout.BeginHorizontal();
            GUILayout.Label($"ID: {item.id}", CreateLabelStyle(13, colorHeader), GUILayout.Width(70));
            GUILayout.Label(item.name, CreateLabelStyle(13, Color.white));
            GUILayout.EndHorizontal();

            // Description
            if (!string.IsNullOrEmpty(item.description))
            {
                var descStyle = CreateLabelStyle(12, colorMuted);
                descStyle.wordWrap = true;
                float maxWidth = baseWindowWidth - 30;
                GUILayout.Label(item.description, descStyle, GUILayout.MaxWidth(maxWidth / uiScale));
            }

            // Properties & Action Buttons
            GUILayout.BeginHorizontal(GUILayout.Height(35));
            GUILayout.Label($"Value: {item.value} | Weight: {item.weight:F1}kg | Stack: {item.maxStack}",
                CreateLabelStyle(12, colorMuted), GUILayout.ExpandWidth(false));

            var btnStyle = CreateButtonStyle(12);
            if (GUILayout.Button("x1", btnStyle, GUILayout.Width(45), GUILayout.Height(35)))
                SpawnItem(item.id, 1);
            if (GUILayout.Button("x10", btnStyle, GUILayout.Width(50), GUILayout.Height(35)))
                SpawnItem(item.id, 10);
            if (GUILayout.Button("x99", btnStyle, GUILayout.Width(50), GUILayout.Height(35)))
                SpawnItem(item.id, 99);
            if (GUILayout.Button($"x{item.maxStack}", btnStyle, GUILayout.Width(70), GUILayout.Height(35)))
                SpawnItem(item.id, item.maxStack);
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.Space(5);
        }

        private void DrawPlayerCheatTab()
        {
            DrawSectionHeader("玩家能力");

            // Invincibility Button
            var invincibleStyle = CreateButtonStyle(16);
            if (GUILayout.Button("⚡ 无敌模式", invincibleStyle, GUILayout.Height(50)))
            {
                ToggleInvincible();
            }

            GUILayout.Space(10);

            // Teleport Toggle
            DrawSectionHeader("传送功能");

            string teleportButtonText = teleportEnabled
                ? "✓ 已开启"
                : "✗ 已关闭";

            var teleportStyle = CreateButtonStyle(16);
            if (teleportEnabled)
                teleportStyle.normal.textColor = colorSuccess;
            else
                teleportStyle.normal.textColor = colorError;

            if (GUILayout.Button($"🚀 传送到光标位置  [{teleportButtonText}]", teleportStyle, GUILayout.Height(50)))
            {
                teleportEnabled = !teleportEnabled;
                UnityEngine.Debug.Log($"[成功] 传送功能 {(teleportEnabled ? "已开启" : "已关闭")}");
            }

            if (teleportEnabled)
            {
                GUILayout.Space(8);
                var tipsStyle = CreateBoxStyle(12, colorWarning);
                tipsStyle.wordWrap = true;
                GUILayout.Label("💡 按鼠标中键（滚轮）传送到光标指向的位置", tipsStyle, GUILayout.Height(40));
            }

            GUILayout.Space(15);

            // Important Notice
            DrawSectionHeader("重要提示");
            var warningStyle = CreateBoxStyle(13, colorWarning);
            warningStyle.wordWrap = true;
            GUILayout.Label("⚠️ 某些功能需要在游戏场景中才能使用。请确保您在游戏中。", warningStyle, GUILayout.Height(50));
        }

        private void DrawSettingsTab()
        {
            GUILayout.BeginVertical(GUILayout.ExpandHeight(true));

            // Statistics Section
            DrawSectionHeader("统计信息");
            var mainGameCount = allItems.Count(i => !i.isMod);
            var modCount = allItems.Count(i => i.isMod);

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"📦 已加载物品: {allItems.Count} 个", CreateLabelStyle(14, colorSuccess));
            GUILayout.Label($"🎮 游戏物品: {mainGameCount} 个", CreateLabelStyle(13, colorHeader));
            GUILayout.Label($"🔧 MOD物品: {modCount} 个", CreateLabelStyle(13, colorHeader));
            GUILayout.Label($"⏱️ 缓存时间: {(cacheTime != DateTime.MinValue ? cacheTime.ToString("yyyy-MM-dd HH:mm:ss") : "未缓存")}", CreateLabelStyle(13, colorMuted));
            GUILayout.Label($"🎯 FPS: {(int)(1f / Time.deltaTime)}", CreateLabelStyle(13, Color.yellow));
            GUILayout.EndVertical();

            GUILayout.Space(12);

            // UI Scale Section
            DrawSectionHeader("界面缩放");

            GUILayout.BeginHorizontal(GUILayout.Height(35));
            GUILayout.Label($"当前: {uiScale:F2}×", CreateLabelStyle(14, colorSuccess), GUILayout.Width(80));

            var btnStyle = CreateButtonStyle(13);
            if (GUILayout.Button("100%", btnStyle, GUILayout.Height(35)))
            {
                uiScale = 1.0f;
                ApplyScale();
                SaveConfig();
            }
            if (GUILayout.Button("125%", btnStyle, GUILayout.Height(35)))
            {
                uiScale = 1.25f;
                ApplyScale();
                SaveConfig();
            }
            if (GUILayout.Button("150%", btnStyle, GUILayout.Height(35)))
            {
                uiScale = 1.5f;
                ApplyScale();
                SaveConfig();
            }
            if (GUILayout.Button("200%", btnStyle, GUILayout.Height(35)))
            {
                uiScale = 2.0f;
                ApplyScale();
                SaveConfig();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(12);

            // Cache Management Section
            DrawSectionHeader("缓存管理");

            if (GUILayout.Button("🔄 重新扫描物品", btnStyle, GUILayout.Height(45)))
            {
                ScanAndCacheItems();
            }

            GUILayout.Space(5);

            if (GUILayout.Button("🗑️ 删除缓存", btnStyle, GUILayout.Height(40)))
            {
                try
                {
                    if (File.Exists(ModBehaviour.CacheFilePath))
                    {
                        File.Delete(ModBehaviour.CacheFilePath);
                        UnityEngine.Debug.Log("[成功] 缓存文件已删除");
                        itemsLoaded = false;
                        allItems.Clear();
                        searchResults.Clear();
                    }
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"删除失败: {e.Message}");
                }
            }

            GUILayout.Space(5);

            if (GUILayout.Button("📁 打开缓存文件夹", btnStyle, GUILayout.Height(40)))
            {
                try
                {
                    System.Diagnostics.Process.Start(Path.GetDirectoryName(ModBehaviour.CacheFilePath));
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"打开失败: {e.Message}");
                }
            }

            GUILayout.Space(12);

            // Debug Section
            DrawSectionHeader("调试");

            if (GUILayout.Button("📋 输出物品列表", btnStyle, GUILayout.Height(40)))
            {
                UnityEngine.Debug.Log("=== 物品列表（前20个）===");
                foreach (var item in allItems.Take(20))
                {
                    UnityEngine.Debug.Log($"ID:{item.id} | {item.name} | MOD:{item.isMod}");
                }
            }

            GUILayout.Space(5);

            if (GUILayout.Button("📦 输出MOD物品", btnStyle, GUILayout.Height(40)))
            {
                var modItems = allItems.Where(i => i.isMod).ToList();
                UnityEngine.Debug.Log($"=== MOD物品列表（共{modItems.Count}个）===");
                foreach (var item in modItems)
                {
                    UnityEngine.Debug.Log($"ID:{item.id} | {item.name}");
                }
            }

            GUILayout.EndVertical();
        }

        private void PerformSearch()
        {
            searchResults.Clear();
            
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
            
            // 限制结果数量
            if (searchResults.Count > 100)
            {
                searchResults = searchResults.Take(100).ToList();
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
                    UnityEngine.Debug.LogWarning("[警告] 请输入有效数字");
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"错误: {e.Message}");
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
                    
                    UnityEngine.Debug.Log($"[成功] 生成 {name} x{count}");
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[警告] CheatingManager 未就绪");
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"生成错误: {e.Message}");
            }
        }

        private void ToggleInvincible()
        {
            try
            {
                if (CheatingManager.Instance != null)
                {
                    CheatingManager.Instance.ToggleInvincible();
                    UnityEngine.Debug.Log("[成功] 无敌模式切换");
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[警告] CheatingManager 未就绪");
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"错误: {e.Message}");
            }
        }

        private void CheatMove()
        {
            try
            {
                if (CheatingManager.Instance != null)
                {
                    CheatingManager.Instance.CheatMove();
                    UnityEngine.Debug.Log("[成功] 传送成功");
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[警告] CheatingManager 未就绪");
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"错误: {e.Message}");
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
    }
}