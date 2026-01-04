using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using VoiceMaster.DataClasses;
using VoiceMaster.Enums;
using VoiceMaster.Helper.Data;
using VoiceMaster.Helper.DataHelper;
using VoiceMaster.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace VoiceMaster.Helper.Functional
{
    public static class CommandHelper
    {
        public static List<string> CommandKeys;

        public static void Initialize()
        {

            RegisterCommands();
        }

        public static void RegisterCommands()
        {
            Plugin.CommandManager.AddHandler("/vm", new CommandInfo(CommandHelper.OnCommand)
            {
                HelpMessage = "Toggles VoiceMaster"
            });
            Plugin.CommandManager.AddHandler("/vmtalk", new CommandInfo(CommandHelper.OnCommand)
            {
                HelpMessage = "Toggles dialogue voicing"
            });
            Plugin.CommandManager.AddHandler("/vmbtalk", new CommandInfo(CommandHelper.OnCommand)
            {
                HelpMessage = "Toggles battle dialogue voicing"
            });
            Plugin.CommandManager.AddHandler("/vmbubble", new CommandInfo(CommandHelper.OnCommand)
            {
                HelpMessage = "Toggles bubble voicing"
            });
            Plugin.CommandManager.AddHandler("/vmchat", new CommandInfo(CommandHelper.OnCommand)
            {
                HelpMessage = "Toggles chat voicing"
            });
            Plugin.CommandManager.AddHandler("/vmcutschoice", new CommandInfo(CommandHelper.OnCommand)
            {
                HelpMessage = "Toggles cutscene choice voicing"
            });
            Plugin.CommandManager.AddHandler("/vmchoice", new CommandInfo(CommandHelper.OnCommand)
            {
                HelpMessage = "Toggles choice voicing"
            });

            Plugin.CommandManager.AddHandler("/vmignore", new CommandInfo(CommandHelper.OnCommand)
            {
                HelpMessage = "Ignores the current target NPC for voiced overlap. Usage: /vmignore [instance|session|always]"
            });
            Plugin.CommandManager.AddHandler("/vmunignore", new CommandInfo(CommandHelper.OnCommand)
            {
                HelpMessage = "Removes current target NPC from ignore list(s). Usage: /vmunignore"
            });
            Plugin.CommandManager.AddHandler("/vmlistignore", new CommandInfo(CommandHelper.OnCommand)
            {
                HelpMessage = "Prints ignored NPCs (instance/session/always)."
            });

            Plugin.CommandManager.AddHandler("/ek", new CommandInfo(CommandHelper.OnCommand)
            {
                HelpMessage = "Opens the Plugin.Configuration window"
            });
            Plugin.CommandManager.AddHandler("/ekid", new CommandInfo(CommandHelper.OnCommand)
            {
                HelpMessage = "Echoes info about current target"
            });
            Plugin.CommandManager.AddHandler("/ekdb", new CommandInfo(CommandHelper.OnCommand)
            {
                HelpMessage = "Echoes current debug info"
            });
            Plugin.CommandManager.AddHandler("/ekdel", new CommandInfo(CommandHelper.OnCommand)
            {
                HelpMessage = "/ekdel n -> Deletes last 'n' local saved files. Default 10"
            });
            Plugin.CommandManager.AddHandler("/ekdelmin", new CommandInfo(CommandHelper.OnCommand)
            {
                HelpMessage = "/ekdelmin n -> Deletes last 'n' minutes generated local saved files. Default 10"
            });

            CommandKeys = Plugin.CommandManager.Commands.Keys.ToList().FindAll(p => p.StartsWith("/ek"));
            CommandKeys.Sort();
        }

        public static void OnCommand(string command, string args)
        {
            // in response to the slash command, just toggle the display status of our config ui

            var activationText = "";
            var activationType = "";
            var errorText = "";
            switch (command)
            {
                case "/vmignore":
                    IgnoreCurrentTarget(args);
                    break;
                case "/vmunignore":
                    UnignoreCurrentTarget();
                    break;
                case "/vmlistignore":
                    PrintIgnoreLists();
                    break;
                case "/ek":
                    if (!Plugin.Configuration.FirstTime)
                        ToggleConfigUi();
                    break;
                case "/ekid":
                    PrintTargetInfo();
                    break;
                case "/ekdb":
                    PrintDebugInfo();
                    break;
                case "/ekdel":
                    try
                    {
                        var deleteNFiles = 10;
                        if (args.Trim().Length > 0)
                            deleteNFiles = Convert.ToInt32(args);

                        var deletedFiles = AudioFileHelper.DeleteLastNFiles(deleteNFiles);
                        PrintText("", $"Deleted {deletedFiles} generated audio files");
                    }
                    catch (Exception ex)
                    {
                        errorText = $"Please enter a valid number or leave empty";
                    }
                    break;
                case "/ekdelmin":
                    try
                    {
                        var deleteNMinutesFiles = 10;
                        if (args.Trim().Length > 0)
                            deleteNMinutesFiles = Convert.ToInt32(args);

                        var deletedFiles = AudioFileHelper.DeleteLastNMinutesFiles(deleteNMinutesFiles);
                        PrintText("", $"Deleted {deletedFiles} generated audio files");
                    }
                    catch (Exception ex)
                    {
                        errorText = $"Please enter a valid number or leave empty";
                    }
                    break;
                case "/vm":
                    Plugin.Configuration.Enabled = !Plugin.Configuration.Enabled;
                    Plugin.Configuration.Save();
                    
                    if (!Plugin.Configuration.Enabled)
                        Plugin.CancelAll(new EKEventId(0, TextSource.None));
                    activationText = (Plugin.Configuration.Enabled ? "Enabled" : "Disabled");
                    activationType = "plugin";
                    break;
                case "/vmtalk":
                    Plugin.Configuration.VoiceDialogue = !Plugin.Configuration.VoiceDialogue;
                    Plugin.Configuration.Save();
                    activationText = (Plugin.Configuration.VoiceDialogue ? "Enabled" : "Disabled");
                    activationType = "dialogue";
                    break;
                case "/vmbtalk":
                    Plugin.Configuration.VoiceBattleDialogue = !Plugin.Configuration.VoiceBattleDialogue;
                    Plugin.Configuration.Save();
                    activationText = (Plugin.Configuration.VoiceBattleDialogue ? "Enabled" : "Disabled");
                    activationType = "battle dialogue";
                    break;
                case "/vmbubble":
                    Plugin.Configuration.VoiceBubble = !Plugin.Configuration.VoiceBubble;
                    Plugin.Configuration.Save();
                    activationText = (Plugin.Configuration.VoiceBubble ? "Enabled" : "Disabled");
                    activationType = "bubble";
                    break;
                case "/vmchat":
                    Plugin.Configuration.VoiceChat = !Plugin.Configuration.VoiceChat;
                    Plugin.Configuration.Save();
                    activationText = (Plugin.Configuration.VoiceChat ? "Enabled" : "Disabled");
                    activationType = "chat";
                    break;
                case "/vmcutschoice":
                    Plugin.Configuration.VoicePlayerChoicesCutscene = !Plugin.Configuration.VoicePlayerChoicesCutscene;
                    Plugin.Configuration.Save();
                    activationText = (Plugin.Configuration.VoicePlayerChoicesCutscene ? "Enabled" : "Disabled");
                    activationType = "player choice in cutscene";
                    break;
                case "/vmchoice":
                    Plugin.Configuration.VoicePlayerChoices = !Plugin.Configuration.VoicePlayerChoices;
                    Plugin.Configuration.Save();
                    activationText = (Plugin.Configuration.VoicePlayerChoices ? "Enabled" : "Disabled");
                    activationType = "player choice";
                    break;
            }

            if (!string.IsNullOrWhiteSpace(activationType) && !string.IsNullOrWhiteSpace(activationText))
            {
                PrintText("", $"{activationText} {activationType} voicing");

                LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"New Command triggered: {command}", new EKEventId(0, TextSource.None));

                if (!string.IsNullOrWhiteSpace(errorText))
                    PrintText("", errorText);
            }
        }

        public static void ToggleConfigUi()
        {
            if (!Plugin.Configuration.FirstTime)
                Plugin.ConfigWindow.Toggle();
            else
                Plugin.FirstTimeWindow.Toggle();
        }

        public static void ToggleDialogUi() => Plugin.DialogExtraOptionsWindow.Toggle();

        public static void ToggleFirstTimeUi() => Plugin.FirstTimeWindow.Toggle();

        public unsafe static void PrintTargetInfo()
        {
            if (DalamudHelper.LocalPlayer != null)
            {
                var target = DalamudHelper.LocalPlayer.TargetObject;
                if (target != null)
                {
                    var race = CharacterDataHelper.GetSpeakerRace(new EKEventId(0, TextSource.None), target, out var raceStr, out var modelId);
                    var gender = CharacterDataHelper.GetCharacterGender(new EKEventId(0, TextSource.None), target, race, out var modelBody);
                    var bodyType = LuminaHelper.GetENpcBase(target.DataId, new EKEventId(0, TextSource.None))?.BodyType;
                    PrintText(target.Name.TextValue, $"Target -> Name: {target.Name}, Race: {race}, Gender: {gender}, ModelID: {modelId}, ModelBody: {modelBody}, BodyType: {bodyType}");
                }
            }
        }
        
        
        private static string? GetCurrentTargetName()
        {
            try
            {
                var lp = DalamudHelper.LocalPlayer;
                var target = lp?.TargetObject;
                var name = target?.Name.TextValue;
                if (!string.IsNullOrWhiteSpace(name))
                    return name;
                return target?.Name.ToString();
            }
            catch
            {
                return null;
            }
        }

        private static void IgnoreCurrentTarget(string args)
        {
            var targetName = GetCurrentTargetName();
            if (string.IsNullOrWhiteSpace(targetName))
            {
                PrintText("Ignore", "No target selected.");
                return;
            }

            var scope = ParseIgnoreScope(args);
            Plugin.AddIgnoredNpc(targetName, scope);
            var scopeText = scope switch { 0 => "instance", 1 => "session", 2 => "always", _ => "instance" };
            PrintText("Ignore", $"Ignoring \"{targetName}\" ({scopeText}).");
        }

        private static void UnignoreCurrentTarget()
        {
            var targetName = GetCurrentTargetName();
            if (string.IsNullOrWhiteSpace(targetName))
            {
                PrintText("Ignore", "No target selected.");
                return;
            }

            Plugin.RemoveIgnoredNpcEverywhere(targetName);
            PrintText("Ignore", $"Unignored \"{targetName}\".");
        }

        private static int ParseIgnoreScope(string args)
        {
            var a = (args ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(a))
                return 0;

            if (a.StartsWith("inst"))
                return 0;
            if (a.StartsWith("sess"))
                return 1;
            if (a.StartsWith("alwa") || a.StartsWith("perm") || a.StartsWith("forev"))
                return 2;

            // Also allow numeric scopes: 0/1/2
            if (int.TryParse(a, out var n) && n is >= 0 and <= 2)
                return n;

            return 0;
        }

        private static void PrintIgnoreLists()
        {
            try
            {
                // Instance + Session are runtime sets
                var inst = Plugin.IgnoredNpcInstance.Count;
                var sess = Plugin.IgnoredNpcSession.Count;
                var always = Plugin.Configuration.IgnoredNpcAlways.Count;

                PrintText("Ignore", $"Ignored NPCs -> Instance: {inst}, Session: {sess}, Always: {always}");

                if (inst > 0)
                    PrintText("Ignore", "Instance -> " + string.Join(", ", Plugin.IgnoredNpcInstance));

                if (sess > 0)
                    PrintText("Ignore", "Session -> " + string.Join(", ", Plugin.IgnoredNpcSession));

                if (always > 0)
                    PrintText("Ignore", "Always -> " + string.Join(", ", Plugin.Configuration.IgnoredNpcAlways));
            }
            catch (Exception ex)
            {
                PrintText("Ignore", $"Error listing ignores: {ex.Message}");
            }
        }

public static void PrintDebugInfo()
        {
            var cond1 = Plugin.Condition[ConditionFlag.OccupiedInQuestEvent];
            var cond2 = Plugin.Condition[ConditionFlag.Occupied];
            var cond3 = Plugin.Condition[ConditionFlag.Occupied30];
            var cond4 = Plugin.Condition[ConditionFlag.Occupied33];
            var cond5 = Plugin.Condition[ConditionFlag.Occupied38];
            var cond6 = Plugin.Condition[ConditionFlag.Occupied39];
            var cond7 = Plugin.Condition[ConditionFlag.OccupiedInCutSceneEvent];
            var cond8 = Plugin.Condition[ConditionFlag.OccupiedInEvent];
            var cond9 = Plugin.Condition[ConditionFlag.OccupiedSummoningBell];
            var cond10 = Plugin.Condition[ConditionFlag.BoundByDuty];
            PrintText("Debug", $"Debug -> ---Start---");
            PrintText("Debug", $"Debug -> OccupiedInQuestEvent: {cond1}");
            PrintText("Debug", $"Debug -> Occupied: {cond2}");
            PrintText("Debug", $"Debug -> Occupied30: {cond3}");
            PrintText("Debug", $"Debug -> Occupied33: {cond4}");
            PrintText("Debug", $"Debug -> Occupied38: {cond5}");
            PrintText("Debug", $"Debug -> Occupied39: {cond6}");
            PrintText("Debug", $"Debug -> OccupiedInCutSceneEvent: {cond7}");
            PrintText("Debug", $"Debug -> OccupiedInEvent: {cond8}");
            PrintText("Debug", $"Debug -> OccupiedSummoningBell: {cond9}");
            PrintText("Debug", $"Debug -> BoundByDuty: {cond10}");
            PrintText("Debug", $"Debug -> ---End---");
        }

        public static void PrintText(string name, string text)
        {
            Plugin.ChatGui.Print(new Dalamud.Game.Text.XivChatEntry() { Name = name, Message = "VoiceMaster: " + text, Timestamp = DateTime.Now.Hour * 60 + DateTime.Now.Minute, Type = Dalamud.Game.Text.XivChatType.Echo });
        }

        internal static void Dispose()
        {
            Plugin.CommandManager.RemoveHandler("/ek");
            Plugin.CommandManager.RemoveHandler("/vm");
            Plugin.CommandManager.RemoveHandler("/ekdb");
            Plugin.CommandManager.RemoveHandler("/ekid");
            Plugin.CommandManager.RemoveHandler("/vmtalk");
            Plugin.CommandManager.RemoveHandler("/vmbtalk");
            Plugin.CommandManager.RemoveHandler("/vmbubble");
            Plugin.CommandManager.RemoveHandler("/vmcutschoice");
            Plugin.CommandManager.RemoveHandler("/vmchoice");
            Plugin.CommandManager.RemoveHandler("/vmignore");
            Plugin.CommandManager.RemoveHandler("/vmunignore");
            Plugin.CommandManager.RemoveHandler("/vmlistignore");
            Plugin.CommandManager.RemoveHandler("/ekdel");
            Plugin.CommandManager.RemoveHandler("/ekdelmin");
        }
    }
}
