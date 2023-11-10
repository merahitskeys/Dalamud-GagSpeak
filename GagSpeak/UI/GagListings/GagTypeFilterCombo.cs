using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using ImGuiScene;
using OtterGui;
using OtterGui.Raii;
﻿using Dalamud.Game.Text;
using Dalamud.Plugin;
using System.Diagnostics;
using Num = System.Numerics;
using System.Collections.Generic;
using System.Linq;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using OtterGui.Widgets;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

using GagSpeak.Services;
using GagSpeak.UI.Helpers;
using GagSpeak.Events;
using GagSpeak.Chat;

namespace GagSpeak.UI.GagListings;

public sealed class GagTypeFilterCombo
{
    private GagSpeakConfig _config;
    private string _comboSearchText;
    private Dictionary<string,int> _gagTypes;

    private string _displayLabel; // the "current item"

    public GagTypeFilterCombo(Dictionary<string,int> gagTypes, string displayLabel, GagSpeakConfig config) {
        _comboSearchText = string.Empty;
        _displayLabel = displayLabel;
        _gagTypes = gagTypes;
        _config = config;
    } 

    /// <summary>
    /// This function draws ImGui's Combo list, but with a search filter. (and for Dictionary<string,int>)
    /// <list type="bullet">
    /// <item><c>ID</c><param name="ID"> - The list of items to display in the combo box</param></item>
    /// <item><c>label</c><param name="label"> - The label to display outside the combo box</param></item>
    /// <item><c>layerindex</c><param name="layerIndex"> - a list where the stored selection from the list is saved</param></item>
    /// </list>
    /// </summary>
    public void Draw(int ID, string label, int layerIndex, int width) {
        try
        {
        ImGui.SetNextItemWidth(width);
        using( var gagTypeOneCombo = ImRaii.Combo($"##{ID}_Type", _config.selectedGagTypes[layerIndex], ImGuiComboFlags.PopupAlignLeft | ImGuiComboFlags.HeightLargest)) { // Create the combo
            //ImGui.SetKeyboardFocusHere(); // focus our text into the filter thingy
            if( gagTypeOneCombo ) { // Assign it an ID if combo is sucessful.
                // add the popup state
                using var id = ImRaii.PushId($"##{ID}_Type"); // Push an ID for the combo box (based on label / name)
                ImGui.SetNextItemWidth(width); // Set filter length to full
                if( ImGui.InputTextWithHint("##filter", "Filter...", ref _comboSearchText, 255 ) ) { // Draw filter bar
                    // If the search bar is empty, display all the types from the strings in contentList, otherwise, display only search matches
                    _gagTypes = string.IsNullOrEmpty(_comboSearchText) ? (
                        _config.GagTypes
                    ) : (
                        _config.GagTypes.Where(x=>x.Key.ToLower().Contains(_comboSearchText.ToLower())).ToDictionary(x=>x.Key, x=>x.Value)
                    );
                }
                // Now that we have our results, so draw the childs
                var       height = ImGui.GetTextLineHeightWithSpacing() * 12 - ImGui.GetFrameHeight() - ImGui.GetStyle().WindowPadding.Y;
                using var child = ImRaii.Child("Child", new Vector2( width, 200),true);
                using var indent = ImRaii.PushIndent(ImGuiHelpers.GlobalScale);

                // draw list
                foreach( var item in _gagTypes.Keys ) { // We will draw out one selectable for each item.
                    if( ImGui.Selectable( item, item == _config.selectedGagTypes[layerIndex] ) ) { // If our item is selected, set it and break
                        _config.selectedGagTypes[layerIndex] = item;
                        GagSpeak.Log.Debug($"GagSpeak: Layer {layerIndex} GagType changed to {item}");
                        // we need to clear the search filter now and close the window.
                        _comboSearchText = string.Empty;
                        _gagTypes = _config.GagTypes;
                        ImGui.CloseCurrentPopup();
                        _config.Save();
                        return;
                    }
                }
            }
        }
        }
        catch (Exception e)
        {
            GagSpeak.Log.Debug(e.ToString());
        }
    }
}