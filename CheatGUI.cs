using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using ItemStatsSystem;
using Newtonsoft.Json;

namespace DuckovCheatGUI
{
    [BepInPlugin("com.dandan.duckov.cheatgui", "Duckov Cheat GUI", "0.1.0")]
    public class CheatGUI : BaseUnityPlugin
    {
        public static ManualLogSource Log { get; private set; }
        public static GUIRenderer Renderer;
        public static string CacheFilePath;
        
        private void Awake()
        {
            Log = Logger;
            
            // 设置缓存文件路径
            CacheFilePath = Path.Combine(Paths.ConfigPath, "DuckovCheatGUI_ItemCache.json");
            
            Log.LogInfo("=================================");
            Log.LogInfo("作弊菜单 Mod v0.1.0 已加载！");
            Log.LogInfo($"缓存文件: {CacheFilePath}");
            Log.LogInfo("=================================");
            
            try
            {
                var harmony = new Harmony("com.dandan.duckov.cheatgui");
                harmony.PatchAll();
                
                Log.LogInfo("[成功] Harmony 补丁已应用");
                Log.LogInfo("进入游戏后按 Home 键打开菜单");
            }
            catch (Exception e)
            {
                Log.LogError($"初始化失败: {e}");
            }
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
                    CheatGUI.Log.LogInfo("[>>] 创建GUI GameObject...");
                    
                    guiObject = new GameObject("CheatGUI_Renderer");
                    UnityEngine.Object.DontDestroyOnLoad(guiObject);
                    CheatGUI.Renderer = guiObject.AddComponent<GUIRenderer>();
                    
                    CheatGUI.Log.LogInfo("[成功] GUI 创建成功！");
                    initialized = true;
                }
                catch (Exception e)
                {
                    CheatGUI.Log.LogError($"创建GUI失败: {e}");
                }
            }
            
            if (Input.GetKeyDown(KeyCode.Home))
            {
                CheatGUI.Renderer?.ToggleWindow();
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
                
                // 首次打开时加载缓存
                if (!itemsLoaded)
                {
                    LoadItemsFromCache();
                }
                
                CheatGUI.Log.LogInfo("[菜单] 菜单打开");
            }
            else
            {
                CheatGUI.Log.LogInfo("[菜单] 菜单关闭");
            }
        }

        // 从缓存加载物品
        private void LoadItemsFromCache()
        {
            try
            {
                if (File.Exists(CheatGUI.CacheFilePath))
                {
                    CheatGUI.Log.LogInfo("[加载] 从缓存加载物品列表...");
                    
                    string json = File.ReadAllText(CheatGUI.CacheFilePath);
                    var cache = JsonConvert.DeserializeObject<ItemCache>(json);
                    
                    allItems = cache.Items;
                    cacheTime = cache.CacheTime;
                    
                    CheatGUI.Log.LogInfo($"[成功] 从缓存加载 {allItems.Count} 个物品");
                    CheatGUI.Log.LogInfo($"缓存时间: {cacheTime:yyyy-MM-dd HH:mm:ss}");
                    itemsLoaded = true;
                }
                else
                {
                    CheatGUI.Log.LogInfo("[警告] 缓存文件不存在，需要扫描物品");
                }
            }
            catch (Exception e)
            {
                CheatGUI.Log.LogError($"加载缓存失败: {e.Message}");
            }
        }

        // 扫描并保存到缓存
        private void ScanAndCacheItems()
        {
            isScanning = true;
            allItems.Clear();
            
            try
            {
                CheatGUI.Log.LogInfo("[扫描] 扫描游戏物品...");
                
                Item[] allItemComponents = Resources.FindObjectsOfTypeAll<Item>();
                CheatGUI.Log.LogInfo($"找到 {allItemComponents.Length} 个 Item 组件");
                
                HashSet<int> addedIds = new HashSet<int>();
                
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
                            weight = item.UnitSelfWeight
                        };
                        
                        allItems.Add(info);
                    }
                    catch (Exception e)
                    {
                        CheatGUI.Log.LogWarning($"读取物品失败: {e.Message}");
                    }
                }
                
                // 排序
                allItems = allItems.OrderBy(i => i.id).ToList();
                
                // 保存到缓存
                SaveItemsToCache();
                
                CheatGUI.Log.LogInfo($"[成功] 扫描完成！共 {allItems.Count} 个物品");
                itemsLoaded = true;
            }
            catch (Exception e)
            {
                CheatGUI.Log.LogError($"扫描失败: {e}");
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
                File.WriteAllText(CheatGUI.CacheFilePath, json);
                
                cacheTime = cache.CacheTime;
                CheatGUI.Log.LogInfo($"[保存] 已保存到缓存: {CheatGUI.CacheFilePath}");
            }
            catch (Exception e)
            {
                CheatGUI.Log.LogError($"保存缓存失败: {e.Message}");
            }
        }

        // 搜索物品
        private void SearchItems()
        {
            searchResults.Clear();
            
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return;
            }
            
            string search = searchText.ToLower().Trim();
            
            // 如果是纯数字，按ID精确搜索
            if (int.TryParse(search, out int searchId))
            {
                searchResults = allItems.Where(item => item.id == searchId).ToList();
            }
            else
            {
                // 按名称和描述模糊搜索
                searchResults = allItems.Where(item =>
                    item.name.ToLower().Contains(search) ||
                    item.description.ToLower().Contains(search)
                ).Take(50).ToList(); // 限制50个结果
            }
        }

        private void OnGUI()
        {
            if (!showWindow) return;

            try
            {
                GUI.skin.label.fontSize = 13;
                GUI.skin.button.fontSize = 13;
                GUI.skin.textField.fontSize = 13;
                
                windowRect = GUI.Window(88888, windowRect, DrawWindow, "Duckov 作弊菜单 v0.1.0");
            }
            catch (Exception e)
            {
                CheatGUI.Log.LogError($"OnGUI错误: {e.Message}");
            }
        }

        private void DrawWindow(int windowID)
        {
            try
            {
                GUILayout.BeginVertical();

                // 标签页
                GUILayout.BeginHorizontal();
                for (int i = 0; i < tabs.Length; i++)
                {
                    if (GUILayout.Button(tabs[i], GUILayout.Height(30)))
                    {
                        currentTab = i;
                    }
                }
                GUILayout.EndHorizontal();
                
                GUILayout.Space(5);
                
                switch (currentTab)
                {
                    case 0:
                        DrawItemSpawnerTab();
                        break;
                    case 1:
                        DrawPlayerCheatTab();
                        break;
                    case 2:
                        DrawSettingsTab();
                        break;
                }
                
                GUILayout.Space(10);
                
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("[X] 关闭 (Home)", GUILayout.Height(35)))
                {
                    ToggleWindow();
                }
                GUILayout.EndHorizontal();

                GUILayout.EndVertical();
                
                GUI.DragWindow(new Rect(0, 0, 10000, 25));
            }
            catch (Exception e)
            {
                CheatGUI.Log.LogError($"DrawWindow错误: {e.Message}");
            }
        }

        private void DrawItemSpawnerTab()
        {
            GUILayout.Label("=== 物品生成器 ===", GUI.skin.box);
            
            if (isScanning)
            {
                GUILayout.Label("[等待] 正在扫描物品，请稍候...", GUI.skin.box);
                return;
            }
            
            if (!itemsLoaded)
            {
                GUILayout.Label("物品列表未加载", GUI.skin.box);
                if (GUILayout.Button("扫描游戏物品", GUILayout.Height(40)))
                {
                    ScanAndCacheItems();
                }
                return;
            }
            
            // 快速生成（按ID）
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("快速生成（输入物品ID）:");
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("ID:", GUILayout.Width(30));
            itemIdInput = GUILayout.TextField(itemIdInput, GUILayout.Width(100));
            
            GUILayout.Label("数量:", GUILayout.Width(50));
            itemCountInput = GUILayout.TextField(itemCountInput, GUILayout.Width(80));
            
            if (GUILayout.Button("生成", GUILayout.Height(25), GUILayout.Width(80)))
            {
                SpawnItemById();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            
            GUILayout.Space(10);
            
            // 搜索框
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"搜索物品（共 {allItems.Count} 个）:");
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("搜索:", GUILayout.Width(50));
            string newSearch = GUILayout.TextField(searchText, GUILayout.Width(350));
            if (newSearch != searchText)
            {
                searchText = newSearch;
                SearchItems();
            }
            if (GUILayout.Button("清空", GUILayout.Width(60)))
            {
                searchText = "";
                searchResults.Clear();
            }
            if (GUILayout.Button("搜索", GUILayout.Width(50)))
            {
                SearchItems();
            }
            GUILayout.EndHorizontal();
            
            GUILayout.Label("提示: 输入ID或名称进行搜索", GUI.skin.label);
            GUILayout.EndVertical();
            
            GUILayout.Space(5);
            
            // 显示搜索结果
            if (string.IsNullOrWhiteSpace(searchText))
            {
                GUILayout.Label("[提示] 请在上方输入搜索关键词", GUI.skin.box, GUILayout.Height(400));
            }
            else if (searchResults.Count == 0)
            {
                GUILayout.Label($"[X] 未找到匹配 \"{searchText}\" 的物品", GUI.skin.box, GUILayout.Height(400));
            }
            else
            {
                GUILayout.Label($"找到 {searchResults.Count} 个物品:", GUI.skin.box);
                
                itemScrollPosition = GUILayout.BeginScrollView(itemScrollPosition, GUILayout.Height(380));
                
                foreach (var item in searchResults)
                {
                    GUILayout.BeginVertical(GUI.skin.box);
                    
                    // 第一行：标题
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"【ID:{item.id}】{item.name}", GUI.skin.label);
                    GUILayout.FlexibleSpace();
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
                CheatGUI.Log.LogInfo($"[成功] 传送功能 {(teleportEnabled ? "已开启" : "已关闭")}");
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
            GUILayout.Label($"缓存时间: {(cacheTime != DateTime.MinValue ? cacheTime.ToString("yyyy-MM-dd HH:mm:ss") : "无")}");
            GUILayout.Label($"缓存文件: {Path.GetFileName(CheatGUI.CacheFilePath)}");
            GUILayout.Label($"FPS: {(int)(1f / Time.deltaTime)}");
            
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
                    if (File.Exists(CheatGUI.CacheFilePath))
                    {
                        File.Delete(CheatGUI.CacheFilePath);
                        CheatGUI.Log.LogInfo("[成功] 缓存文件已删除");
                        itemsLoaded = false;
                        allItems.Clear();
                        searchResults.Clear();
                    }
                }
                catch (Exception e)
                {
                    CheatGUI.Log.LogError($"删除失败: {e.Message}");
                }
            }
            
            GUILayout.Space(5);
            
            if (GUILayout.Button("打开缓存文件夹", GUILayout.Height(45)))
            {
                try
                {
                    System.Diagnostics.Process.Start(Path.GetDirectoryName(CheatGUI.CacheFilePath));
                }
                catch (Exception e)
                {
                    CheatGUI.Log.LogError($"打开失败: {e.Message}");
                }
            }
            
            GUILayout.Space(15);
            
            GUILayout.Label("=== 调试 ===", GUI.skin.box);
            
            if (GUILayout.Button("输出前20个物品到日志", GUILayout.Height(40)))
            {
                CheatGUI.Log.LogInfo("=== 物品列表（前20个）===");
                foreach (var item in allItems.Take(20))
                {
                    CheatGUI.Log.LogInfo($"ID:{item.id} | {item.name}");
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
                    CheatGUI.Log.LogWarning("[警告] 请输入有效数字");
                }
            }
            catch (Exception e)
            {
                CheatGUI.Log.LogError($"错误: {e.Message}");
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
                    
                    CheatGUI.Log.LogInfo($"[成功] 生成 {name} x{count}");
                }
                else
                {
                    CheatGUI.Log.LogWarning("[警告] CheatingManager 未就绪");
                }
            }
            catch (Exception e)
            {
                CheatGUI.Log.LogError($"生成错误: {e.Message}");
            }
        }

        private void ToggleInvincible()
        {
            try
            {
                if (CheatingManager.Instance != null)
                {
                    CheatingManager.Instance.ToggleInvincible();
                    CheatGUI.Log.LogInfo("[成功] 无敌模式切换");
                }
                else
                {
                    CheatGUI.Log.LogWarning("[警告] CheatingManager 未就绪");
                }
            }
            catch (Exception e)
            {
                CheatGUI.Log.LogError($"错误: {e.Message}");
            }
        }

        private void CheatMove()
        {
            try
            {
                if (CheatingManager.Instance != null)
                {
                    CheatingManager.Instance.CheatMove();
                    CheatGUI.Log.LogInfo("[成功] 传送成功");
                }
                else
                {
                    CheatGUI.Log.LogWarning("[警告] CheatingManager 未就绪");
                }
            }
            catch (Exception e)
            {
                CheatGUI.Log.LogError($"错误: {e.Message}");
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
    }

    // 缓存结构
    [Serializable]
    public class ItemCache
    {
        public List<ItemInfo> Items;
        public DateTime CacheTime;
    }
}