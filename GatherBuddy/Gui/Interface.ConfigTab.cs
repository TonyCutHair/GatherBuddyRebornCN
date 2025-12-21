using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface.Utility;

using FFXIVClientStructs.STD;
using GatherBuddy.Alarms;
using GatherBuddy.AutoGather;
using GatherBuddy.AutoGather.Collectables;
using GatherBuddy.AutoGather.Collectables.Data;
using GatherBuddy.Classes;
using GatherBuddy.Config;
using GatherBuddy.Enums;
using GatherBuddy.FishTimer;
using OtterGui;
using OtterGui.Widgets;
using FishRecord = GatherBuddy.FishTimer.FishRecord;
using GatheringType = GatherBuddy.Enums.GatheringType;
using ImRaii = OtterGui.Raii.ImRaii;

namespace GatherBuddy.Gui;

public partial class Interface
{
    private static class ConfigFunctions
    {
        public static Interface _base = null!;
        
        private static string _fishFilterText = "";
        private static Fish? _selectedFish = null;
        private static string _presetName = "";
        private static string _scripShopFilterText = "";

        public static void DrawSetInput(string jobName, string oldName, Action<string> setName)
        {
            var tmp = oldName;
            ImGui.SetNextItemWidth(SetInputWidth);
            if (ImGui.InputText($"{jobName} Set", ref tmp, 15) && tmp != oldName)
            {
                setName(tmp);
                GatherBuddy.Config.Save();
            }

            ImGuiUtil.HoverTooltip(Label(
                $"Set the name of your {jobName.ToLowerInvariant()} set. Can also be the numerical id instead.",
                $"设置你的{jobName.ToLowerInvariant()}套装名称。也可以使用数字ID。"));
        }

        private static void DrawCheckbox(string label, string description, bool oldValue, Action<bool> setter)
        {
            if (ImGuiUtil.Checkbox(label, description, oldValue, setter))
                GatherBuddy.Config.Save();
        }

        private static void DrawChatTypeSelector(string label, string description, XivChatType currentValue, Action<XivChatType> setter)
        {
            ImGui.SetNextItemWidth(SetInputWidth);
            if (Widget.DrawChatTypeSelector(label, description, currentValue, setter))
                GatherBuddy.Config.Save();
        }

        // Auto-Gather Config
        public static void DrawAutoGatherBox()
            => DrawCheckbox(Label("Enable Gathering Window Interaction (DISABLING THIS IS UNSUPPORTED)", "启用采集窗口交互(禁用此项不受支持)"),
                Label("Toggle whether to automatically gather items. (Disable this for 'nav only mode')", "切换是否自动采集物品(禁用此项仅用于导航模式)"),
                GatherBuddy.Config.AutoGatherConfig.DoGathering, b => GatherBuddy.Config.AutoGatherConfig.DoGathering = b);

        public static void DrawGoHomeBox()
        {
            DrawCheckbox(Label("Go home when done", "完成后回家"),
                Label("Uses the '/li auto' command to take you home when done gathering", "采集完成后使用'/li auto'命令回家"),
                GatherBuddy.Config.AutoGatherConfig.GoHomeWhenDone, b => GatherBuddy.Config.AutoGatherConfig.GoHomeWhenDone = b);
            ImGui.SameLine();
            ImGuiEx.PluginAvailabilityIndicator([new("Lifestream")]);
            DrawCheckbox(Label("Go home when idle", "空闲时回家"),
                Label("Uses the '/li auto' command to take you home when waiting for timed nodes", "等待限时采集点时使用'/li auto'命令回家"),
                GatherBuddy.Config.AutoGatherConfig.GoHomeWhenIdle, b => GatherBuddy.Config.AutoGatherConfig.GoHomeWhenIdle = b);
            ImGui.SameLine();
            ImGuiEx.PluginAvailabilityIndicator([new("Lifestream")]);
        }

        public static void DrawUseSkillsForFallabckBox()
            => DrawCheckbox(Label("Use skills for fallback items", "备用物品使用技能"),
                Label("Use skills when gathering items from fallback presets", "从备用预设采集物品时使用技能"),
                GatherBuddy.Config.AutoGatherConfig.UseSkillsForFallbackItems,
                b => GatherBuddy.Config.AutoGatherConfig.UseSkillsForFallbackItems = b);

        public static void DrawAbandonNodesBox()
            => DrawCheckbox(Label("Abandon nodes without needed items", "放弃无需物品的采集点"),
                Label("Stop gathering and abandon the node when you have gathered enough items,\n"
              + "or if the node didn't have any needed items on the first place.",
                "当已采集足够物品或采集点没有需要的物品时停止采集并放弃该点。"),
                GatherBuddy.Config.AutoGatherConfig.AbandonNodes, b => GatherBuddy.Config.AutoGatherConfig.AbandonNodes = b);

        public static void DrawCheckRetainersBox()
        {
            DrawCheckbox(Label("Check Retainer Inventories", "检查雇员库存"),
                Label("Use Allagan Tools to check retainer inventories when doing inventory calculations", "进行库存计算时使用Allagan Tools检查雇员库存"),
                GatherBuddy.Config.AutoGatherConfig.CheckRetainers, b => GatherBuddy.Config.AutoGatherConfig.CheckRetainers = b);
            ImGui.SameLine();
            ImGuiEx.PluginAvailabilityIndicator([new("InventoryTools", "Allagan Tools")]);
        }

        public static void DrawHonkVolumeSlider()
        {
            ImGui.SetNextItemWidth(150);
            var volume = GatherBuddy.Config.AutoGatherConfig.SoundPlaybackVolume;
            if (ImGui.DragInt(Label("Playback Volume", "播放音量"), ref volume, 1, 0, 100))
            {
                if (volume < 0)
                    volume = 0;
                else if (volume > 100)
                    volume = 100;
                GatherBuddy.Config.AutoGatherConfig.SoundPlaybackVolume = volume;
                GatherBuddy.Config.Save();
            }

            ImGuiUtil.HoverTooltip(Label(
                "The volume of the sound played when auto-gathering shuts down because your list is complete.\nHold CTRL and click to enter custom value",
                "自动采集因列表完成而停止时播放的声音音量。\n按住CTRL并点击输入自定义值"));
        }

        public static void DrawHonkModeBox()
            => DrawCheckbox(Label("Play a sound when done gathering", "采集完成时播放声音"),
                Label("Play a sound when auto-gathering shuts down because your list is complete", "自动采集因列表完成而停止时播放声音"),
                GatherBuddy.Config.AutoGatherConfig.HonkMode,   b => GatherBuddy.Config.AutoGatherConfig.HonkMode = b);

        public static void DrawRepairBox()
            => DrawCheckbox(Label("Repair gear when needed", "需要时修理装备"),
                Label("Repair gear when it is almost broken", "装备即将损坏时自动修理"),
                GatherBuddy.Config.AutoGatherConfig.DoRepair, b => GatherBuddy.Config.AutoGatherConfig.DoRepair = b);

        public static void DrawRepairThreshold()
        {
            ImGui.SetNextItemWidth(150);
            var tmp = GatherBuddy.Config.AutoGatherConfig.RepairThreshold;
            if (ImGui.DragInt(Label("Repair Threshold", "修理阈值"), ref tmp, 1, 1, 100))
            {
                GatherBuddy.Config.AutoGatherConfig.RepairThreshold = tmp;
                GatherBuddy.Config.Save();
            }

            ImGuiUtil.HoverTooltip(Label("The percentage of durability at which you will repair your gear.", "装备耐久度达到此百分比时将进行修理。"));
        }

        public static void DrawFishingSpotMinutes()
        {
            ImGui.SetNextItemWidth(150);
            var tmp = GatherBuddy.Config.AutoGatherConfig.MaxFishingSpotMinutes;
            if (ImGui.DragInt(Label("Max Fishing Spot Minutes", "最大钓鱼时长(分钟)"), ref tmp, 1, 1, 40))
            {
                GatherBuddy.Config.AutoGatherConfig.MaxFishingSpotMinutes = tmp;
                GatherBuddy.Config.Save();
            }

            ImGuiUtil.HoverTooltip(Label("The maximum number of minutes you will fish at a fishing spot.", "在一个钓场最多钓鱼的分钟数。"));
        }

        public static void DrawAutoretainerBox()
        {
            DrawCheckbox(Label("Wait for AutoRetainer Multi-mode", "等待AutoRetainer多模式"),
                Label("Pause GBR automatically when AutoRetainer has retainers to process during Multi-mode", "当AutoRetainer在多模式下需要处理雇员时自动暂停GBR"),
                GatherBuddy.Config.AutoGatherConfig.AutoRetainerMultiMode, b => GatherBuddy.Config.AutoGatherConfig.AutoRetainerMultiMode = b);
            ImGui.SameLine();
            ImGuiEx.PluginAvailabilityIndicator([new ImGuiEx.RequiredPluginInfo("AutoRetainer")]);
        }

        public static void DrawAutoretainerThreshold()
        {
            ImGui.SetNextItemWidth(150);
            var tmp = GatherBuddy.Config.AutoGatherConfig.AutoRetainerMultiModeThreshold;
            if (ImGui.DragInt(Label("AutoRetainer Threshold (Seconds)", "AutoRetainer阈值(秒)"), ref tmp, 1, 0, 3600))
            {
                GatherBuddy.Config.AutoGatherConfig.AutoRetainerMultiModeThreshold = tmp;
                GatherBuddy.Config.Save();
            }

            ImGuiUtil.HoverTooltip(Label("How many seconds before a retainer venture completes GBR should pause and wait for MultiMode.", "雇员探险完成前多少秒GBR应暂停并等待多模式。"));
        }

        public static void DrawAutoretainerTimedNodeDelayBox()
            => DrawCheckbox(Label("Delay AutoRetainer for timed nodes", "为限时采集点延迟AutoRetainer"),
                Label("Wait to process retainers until after active/upcoming timed nodes are gathered.", "等待活跃/即将出现的限时采集点采集完后再处理雇员。"),
                GatherBuddy.Config.AutoGatherConfig.AutoRetainerDelayForTimedNodes,
                b => GatherBuddy.Config.AutoGatherConfig.AutoRetainerDelayForTimedNodes = b);

        public static void DrawLifestreamCommandTextInput()
        {
            ImGui.SetNextItemWidth(150);
            var tmp = GatherBuddy.Config.AutoGatherConfig.LifestreamCommand;
            if (ImGui.InputText(Label("Lifestream Command", "Lifestream命令"), ref tmp, 100))
            {
                if (string.IsNullOrEmpty(tmp))
                    tmp = "auto";
                GatherBuddy.Config.AutoGatherConfig.LifestreamCommand = tmp;
                GatherBuddy.Config.Save();
            }

            ImGuiUtil.HoverTooltip(Label(
                "The command used when idling or done gathering. DO NOT include '/li'\nBe careful when changing this, GBR does not validate this command!",
                "空闲或采集完成时使用的命令。不要包含'/li'\n修改此项请小心，GBR不验证此命令！"));
        }

        public static void DrawFishCollectionBox()
            => DrawCheckbox(Label("Opt-in to fishing data collection", "选择加入钓鱼数据收集"),
                Label("With this enabled, whenever you catch a fish the data for that fish will be uploaded to a remote server\n"
              + "The purpose of this data collection is to allow for a usable auto-fishing feature to be built\n"
              + "No personal information about you or your character will be collected, only data relevant to the caught fish\n"
              + "You can opt-out again at any time by simply disabling this checkbox.",
                "启用后，每次捕鱼时数据将上传到远程服务器\n"
              + "数据收集的目的是为了构建可用的自动钓鱼功能\n"
              + "不会收集你或你角色的任何个人信息，只收集与捕获的鱼相关的数据\n"
              + "你可以随时通过禁用此复选框退出。"),
                GatherBuddy.Config.AutoGatherConfig.FishDataCollection,
                b => GatherBuddy.Config.AutoGatherConfig.FishDataCollection = b);

        public static void DrawMaterialExtraction()
            => DrawCheckbox(Label("Enable materia extraction", "启用魔晶提取"),
                Label("Automatically extract materia from items with a complete spiritbond", "自动从精炼度满的装备中提取魔晶"),
                GatherBuddy.Config.AutoGatherConfig.DoMaterialize,
                b => GatherBuddy.Config.AutoGatherConfig.DoMaterialize = b);

        public static void DrawAetherialReduction()
            => DrawCheckbox(Label("Enable Aetherial Reduction", "启用灵性压缩"),
                Label("Automatically perform Aetherial Reduction when idling or the inventory is full", "空闲或背包满时自动执行灵性压缩"),
                GatherBuddy.Config.AutoGatherConfig.DoReduce,
                b => GatherBuddy.Config.AutoGatherConfig.DoReduce = b);

        public static void DrawUseFlagBox()
            => DrawCheckbox(Label("Disable map marker navigation", "禁用地图标记导航"),
                Label("Whether or not to navigate using map markers (timed nodes only)", "是否使用地图标记导航(仅限时采集点)"),
                GatherBuddy.Config.AutoGatherConfig.DisableFlagPathing, b => GatherBuddy.Config.AutoGatherConfig.DisableFlagPathing = b);

        public static void DrawFarNodeFilterDistance()
        {
            ImGui.SetNextItemWidth(150);
            var tmp = GatherBuddy.Config.AutoGatherConfig.FarNodeFilterDistance;
            if (ImGui.DragFloat(Label("Far Node Filter Distance", "远距离采集点过滤距离"), ref tmp, 0.1f, 0.1f, 100f))
            {
                GatherBuddy.Config.AutoGatherConfig.FarNodeFilterDistance = tmp;
                GatherBuddy.Config.Save();
            }

            ImGuiUtil.HoverTooltip(Label(
                "When looking for non-empty nodes GBR will filter out any nodes that are closer to you than this. Prevents checking nodes you can already see are empty.",
                "寻找非空采集点时，GBR将过滤掉比此距离更近的采集点。防止检查你已经看到的空采集点。"));
        }

        public static void DrawTimedNodePrecog()
        {
            ImGui.SetNextItemWidth(150);
            var tmp = GatherBuddy.Config.AutoGatherConfig.TimedNodePrecog;
            if (ImGui.DragInt(Label("Timed Node Precognition (Seconds)", "限时采集点预知(秒)"), ref tmp, 1, 0, 600))
            {
                GatherBuddy.Config.AutoGatherConfig.TimedNodePrecog = tmp;
                GatherBuddy.Config.Save();
            }

            ImGuiUtil.HoverTooltip(Label("How far in advance of the node actually being up GBR should consider the node to be up", "在采集点实际出现之前GBR应该提前多久认为该采集点已出现"));
        }

        public static void DrawExecutionDelay()
        {
            ImGui.SetNextItemWidth(150);
            var tmp = (int)GatherBuddy.Config.AutoGatherConfig.ExecutionDelay;
            if (ImGui.DragInt(Label("Execution delay (Milliseconds)", "执行延迟(毫秒)"), ref tmp, 1, 0, 1500))
            {
                GatherBuddy.Config.AutoGatherConfig.ExecutionDelay = (uint)Math.Min(Math.Max(0, tmp), 10000);
                GatherBuddy.Config.Save();
            }

            ImGuiUtil.HoverTooltip(Label("Delay executing each action by the specified amount.", "按指定量延迟执行每个动作。"));
        }

        public static void DrawUseGivingLandOnCooldown()
            => DrawCheckbox(Label("Gather any crystals when The Giving Land is off cooldown", "大地恩惠冷却完毕时采集任意水晶"),
                Label("Gather random crystals on any regular node when The Giving Land is avaiable regardles of current target item.", "当大地恩惠可用时，无论当前目标物品，在任何普通采集点采集随机水晶。"),
                GatherBuddy.Config.AutoGatherConfig.UseGivingLandOnCooldown,
                b => GatherBuddy.Config.AutoGatherConfig.UseGivingLandOnCooldown = b);

        public static void DrawMountUpDistance()
        {
            ImGui.SetNextItemWidth(150);
            var tmp = GatherBuddy.Config.AutoGatherConfig.MountUpDistance;
            if (ImGui.DragFloat(Label("Mount Up Distance", "上坐骑距离"), ref tmp, 0.1f, 0.1f, 100f))
            {
                GatherBuddy.Config.AutoGatherConfig.MountUpDistance = tmp;
                GatherBuddy.Config.Save();
            }

            ImGuiUtil.HoverTooltip(Label("The distance at which you will mount up to move to a node.", "移动到节点时上坐骑的距离。"));
        }

        public static void DrawMoveWhileMounting()
            => DrawCheckbox(Label("Move while mounting up", "上坐骑时移动"),
                Label("Begin pathfinding to the next node while summoning a mount", "召唤坐骑时开始寻路到下一个节点"),
                GatherBuddy.Config.AutoGatherConfig.MoveWhileMounting,
                b => GatherBuddy.Config.AutoGatherConfig.MoveWhileMounting = b);

        public static void DrawAntiStuckCooldown()
        {
            ImGui.SetNextItemWidth(150);
            var tmp = GatherBuddy.Config.AutoGatherConfig.NavResetCooldown;
            if (ImGui.DragFloat(Label("Anti-Stuck Cooldown", "防卡死冷却"), ref tmp, 0.1f, 0.1f, 10f))
            {
                GatherBuddy.Config.AutoGatherConfig.NavResetCooldown = tmp;
                GatherBuddy.Config.Save();
            }

            ImGuiUtil.HoverTooltip(Label("The time in seconds before the navigation system will reset if you are stuck.", "卡死时导航系统重置前的秒数。"));
        }

        public static void DrawForceWalkingBox()
            => DrawCheckbox(Label("Force Walking", "强制步行"), Label("Force walking to nodes instead of using mounts.", "强制步行到节点而不使用坐骑。"),
                GatherBuddy.Config.AutoGatherConfig.ForceWalking, b => GatherBuddy.Config.AutoGatherConfig.ForceWalking = b);

        public static void DrawUseNavigationBox()
            => DrawCheckbox(Label("Use vnavmesh Navigation", "使用vnavmesh导航"), Label("Use vnavmesh Navigation to move your character automatically", "使用vnavmesh导航自动移动角色"),
                GatherBuddy.Config.AutoGatherConfig.UseNavigation, b => GatherBuddy.Config.AutoGatherConfig.UseNavigation = b);

        public static void DrawStuckThreshold()
        {
            ImGui.SetNextItemWidth(150);
            var tmp = GatherBuddy.Config.AutoGatherConfig.NavResetThreshold;
            if (ImGui.DragFloat(Label("Stuck Threshold", "卡死阈值"), ref tmp, 0.1f, 0.1f, 10f))
            {
                GatherBuddy.Config.AutoGatherConfig.NavResetThreshold = tmp;
                GatherBuddy.Config.Save();
            }

            ImGuiUtil.HoverTooltip(Label("The time in seconds before the navigation system will consider you stuck.", "导航系统将认为你卡住之前的秒数。"));
        }

        public static void DrawSortingMethodCombo()
        {
            var v = GatherBuddy.Config.AutoGatherConfig.SortingMethod;
            ImGui.SetNextItemWidth(150);

            using var combo = ImRaii.Combo(Label("Item Sorting Method", "物品排序方式"), v.ToString());
            ImGuiUtil.HoverTooltip(Label("What method to use when sorting items internally", "内部排序物品时使用的方法"));
            if (!combo)
                return;

            if (ImGui.Selectable(AutoGatherConfig.SortingType.Location.ToString(), v == AutoGatherConfig.SortingType.Location))
            {
                GatherBuddy.Config.AutoGatherConfig.SortingMethod = AutoGatherConfig.SortingType.Location;
                GatherBuddy.Config.Save();
            }

            if (ImGui.Selectable(AutoGatherConfig.SortingType.None.ToString(), v == AutoGatherConfig.SortingType.None))
            {
                GatherBuddy.Config.AutoGatherConfig.SortingMethod = AutoGatherConfig.SortingType.None;
                GatherBuddy.Config.Save();
            }
        }

        // General Config
        public static void DrawOpenOnStartBox()
            => DrawCheckbox(Label("Open Config UI On Start", "启动时打开配置界面"),
                Label("Toggle whether the GatherBuddy GUI should be visible after you start the game.", "切换启动游戏后GatherBuddy界面是否可见。"),
                GatherBuddy.Config.OpenOnStart, b => GatherBuddy.Config.OpenOnStart = b);

        public static void DrawLockPositionBox()
            => DrawCheckbox(Label("Lock Config UI Movement", "锁定配置界面移动"),
                Label("Toggle whether the GatherBuddy GUI movement should be locked.", "切换是否锁定GatherBuddy界面移动。"),
                GatherBuddy.Config.MainWindowLockPosition, b =>
                {
                    GatherBuddy.Config.MainWindowLockPosition = b;
                    _base.UpdateFlags();
                });

        public static void DrawLockResizeBox()
            => DrawCheckbox(Label("Lock Config UI Size", "锁定配置界面大小"),
                Label("Toggle whether the GatherBuddy GUI size should be locked.", "切换是否锁定GatherBuddy界面大小。"),
                GatherBuddy.Config.MainWindowLockResize, b =>
                {
                    GatherBuddy.Config.MainWindowLockResize = b;
                    _base.UpdateFlags();
                });

        public static void DrawRespectEscapeBox()
            => DrawCheckbox(Label("Escape Closes Main Window", "Escape关闭主窗口"),
                Label("Toggle whether pressing escape while having the main window focused shall close it.", "切换在主窗口获得焦点时按Escape键是否关闭它。"),
                GatherBuddy.Config.CloseOnEscape, b =>
                {
                    GatherBuddy.Config.CloseOnEscape = b;
                    _base.UpdateFlags();
                });

        public static void DrawGearChangeBox()
            => DrawCheckbox(Label("Enable Gear Change", "启用装备切换"),
                Label("Toggle whether to automatically switch gear to the correct job gear for a node.\nUses Miner Set, Botanist Set and Fisher Set.",
                      "切换是否自动切换为采集点对应的正确职业装备。\n使用矿工套装、园艺工套装和捕鱼人套装。"),
                GatherBuddy.Config.UseGearChange, b => GatherBuddy.Config.UseGearChange = b);

        public static void DrawTeleportBox()
            => DrawCheckbox(Label("Enable Teleport", "启用传送"),
                Label("Toggle whether to automatically teleport to a chosen node.", "切换是否自动传送到选定的采集点。"),
                GatherBuddy.Config.UseTeleport, b => GatherBuddy.Config.UseTeleport = b);

        public static void DrawMapOpenBox()
            => DrawCheckbox(Label("Open Map With Location", "打开地图并显示位置"),
                Label("Toggle whether to automatically open the map of the territory of the chosen node with its gathering location highlighted.",
                      "切换是否自动打开选定采集点所在地区的地图并高亮其采集位置。"),
                GatherBuddy.Config.UseCoordinates, b => GatherBuddy.Config.UseCoordinates = b);

        public static void DrawPlaceMarkerBox()
            => DrawCheckbox(Label("Place Flag Marker on Map", "在地图上放置旗帜标记"),
                Label("Toggle whether to automatically set a red flag marker on the approximate location of the chosen node without opening the map.",
                      "切换是否在不打开地图的情况下自动在选定采集点的大致位置设置红旗标记。"),
                GatherBuddy.Config.UseFlag, b => GatherBuddy.Config.UseFlag = b);

        public static void DrawMapMarkerPrintBox()
            => DrawCheckbox(Label("Print Map Location", "打印地图位置"),
                Label("Toggle whether to automatically write a map link to the approximate location of the chosen node to chat.",
                      "切换是否自动将选定采集点的大致位置的地图链接写入聊天。"),
                GatherBuddy.Config.WriteCoordinates, b => GatherBuddy.Config.WriteCoordinates = b);

        public static void DrawPlaceWaymarkBox()
            => DrawCheckbox(Label("Place Custom Waymarks", "放置自定义标点"),
                Label("Toggle whether to place custom Waymarks you set manually set up for certain locations.",
                      "切换是否放置你为特定位置手动设置的自定义标点。"),
                GatherBuddy.Config.PlaceCustomWaymarks, b => GatherBuddy.Config.PlaceCustomWaymarks = b);

        public static void DrawPrintUptimesBox()
            => DrawCheckbox(Label("Print Node Uptimes On Gather", "采集时打印采集点时间"),
                Label("Print the uptimes of nodes you try to /gather in the chat if they are not always up.",
                      "如果采集点不是一直存在，则在聊天中打印你尝试/gather的采集点的出现时间。"),
                GatherBuddy.Config.PrintUptime, b => GatherBuddy.Config.PrintUptime = b);

        public static void DrawSkipTeleportBox()
            => DrawCheckbox(Label("Skip Nearby Teleports", "跳过附近传送"),
                Label("Skips teleports if you are in the same map and closer to the target than the selected aetheryte already.",
                      "如果你在同一地图中并且比选定的以太之光更靠近目标，则跳过传送。"),
                GatherBuddy.Config.SkipTeleportIfClose, b => GatherBuddy.Config.SkipTeleportIfClose = b);

        public static void DrawShowStatusLineBox()
            => DrawCheckbox(Label("Show Status Line", "显示状态栏"),
                Label("Show a status line below the gatherables and fish tables.", "在采集品和鱼类表格下方显示状态栏。"),
                GatherBuddy.Config.ShowStatusLine, v => GatherBuddy.Config.ShowStatusLine = v);

        public static void DrawHideClippyBox()
            => DrawCheckbox(Label("Hide GatherClippy Button", "隐藏GatherClippy按钮"),
                Label("Permanently hide the GatherClippy Button in the Gatherables and Fish tabs.",
                      "永久隐藏采集品和鱼类标签中的GatherClippy按钮。"),
                GatherBuddy.Config.HideClippy, v => GatherBuddy.Config.HideClippy = v);

        private const string ChatInformationString =
            "Note that the message only gets printed to your chat log, regardless of the selected channel"
          + " - other people will not see your 'Say' message.";

        public static void DrawPrintTypeSelector()
            => DrawChatTypeSelector(Label("Chat Type for Messages", "消息聊天类型"),
                Label("The chat type used to print regular messages issued by GatherBuddy.\n"
              + ChatInformationString,
                "用于打印GatherBuddy发出的常规消息的聊天类型。\n"
              + ChatInformationString),
                GatherBuddy.Config.ChatTypeMessage, t => GatherBuddy.Config.ChatTypeMessage = t);

        public static void DrawErrorTypeSelector()
            => DrawChatTypeSelector(Label("Chat Type for Errors", "错误聊天类型"),
                Label("The chat type used to print error messages issued by GatherBuddy.\n"
              + ChatInformationString,
                "用于打印GatherBuddy发出的错误消息的聊天类型。\n"
              + ChatInformationString),
                GatherBuddy.Config.ChatTypeError, t => GatherBuddy.Config.ChatTypeError = t);

        public static void DrawContextMenuBox()
            => DrawCheckbox(Label("Add In-Game Context Menus", "添加游戏内上下文菜单"),
                Label("Add a 'Gather' entry to in-game right-click context menus for gatherable items.",
                "为可采集物品的游戏内右键上下文菜单添加“采集”选项。"),
                GatherBuddy.Config.AddIngameContextMenus, b =>
                {
                    GatherBuddy.Config.AddIngameContextMenus = b;
                    if (b)
                        _plugin.ContextMenu.Enable();
                    else
                        _plugin.ContextMenu.Disable();
                });

        public static void DrawPreferredJobSelect()
        {
            var v       = GatherBuddy.Config.PreferredGatheringType;
            var current = v == GatheringType.Multiple ? Label("No Preference", "无偏好") : (v == GatheringType.Miner ? Label("Miner", "采矿工") : Label("Botanist", "园艺工"));
            ImGui.SetNextItemWidth(SetInputWidth);
            using var combo = ImRaii.Combo(Label("Preferred Job", "首选职业"), current);
            ImGuiUtil.HoverTooltip(
                Label("Choose your job preference when gathering items that can be gathered by miners as well as botanists.\n"
              + "This effectively turns the regular gather command to /gathermin or /gatherbtn when an item can be gathered by both, "
              + "ignoring the other options even on successive tries.",
              "选择采集可由采矿工和园艺工共同采集的物品时的职业偏好。\n"
              + "当物品可由两种职业采集时,这会将常规采集命令转换为 /gathermin 或 /gatherbtn,"
              + "即使连续尝试也会忽略其他选项。"));
            if (!combo)
                return;

            if (ImGui.Selectable(Label("No Preference", "无偏好"), v == GatheringType.Multiple) && v != GatheringType.Multiple)
            {
                GatherBuddy.Config.PreferredGatheringType = GatheringType.Multiple;
                GatherBuddy.Config.Save();
            }

            if (ImGui.Selectable(Label("Miner", "采矿工"), v == GatheringType.Miner) && v != GatheringType.Miner)
            {
                GatherBuddy.Config.PreferredGatheringType = GatheringType.Miner;
                GatherBuddy.Config.Save();
            }

            if (ImGui.Selectable(Label("Botanist", "园艺工"), v == GatheringType.Botanist) && v != GatheringType.Botanist)
            {
                GatherBuddy.Config.PreferredGatheringType = GatheringType.Botanist;
                GatherBuddy.Config.Save();
            }
        }

        public static void DrawPrintClipboardBox()
            => DrawCheckbox(Label("Print Clipboard Information", "打印剪贴板信息"),
                Label("Print to the chat whenever you save an object to the clipboard. Failures will be printed regardless.",
                      "当你将对象保存到剪贴板时打印到聊天。失败时仍会打印。"),
                GatherBuddy.Config.PrintClipboardMessages, b => GatherBuddy.Config.PrintClipboardMessages = b);

        // Weather Tab
        public static void DrawWeatherTabNamesBox()
            => DrawCheckbox(Label("Show Names in Weather Tab", "在天气标签中显示名称"),
                Label("Toggle whether to write the names in the table for the weather tab, or just the icons with names on hover.",
                      "切换是否在天气标签的表格中写出名称，还是只显示图标并在悬停时显示名称。"),
                GatherBuddy.Config.ShowWeatherNames, b => GatherBuddy.Config.ShowWeatherNames = b);

        // Alarms
        public static void DrawAlarmToggle()
            => DrawCheckbox(Label("Enable Alarms", "启用闹钟"), Label("Toggle all alarms on or off.", "切换所有闹钟的开关。"), GatherBuddy.Config.AlarmsEnabled,
                b =>
                {
                    if (b)
                        _plugin.AlarmManager.Enable();
                    else
                        _plugin.AlarmManager.Disable();
                });

        private static bool _gatherDebug = false;

        public static void DrawAlarmsInDutyToggle()
            => DrawCheckbox(Label("Enable Alarms in Duty", "在副本中启用闹钟"),
                Label("Set whether alarms should trigger while you are bound by a duty.", "设置在副本中闹钟是否触发。"),
                GatherBuddy.Config.AlarmsInDuty,     b => GatherBuddy.Config.AlarmsInDuty = b);

        public static void DrawAlarmsOnlyWhenLoggedInToggle()
            => DrawCheckbox(Label("Enable Alarms Only In-Game", "仅在游戏内启用闹钟"),
                Label("Set whether alarms should trigger while you are not logged into any character.", "设置未登录任何角色时闹钟是否触发。"),
                GatherBuddy.Config.AlarmsOnlyWhenLoggedIn, b => GatherBuddy.Config.AlarmsOnlyWhenLoggedIn = b);

        private static void DrawAlarmPicker(string label, string description, Sounds current, Action<Sounds> setter)
        {
            var cur = (int)current;
            ImGui.SetNextItemWidth(90 * ImGuiHelpers.GlobalScale);
            if (ImGui.Combo(new ImU8String(label), ref cur, AlarmCache.SoundIdNames))
                setter((Sounds)cur);
            ImGuiUtil.HoverTooltip(description);
        }

        public static void DrawWeatherAlarmPicker()
            => DrawAlarmPicker(Label("Weather Change Alarm", "天气变化闹钟"), 
                Label("Choose a sound that is played every 8 Eorzea hours on regular weather changes.",
                "选择每8艾欧泽亚小时常规天气变化时播放的声音。"),
                GatherBuddy.Config.WeatherAlarm,       _plugin.AlarmManager.SetWeatherAlarm);

        public static void DrawHourAlarmPicker()
            => DrawAlarmPicker(Label("Eorzea Hour Change Alarm", "艾欧泽亚小时变化闹钟"), 
                Label("Choose a sound that is played every time the current Eorzea hour changes.",
                "选择每次当前艾欧泽亚小时变化时播放的声音。"),
                GatherBuddy.Config.HourAlarm,              _plugin.AlarmManager.SetHourAlarm);

        // Fish Timer
        public static void DrawFishTimerBox()
            => DrawCheckbox(Label("Show Fish Timer", "显示鱼类计时器"),
                Label("Toggle whether to show the fish timer window while fishing.", "切换是否在钓鱼时显示鱼类计时器窗口。"),
                GatherBuddy.Config.ShowFishTimer, b => GatherBuddy.Config.ShowFishTimer = b);

        public static void DrawFishTimerEditBox()
            => DrawCheckbox(Label("Edit Fish Timer", "编辑鱼类计时器"),
                Label("Enable editing the fish timer window.", "启用编辑鱼类计时器窗口。"),
                GatherBuddy.Config.FishTimerEdit, b => GatherBuddy.Config.FishTimerEdit = b);

        public static void DrawFishTimerClickthroughBox()
            => DrawCheckbox(Label("Enable Fish Timer Clickthrough", "启用鱼类计时器点击透传"),
                Label("Allow clicking through the fish timer and disabling the context menus instead.", "允许点击透过鱼类计时器，并禁用上下文菜单。"),
                GatherBuddy.Config.FishTimerClickthrough, b => GatherBuddy.Config.FishTimerClickthrough = b);

        public static void DrawFishTimerHideBox()
            => DrawCheckbox(Label("Hide Uncaught Fish in Fish Timer", "在鱼类计时器中隐藏未捕获鱼类"),
                Label("Hide all fish from the fish timer window that have not been recorded with the given combination of snagging and bait.", "隐藏所有未以给定的拍水和鱼饵组合记录的鱼类。"),
                GatherBuddy.Config.HideUncaughtFish, b => GatherBuddy.Config.HideUncaughtFish = b);

        public static void DrawFishTimerHideBox2()
            => DrawCheckbox(Label("Hide Unavailable Fish in Fish Timer", "在鱼类计时器中隐藏不可用鱼类"),
                Label("Hide all fish from the fish timer window that have have known requirements that are unfulfilled, like Fisher's Intuition or Snagging.", "隐藏所有未满足已知条件(如以小钓大或拍水)的鱼类。"),
                GatherBuddy.Config.HideUnavailableFish, b => GatherBuddy.Config.HideUnavailableFish = b);

        public static void DrawFishTimerUptimesBox()
            => DrawCheckbox(Label("Show Uptimes in Fish Timer", "在鱼类计时器中显示出现时间"),
                Label("Show the uptimes for restricted fish in the fish timer window.", "在鱼类计时器窗口中显示限制鱼类的出现时间。"),
                GatherBuddy.Config.ShowFishTimerUptimes, b => GatherBuddy.Config.ShowFishTimerUptimes = b);

        public static void DrawKeepRecordsBox()
            => DrawCheckbox(Label("Keep Fish Records", "保存鱼类记录"),
                Label("Store Fish Records on your computer. This is necessary for bite timings for the fish timer window.", "在计算机上存储鱼类记录。这对鱼类计时器窗口的咬钩时间是必需的。"),
                GatherBuddy.Config.StoreFishRecords, b => GatherBuddy.Config.StoreFishRecords = b);

        public static void DrawShowLocalTimeInRecordsBox()
            => DrawCheckbox(Label("Use Local Time in Records", "在记录中使用本地时间"),
                Label("When displaying timestamps in the Fish Records Tab, use local time instead of Unix time.", "在鱼类记录标签页中显示时间戳时使用本地时间而不Unix时间。"),
                GatherBuddy.Config.UseUnixTimeFishRecords, b => GatherBuddy.Config.UseUnixTimeFishRecords = b);
        
        public static void DrawFishTimerScale()
        {
            var value = GatherBuddy.Config.FishTimerScale / 1000f;
            ImGui.SetNextItemWidth(SetInputWidth);
            var ret = ImGui.DragFloat(Label("Fish Timer Bite Time Scale", "鱼类计时器咬钩时间缩放"), ref value, 0.1f, FishRecord.MinBiteTime / 500f,
                FishRecord.MaxBiteTime / 1000f,
                "%2.3f Seconds");

            ImGuiUtil.HoverTooltip(Label("The fishing timer window bite times are scaled to this value.\n"
              + "If your bite time exceeds the value, the progress bar and bite windows will not be displayed.\n"
              + "You should probably keep this as high as your highest bite window and as low as possible. About 40 seconds is usually enough.",
              "鱼类计时器窗口的咬钩时间缩放到此值。\n"
              + "如果您的咬钩时间超过此值，将不显示进度条和咬钩窗口。\n"
              + "您应该尽可能保持在最高咬钩窗口的高度，并尽可能低。大约40秒通常足够了。"));

            if (!ret)
                return;

            var newValue = (ushort)Math.Clamp((int)(value * 1000f + 0.9), FishRecord.MinBiteTime * 2, FishRecord.MaxBiteTime);
            if (newValue == GatherBuddy.Config.FishTimerScale)
                return;

            GatherBuddy.Config.FishTimerScale = newValue;
            GatherBuddy.Config.Save();
        }

        public static void DrawFishTimerIntervals()
        {
            int value = GatherBuddy.Config.ShowSecondIntervals;
            ImGui.SetNextItemWidth(SetInputWidth);
            var ret = ImGui.DragInt(Label("Fish Timer Interval Separators", "鱼类计时器间隔分隔符"), ref value, 0.01f, 0, 16);
            ImGuiUtil.HoverTooltip(Label("The fishing timer window can show a number of interval lines and corresponding seconds between 0 and 16.\n"
              + "Set to 0 to turn this feature off.",
              "鱼类计时器窗口可以显示0到16之间的若干间隔线和相应秒数。\n"
              + "设置为0以关闭此功能。"));
            if (!ret)
                return;

            var newValue = (byte)Math.Clamp(value, 0, 16);
            if (newValue == GatherBuddy.Config.ShowSecondIntervals)
                return;

            GatherBuddy.Config.ShowSecondIntervals = newValue;
            GatherBuddy.Config.Save();
        }
        
        public static void DrawFishTimerIntervalsRounding()
        {
            var value = GatherBuddy.Config.SecondIntervalsRounding;
            ImGui.SetNextItemWidth(SetInputWidth);
            var ret = ImGui.DragInt(Label("Fish Timer Interval Rounding", "鱼类计时器间隔四舍五入"), ref value, 0.01f, 0, 3);
            ImGuiUtil.HoverTooltip(Label("Round the displayed second value to this number of digits past the decimal. \n"
                + "Set to 0 to display only whole numbers.",
                "将显示的秒值四舍五入到小数点后的该位数。\n"
                + "设置为0仅显示整数。"));
            if (!ret)
                return;

            var newValue = (byte)Math.Clamp(value, 0, 3);
            if (newValue == GatherBuddy.Config.SecondIntervalsRounding)
                return;

            GatherBuddy.Config.SecondIntervalsRounding = newValue;
            GatherBuddy.Config.Save();
        }

        public static void DrawHideFishPopupBox()
            => DrawCheckbox(Label("Hide Catch Popup", "隐藏捕获弹窗"),
                Label("Prevents the popup window that shows you your caught fish and its size, amount and quality from being shown.", "防止显示显示捕获鱼类及其尺寸、数量和质量的弹窗窗口。"),
                GatherBuddy.Config.HideFishSizePopup, b => GatherBuddy.Config.HideFishSizePopup = b);

        public static void DrawCollectableHintPopupBox()
            => DrawCheckbox(Label("Show Collectable Hints", "显示收藏品提示"),
                Label("Show if a fish is collectable in the fish timer window.", "在鱼类计时器窗口中显示鱼类是否为收藏品。"),
                GatherBuddy.Config.ShowCollectableHints, b => GatherBuddy.Config.ShowCollectableHints = b);

        public static void DrawDoubleHookHintPopupBox()
            => DrawCheckbox(Label("Show Multi Hook Hints", "显示多重提钩提示"),
                Label("Show if a fish can be double or triple hooked in Cosmic Exploration.", "在宇宙探险中显示鱼类是否可以双重或三重提钩。"), // TODO: add ocean fishing when implemented.
                GatherBuddy.Config.ShowMultiHookHints, b => GatherBuddy.Config.ShowMultiHookHints = b);
        
        
        // Fish Stats Window
        public static void DrawEnableFishStats()
            => DrawCheckbox(Label("Enable Fish Stats", "启用鱼类统计"),
                Label("New tab for aggregating and reporting fish stats based on local records. Currently in testing.", "用于汇总和报告基于本地记录的鱼类统计的新标签页。当前正在测试中。"),
                GatherBuddy.Config.EnableFishStats, b => GatherBuddy.Config.EnableFishStats = b);
        public static void DrawEnableReportTime()  
            => DrawCheckbox(Label("Copy Time Stats when reporting.", "报告时复制时间统计。"),
                Label("When copying the report, add min and max times to the report.", "复制报告时，将最小和最大时间添加到报告中。"),
                GatherBuddy.Config.EnableReportTime, b => GatherBuddy.Config.EnableReportTime = b);
        public static void DrawEnableReportSize()  
            => DrawCheckbox(Label("Copy Sizes Stats when reporting.", "报告时复制尺寸统计。"),
                Label("When copying the report, add min and max sizes to the report.", "复制报告时，将最小和最大尺寸添加到报告中。"),
                GatherBuddy.Config.EnableReportSize, b => GatherBuddy.Config.EnableReportSize = b);
        public static void DrawEnableReportMulti() 
            => DrawCheckbox(Label("Copy Multi Hook Stats when reporting.", "报告时复制多重提钩统计。"),
                Label("When copying the report, add stats about multi-hook yields to the report.", "复制报告时，将多重提钩产量统计添加到报告中。"),
                GatherBuddy.Config.EnableReportMulti, b => GatherBuddy.Config.EnableReportMulti = b);
        public static void DrawEnableGraphs()      
            => DrawCheckbox(Label("Enable Graphs.", "启用图表。"),
                Label("When viewing a fishing spot, enable visualization of fish report data. Extreme Testing!", "查看钓场时，启用鱼类报告数据的可视化。极端测试！"),
                GatherBuddy.Config.EnableFishStatsGraphs, b => GatherBuddy.Config.EnableFishStatsGraphs = b);

        // Spearfishing Helper
        public static void DrawSpearfishHelperBox()
            => DrawCheckbox(Label("Show Spearfishing Helper", "显示鱼叉助手"),
                Label("Toggle whether to show the Spearfishing Helper while spearfishing.", "切换是否在鱼叉钓鱼时显示鱼叉助手。"),
                GatherBuddy.Config.ShowSpearfishHelper, b => GatherBuddy.Config.ShowSpearfishHelper = b);

        public static void DrawSpearfishNamesBox()
            => DrawCheckbox(Label("Show Fish Name Overlay", "显示鱼类名称覆盖"),
                Label("Toggle whether to show the identified names of fish in the spearfishing window.", "切换是否在鱼叉窗口中显示识别的鱼类名称。"),
                GatherBuddy.Config.ShowSpearfishNames, b => GatherBuddy.Config.ShowSpearfishNames = b);

        public static void DrawAvailableSpearfishBox()
            => DrawCheckbox(Label("Show List of Available Fish", "显示可用鱼类列表"),
                Label("Toggle whether to show the list of fish available in your current spearfishing spot on the side of the spearfishing window.", "切换是否在鱼叉窗口侧边显示当前鱼叉钓场可用的鱼类列表。"),
                GatherBuddy.Config.ShowAvailableSpearfish, b => GatherBuddy.Config.ShowAvailableSpearfish = b);

        public static void DrawSpearfishSpeedBox()
            => DrawCheckbox(Label("Show Speed of Fish in Overlay", "在覆盖中显示鱼类速度"),
                Label("Toggle whether to show the speed of fish in the spearfishing window in addition to their names.", "切换是否在鱼叉窗口中显示鱼类的速度及其名称。"),
                GatherBuddy.Config.ShowSpearfishSpeed, b => GatherBuddy.Config.ShowSpearfishSpeed = b);

        public static void DrawSpearfishCenterLineBox()
            => DrawCheckbox(Label("Show Center Line", "显示中心线"),
                Label("Toggle whether to show a straight line up from the center of the spearfishing gig in the spearfishing window.", "切换是否在鱼叉窗口中从鱼叉中心向上显示一条直线。"),
                GatherBuddy.Config.ShowSpearfishCenterLine, b => GatherBuddy.Config.ShowSpearfishCenterLine = b);

        public static void DrawSpearfishIconsAsTextBox()
            => DrawCheckbox(Label("Show Speed and Size as Text", "显示速度和尺寸为文本"),
                Label("Toggle whether to show the speed and size of available fish as text instead of icons.", "切换是否将可用鱼类的速度和尺寸显示为文本而非图标。"),
                GatherBuddy.Config.ShowSpearfishListIconsAsText, b => GatherBuddy.Config.ShowSpearfishListIconsAsText = b);

        public static void DrawSpearfishFishNameFixed()
            => DrawCheckbox(Label("Show Fish Names in Fixed Position", "在固定位置显示鱼类名称"),
                Label("Toggle whether to show the identified names of fish on the moving fish themselves or in a fixed position.", "切换是否在移动的鱼上显示识别的鱼类名称或在固定位置显示。"),
                GatherBuddy.Config.FixNamesOnPosition, b => GatherBuddy.Config.FixNamesOnPosition = b);

        public static void DrawSpearfishFishNamePercentage()
        {
            if (!GatherBuddy.Config.FixNamesOnPosition)
                return;

            var tmp = (int)GatherBuddy.Config.FixNamesPercentage;
            ImGui.SetNextItemWidth(SetInputWidth);
            if (!ImGui.DragInt(Label("Fish Name Position Percentage", "鱼类名称位置百分比"), ref tmp, 0.1f, 0, 100, "%i%%"))
                return;

            tmp = Math.Clamp(tmp, 0, 100);
            if (tmp == GatherBuddy.Config.FixNamesPercentage)
                return;

            GatherBuddy.Config.FixNamesPercentage = (byte)tmp;
            GatherBuddy.Config.Save();
        }

        // Gather Window
        public static void DrawShowGatherWindowBox()
            => DrawCheckbox(Label("Show Gather Window", "显示采集窗口"),
                Label("Show a small window with pinned Gatherables and their uptimes.", "显示一个小窗口，显示固定的采集品及其出现时间。"),
                GatherBuddy.Config.ShowGatherWindow, b => GatherBuddy.Config.ShowGatherWindow = b);

        public static void DrawGatherWindowAnchorBox()
            => DrawCheckbox(Label("Anchor Gather Window to Bottom Left", "将采集窗口锉定到左下角"),
                Label("Lets the Gather Window grow to the top and shrink from the top instead of the bottom.", "让采集窗口向上增长并从顶部收缩而不是从底部。"),
                GatherBuddy.Config.GatherWindowBottomAnchor, b => GatherBuddy.Config.GatherWindowBottomAnchor = b);

        public static void DrawGatherWindowTimersBox()
            => DrawCheckbox(Label("Show Gather Window Timers", "显示采集窗口计时器"),
                Label("Show the uptimes for gatherables in the gather window.", "在采集窗口中显示采集品的出现时间。"),
                GatherBuddy.Config.ShowGatherWindowTimers, b => GatherBuddy.Config.ShowGatherWindowTimers = b);

        public static void DrawGatherWindowAlarmsBox()
            => DrawCheckbox(Label("Show Active Alarms in Gather Window", "在采集窗口中显示活动闹钟"),
                Label("Additionally show active alarms as a last gather window preset, obeying the regular rules for the window.", "将活动闹钟显示为最后一个采集窗口预设，遵守窗口的常规规则。"),
                GatherBuddy.Config.ShowGatherWindowAlarms, b =>
                {
                    GatherBuddy.Config.ShowGatherWindowAlarms = b;
                    _plugin.GatherWindowManager.SetShowGatherWindowAlarms(b);
                });

        public static void DrawSortGatherWindowBox()
            => DrawCheckbox(Label("Sort Gather Window by Uptime", "按出现时间排序采集窗口"),
                Label("Sort the items selected for the gather window by their uptimes.", "按出现时间对采集窗口选择的物品进行排序。"),
                GatherBuddy.Config.SortGatherWindowByUptime, b => GatherBuddy.Config.SortGatherWindowByUptime = b);

        public static void DrawGatherWindowShowOnlyAvailableBox()
            => DrawCheckbox(Label("Show Only Available Items", "仅显示可用物品"),
                Label("Show only those items from your gather window setup that are currently available.", "仅显示采集窗口设置中当前可用的物品。"),
                GatherBuddy.Config.ShowGatherWindowOnlyAvailable, b => GatherBuddy.Config.ShowGatherWindowOnlyAvailable = b);

        public static void DrawHideGatherWindowCompletedItemsBox()
            => DrawCheckbox(Label("Hide Completed Items", "隐藏已完成物品"),
                Label("Hide items that have the required inventory amount present in inventory.", "隐藏库存中已有所需数量的物品。"),
                GatherBuddy.Config.HideGatherWindowCompletedItems, b => GatherBuddy.Config.HideGatherWindowCompletedItems = b);

        public static void DrawHideGatherWindowInDutyBox()
            => DrawCheckbox(Label("Hide Gather Window in Duty", "在副本中隐藏采集窗口"),
                Label("Hide the gather window when bound by any duty.", "在任何副本中隐藏采集窗口。"),
                GatherBuddy.Config.HideGatherWindowInDuty, b => GatherBuddy.Config.HideGatherWindowInDuty = b);

        public static void DrawGatherWindowHoldKey()
        {
            DrawCheckbox(Label("Only Show Gather Window if Holding Key", "仅在按住按键时显示采集窗口"),
                Label("Only show the gather window if you are holding your selected key.", "仅在按住所选按键时显示采集窗口。"),
                GatherBuddy.Config.OnlyShowGatherWindowHoldingKey, b => GatherBuddy.Config.OnlyShowGatherWindowHoldingKey = b);

            if (!GatherBuddy.Config.OnlyShowGatherWindowHoldingKey)
                return;

            ImGui.SetNextItemWidth(SetInputWidth);
            Widget.KeySelector(Label("Hotkey to Hold", "按住的快捷键"), Label("Set the hotkey to hold to keep the window visible.", "设置要按住以保持窗口可见的快捷键。"),
                GatherBuddy.Config.GatherWindowHoldKey,
                k => GatherBuddy.Config.GatherWindowHoldKey = k, Configuration.ValidKeys);
        }

        public static void DrawGatherWindowLockBox()
            => DrawCheckbox(Label("Lock Gather Window Position", "锁定采集窗口位置"),
                Label("Prevent moving the gather window by dragging it around.", "防止通过拖动移动采集窗口。"),
                GatherBuddy.Config.LockGatherWindow, b => GatherBuddy.Config.LockGatherWindow = b);


        public static void DrawGatherWindowHotkeyInput()
        {
            if (Widget.ModifiableKeySelector(Label("Hotkey to Open Gather Window", "打开采集窗口的快捷键"), Label("Set a hotkey to open the Gather Window.", "设置打开采集窗口的快捷键。"), SetInputWidth,
                    GatherBuddy.Config.GatherWindowHotkey, k => GatherBuddy.Config.GatherWindowHotkey = k, Configuration.ValidKeys))
                GatherBuddy.Config.Save();
        }

        public static void DrawMainInterfaceHotkeyInput()
        {
            if (Widget.ModifiableKeySelector(Label("Hotkey to Open Main Interface", "打开主界面的快捷键"), Label("Set a hotkey to open the main GatherBuddy interface.", "设置打开GatherBuddy主界面的快捷键。"),
                    SetInputWidth,
                    GatherBuddy.Config.MainInterfaceHotkey, k => GatherBuddy.Config.MainInterfaceHotkey = k, Configuration.ValidKeys))
                GatherBuddy.Config.Save();
        }


        public static void DrawGatherWindowDeleteModifierInput()
        {
            ImGui.SetNextItemWidth(SetInputWidth);
            if (Widget.ModifierSelector(Label("Modifier to Delete Items on Right-Click", "右键删除物品的修饰键"),
                    Label("Set the modifier key to be used while right-clicking items in the gather window to delete them.", "设置在采集窗口中右键删除物品时要使用的修饰键。"),
                    GatherBuddy.Config.GatherWindowDeleteModifier, k => GatherBuddy.Config.GatherWindowDeleteModifier = k))
                GatherBuddy.Config.Save();
        }


        public static void DrawAetherytePreference()
        {
            var tmp     = GatherBuddy.Config.AetherytePreference == AetherytePreference.Cost;
            var oldPref = GatherBuddy.Config.AetherytePreference;
            if (ImGui.RadioButton(Label("Prefer Cheaper Aetherytes", "优先选择更便宜的以太之光"), tmp))
                GatherBuddy.Config.AetherytePreference = AetherytePreference.Cost;
            var hovered = ImGui.IsItemHovered();
            ImGui.SameLine();
            if (ImGui.RadioButton(Label("Prefer Less Travel Time", "优先选择更短移动时间"), !tmp))
                GatherBuddy.Config.AetherytePreference = AetherytePreference.Distance;
            hovered |= ImGui.IsItemHovered();
            if (hovered)
                ImGui.SetTooltip(
                    Label("Specify whether you prefer aetherytes that are closer to your target (less travel time)"
                  + " or aetherytes that are cheaper to teleport to when scanning through all available nodes for an item."
                  + " Only matters if the item is not timed and has multiple sources.",
                    "指定你是否偏好更靠近目标的以太之光（更短移动时间）"
                  + "或者在扫描物品的所有可用节点时偏好传送更便宜的以太之光。"
                  + "仅当物品不是限时且有多个来源时才有影响。"));

            if (oldPref != GatherBuddy.Config.AetherytePreference)
            {
                GatherBuddy.UptimeManager.ResetLocations();
                GatherBuddy.Config.Save();
            }
        }

        public static void DrawAlarmFormatInput()
            => DrawFormatInput("Alarm Chat Format",
                "Keep empty to have no chat output.\nCan replace:\n- {Alarm} with the alarm name in brackets.\n- {Item} with the item link.\n- {Offset} with the alarm offset in seconds.\n- {DurationString} with 'will be up for the next ...' or 'is currently up for ...'.\n- {Location} with the map flag link and location name.",
                GatherBuddy.Config.AlarmFormat, Configuration.DefaultAlarmFormat, s => GatherBuddy.Config.AlarmFormat = s);

        public static void DrawIdentifiedGatherableFormatInput()
            => DrawFormatInput("Identified Gatherable Chat Format",
                "Keep empty to have no chat output.\nCan replace:\n- {Input} with the entered search text.\n- {Item} with the item link.",
                GatherBuddy.Config.IdentifiedGatherableFormat, Configuration.DefaultIdentifiedGatherableFormat,
                s => GatherBuddy.Config.IdentifiedGatherableFormat = s);

        public static void DrawAlwaysMapsBox()
            => DrawCheckbox("可用时始终采集地图",      "GBR在节点中看到地图时会优先采集",
                GatherBuddy.Config.AutoGatherConfig.AlwaysGatherMaps, b => GatherBuddy.Config.AutoGatherConfig.AlwaysGatherMaps = b);

        public static void DrawUseExistingAutoHookPresetsBox()
        {
            DrawCheckbox("Use existing AutoHook presets",
                "Use your own AutoHook presets instead of GBR-generated ones.\n"
              + "Name your preset using the fish's Item ID (e.g., '46188' for Goldentail).\n"
              + "Find Fish IDs by hovering over fish in the Fish tab.\n"
              + "Your presets will never be deleted - only GBR-generated presets are cleaned up.",
                GatherBuddy.Config.AutoGatherConfig.UseExistingAutoHookPresets,
                b => GatherBuddy.Config.AutoGatherConfig.UseExistingAutoHookPresets = b);
            ImGui.SameLine();
            ImGuiEx.PluginAvailabilityIndicator([new("AutoHook")]);
        }

        public static void DrawSurfaceSlapConfig()
        {
            DrawCheckbox("Enable automatic Surface Slap",
                "Automatically enable Surface Slap for non-target fish that share the same bite type as your target fish.\n"
              + "This helps remove unwanted fish to increase catch rates of your target.",
                GatherBuddy.Config.AutoGatherConfig.EnableSurfaceSlap,
                b => GatherBuddy.Config.AutoGatherConfig.EnableSurfaceSlap = b);
            
            if (GatherBuddy.Config.AutoGatherConfig.EnableSurfaceSlap)
            {
                ImGui.Indent();
                
                var gpAbove = GatherBuddy.Config.AutoGatherConfig.SurfaceSlapGPAbove;
                if (ImGui.RadioButton("Use Surface Slap when GP is Above", gpAbove))
                {
                    GatherBuddy.Config.AutoGatherConfig.SurfaceSlapGPAbove = true;
                    GatherBuddy.Config.Save();
                }
                
                ImGui.SameLine();
                if (ImGui.RadioButton("Below", !gpAbove))
                {
                    GatherBuddy.Config.AutoGatherConfig.SurfaceSlapGPAbove = false;
                    GatherBuddy.Config.Save();
                }
                
                var gpThreshold = GatherBuddy.Config.AutoGatherConfig.SurfaceSlapGPThreshold;
                ImGui.SetNextItemWidth(SetInputWidth);
                if (ImGui.DragInt("GP Threshold", ref gpThreshold, 1, 0, 10000))
                {
                    GatherBuddy.Config.AutoGatherConfig.SurfaceSlapGPThreshold = Math.Max(0, gpThreshold);
                    GatherBuddy.Config.Save();
                }
                ImGuiUtil.HoverTooltip("当你的GP高于/低于此阈值时将使用水面拍击。");
                
                ImGui.Unindent();
            }
        }

        public static void DrawIdenticalCastConfig()
        {
            DrawCheckbox("Enable automatic Identical Cast",
                "Automatically enable Identical Cast for your target fish to increase catch rates.\n"
              + "Identical Cast improves catch rate when used on the same fishing hole.",
                GatherBuddy.Config.AutoGatherConfig.EnableIdenticalCast,
                b => GatherBuddy.Config.AutoGatherConfig.EnableIdenticalCast = b);
            
            if (GatherBuddy.Config.AutoGatherConfig.EnableIdenticalCast)
            {
                ImGui.Indent();
                
                var gpAbove = GatherBuddy.Config.AutoGatherConfig.IdenticalCastGPAbove;
                if (ImGui.RadioButton("Use Identical Cast when GP is Above", gpAbove))
                {
                    GatherBuddy.Config.AutoGatherConfig.IdenticalCastGPAbove = true;
                    GatherBuddy.Config.Save();
                }
                
                ImGui.SameLine();
                if (ImGui.RadioButton("Below##IdenticalCast", !gpAbove))
                {
                    GatherBuddy.Config.AutoGatherConfig.IdenticalCastGPAbove = false;
                    GatherBuddy.Config.Save();
                }
                
                var gpThreshold = GatherBuddy.Config.AutoGatherConfig.IdenticalCastGPThreshold;
                ImGui.SetNextItemWidth(SetInputWidth);
                if (ImGui.DragInt("GP Threshold##IdenticalCast", ref gpThreshold, 1, 0, 10000))
                {
                    GatherBuddy.Config.AutoGatherConfig.IdenticalCastGPThreshold = Math.Max(0, gpThreshold);
                    GatherBuddy.Config.Save();
                }
                ImGuiUtil.HoverTooltip("当你的GP高于/低于此阈值时将使用相同抛竿。");
                
                ImGui.Unindent();
            }
        }

        public static void DrawUseHookTimersBox()
        {
            DrawCheckbox("在AutoHook预设中使用提钩计时器",
                "在生成的AutoHook预设中启用咬钩计时窗口。",
                GatherBuddy.Config.AutoGatherConfig.UseHookTimers,
                b => GatherBuddy.Config.AutoGatherConfig.UseHookTimers = b);
            ImGui.SameLine();
            ImGuiEx.PluginAvailabilityIndicator([new("AutoHook")]);
        }

        public static void DrawAutoCollectablesFishingBox()
            => DrawCheckbox("自动收藏品",
                "根据最低收藏价值自动接受/拒绝收藏品鱼类。",
                GatherBuddy.Config.AutoGatherConfig.AutoCollectablesFishing,
                b => GatherBuddy.Config.AutoGatherConfig.AutoCollectablesFishing = b);
        
        public static void DrawDiademAutoAetherCannonBox()
            => DrawCheckbox(Label("Diadem Auto-Aethercannon", "云海自动以太加农炮"),
                Label("Automatically target and fire aethercannon at nearby enemies when gauge is ready (≥200).\n"
              + "Only fires while not pathing/navigating. 2-second cooldown between uses.",
                "当能量槽就绪时（≥200）自动锁定并向附近敌人发射以太加农炮。\n"
              + "仅在未寻路/导航时发射。使用间隔2秒冷却。"),
                GatherBuddy.Config.AutoGatherConfig.DiademAutoAetherCannon,
                b => GatherBuddy.Config.AutoGatherConfig.DiademAutoAetherCannon = b);
        
        public static void DrawCollectOnAutogatherDisabledBox()
            => DrawCheckbox(Label("Turn in collectables when AutoGather stops", "自动采集停止时交付收藏品"),
                Label("Automatically turn in collectables when AutoGather is disabled",
                "当自动采集禁用时自动交付收藏品"),
                GatherBuddy.Config.CollectableConfig.CollectOnAutogatherDisabled,
                b => GatherBuddy.Config.CollectableConfig.CollectOnAutogatherDisabled = b);
        
        public static void DrawEnableAutogatherOnFinishBox()
            => DrawCheckbox(Label("Re-enable AutoGather after turning in", "交付后重新启用自动采集"),
                Label("Automatically re-enable AutoGather after collectable turn-in completes",
                "收藏品交付完成后自动重新启用自动采集"),
                GatherBuddy.Config.CollectableConfig.EnableAutogatherOnFinish,
                b => GatherBuddy.Config.CollectableConfig.EnableAutogatherOnFinish = b);
        
        public static void DrawBuyAfterEachCollectBox()
            => DrawCheckbox(Label("Buy scrip shop items after each turn-in", "每次交付后购买票据商店物品"),
                Label("Automatically purchase scrip shop items after turning in collectables",
                "交付收藏品后自动购买票据商店物品"),
                GatherBuddy.Config.CollectableConfig.BuyAfterEachCollect,
                b => GatherBuddy.Config.CollectableConfig.BuyAfterEachCollect = b);
        
        public static void DrawScripShopItemManager()
        {
            var shopItems = ScripShopItemManager.ShopItems;
            var purchaseList = GatherBuddy.Config.CollectableConfig.ScripShopItems;
            
            ImGui.TextUnformatted(Label("Items in purchase queue:", "购买队列中的物品："));
            ImGui.Spacing();
            
            if (purchaseList.Count == 0)
            {
                ImGui.TextDisabled(Label("No items in queue. Add items below.", "队列中没有物品。请在下方添加物品。"));
            }
            else
            {
                ItemToPurchase? toRemove = null;
                
                foreach (var purchaseItem in purchaseList)
                {
                    using var id = ImRaii.PushId($"{purchaseItem.Name}");
                    
                    if (purchaseItem.Item != null && purchaseItem.Item.IconTexture.TryGetWrap(out var wrap, out _))
                    {
                        ImGui.Image(wrap.Handle, new Vector2(24, 24));
                        ImGui.SameLine();
                    }
                    
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text($"{purchaseItem.Name}");
                    ImGui.SameLine(300);
                    
                    unsafe
                    {
                        var inventory = FFXIVClientStructs.FFXIV.Client.Game.InventoryManager.Instance();
                        var currentInventory = purchaseItem.Item != null ? inventory->GetInventoryItemCount(purchaseItem.Item.ItemId) : 0;
                        ImGui.Text($"{currentInventory}");
                    }
                    
                    ImGui.SameLine();
                    ImGui.Text("/");
                    ImGui.SameLine();
                    
                    var quantity = purchaseItem.Quantity;
                    ImGui.SetNextItemWidth(80);
                    if (ImGui.InputInt($"##{purchaseItem.Name}_Quantity", ref quantity, 1, 10))
                    {
                        purchaseItem.Quantity = Math.Max(0, quantity);
                        GatherBuddy.Config.Save();
                    }
                    
                    ImGui.SameLine();
                    if (ImGui.Button(Label($"Remove##{purchaseItem.Name}", $"移除##{purchaseItem.Name}")))
                    {
                        toRemove = purchaseItem;
                    }
                }
                
                if (toRemove != null)
                {
                    purchaseList.Remove(toRemove);
                    GatherBuddy.Config.Save();
                }
            }
            
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            ImGui.TextUnformatted(Label("Add Item:", "添加物品："));
            
            if (shopItems.Count() == 0)
            {
                ImGui.TextDisabled(Label("No scrip shop items available. Data may not be loaded.", "没有可用的票据商店物品。数据可能未加载。"));
            }
            else
            {
                if (ImGui.BeginCombo("###AddScripShopItem", Label("Select item...", "选择物品...")))
                {
                    ImGui.SetNextItemWidth(SetInputWidth - 20);
                    ImGui.InputTextWithHint("###ScripShopFilter", Label("Search...", "搜索..."), ref _scripShopFilterText, 100);
                    ImGui.Separator();
                    
                    foreach (var item in shopItems)
                    {
                        if (_scripShopFilterText.Length > 0 && !item.Name.Contains(_scripShopFilterText, StringComparison.OrdinalIgnoreCase))
                            continue;
                        
                        using var id = ImRaii.PushId($"AddItem_{item.Name}");
                        
                        var alreadyAdded = purchaseList.Any(p => p.Name == item.Name);
                        if (alreadyAdded)
                        {
                            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.5f);
                        }
                        
                        if (ImGui.Selectable(item.Name, false, alreadyAdded ? ImGuiSelectableFlags.Disabled : ImGuiSelectableFlags.None))
                        {
                            if (!alreadyAdded)
                            {
                                purchaseList.Add(new ItemToPurchase { Item = item, Quantity = 1 });
                                GatherBuddy.Config.Save();
                                _scripShopFilterText = "";
                            }
                        }
                        
                        if (alreadyAdded)
                        {
                            ImGui.PopStyleVar();
                        }
                    }
                    
                    ImGui.EndCombo();
                }
            }
        }
        
        public static void DrawManualPresetGenerator()
        {
            ImGui.Separator();
            ImGui.TextUnformatted("Manual Preset Generator");
            ImGui.Spacing();
            
            var availableFish = GatherBuddy.GameData.Fishes.Values.Where(f => !f.IsSpearFish).ToList();
            
            ImGui.TextUnformatted("Select Target Fish:");
            ImGui.SetNextItemWidth(SetInputWidth);
            
            if (ImGui.BeginCombo("###FishSelector", _selectedFish?.Name[GatherBuddy.Language] ?? "None"))
            {
                ImGui.SetNextItemWidth(SetInputWidth - 20);
                ImGui.InputTextWithHint("###FishFilter", "Search...", ref _fishFilterText, 100);
                ImGui.Separator();
                
                using (var child = ImRaii.Child("###FishList", new Vector2(0, 200 * ImGuiHelpers.GlobalScale), false))
                {
                    for (int i = 0; i < availableFish.Count; i++)
                    {
                        var fish = availableFish[i];
                        var fishName = fish.Name[GatherBuddy.Language];
                        
                        if (_fishFilterText.Length > 0 && !fishName.ToLower().Contains(_fishFilterText.ToLower()))
                            continue;
                        
                        using var id = ImRaii.PushId($"{fish.ItemId}###{i}");
                        if (ImGui.Selectable(fishName, _selectedFish?.ItemId == fish.ItemId))
                        {
                            _selectedFish = fish;
                            _presetName = fish.ItemId.ToString();
                            _fishFilterText = "";
                            ImGui.CloseCurrentPopup();
                        }
                    }
                }
                
                ImGui.EndCombo();
            }
            
            if (_selectedFish != null)
            {
                ImGui.Spacing();
                ImGui.TextUnformatted("Preset Name:");
                ImGui.SetNextItemWidth(SetInputWidth);
                ImGui.InputText("###PresetNameInput", ref _presetName, 64);
                ImGuiUtil.HoverTooltip("预设名称应与鱼的物品ID匹配，以便GBR自动使用。");
                
                ImGui.Spacing();
                if (ImGui.Button("Generate Preset"))
                {
                    GenerateManualPreset(_selectedFish, _presetName);
                }
            }
        }
        
        private static void GenerateManualPreset(Fish fish, string presetName)
        {
            if (string.IsNullOrWhiteSpace(presetName))
                presetName = fish.ItemId.ToString();
            
            var success = AutoHookIntegration.AutoHookService.ExportPresetToAutoHook(presetName, [fish]);
            
            if (success)
            {
                Dalamud.Chat.Print($"[GatherBuddy] Generated preset '{presetName}' for {fish.Name[GatherBuddy.Language]}");
            }
            else
            {
                Dalamud.Chat.PrintError($"[GatherBuddy] Failed to generate preset for {fish.Name[GatherBuddy.Language]}");
            }
        }
    }


    private void DrawConfigTab()
    {
        using var id  = ImRaii.PushId("Config");
        using var tab = ImRaii.TabItem(Label("Config", "配置"));
        ImGuiUtil.HoverTooltip("根据你的精确規格设置你自己的GatherBuddy。\n"
          + "如果你待他好，他甚至可能变成一个真正的男孩。");

        if (!tab)
            return;

        using var child = ImRaii.Child("ConfigTab");
        if (!child)
            return;

        if (ImGui.CollapsingHeader(Label("Auto-Gather", "自动采集")))
        {
            if (ImGui.TreeNodeEx(Label("General##autoGeneral", "通用##autoGeneral")))
            {
                AutoGatherUI.DrawMountSelector();
                ConfigFunctions.DrawMountUpDistance();
                ConfigFunctions.DrawMoveWhileMounting();
                ConfigFunctions.DrawHonkModeBox();
                if (GatherBuddy.Config.AutoGatherConfig.HonkMode)
                {
                    ConfigFunctions.DrawHonkVolumeSlider();
                }
                ConfigFunctions.DrawCheckRetainersBox();
                ConfigFunctions.DrawGoHomeBox();
                ConfigFunctions.DrawUseGivingLandOnCooldown();
                ConfigFunctions.DrawUseSkillsForFallabckBox();
                ConfigFunctions.DrawAbandonNodesBox();
                ConfigFunctions.DrawAlwaysMapsBox();
                ImGui.TreePop();
            }

            if (ImGui.TreeNodeEx(Label("Fishing", "钓鱼")))
            {
                ConfigFunctions.DrawUseExistingAutoHookPresetsBox();
                ConfigFunctions.DrawFishingSpotMinutes();
                ConfigFunctions.DrawFishCollectionBox();
                ConfigFunctions.DrawAutoCollectablesFishingBox();
                ConfigFunctions.DrawUseHookTimersBox();
                ConfigFunctions.DrawSurfaceSlapConfig();
                ConfigFunctions.DrawIdenticalCastConfig();
                ConfigFunctions.DrawManualPresetGenerator();
                ImGui.TreePop();
            }

            if (ImGui.TreeNodeEx(Label("Advanced", "高级")))
            {
                ConfigFunctions.DrawRepairBox();
                if (GatherBuddy.Config.AutoGatherConfig.DoRepair)
                {
                    ConfigFunctions.DrawRepairThreshold();
                }
                ConfigFunctions.DrawMaterialExtraction();
                ConfigFunctions.DrawAetherialReduction();
                ConfigFunctions.DrawAutoretainerBox();
                if (GatherBuddy.Config.AutoGatherConfig.AutoRetainerMultiMode)
                {
                    ConfigFunctions.DrawAutoretainerThreshold();
                    ConfigFunctions.DrawAutoretainerTimedNodeDelayBox();
                }
                ConfigFunctions.DrawDiademAutoAetherCannonBox();
                ConfigFunctions.DrawSortingMethodCombo();
                ConfigFunctions.DrawLifestreamCommandTextInput();
                ConfigFunctions.DrawAntiStuckCooldown();
                ConfigFunctions.DrawStuckThreshold();
                ConfigFunctions.DrawTimedNodePrecog();
                ConfigFunctions.DrawExecutionDelay();
                ConfigFunctions.DrawAutoGatherBox();
                ConfigFunctions.DrawUseFlagBox();
                ConfigFunctions.DrawUseNavigationBox();
                ConfigFunctions.DrawForceWalkingBox();
                ImGui.TreePop();
            }
            
            if (ImGui.TreeNodeEx(Label("Collectable", "收藏品")))
            {
                ConfigFunctions.DrawCollectOnAutogatherDisabledBox();
                ConfigFunctions.DrawEnableAutogatherOnFinishBox();
                ConfigFunctions.DrawBuyAfterEachCollectBox();
                
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
                
                if (ImGui.CollapsingHeader(Label("Scrip Shop Purchase List", "票据商店购买清单")))
                {
                    ConfigFunctions.DrawScripShopItemManager();
                }
                
                ImGui.TreePop();
            }
        }

        if (ImGui.CollapsingHeader(Label("General", "通用")))
        {
            if (ImGui.TreeNodeEx(Label("Gather Command", "采集命令")))
            {
                ConfigFunctions.DrawPreferredJobSelect();
                ConfigFunctions.DrawGearChangeBox();
                ConfigFunctions.DrawTeleportBox();
                ConfigFunctions.DrawMapOpenBox();
                ConfigFunctions.DrawPlaceMarkerBox();
                ConfigFunctions.DrawPlaceWaymarkBox();
                ConfigFunctions.DrawAetherytePreference();
                ConfigFunctions.DrawSkipTeleportBox();
                ConfigFunctions.DrawContextMenuBox();
                ImGui.TreePop();
            }

            if (ImGui.TreeNodeEx(Label("Set Names", "套装名称")))
            {
                ConfigFunctions.DrawSetInput("Miner",    GatherBuddy.Config.MinerSetName,    s => GatherBuddy.Config.MinerSetName    = s);
                ConfigFunctions.DrawSetInput("Botanist", GatherBuddy.Config.BotanistSetName, s => GatherBuddy.Config.BotanistSetName = s);
                ConfigFunctions.DrawSetInput("Fisher",   GatherBuddy.Config.FisherSetName,   s => GatherBuddy.Config.FisherSetName   = s);
                ImGui.TreePop();
            }

            if (ImGui.TreeNodeEx(Label("Alarms", "闹钟")))
            {
                ConfigFunctions.DrawAlarmToggle();
                ConfigFunctions.DrawAlarmsInDutyToggle();
                ConfigFunctions.DrawAlarmsOnlyWhenLoggedInToggle();
                ConfigFunctions.DrawWeatherAlarmPicker();
                ConfigFunctions.DrawHourAlarmPicker();
                ImGui.TreePop();
            }

            if (ImGui.TreeNodeEx(Label("Messages", "消息")))
            {
                ConfigFunctions.DrawPrintTypeSelector();
                ConfigFunctions.DrawErrorTypeSelector();
                ConfigFunctions.DrawMapMarkerPrintBox();
                ConfigFunctions.DrawPrintUptimesBox();
                ConfigFunctions.DrawPrintClipboardBox();
                ConfigFunctions.DrawAlarmFormatInput();
                ConfigFunctions.DrawIdentifiedGatherableFormatInput();
                ImGui.TreePop();
            }

            ImGui.NewLine();
        }

        if (ImGui.CollapsingHeader(Label("Interface", "界面")))
        {
            if (ImGui.TreeNodeEx(Label("Config Window", "配置窗口")))
            {
                ConfigFunctions._base = this;
                ConfigFunctions.DrawOpenOnStartBox();
                ConfigFunctions.DrawRespectEscapeBox();
                ConfigFunctions.DrawLockPositionBox();
                ConfigFunctions.DrawLockResizeBox();
                ConfigFunctions.DrawWeatherTabNamesBox();
                ConfigFunctions.DrawShowStatusLineBox();
                ConfigFunctions.DrawHideClippyBox();
                ConfigFunctions.DrawMainInterfaceHotkeyInput();
                ImGui.TreePop();
            }

            if (ImGui.TreeNodeEx(Label("Fish Timer", "鱼类计时器")))
            {
                ConfigFunctions.DrawKeepRecordsBox();
                ConfigFunctions.DrawShowLocalTimeInRecordsBox();
                ConfigFunctions.DrawFishTimerBox();
                ConfigFunctions.DrawFishTimerEditBox();
                ConfigFunctions.DrawFishTimerClickthroughBox();
                ConfigFunctions.DrawFishTimerHideBox();
                ConfigFunctions.DrawFishTimerHideBox2();
                ConfigFunctions.DrawFishTimerUptimesBox();
                ConfigFunctions.DrawFishTimerScale();
                ConfigFunctions.DrawFishTimerIntervals();
                ConfigFunctions.DrawFishTimerIntervalsRounding();
                ConfigFunctions.DrawHideFishPopupBox();
                ConfigFunctions.DrawCollectableHintPopupBox();
                ConfigFunctions.DrawDoubleHookHintPopupBox();
                ImGui.TreePop();
            }

            if (ImGui.TreeNodeEx(Label("Fish Stats [Testing]", "鱼类统计 [测试]")))
            {
                ConfigFunctions.DrawEnableFishStats();
                ConfigFunctions.DrawEnableReportTime();
                ConfigFunctions.DrawEnableReportSize();
                ConfigFunctions.DrawEnableReportMulti();
                ConfigFunctions.DrawEnableGraphs();
                ImGui.TreePop();
            }

            if (ImGui.TreeNodeEx(Label("Gather Window", "采集窗口")))
            {
                ConfigFunctions.DrawShowGatherWindowBox();
                ConfigFunctions.DrawGatherWindowAnchorBox();
                ConfigFunctions.DrawGatherWindowTimersBox();
                ConfigFunctions.DrawGatherWindowAlarmsBox();
                ConfigFunctions.DrawSortGatherWindowBox();
                ConfigFunctions.DrawGatherWindowShowOnlyAvailableBox();
                ConfigFunctions.DrawHideGatherWindowCompletedItemsBox();
                ConfigFunctions.DrawHideGatherWindowInDutyBox();
                ConfigFunctions.DrawGatherWindowHoldKey();
                ConfigFunctions.DrawGatherWindowLockBox();
                ConfigFunctions.DrawGatherWindowHotkeyInput();
                ConfigFunctions.DrawGatherWindowDeleteModifierInput();
                ImGui.TreePop();
            }

            if (ImGui.TreeNodeEx(Label("Spearfishing Helper", "鱼叉助手")))
            {
                ConfigFunctions.DrawSpearfishHelperBox();
                ConfigFunctions.DrawSpearfishNamesBox();
                ConfigFunctions.DrawSpearfishSpeedBox();
                ConfigFunctions.DrawAvailableSpearfishBox();
                ConfigFunctions.DrawSpearfishIconsAsTextBox();
                ConfigFunctions.DrawSpearfishCenterLineBox();
                ConfigFunctions.DrawSpearfishFishNameFixed();
                ConfigFunctions.DrawSpearfishFishNamePercentage();
                ImGui.TreePop();
            }

            ImGui.NewLine();
        }

        if (ImGui.CollapsingHeader(Label("Colors", "颜色")))
        {
            foreach (var color in Enum.GetValues<ColorId>())
            {
                var (defaultColor, name, description) = color.Data();
                var currentColor = GatherBuddy.Config.Colors.TryGetValue(color, out var current) ? current : defaultColor;
                if (Widget.ColorPicker(name, description, currentColor, c => GatherBuddy.Config.Colors[color] = c, defaultColor))
                    GatherBuddy.Config.Save();
            }

            ImGui.NewLine();

            if (Widget.PaletteColorPicker("Names in Chat", Vector2.One * ImGui.GetFrameHeight(), GatherBuddy.Config.SeColorNames,
                    Configuration.DefaultSeColorNames, Configuration.ForegroundColors, out var idx))
                GatherBuddy.Config.SeColorNames = idx;
            if (Widget.PaletteColorPicker("Commands in Chat", Vector2.One * ImGui.GetFrameHeight(), GatherBuddy.Config.SeColorCommands,
                    Configuration.DefaultSeColorCommands, Configuration.ForegroundColors, out idx))
                GatherBuddy.Config.SeColorCommands = idx;
            if (Widget.PaletteColorPicker("Arguments in Chat", Vector2.One * ImGui.GetFrameHeight(), GatherBuddy.Config.SeColorArguments,
                    Configuration.DefaultSeColorArguments, Configuration.ForegroundColors, out idx))
                GatherBuddy.Config.SeColorArguments = idx;
            if (Widget.PaletteColorPicker("Alarm Message in Chat", Vector2.One * ImGui.GetFrameHeight(), GatherBuddy.Config.SeColorAlarm,
                    Configuration.DefaultSeColorAlarm, Configuration.ForegroundColors, out idx))
                GatherBuddy.Config.SeColorAlarm = idx;

            ImGui.NewLine();
        }
    }
}

