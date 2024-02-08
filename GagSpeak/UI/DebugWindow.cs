﻿﻿using System;
using System.Numerics;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using OtterGui;
using GagSpeak.ChatMessages;
using GagSpeak.Services;
using GagSpeak.Utility;
using GagSpeak.Garbler.Translator;
using GagSpeak.Events;
using Penumbra.GameData.Enums;
using System.Linq;
using Penumbra.GameData.Structs;
using Penumbra.GameData.Data;
using GagSpeak.Gagsandlocks;
using GagSpeak.UI.Tabs.GeneralTab;
using GagSpeak.CharacterData;

namespace GagSpeak.UI;
// probably can remove this later, atm it is literally just used for the debug window
public enum EquipmentSlotNameByEnum {
    MainHand,
    OffHand,
    Head,
    Body,
    Hands,
    Belt,
    Legs,
    Feet,
    Ears,
    Neck,
    Wrists,
    RFinger,
    BothHand,
    LFinger,
}
/// <summary> This class is used to show the debug menu in its own window. </summary>
public class DebugWindow : Window //, IDisposable
{
    private          GagSpeakConfig         _config;                        // for retrieving the config data to display to the window
    private readonly CharacterHandler       _characterHandler;
    private readonly IpaParserEN_FR_JP_SP   _translatorLanguage;            // creates an instance of the EnglishToIPA class
    private readonly GagGarbleManager       _gagManager;                    // for knowing what gags are equipped
    private readonly GagListingsDrawer      _gagListingsDrawer;             // for knowing the information in the currently equipped gags
    private readonly FontService            _fontService;                   // for displaying the IPA symbols on the bottom chart
    private readonly GagService             _gagService;                    // for displaying the number of registered gags
    private readonly ItemAutoEquipEvent     _itemAutoEquipEvent;            // for knowing when a gag is equipped
    private readonly ItemData               _itemData;                      // for knowing the item data
    private          string?                _tempTestMessage;               // stores the input password for the test translation system
    private          string?                _translatedMessage = "";        // stores the translated message for the test translation system
    private          string?                _translatedMessageSpaced ="";   // stores the translated message for the test translation system
    private          string?                _translatedMessageOutput ="";   // stores the translated message for the test translation system

    public DebugWindow(DalamudPluginInterface pluginInt, FontService fontService, GagService gagService,
    IpaParserEN_FR_JP_SP translatorLanguage, GagSpeakConfig config, CharacterHandler characterHandler,
    GagGarbleManager GagGarbleManager, GagListingsDrawer gagListingsDrawer, ItemAutoEquipEvent itemAutoEquipEvent,
    ItemData itemData) : base(GetLabel()) {
        // Let's first make sure that we disable the plugin while inside of gpose.
        pluginInt.UiBuilder.DisableGposeUiHide = true;
        // Next let's set the size of the window
        SizeConstraints = new WindowSizeConstraints() {
            MinimumSize = new Vector2(300, 400),     // Minimum size of the window
            MaximumSize = ImGui.GetIO().DisplaySize, // Maximum size of the window
        };
        _config = config;
        _characterHandler = characterHandler;
        _itemAutoEquipEvent = itemAutoEquipEvent;
        _fontService = fontService;
        _gagService = gagService;
        _gagManager = GagGarbleManager;
        _gagListingsDrawer = gagListingsDrawer;
        _translatorLanguage = translatorLanguage;
        _itemData = itemData;
    }


    public override void Draw() {
        // temp
        ImGui.Text($"Whitelist Count: {_characterHandler.whitelistChars.Count()}");
        ImGui.Text($"AliasCount: {_characterHandler.playerChar._triggerAliases.Count()}");
        ImGui.Text($"Extended Lock Times Count: {_characterHandler.playerChar._grantExtendedLockTimes.Count()}");
        ImGui.Text($"Trigger Phrase Count: {_characterHandler.playerChar._triggerPhraseForPuppeteer.Count()}");
        ImGui.Text($"Allow Sit Requests Count: {_characterHandler.playerChar._allowSitRequests.Count()}");
        ImGui.Text($"Allow Motion Requests Count: {_characterHandler.playerChar._allowMotionRequests.Count()}");
        ImGui.Text($"Allow All Commands Count: {_characterHandler.playerChar._allowAllCommands.Count()}");
        ImGui.Text($"Allow Changing Toy State Count: {_characterHandler.playerChar._allowChangingToyState.Count()}");
        ImGui.Text($"Allow Using Patterns Count: {_characterHandler.playerChar._allowUsingPatterns.Count()}");
        ImGui.Text($"Active whitelist idx {_characterHandler.activeListIdx}");
        DrawPlayerCharInfo();
        DrawAdvancedGarblerInspector();
        DrawDebugInformationBasic();
        DrawDebugInformationWhitelistAndLocks();
        DrawPhoneticDebugInformation();
        DrawCachedCharacterInformation();
    }

    // temp vibe debug stuff
    /*
    ImGui.SetCursorPosY(yPos - 5*ImGuiHelpers.GlobalScale);
    if(!_plugService.anyDeviceConnected) { 
        DisplayText("No Device Connected!");
    }
    else {
        #pragma warning disable CS8602 // Dereference of a possibly null reference.
        if(_plugService.activeDevice.DisplayName.IsNullOrEmpty()) {
            DisplayText($"{_plugService.activeDevice.Name} Connected");
        }
        else {
            DisplayText($"{_plugService.activeDevice.DisplayName} Connected");
        }
        #pragma warning restore CS8602 // Dereference of a possibly null reference.
        // print all the juicy into to figure out how this service works
        // Print ButtplugClient details
        ImGui.Text($"Client Name: {_plugService.client.Name}");
        ImGui.Text($"Connected: {_plugService.client.Connected}");
        if(_plugService.IsClientConnected() && _plugService.HasConnectedDevice()) {
            ImGui.Text($"Devices: {string.Join(", ", _plugService.client.Devices.Select(d => d.Name))}");
        } else {
            ImGui.Text($"Devices: No Devices Connected");
        }
        // Print ButtplugClientDevice details if a device is connected
        if (_plugService.activeDevice != null)
        {
            ImGui.Text($"Device Index: {_plugService.activeDevice.Index}");
            ImGui.Text($"Device Name: {_plugService.activeDevice.Name}");
            ImGui.Text($"Device Display Name: {_plugService.activeDevice.DisplayName}");
            ImGui.Text($"Message Timing Gap: {_plugService.activeDevice.MessageTimingGap}");
            ImGui.Text($"ActiveToy's Step Size: {_plugService.stepInterval}");
            ImGui.Text($"Has Battery: {_plugService.activeDevice.HasBattery}");
            ImGui.TextWrapped($"Vibrate Attributes: {string.Join(", ", _plugService.activeDevice.VibrateAttributes.Select(a => a.ActuatorType.ToString()))}");
            ImGui.Text($"Oscillate Attributes: {string.Join(", ", _plugService.activeDevice.OscillateAttributes.Select(a => a.ActuatorType.ToString()))}");
            ImGui.Text($"Rotate Attributes: {string.Join(", ", _plugService.activeDevice.RotateAttributes.Select(a => a.ActuatorType.ToString()))}");
            ImGui.Text($"Linear Attributes: {string.Join(", ", _plugService.activeDevice.LinearAttributes.Select(a => a.ActuatorType.ToString()))}");
        }
    }
    */

    // basic string function to get the label of title for the window
    private static string GetLabel() => "GagSpeakDebug###GagSpeakDebug";    


    /// <summary> Draws the advanced garbler inspector. </summary>
    public void DrawAdvancedGarblerInspector() {
        // create a collapsing header for this.
        if(!ImGui.CollapsingHeader("Advanced Garbler Debug Testing")) { return; }
        // create a input text field here, that stores the result into a string. On the same line, have a button that says garble message. It should display the garbled message in text on the next l
        var testMessage  = _tempTestMessage ?? ""; // temp storage to hold until we de-select the text input
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X/2);
        if (ImGui.InputText("##GarblerTesterField", ref testMessage, 400, ImGuiInputTextFlags.None))
            _tempTestMessage = testMessage;

        ImGui.SameLine();
        if (ImGui.Button("Garble Message")) {
            // Use the EnglishToIPA instance to translate the message
            try {
                _translatedMessage       = _translatorLanguage.ToIPAStringDisplay(testMessage);
                _translatedMessageSpaced = _translatorLanguage.ToIPAStringSpacedDisplay(testMessage);
                _translatedMessageOutput = _gagManager.ProcessMessage(testMessage);
            } catch (Exception ex) {
                GagSpeak.Log.Debug($"An error occurred while attempting to parse phonetics: {ex.Message}");
            }
        }
        // DISPLAYS THE ORIGINAL MESSAGE STRING
        ImGui.Text($"Original Message: {testMessage}");
        // DISPLAYS THE IPA PARSED DEFINED MESSAGE DISPLAY
        ImGui.Text("Decoded Message: "); ImGui.SameLine();
        UIHelpers.FontText($"{_translatedMessage}", _fontService.UidFont);
        // DISPLAYS THE DECODED MESSAGE SPACED
        ImGui.Text("Decoded Message: "); ImGui.SameLine();
        UIHelpers.FontText($"{_translatedMessageSpaced}", _fontService.UidFont);   
        // DISPLAYS THE OUTPUT STRING 
        ImGui.Text("Output Message: "); ImGui.SameLine();
        UIHelpers.FontText($"{_translatedMessageOutput}", _fontService.UidFont);
        // DISPLAYS THE UNIQUE SYMBOLS FOR CURRENT LANGUAGE DIALECT
        string uniqueSymbolsString = _translatorLanguage.uniqueSymbolsString;
        ImGui.PushFont(_fontService.UidFont);
        ImGui.Text($"Unique Symbols for {_config.language} with dialect {_config.languageDialect}: ");
        ImGui.InputText("##UniqueSymbolsField", ref uniqueSymbolsString, 128, ImGuiInputTextFlags.ReadOnly);
        ImGui.PopFont();
    }

    /// <summary> Draws the debug information. Needs a serious Massive overhaul </summary>
    public void DrawDebugInformationBasic() {
        if(!ImGui.CollapsingHeader("DEBUG INFORMATION")) { return; }
        // General plugin information
        ImGui.Text($"Fresh Install?: {_config.FreshInstall}");
        ImGui.Text($"Safeword: {_characterHandler.playerChar._safeword}");
        ImGui.Text($"Has Safeword Been Used?: {_characterHandler.playerChar._safewordUsed}");
        ImGui.Separator();
        // configuration tab options & details
        ImGui.Text($"Allow Commands from Friends?: {_characterHandler.playerChar._doCmdsFromFriends}");
        ImGui.Text($"Allow Commands from Party Members?: {_characterHandler.playerChar._doCmdsFromParty}");
        ImGui.Text($"In DirectChatGarbler Mode?: {_characterHandler.playerChar._directChatGarblerActive}");
        ImGui.Text($"Selected Language: {_config.language}");
        ImGui.Text($"Selected Dialect: {_config.languageDialect}");
        ImGui.Text($"Translatable Chat Types:");
        foreach (var chanel in _config.ChannelsGagSpeak) { ImGui.SameLine(); ImGui.Text($"{chanel.ToString()}, "); };
        ImGui.Text($"Current ChatBox Channel: {ChatChannel.GetChatChannel()}");
        ImGui.Text($"Player Current Requesting Info: {_config.sendInfoName}");
        ImGui.Text($"Ready To Accept sending player information?: {_config.acceptingInfoRequests}");
        ImGui.Separator();
        // wardrobe details
        ImGui.Text($"Enable Wardrobe?: {_characterHandler.playerChar._enableWardrobe}");
        ImGui.Text($"Enable Item Auto-Equip?: {_characterHandler.playerChar._allowItemAutoEquip}");
        ImGui.Text($"Allow Restraint Locking?: {_characterHandler.playerChar._allowRestraintSetAutoEquip}");
        // Gag details
        ImGui.Separator();
        ImGui.Text($"Total Gag List Count: {_gagService._gagTypes.Count}");
        ImGui.Text("Selected GagTypes: ||"); ImGui.SameLine(); foreach (var gagType in _characterHandler.playerChar._selectedGagTypes) { ImGui.SameLine(); ImGui.Text($"{gagType} ||"); };
        ImGui.Text("Selected Padlocks: ||"); ImGui.SameLine(); foreach (Padlocks gagPadlock in _characterHandler.playerChar._selectedGagPadlocks) { ImGui.SameLine(); ImGui.Text($"{gagPadlock.ToString()} ||");};
        ImGui.Text("Selected Padlocks Passwords: ||"); ImGui.SameLine(); foreach (var gagPadlockPassword in _characterHandler.playerChar._selectedGagPadlockPassword) { ImGui.SameLine(); ImGui.Text($"{gagPadlockPassword} ||"); };
        ImGui.Text("Selected GagPadlock Timers: ||"); ImGui.SameLine(); foreach (var gagPadlockTimer in _characterHandler.playerChar._selectedGagPadlockTimer) { ImGui.SameLine(); ImGui.Text($"{UIHelpers.FormatTimeSpan(gagPadlockTimer - DateTimeOffset.Now)} ||"); };
        ImGui.Text("Selected Padlocks Assigners: ||"); ImGui.SameLine(); foreach (var gagPadlockAssigner in _characterHandler.playerChar._selectedGagPadlockAssigner) { ImGui.SameLine(); ImGui.Text($"{gagPadlockAssigner} ||"); };
    }

    public void DrawPlayerCharInfo() {
        if(!ImGui.CollapsingHeader("Player Character Info")) { return; }

        // Player character information
        ImGui.Text("Player Character:");
        ImGui.Separator();
        ImGui.Text($"Safeword: {_characterHandler.playerChar._safeword}");
        ImGui.Text($"Commands from Friends: {_characterHandler.playerChar._doCmdsFromFriends}");
        ImGui.Text($"Commands from Party: {_characterHandler.playerChar._doCmdsFromParty}");
        ImGui.Text($"Live Garbler Warning on Zone Change: {_characterHandler.playerChar._liveGarblerWarnOnZoneChange}");
        ImGui.Text($"Allow Item Auto Equip: {_characterHandler.playerChar._allowItemAutoEquip}");
        ImGui.Text($"Allow Restraint Set Auto Equip: {_characterHandler.playerChar._allowRestraintSetAutoEquip}");
        ImGui.Text($"Allow Puppeteer: {_characterHandler.playerChar._allowPuppeteer}");
        ImGui.Separator();
        var triggerlist = _characterHandler.playerChar._triggerAliases[_characterHandler.activeListIdx];
        ImGui.Text($"Trigger Aliases: || "); ImGui.SameLine(); foreach (var alias in triggerlist._aliasTriggers) { ImGui.Text(alias._inputCommand); };
        ImGui.Separator();
        ImGui.Text($"Allow Extended Lock Times: || "); ImGui.SameLine(); foreach (var extendedLock in _characterHandler.playerChar._grantExtendedLockTimes) { ImGui.Text(extendedLock.ToString()); };
        ImGui.Separator();
        ImGui.Text($"Trigger Phrase for Puppeteer: || "); ImGui.SameLine(); foreach (var triggerPhrase in _characterHandler.playerChar._triggerPhraseForPuppeteer) { ImGui.Text(triggerPhrase); };
        ImGui.Separator();
        ImGui.Text($"Start Char for Puppeteer Trigger: || "); ImGui.SameLine(); foreach (var startChar in _characterHandler.playerChar._StartCharForPuppeteerTrigger) { ImGui.Text(startChar); };
        ImGui.Separator();
        ImGui.Text($"End Char for Puppeteer Trigger: || "); ImGui.SameLine(); foreach (var endChar in _characterHandler.playerChar._EndCharForPuppeteerTrigger) { ImGui.Text(endChar); };
        ImGui.Separator();
        ImGui.Text($"Allow Sit Requests: || "); ImGui.SameLine(); foreach (var sitRequest in _characterHandler.playerChar._allowSitRequests) { ImGui.Text(sitRequest.ToString()); };
        ImGui.Separator();
        ImGui.Text($"Allow Motion Requests: || "); ImGui.SameLine(); foreach (var motionRequest in _characterHandler.playerChar._allowMotionRequests) { ImGui.Text(motionRequest.ToString()); };
        ImGui.Separator();
        ImGui.Text($"Allow All Commands: || "); ImGui.SameLine(); foreach (var allCommands in _characterHandler.playerChar._allowAllCommands) { ImGui.Text(allCommands.ToString()); };
        ImGui.Separator();
        ImGui.Text($"Allow Changing Toy State: || "); ImGui.SameLine(); foreach (var toyState in _characterHandler.playerChar._allowChangingToyState) { ImGui.Text(toyState.ToString()); };
        ImGui.Separator();
        ImGui.Text($"Allow Using Patterns: || "); ImGui.SameLine(); foreach (var usingPatterns in _characterHandler.playerChar._allowUsingPatterns) { ImGui.Text(usingPatterns.ToString()); };
        ImGui.Separator();
        ImGui.Text($"Safeword Used: {_characterHandler.playerChar._safewordUsed}");
        ImGui.Text($"Direct Chat Garbler Active: {_characterHandler.playerChar._directChatGarblerActive}");
        ImGui.Text($"Direct Chat Garbler Locked: {_characterHandler.playerChar._directChatGarblerLocked}");
        ImGui.Separator();
        ImGui.Text($"Selected GagTypes: || "); ImGui.SameLine(); foreach (var gagType in _characterHandler.playerChar._selectedGagTypes) { ImGui.SameLine(); ImGui.Text(gagType); };
        ImGui.Text($"Selected Padlocks: || "); ImGui.SameLine(); foreach (Padlocks gagPadlock in _characterHandler.playerChar._selectedGagPadlocks) { ImGui.SameLine(); ImGui.Text($"{gagPadlock.ToString()} || ");};
        ImGui.Text($"Selected Padlocks Passwords: || "); ImGui.SameLine(); foreach (var gagPadlockPassword in _characterHandler.playerChar._selectedGagPadlockPassword) { ImGui.SameLine(); ImGui.Text($"{gagPadlockPassword} || "); };
        ImGui.Text($"Selected Padlocks Timers: || "); ImGui.SameLine(); foreach (var gagPadlockTimer in _characterHandler.playerChar._selectedGagPadlockTimer) { ImGui.SameLine(); ImGui.Text($"{UIHelpers.FormatTimeSpan(gagPadlockTimer - DateTimeOffset.Now)} || "); };
        ImGui.Text($"Selected Padlocks Assigners: || "); ImGui.SameLine(); foreach (var gagPadlockAssigner in _characterHandler.playerChar._selectedGagPadlockAssigner) { ImGui.SameLine(); ImGui.Text($"{gagPadlockAssigner} || "); };
        ImGui.Separator();
        ImGui.Text($"Enable Wardrobe: {_characterHandler.playerChar._enableWardrobe}");
        ImGui.Text($"Lock Gag Storage on Gag Lock: {_characterHandler.playerChar._lockGagStorageOnGagLock}");
        ImGui.Text($"Enable Restraint Sets: {_characterHandler.playerChar._enableRestraintSets}");
        ImGui.Text($"Restraint Set Locking: {_characterHandler.playerChar._restraintSetLocking}");
        ImGui.Separator();
        ImGui.Text($"Enable Toybox: {_characterHandler.playerChar._enableToybox}");
        ImGui.Text($"Allow Intensity Control: {_characterHandler.playerChar._allowIntensityControl}");
        ImGui.Text($"Intensity Level: {_characterHandler.playerChar._intensityLevel}");
        ImGui.Text($"Allow Toybox Locking: {_characterHandler.playerChar._allowToyboxLocking}");
        ImGui.Separator();
            }

    public void DrawDebugInformationWhitelistAndLocks() {
        if(!ImGui.CollapsingHeader("Whitelist & Locks Info")) { return; }
        // Whitelist uder information
        ImGui.Text("Whitelist:"); ImGui.Indent();
        foreach (var whitelistPlayerData in _characterHandler.whitelistChars) {
            ImGui.Text(whitelistPlayerData._name);
            ImGui.Indent();
            ImGui.Text($"Relationship to this Player: {whitelistPlayerData._yourStatusToThem}");
            ImGui.Text($"Relationship to You: {whitelistPlayerData._theirStatusToYou}");
            ImGui.Text($"Commitment Duration: {whitelistPlayerData.GetCommitmentDuration()}");
            ImGui.Text($"Locked Live Chat Garbler: {whitelistPlayerData._directChatGarblerLocked}");
            ImGui.Text($"Pending Relationship Request From You: {whitelistPlayerData._pendingRelationRequestFromYou}");
            ImGui.Text($"Pending Relationship Request: {whitelistPlayerData._pendingRelationRequestFromPlayer}");
            ImGui.Text($"Selected GagTypes: || "); ImGui.SameLine(); foreach (var gagType in whitelistPlayerData._selectedGagTypes) { ImGui.SameLine(); ImGui.Text(gagType); };
            ImGui.Text($"Selected Padlocks: || "); ImGui.SameLine(); foreach (Padlocks gagPadlock in whitelistPlayerData._selectedGagPadlocks) { ImGui.SameLine(); ImGui.Text($"{gagPadlock.ToString()} || ");};
            ImGui.Text($"Selected Padlocks Passwords: || "); ImGui.SameLine(); foreach (var gagPadlockPassword in whitelistPlayerData._selectedGagPadlockPassword) { ImGui.SameLine(); ImGui.Text($"{gagPadlockPassword} || "); };
            ImGui.Text($"Selected Padlocks Timers: || "); ImGui.SameLine(); foreach (var gagPadlockTimer in whitelistPlayerData._selectedGagPadlockTimer) { ImGui.SameLine(); ImGui.Text($"{UIHelpers.FormatTimeSpan(gagPadlockTimer - DateTimeOffset.Now)} || "); };
            ImGui.Text($"Selected Padlocks Assigners: || "); ImGui.SameLine(); foreach (var gagPadlockAssigner in whitelistPlayerData._selectedGagPadlockAssigner) { ImGui.SameLine(); ImGui.Text($"{gagPadlockAssigner} || "); };
            ImGui.Unindent();
        }
        ImGui.Unindent();
        ImGui.Separator();

        // Padlock identifier Information
        ImGui.Text("Padlock Identifiers Variables:");
        // output debug messages to display the gaglistingdrawers boolean list for _islocked, _adjustDisp. For each padlock identifer, diplay all of its public varaibles
        ImGui.Text($"Listing Drawer isLocked: ||"); ImGui.SameLine(); foreach(var index in _config.isLocked) { ImGui.SameLine(); ImGui.Text($"{index} ||"); };
        ImGui.Text($"Listing Drawer _adjustDisp: ||"); ImGui.SameLine(); foreach(var index in _gagListingsDrawer._adjustDisp) { ImGui.SameLine(); ImGui.Text($"{index} ||"); };
        var width = ImGui.GetContentRegionAvail().X / 3;
        foreach(var index in _config.padlockIdentifier) {
            ImGui.Columns(3,"DebugColumns", true);
            ImGui.SetColumnWidth(0,width); ImGui.SetColumnWidth(1,width); ImGui.SetColumnWidth(2,width);
            ImGui.Text($"Input Password: {index._inputPassword}"); ImGui.NextColumn();
            ImGui.Text($"Input Combination: {index._inputCombination}"); ImGui.NextColumn();
            ImGui.Text($"Input Timer: {index._inputTimer}");ImGui.NextColumn();
            ImGui.Text($"Stored Password: {index._storedPassword}");ImGui.NextColumn();
            ImGui.Text($"Stored Combination: {index._storedCombination}");ImGui.NextColumn();
            ImGui.Text($"Stored Timer: {index._storedTimer}");ImGui.NextColumn();
            ImGui.Text($"Padlock Type: {index._padlockType}");ImGui.NextColumn();
            ImGui.Text($"Padlock Assigner: {index._mistressAssignerName}");ImGui.NextColumn();
            ImGui.Columns(1);
            ImGui.NewLine();
        } // This extra one is just the whitelist padlock stuff
        ImGui.Columns(3,"DebugColumns", true);
        ImGui.SetColumnWidth(0,width); ImGui.SetColumnWidth(1,width); ImGui.SetColumnWidth(2,width);
        ImGui.Text($"Input Password: {_config.whitelistPadlockIdentifier._inputPassword}"); ImGui.NextColumn();
        ImGui.Text($"Input Combination: {_config.whitelistPadlockIdentifier._inputCombination}"); ImGui.NextColumn();
        ImGui.Text($"Input Timer: {_config.whitelistPadlockIdentifier._inputTimer}");ImGui.NextColumn();
        ImGui.Text($"Stored Password: {_config.whitelistPadlockIdentifier._storedPassword}");ImGui.NextColumn();
        ImGui.Text($"Stored Combination: {_config.whitelistPadlockIdentifier._storedCombination}");ImGui.NextColumn();
        ImGui.Text($"Stored Timer: {_config.whitelistPadlockIdentifier._storedTimer}");ImGui.NextColumn();
        ImGui.Text($"Padlock Type: {_config.whitelistPadlockIdentifier._padlockType}");ImGui.NextColumn();
        ImGui.Text($"Padlock Assigner: {_config.whitelistPadlockIdentifier._mistressAssignerName}");ImGui.NextColumn();
        ImGui.Columns(1);
    }

    public void DrawPhoneticDebugInformation() {
        if(!ImGui.CollapsingHeader("Phonetic Debug Information")) { return; }
        var width = ImGui.GetContentRegionAvail().X / 3;
        ImGui.Text("Gag Manager Information:");
        // define the columns and the gag names
        ImGui.Columns(3, "GagColumns", true);
        ImGui.SetColumnWidth(0, width); ImGui.SetColumnWidth(1, width); ImGui.SetColumnWidth(2, width);
        ImGui.Text($"Gag Name: {_gagManager._activeGags[0]._gagName}"); ImGui.NextColumn();
        ImGui.Text($"Gag Name: {_gagManager._activeGags[1]._gagName}"); ImGui.NextColumn();
        ImGui.Text($"Gag Name: {_gagManager._activeGags[2]._gagName}"); ImGui.NextColumn();
        try {
        ImGui.PushFont(_fontService.UidFont);
        foreach (var gag in _gagManager._activeGags) {
            // Create a table for the relations manager
            using (var table = ImRaii.Table($"InfoTable_{gag._gagName}", 3, ImGuiTableFlags.RowBg)) {
                if (!table) { return; }
                // Create the headers for the table
                ImGui.TableSetupColumn("Symbol", ImGuiTableColumnFlags.WidthFixed, width/4);
                ImGui.TableSetupColumn("Strength", ImGuiTableColumnFlags.WidthFixed, width/3);
                ImGui.TableSetupColumn("Sound", ImGuiTableColumnFlags.WidthFixed, width/4);
                ImGui.TableNextRow(); ImGui.TableNextColumn();
                ImGui.Text("Symbol"); ImGui.TableNextColumn();
                ImGui.Text("Strength"); ImGui.TableNextColumn();
                ImGui.Text("Sound"); ImGui.TableNextColumn();
                foreach (var phoneme in gag._muffleStrOnPhoneme){
                    ImGui.Text($"{phoneme.Key}"); ImGui.TableNextColumn();
                    ImGui.Text($"{phoneme.Value}"); ImGui.TableNextColumn();
                    ImGui.Text($"{gag._ipaSymbolSound[phoneme.Key]}"); ImGui.TableNextColumn();
                }
            } // table ends here
            ImGui.NextColumn();
        }
        ImGui.Columns(1);
        ImGui.PopFont();
        } catch (Exception e) {
            ImGui.NewLine();
            ImGui.Text($"Error while fetching config in debug: {e}");
            ImGui.NewLine();
            GagSpeak.Log.Error($"Error while fetching config in debug: {e}");
        }
    }

    /// <summary>
    /// Draws the cached character information.
    /// </summary>
    public void DrawCachedCharacterInformation() {
        if(!ImGui.CollapsingHeader("Cached Character Information")) { return; }
        // draw all the design data we need to know
        using var table = ImRaii.Table("##equip", 6, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit);
        if (!table) { return; }
        ImGui.TableNextRow();
        // temp creation for equipment class
        var properties = typeof(Interop.Equipment).GetProperties().Where(p => p.Name != "Hat" && p.Name != "Visor" && p.Name != "Weapon").ToArray();
        // display equipment slots:
        var equipSlots = EquipSlotExtensions.EqdpSlots.Prepend(EquipSlot.OffHand).Prepend(EquipSlot.MainHand);
        for (int i = 0; i < properties.Length; i++) {
            try{
                // get our property
                var property = properties[i];
                // get the value of the property
                dynamic? slot = property.GetValue(_config.cachedCharacterData.Equipment);
                // get rest of the objects values
                EquipSlot equipslot = equipSlots.ElementAt(i);
                CustomItemId itemId = slot?.ItemId;
                int stain = slot?.Stain;
                bool apply = slot?.Apply;
                bool applyStain = slot?.ApplyStain;
                EquipItem temp = Resolve(equipslot, itemId);
                // draw them outttt
                ImGuiUtil.DrawTableColumn(((EquipmentSlotNameByEnum)i).ToString()); // the slot name
                ImGuiUtil.DrawTableColumn(temp.Name); // the item name
                ImGuiUtil.DrawTableColumn(itemId.ToString());
                ImGuiUtil.DrawTableColumn(stain.ToString());
                ImGuiUtil.DrawTableColumn(apply ? "Apply" : "Keep");
                ImGuiUtil.DrawTableColumn(applyStain ? "Apply" : "Keep");

            } catch (Exception e) {
                GagSpeak.Log.Error($"Error while slot property in character print in debug: {e}");
            }
        }
        ImGuiUtil.DrawTableColumn("Hat Visible");
        ImGuiUtil.DrawTableColumn($"{_config.cachedCharacterData.Equipment.Hat.Show}");
        ImGuiUtil.DrawTableColumn($"{_config.cachedCharacterData.Equipment.Hat.Apply}");
        ImGui.TableNextRow();
        ImGuiUtil.DrawTableColumn("Visor Toggled");
        ImGuiUtil.DrawTableColumn($"{_config.cachedCharacterData.Equipment.Visor.IsToggled}");
        ImGuiUtil.DrawTableColumn($"{_config.cachedCharacterData.Equipment.Visor.Apply}");
        ImGui.TableNextRow();
        ImGuiUtil.DrawTableColumn("Weapon Visible");
        ImGuiUtil.DrawTableColumn($"{_config.cachedCharacterData.Equipment.Weapon.Show}");
        ImGuiUtil.DrawTableColumn($"{_config.cachedCharacterData.Equipment.Weapon.Apply}");
        ImGui.TableNextRow();
        // customization
        ImGuiUtil.DrawTableColumn("Model ID");
        ImGuiUtil.DrawTableColumn(_config.cachedCharacterData.Customize.ModelId.ToString());
        ImGui.TableNextRow();

        foreach (var index in Enum.GetValues<CustomizeIndex>())
        {
            if (index.ToString() == "ModelId") continue; // Skip ModelId

            var property = typeof(Interop.Customize).GetProperty(index.ToString());
            dynamic? value = property?.GetValue(_config.cachedCharacterData.Customize);
            int valueInt = value?.Value;
            bool apply = value?.Apply;

            ImGuiUtil.DrawTableColumn(index.ToString());
            ImGuiUtil.DrawTableColumn(valueInt.ToString());
            ImGuiUtil.DrawTableColumn(apply ? "Apply" : "Keep");
            ImGui.TableNextRow();
        }

        ImGuiUtil.DrawTableColumn("Is Wet");
        ImGuiUtil.DrawTableColumn($"{_config.cachedCharacterData.Customize.Wetness.Value}");
        ImGui.TableNextRow();
        // finished debugging
    }

    public EquipItem Resolve(EquipSlot slot, CustomItemId itemId)
    {
        slot = slot.ToSlot();
        if (itemId == ItemIdVars.NothingId(slot))
            return ItemIdVars.NothingItem(slot);
        if (itemId == ItemIdVars.SmallclothesId(slot))
            return ItemIdVars.SmallClothesItem(slot);
        if (!itemId.IsItem || !_itemData.TryGetValue(itemId.Item, slot, out var item))
            return EquipItem.FromId(itemId);

        if (item.Type.ToSlot() != slot)
            return new EquipItem(string.Intern($"Invalid #{itemId}"), itemId, item.IconId, item.PrimaryId, item.SecondaryId, item.Variant, 0, 0,
                0,
                0);
        return item;
    }


}

