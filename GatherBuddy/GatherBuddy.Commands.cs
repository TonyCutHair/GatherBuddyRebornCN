using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Command;
using Dalamud.Game.Text.SeStringHandling;
using GatherBuddy.Enums;
using GatherBuddy.Plugin;
using GatherBuddy.Time;

namespace GatherBuddy;

public partial class GatherBuddy
{
    public const string IdentifyCommand       = "identify";
    public const string GearChangeCommand     = "gearchange";
    public const string TeleportCommand       = "teleport";
    public const string MapMarkerCommand      = "mapmarker";
    public const string AdditionalInfoCommand = "information";
    public const string SetWaymarksCommand    = "waymarks";
    public const string AutoCommand           = "auto";
    public const string AutoOnCommand         = "auto on";
    public const string AutoOffCommand        = "auto off";
    public const string FullIdentify          = $"/gatherbuddy {IdentifyCommand}";
    public const string FullGearChange        = $"/gatherbuddy {GearChangeCommand}";
    public const string FullTeleport          = $"/gatherbuddy {TeleportCommand}";
    public const string FullMapMarker         = $"/gatherbuddy {MapMarkerCommand}";
    public const string FullAdditionalInfo    = $"/gatherbuddy {AdditionalInfoCommand}";
    public const string FullSetWaymarks       = $"/gatherbuddy {SetWaymarksCommand}";
    public const string FullAuto              = $"/gatherbuddy {AutoCommand}";
    public const string FullAutoOn            = $"/gatherbuddy {AutoOnCommand}";
    public const string FullAutoOff           = $"/gatherbuddy {AutoOffCommand}";

    private readonly Dictionary<string, CommandInfo> _commands = new();

    private void InitializeCommands()
    {
        _commands["/gatherbuddy"] = new CommandInfo(OnGatherBuddy)
        {
            HelpMessage = "用于打开 GatherBuddy 界面。",
            ShowInHelp  = false,
        };

        _commands["/gbr"] = new CommandInfo(OnGatherBuddy)
        {
            HelpMessage = "用于打开 GatherBuddy 界面。",
            ShowInHelp  = true,
        };

        _commands["/gather"] = new CommandInfo(OnGather)
        {
            HelpMessage = "标记包含指定物品的最近采集点，传送到最近的以太水晶，装备适当的装备。\n"
              + "您可以使用 'alarm' 来采集最后触发的闹钟，或使用 'next' 来采集与之前相同的物品，但在次优位置。",
            ShowInHelp = true,
        };

        _commands["/gatherbtn"] = new CommandInfo(OnGatherBtn)
        {
            HelpMessage =
                "标记包含指定物品的最近园艺工采集点，传送到最近的以太水晶，装备适当的装备。",
            ShowInHelp = true,
        };

        _commands["/gathermin"] = new CommandInfo(OnGatherMin)
        {
            HelpMessage =
                "标记包含指定物品的最近采矿工采集点，传送到最近的以太水晶，装备适当的装备。",
            ShowInHelp = true,
        };

        _commands["/gatherfish"] = new CommandInfo(OnGatherFish)
        {
            HelpMessage =
                "标记包含指定鱼类的最近钓场，传送到最近的以太水晶并装备钓鱼装备。",
            ShowInHelp = true,
        };

        _commands["/gathergroup"] = new CommandInfo(OnGatherGroup)
        {
            HelpMessage = "传送到与当前时间对应的组采集点。使用 /gathergroup 查看更多详情。",
            ShowInHelp  = true,
        };

        _commands["/gbc"] = new CommandInfo(OnGatherBuddyShort)
        {
            HelpMessage = "一些快速切换配置选项的命令。不带参数使用查看帮助。",
            ShowInHelp  = true,
        };

        _commands["/gatherdebug"] = new CommandInfo(OnGatherDebug)
        {
            ShowInHelp = false,
        };

        foreach (var (command, info) in _commands)
            Dalamud.Commands.AddHandler(command, info);
    }

    private void DisposeCommands()
    {
        foreach (var command in _commands.Keys)
            Dalamud.Commands.RemoveHandler(command);
    }

    private void OnGatherBuddy(string command, string arguments)
    {
        if (!Executor.DoCommand(arguments))
            Interface.Toggle();
    }

    private void OnGather(string command, string arguments)
    {
        if (arguments.Length == 0)
            Communicator.NoItemName(command, "item");
        else
            Executor.GatherItemByName(arguments);
    }

    private void OnGatherBtn(string command, string arguments)
    {
        if (arguments.Length == 0)
            Communicator.NoItemName(command, "item");
        else
            Executor.GatherItemByName(arguments, GatheringType.Botanist);
    }

    private void OnGatherMin(string command, string arguments)
    {
        if (arguments.Length == 0)
            Communicator.NoItemName(command, "item");
        else
            Executor.GatherItemByName(arguments, GatheringType.Miner);
    }

    private void OnGatherFish(string command, string arguments)
    {
        if (arguments.Length == 0)
            Communicator.NoItemName(command, "fish");
        else
            Executor.GatherFishByName(arguments);
    }

    private void OnGatherGroup(string command, string arguments)
    {
        if (arguments.Length == 0)
        {
            Communicator.Print(GatherGroupManager.CreateHelp());
            return;
        }

        var argumentParts = arguments.Split();
        var minute = (Time.EorzeaMinuteOfDay + (argumentParts.Length < 2 ? 0 : int.TryParse(argumentParts[1], out var offset) ? offset : 0))
          % RealTime.MinutesPerDay;
        if (!GatherGroupManager.TryGetValue(argumentParts[0], out var group))
        {
            Communicator.NoGatherGroup(argumentParts[0]);
            return;
        }

        var node = group.CurrentNode((uint)minute);
        if (node == null)
        {
            Communicator.NoGatherGroupItem(argumentParts[0], minute);
        }
        else
        {
            if (node.Annotation.Any())
                Communicator.Print(node.Annotation);
            if (node.PreferLocation == null)
                Executor.GatherItem(node.Item);
            else
                Executor.GatherLocation(node.PreferLocation);
        }
    }

    private void OnGatherBuddyShort(string command, string arguments)
    {
        switch (arguments.ToLowerInvariant())
        {
            case "window":
                Config.ShowGatherWindow = !Config.ShowGatherWindow;
                break;
            case "alarm":
                if (Config.AlarmsEnabled)
                    AlarmManager.Disable();
                else
                    AlarmManager.Enable();
                break;
            case "spear":
                Config.ShowSpearfishHelper = !Config.ShowSpearfishHelper;
                break;
            case "fish":
                Config.ShowFishTimer = !Config.ShowFishTimer;
                break;
            case "edit":
                if (!Config.FishTimerEdit)
                {
                    Config.ShowFishTimer = true;
                    Config.FishTimerEdit = true;
                }
                else
                {
                    Config.FishTimerEdit = false;
                }

                break;
            case "unlock":
                Config.MainWindowLockPosition = false;
                Config.MainWindowLockResize   = false;
                break;
            case "collect":
                CollectableManager.Start();
                return;
            case "collectstop":
                CollectableManager.Stop();
                return;
            default:
                var shortHelpString = new SeStringBuilder().AddText("使用 ").AddColoredText(command, Config.SeColorCommands)
                    .AddText("，后跟以下参数之一：\n")
                    .AddColoredText("        window", Config.SeColorArguments).AddText(" - 切换采集窗口开/关。\n")
                    .AddColoredText("        alarm",  Config.SeColorArguments).AddText(" - 切换闹钟开/关。\n")
                    .AddColoredText("        spear",  Config.SeColorArguments).AddText(" - 切换鱼叉助手开/关。\n")
                    .AddColoredText("        fish",   Config.SeColorArguments).AddText(" - 切换鱼类计时器窗口开/关。\n")
                    .AddColoredText("        edit",   Config.SeColorArguments).AddText(" - 切换鱼类计时器编辑模式。\n")
                    .AddColoredText("        unlock", Config.SeColorArguments).AddText(" - 解锁主窗口位置和大小。")
                    .BuiltString;
                Communicator.Print(shortHelpString);
                return;
        }

        Config.Save();
    }

    private static void OnGatherDebug(string command, string arguments)
    {
        DebugMode = !DebugMode;
    }
}
