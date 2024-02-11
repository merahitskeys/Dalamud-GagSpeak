using System;
using System.Numerics;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Interface;
using Dalamud.Interface.Internal;
using ImGuiNET;
using OtterGui.Raii;
using OtterGui.Widgets;
using GagSpeak.ChatMessages;
using GagSpeak.Interop;
using GagSpeak.Services;
using GagSpeak.Utility;
using GagSpeak.UI.Tabs.GeneralTab;
using GagSpeak.CharacterData;

namespace GagSpeak.UI.Tabs.ConfigSettingsTab;
/// <summary> This class is used to handle the ConfigSettings Tab. </summary>
public class ConfigSettingsTab : ITab
{
    private readonly    IDalamudTextureWrap             _dalamudTextureWrap;    // for loading images
    private readonly    GagSpeakConfig                  _config;                // for getting the config
    private readonly    CharacterHandler                _characterHandler;      // for getting the whitelist
    private readonly    UiBuilder                       _uiBuilder;             // for loading images
    private readonly    GlamourerFunctions              _glamourerInterop;      // for getting the glamourer interop
    private             Dictionary<string, string[]>    _languages;             // the dictionary of languages & dialects 
    private             string[]                        _currentDialects;       // the array of language names
    private             string                          _activeDialect;         // the dialect selected

    /// <summary> Initializes a new instance of the <see cref="ConfigSettingsTab"/> class. <summary>
    public ConfigSettingsTab(GagSpeakConfig config, CharacterHandler characterHandler, UiBuilder uiBuilder, DalamudPluginInterface pluginInterface,
    GagListingsDrawer gagListingsDrawer, GagService gagService, GlamourerFunctions GlamourerService) {
        _config = config;
        _characterHandler = characterHandler;
        _uiBuilder = uiBuilder;
        _glamourerInterop = GlamourerService;
        var imagePath = Path.Combine(pluginInterface.AssemblyLocation.Directory?.FullName!, "iconUI.png");
        var IconImage = _uiBuilder.LoadImage(imagePath);
        // sets the icon
        _dalamudTextureWrap = IconImage;
        // load the dropdown info
        _languages = new Dictionary<string, string[]> {
            { "English", new string[] { "US", "UK" } },
            { "Spanish", new string[] { "Spain", "Mexico" } },
            { "French", new string[] { "France", "Quebec" } },
            { "Japanese", new string[] { "Japan" } }
        };

        _currentDialects = _languages[_config.language]; // put all dialects into an array
        _activeDialect = GetDialectFromConfigDialect();  // set the active dialect to the one in the config
    }

    public ReadOnlySpan<byte> Label => "Settings"u8; // apply the tab label

    /// <summary> This Function draws the content for the window of the ConfigSettings Tab </summary>
    public void DrawContent() {
        // Create a child for the Main Window (not sure if we need this without a left selection panel)
        using var child = ImRaii.Child("MainWindowChild");
        if (!child)
            return;
        // Draw the child grouping for the ConfigSettings Tab
        using (var child2 = ImRaii.Child("SettingsChild")) {
            DrawHeader();
            DrawConfigSettings();
        }
    }

    /// <summary> This Function draws the header for the ConfigSettings Tab </summary>
    private void DrawHeader()
        => WindowHeader.Draw("Configuration & Settings", 0, ImGui.GetColorU32(ImGuiCol.FrameBg), 0);

    /// <summary> This Function draws the content for the ConfigSettings Tab </summary>
    private void DrawConfigSettings() {
        // Lets start by drawing the child.
        using var child = ImRaii.Child("##SettingsPanel", -Vector2.One, true, ImGuiWindowFlags.NoScrollbar);
        // define our spacing
        var spacing = ImGui.GetStyle().ItemInnerSpacing with { Y = ImGui.GetStyle().ItemInnerSpacing.Y };
        ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, spacing);

        if(_characterHandler.playerChar._directChatGarblerLocked) {ImGui.BeginDisabled();}
        UIHelpers.CheckboxNoConfig("Direct Chat Garbler",
            "AUTOMATICALLY Translate any NON-COMMAND chat message to gagspeak.\n\n"+
            ">> This will ONLY occur in any of the checked off channels under ENABLED CHANNELS below.\n\n"+
            ">> This is Serverside, just like /gsm.",
            _characterHandler.playerChar._directChatGarblerActive, 
            v => _characterHandler.ToggleDirectChatGarbler()
        );
        if(_characterHandler.playerChar._directChatGarblerLocked) {ImGui.EndDisabled();}
        ImGui.SameLine();
        UIHelpers.CheckboxNoConfig("Garbler Zone Warnings",
            "Sends a warning to your chat every time you switch zones when direct chat garbler is enabled.\n",
            _characterHandler.playerChar._liveGarblerWarnOnZoneChange,
            v => _characterHandler.ToggleZoneWarnings()
        );
        ImGui.SameLine();
        // Create the language dropdown
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X*.65f);
        string prevLang = _config.language; // to only execute code to update data once it is changed
        if (ImGui.BeginCombo("##Language", _config.language)) {
            foreach (var language in _languages.Keys.ToArray()) {
                bool isSelected = (_config.language == language);
                if (ImGui.Selectable(language, isSelected)) {
                    _config.language = language;
                    GagSpeak.Log.Debug($"[ConfigSettingsTab] Language changed to: {_config.language}");
                }
                if (isSelected) {
                    ImGui.SetItemDefaultFocus();
                }
            }
            ImGui.EndCombo();
        }
        if(ImGui.IsItemHovered()) {
            ImGui.SetTooltip("Select the language you want to use for GagSpeak.");
        }
        //update if changed 
        if (prevLang != _config.language) { // set the language to the newly selected language once it is changed
            _currentDialects = _languages[_config.language]; // update the dialects for the new language
            _activeDialect = _currentDialects[0]; // set the active dialect to the first dialect of the new language
            SetConfigDialectFromDialect(_activeDialect);
            _config.Save();
        }
        ImGui.SameLine(); 
        // Create the dialect dropdown
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        string[] dialects = _languages[_config.language];
        string prevDialect = _activeDialect; // to only execute code to update data once it is changed
        if (ImGui.BeginCombo("##Dialect", _activeDialect)) {
            foreach (var dialect in dialects) {
                bool isSelected = (_activeDialect == dialect);
                if (ImGui.Selectable(dialect, isSelected)) {
                    _activeDialect = dialect;
                }
                if (isSelected) {
                    ImGui.SetItemDefaultFocus();
                }
            }
            ImGui.EndCombo();
        }
        if(ImGui.IsItemHovered()) {
            ImGui.SetTooltip("Select the Dialect you want to use for GagSpeak.");
        }
        //update if changed
        if (prevDialect != _activeDialect) { // set the dialect to the newly selected dialect once it is changed
            SetConfigDialectFromDialect(_activeDialect);
            _config.Save();
        }
        ImGui.Separator();
        ImGui.Columns(2,"ConfigColumns", false);
        ImGui.SetColumnWidth(0, ImGui.GetWindowWidth() / 2 - 10);
        UIHelpers.CheckboxNoConfig("Commands From Friends", 
            "Commands & Interactions from other players are only recieved by GagSpeak if in your Friend List.",
            _characterHandler.playerChar._doCmdsFromFriends,
            v => _characterHandler.ToggleCmdFromFriends()
        );
        UIHelpers.CheckboxNoConfig("Commands From Party",
            "Commands & Interactions from other players are only recieved by GagSpeak if in the Party List.",
            _characterHandler.playerChar._doCmdsFromParty,
            v => _characterHandler.ToggleCmdFromParty()
        );
        UIHelpers.CheckboxNoConfig("Enable Wardrobe",
            "Must be enabled for anything in the Kink Wardrobe component of GagSpeak to function.",
            _characterHandler.playerChar._enableWardrobe,
            v => _characterHandler.ToggleEnableWardrobe()
        );

        UIHelpers.CheckboxNoConfig("Gag Items Auto-Equip",
            "If this option is enabled, anyone in your whitelist that applies a gag to you, applies the GagStorage item as well.\n"+
            ">> This does not happen regardless if that item is disabled in your GagStorage.",
            _characterHandler.playerChar._allowItemAutoEquip,
            v => _characterHandler.ToggleGagItemAutoEquip()
        );

        UIHelpers.CheckboxNoConfig("Restraint Sets Auto-Equip",
            "If enabled, anyone in your whitelist, any restraint set that is enabled will auto equip.\n"+
            ">> You can manually disable this in the wardrobe tab so they only auto equip when you want them to.",
            _characterHandler.playerChar._allowRestraintSetAutoEquip,
            v => _characterHandler.ToggleRestraintSetAutoEquip()
        );

        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X*.7f);
        RevertStyle prevRevertStyle = _characterHandler.playerChar._revertStyle; // to only execute code to update data once it is changed
        if (ImGui.BeginCombo("##RevertStyle", _characterHandler.playerChar._revertStyle.ToString())) {
            foreach (RevertStyle style in Enum.GetValues(typeof(RevertStyle))) {
                bool isSelected = (_characterHandler.playerChar._revertStyle == style);
                if (ImGui.Selectable(style.ToString(), isSelected)) {
                    _characterHandler.SetRevertStyle(style);
                    GagSpeak.Log.Debug($"[ConfigSettingsTab] RevertStyle changed to: {_characterHandler.playerChar._revertStyle}");
                }
                if (isSelected) {
                    ImGui.SetItemDefaultFocus();
                }
            }
            ImGui.EndCombo();
        }
        if(ImGui.IsItemHovered()) {
            ImGui.SetTooltip("Select how you want your attire to revert when a restraint set is removed.");
        }
        UIHelpers.CheckboxNoConfig("Enable Puppeteer",
            "Allows the use of the Puppeteer Module of GagSpeak.\n"+
            ">> This will allow anyone in your whitelist to use the Puppeteer Module on you who is allowed.",
            _characterHandler.playerChar._allowPuppeteer,
            v => _characterHandler.TogglePuppeteer()
        );
        UIHelpers.CheckboxNoConfig("Enable Toybox", 
            "Allows the use of the Toybox Module of GagSpeak.\n"+
            ">> This will allow anyone in your whitelist to use the Toybox Module on you who is allowed.",
            _characterHandler.playerChar._enableToybox,
            v => _characterHandler.ToggleEnableToybox()
        );
        

        // channel listings
        ImGui.NextColumn();
        ImGui.SetCursorPosX(ImGui.GetCursorPosX());
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 10);
        // you might normally want to embed resources and load them from the manifest stream
        ImGui.Image(_dalamudTextureWrap.ImGuiHandle, new Vector2(_dalamudTextureWrap.Width, _dalamudTextureWrap.Height));
        ImGui.Columns(1);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 10);
        // Show Debug Menu when Debug logging is enabled
        if(_characterHandler.playerChar._directChatGarblerLocked == true) {ImGui.BeginDisabled();}
        ImGui.Text("Enabled Channels:"); ImGui.Separator();
        var i = 0;
        foreach (var e in ChatChannel.GetOrderedChannels()) {
            // See if it is already enabled by default
            var enabled = _config.ChannelsGagSpeak.Contains(e);
            // Create a new line after every 4 columns
            if (i != 0 && (i==5 || i==9 || i==13 || i==17 || i == 21)) {
                ImGui.NewLine();
                //i = 0;
            }
            // Move to the next row if it is LS1 or CWLS1
            if (e is ChatChannel.ChatChannels.LS1 or ChatChannel.ChatChannels.CWL1)
                ImGui.Separator();

            if (ImGui.Checkbox($"{e}", ref enabled)) {
                // See If the UIHelpers.Checkbox is clicked, If not, add to the list of enabled channels, otherwise, remove it.
                if (enabled) _config.ChannelsGagSpeak.Add(e);
                else _config.ChannelsGagSpeak.Remove(e);
                _config.Save();
            }

            ImGui.SameLine();
            i++;
        }
        // Set the columns back to 1 now and space over to next section
        ImGui.Columns(1);
        ImGui.PopStyleVar();
        if(_characterHandler.playerChar._directChatGarblerLocked == true) {ImGui.EndDisabled();}
    }

    /// <summary>
    /// Used to restore the dropdown to the selection from the config
    /// </summary>
    private string GetDialectFromConfigDialect() {
        switch (_config.languageDialect) {
            case "IPA_US": return "US";
            case "IPA_UK": return "UK";
            case "IPA_FRENCH": return "France";
            case "IPA_QUEBEC": return "Quebec";
            case "IPA_JAPAN": return "Japan";
            case "IPA_SPAIN": return "Spain";
            case "IPA_MEXICO": return "Mexico";
            default: return "US";
        }
    }

    /// <summary>
    /// Sets the config dialect from dialect string selected by the dropdown.
    /// </summary>
    private void SetConfigDialectFromDialect(string dialect) {
        switch (dialect) {
            case "US": _config.languageDialect = "IPA_US"; break;
            case "UK": _config.languageDialect = "IPA_UK"; break;
            case "France": _config.languageDialect = "IPA_FRENCH"; break;
            case "Quebec": _config.languageDialect = "IPA_QUEBEC"; break;
            case "Japan": _config.languageDialect = "IPA_JAPAN"; break;
            case "Spain": _config.languageDialect = "IPA_SPAIN"; break;
            case "Mexico": _config.languageDialect = "IPA_MEXICO"; break;
            default: _config.languageDialect = "IPA_US"; break;
        }
    }
}
