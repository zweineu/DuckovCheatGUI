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
            
            if (Input.GetKeyDown(KeyCode.Home))
            {
                ModBehaviour.Renderer?.ToggleWindow();
            }

        }
    }

    public class GUIRenderer : MonoBehaviour
    {
        private bool showWindow = false;
        private Rect windowRect = new Rect(50, 50, 750, 700);
        
        private int currentTab = 0;
        private string[] tabs = { "物品生成", "玩家作弊", "设置" };
        
        // 物品相关
        private string searchText = "";
        private string itemIdInput = "";
        private string itemCountInput = "1";
        private Vector2 itemScrollPosition = Vector2.zero;
        private List<ItemInfo> allItems = new List<ItemInfo>();
        private List<ItemInfo> searchResults = new List<ItemInfo>();
        private bool itemsLoaded = false;
        private bool isScanning = false;
        private DateTime cacheTime = DateTime.MinValue;
        
        // 传送功能
        private bool teleportEnabled = false;
        
        // UI缩放相关 ✨ NEW
        private float uiScale = 1.0f;
        private const float MIN_SCALE = 0.5f;
        private const float MAX_SCALE = 2.0f;
        private const float SCALE_STEP = 0.1f;
        private float baseWindowWidth = 750f;
        private float baseWindowHeight = 700f;

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

        private void OnGUI()
        {
            if (!showWindow) return;
            
            // 应用UI缩放 ✨ NEW
            Matrix4x4 originalMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1));
            
            // 调整窗口矩形以适应缩放
            Rect scaledRect = new Rect(
                windowRect.x / uiScale,
                windowRect.y / uiScale,
                baseWindowWidth,
                baseWindowHeight
            );
            
            scaledRect = GUILayout.Window(123456, scaledRect, DrawWindow, $"作弊菜单 v0.3.0 [UI缩放: {uiScale:F1}x]");
            
            // 保存缩放后的位置
            windowRect.x = scaledRect.x * uiScale;
            windowRect.y = scaledRect.y * uiScale;
            
            // 恢复原始矩阵
            GUI.matrix = originalMatrix;
        }

        private void DrawWindow(int windowID)
        {
            GUILayout.BeginVertical();
            
            // 标签页
            GUILayout.BeginHorizontal();
            for (int i = 0; i < tabs.Length; i++)
            {
                if (GUILayout.Toggle(currentTab == i, tabs[i], GUI.skin.button, GUILayout.Height(35)))
                {
                    currentTab = i;
                }
            }
            GUILayout.EndHorizontal();
            
            GUILayout.Space(10);
            
            // 显示对应标签页
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

        private void DrawItemSpawnTab()
        {
            GUILayout.Label("=== 物品生成 ===", GUI.skin.box);
            
            GUILayout.Space(5);
            
            // 状态信息
            if (isScanning)
            {
                GUILayout.Label("[扫描中...] 正在扫描物品，请稍候", GUI.skin.box);
            }
            else if (!itemsLoaded)
            {
                GUILayout.Label("[未加载] 点击设置标签页扫描物品", GUI.skin.box);
            }
            
            GUILayout.Space(5);
            
            // ID直接生成
            GUILayout.BeginHorizontal();
            GUILayout.Label("物品ID:", GUILayout.Width(60));
            itemIdInput = GUILayout.TextField(itemIdInput, GUILayout.Width(100));
            GUILayout.Label("数量:", GUILayout.Width(50));
            itemCountInput = GUILayout.TextField(itemCountInput, GUILayout.Width(80));
            if (GUILayout.Button("生成", GUILayout.Height(30)))
            {
                SpawnItemById();
            }
            GUILayout.EndHorizontal();
            
            GUILayout.Space(10);
            
            // 搜索
            GUILayout.BeginHorizontal();
            GUILayout.Label("搜索:", GUILayout.Width(50));
            string newSearch = GUILayout.TextField(searchText);
            if (newSearch != searchText)
            {
                searchText = newSearch;
                PerformSearch();
            }
            GUILayout.EndHorizontal();
            
            GUILayout.Space(5);
            
            // 物品列表
            if (itemsLoaded)
            {
                GUILayout.Label($"搜索结果: {searchResults.Count} 个物品", GUI.skin.box);
                
                itemScrollPosition = GUILayout.BeginScrollView(itemScrollPosition, GUILayout.Height(450));
                
                foreach (var item in searchResults)
                {
                    GUILayout.BeginVertical(GUI.skin.box);
                    
                    // 第一行：ID和名称
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"[{item.id}]", GUILayout.Width(60));
                    GUILayout.Label(item.name, GUILayout.Width(300));
                    GUILayout.EndHorizontal();
                    
                    // 第二行：描述
                    if (!string.IsNullOrEmpty(item.description))
                    {
                        GUILayout.Label(item.description, GUI.skin.box);
                    }
                    
                    // 第三行：属性和按钮
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"价值:{item.value} | 重量:{item.weight:F2}kg | 堆叠:{item.maxStack}", GUILayout.Width(250));
                    
                    GUILayout.FlexibleSpace();
                    
                    if (GUILayout.Button("x1", GUILayout.Width(50), GUILayout.Height(25)))
                    {
                        SpawnItem(item.id, 1);
                    }
                    if (GUILayout.Button("x10", GUILayout.Width(50), GUILayout.Height(25)))
                    {
                        SpawnItem(item.id, 10);
                    }
                    if (GUILayout.Button("x99", GUILayout.Width(50), GUILayout.Height(25)))
                    {
                        SpawnItem(item.id, 99);
                    }
                    if (GUILayout.Button($"x{item.maxStack}", GUILayout.Width(60), GUILayout.Height(25)))
                    {
                        SpawnItem(item.id, item.maxStack);
                    }
                    GUILayout.EndHorizontal();
                    
                    GUILayout.EndVertical();
                    GUILayout.Space(3);
                }
                
                GUILayout.EndScrollView();
            }
        }

        private void DrawPlayerCheatTab()
        {
            GUILayout.Label("=== 玩家作弊 ===", GUI.skin.box);
            
            GUILayout.Space(10);
            
            if (GUILayout.Button("切换无敌模式", GUILayout.Height(50)))
            {
                ToggleInvincible();
            }
            
            GUILayout.Space(5);
            
            // 传送开关
            string teleportButtonText = teleportEnabled ? "传送开关: 已开启 (鼠标中键传送)" : "传送开关: 已关闭";
            if (GUILayout.Button(teleportButtonText, GUILayout.Height(50)))
            {
                teleportEnabled = !teleportEnabled;
                UnityEngine.Debug.Log($"[成功] 传送功能 {(teleportEnabled ? "已开启" : "已关闭")}");
            }
            
            GUILayout.Space(5);
            
            // 如果传送开启，显示提示信息
            if (teleportEnabled)
            {
                GUILayout.Label("[提示] 按下鼠标中键（滚轮）传送到鼠标指向位置", GUI.skin.box);
            }
            
            GUILayout.Space(10);
            
            GUILayout.Label("[警告] 提示: 某些功能需要在游戏场景中才能使用", GUI.skin.box);
        }

        private void DrawSettingsTab()
        {
            GUILayout.Label("=== 设置 ===", GUI.skin.box);
            
            GUILayout.Space(10);
            
            GUILayout.Label($"已加载物品: {allItems.Count} 个");
            GUILayout.Label($"主游戏物品: {allItems.Count(i => !i.isMod)} 个");
            GUILayout.Label($"MOD物品: {allItems.Count(i => i.isMod)} 个");
            GUILayout.Label($"缓存时间: {(cacheTime != DateTime.MinValue ? cacheTime.ToString("yyyy-MM-dd HH:mm:ss") : "无")}");
            GUILayout.Label($"缓存文件: {Path.GetFileName(ModBehaviour.CacheFilePath)}");
            GUILayout.Label($"FPS: {(int)(1f / Time.deltaTime)}");
            
            GUILayout.Space(15);
            
            // UI缩放控制 ✨ NEW
            GUILayout.Label("=== UI缩放 ===", GUI.skin.box);
            
            GUILayout.BeginHorizontal();
            GUILayout.Label($"当前缩放: {uiScale:F1}x", GUILayout.Width(120));
            
            if (GUILayout.Button("-", GUILayout.Width(40), GUILayout.Height(35)))
            {
                uiScale = Mathf.Max(MIN_SCALE, uiScale - SCALE_STEP);
                ApplyScale();
                SaveConfig();
                UnityEngine.Debug.Log($"[UI] 缩放调整为: {uiScale:F1}x");
            }
            
            // 滑块
            float newScale = GUILayout.HorizontalSlider(uiScale, MIN_SCALE, MAX_SCALE, GUILayout.Width(200));
            if (Mathf.Abs(newScale - uiScale) > 0.01f)
            {
                uiScale = newScale;
                ApplyScale();
                SaveConfig();
            }
            
            if (GUILayout.Button("+", GUILayout.Width(40), GUILayout.Height(35)))
            {
                uiScale = Mathf.Min(MAX_SCALE, uiScale + SCALE_STEP);
                ApplyScale();
                SaveConfig();
                UnityEngine.Debug.Log($"[UI] 缩放调整为: {uiScale:F1}x");
            }
            GUILayout.EndHorizontal();
            
            GUILayout.Space(5);
            
            // 快捷缩放按钮
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("50%", GUILayout.Height(30)))
            {
                uiScale = 0.5f;
                ApplyScale();
                SaveConfig();
            }
            if (GUILayout.Button("75%", GUILayout.Height(30)))
            {
                uiScale = 0.75f;
                ApplyScale();
                SaveConfig();
            }
            if (GUILayout.Button("100%", GUILayout.Height(30)))
            {
                uiScale = 1.0f;
                ApplyScale();
                SaveConfig();
            }
            if (GUILayout.Button("125%", GUILayout.Height(30)))
            {
                uiScale = 1.25f;
                ApplyScale();
                SaveConfig();
            }
            if (GUILayout.Button("150%", GUILayout.Height(30)))
            {
                uiScale = 1.5f;
                ApplyScale();
                SaveConfig();
            }
            if (GUILayout.Button("200%", GUILayout.Height(30)))
            {
                uiScale = 2.0f;
                ApplyScale();
                SaveConfig();
            }
            GUILayout.EndHorizontal();
            
            GUILayout.Space(15);
            
            GUILayout.Label("=== 缓存管理 ===", GUI.skin.box);
            
            if (GUILayout.Button("重新扫描物品（更新缓存）", GUILayout.Height(45)))
            {
                ScanAndCacheItems();
            }
            
            GUILayout.Space(5);
            
            if (GUILayout.Button("删除缓存文件", GUILayout.Height(45)))
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
            
            if (GUILayout.Button("打开缓存文件夹", GUILayout.Height(45)))
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
            
            GUILayout.Space(15);
            
            GUILayout.Label("=== 调试 ===", GUI.skin.box);
            
            if (GUILayout.Button("输出前20个物品到日志", GUILayout.Height(40)))
            {
                UnityEngine.Debug.Log("=== 物品列表（前20个）===");
                foreach (var item in allItems.Take(20))
                {
                    UnityEngine.Debug.Log($"ID:{item.id} | {item.name} | MOD:{item.isMod}");
                }
            }
            
            GUILayout.Space(5);
            
            if (GUILayout.Button("输出所有MOD物品到日志", GUILayout.Height(40)))
            {
                var modItems = allItems.Where(i => i.isMod).ToList();
                UnityEngine.Debug.Log($"=== MOD物品列表（共{modItems.Count}个）===");
                foreach (var item in modItems)
                {
                    UnityEngine.Debug.Log($"ID:{item.id} | {item.name}");
                }
            }
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