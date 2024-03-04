using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Internal.Notifications;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using GagSpeak.Events;
using GagSpeak.Interop.Penumbra;
using GagSpeak.Wardrobe;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;

namespace GagSpeak.Interop.Penumbra;
public class ModAssociations : IDisposable
{
    private readonly PenumbraService          _penumbra;
    private readonly RestraintSetManager      _manager;
    private readonly ModCombo                 _modCombo;
    private readonly IClientState             _clientState;
    private readonly RS_ToggleEvent           _rsToggleEvent;

    public ModAssociations(PenumbraService penumbra, RestraintSetManager manager,
    RS_ToggleEvent rsToggleEvent, IClientState clientState)
    {
        _penumbra = penumbra;
        _manager  = manager;
        _modCombo = new ModCombo(penumbra, GagSpeak.Log);
        _rsToggleEvent = rsToggleEvent;
        _clientState = clientState;

        _rsToggleEvent.SetToggled += ApplyModsOnSetToggle;
    }

    public void Dispose() {
        _rsToggleEvent.SetToggled -= ApplyModsOnSetToggle;
    }

    private void ApplyModsOnSetToggle(object sender, RS_ToggleEventArgs e) {
        // if the set is being enabled, we should toggle on the mods
        if(_clientState.IsLoggedIn && _clientState.LocalContentId != 0) {
            if (e.ToggleType == RestraintSetToggleType.Enabled) {
                foreach (var (mod, settings) in _manager._restraintSets[e.SetIndex]._associatedMods) {
                    _penumbra.SetMod(mod, settings, true, _manager._restraintSets[e.SetIndex]._disableModsWhenInactive[e.SetIndex]);
                }
            }
            // otherwise, we should toggle off the mods
            else {
                foreach (var (mod, settings) in _manager._restraintSets[e.SetIndex]._associatedMods) {
                    _penumbra.SetMod(mod, settings, false, _manager._restraintSets[e.SetIndex]._disableModsWhenInactive[e.SetIndex]);
                }
            }
        }
    }

    // main draw function for the mod associations table
    public void Draw() {
        DrawTable();
    }

    // draw the table for constructing the associated mods.
    private void DrawTable() {
        using var table = ImRaii.Table("Mods", 4, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY);
        if (!table) { return; }

        ImGui.TableSetupColumn("##Delete",       ImGuiTableColumnFlags.WidthFixed, ImGui.GetFrameHeight());
        ImGui.TableSetupColumn("Mods to enable with this Restraint Set",       ImGuiTableColumnFlags.WidthStretch);        
        ImGui.TableSetupColumn("Toggle",          ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("Toggle").X);        
        ImGui.TableSetupColumn("##Update",       ImGuiTableColumnFlags.WidthFixed, ImGui.GetFrameHeight());             // update to reflect what is in
        ImGui.TableHeadersRow();

        Mod? removedMod = null;
        (Mod mod, ModSettings settings)? updatedMod = null;
        foreach (var ((mod, settings), idx) in _manager._restraintSets[_manager._selectedIdx]._associatedMods.WithIndex())
        {
            using var id = ImRaii.PushId(idx);
            DrawAssociatedModRow(mod, settings, out var removedModTmp, out var updatedModTmp);
            if (removedModTmp.HasValue)
                removedMod = removedModTmp;
            if (updatedModTmp.HasValue)
                updatedMod = updatedModTmp;
        }

        DrawNewModRow();

        if (removedMod.HasValue)
            _manager.RemoveMod(_manager._selectedIdx, removedMod.Value);
        
        if (updatedMod.HasValue)
            _manager.UpdateMod(_manager._selectedIdx, updatedMod.Value.mod, updatedMod.Value.settings);
    }

    private void DrawAssociatedModRow(Mod mod, ModSettings settings, out Mod? removedMod, out (Mod, ModSettings)? updatedMod) {
        removedMod = null;
        updatedMod = null;
        ImGui.TableNextColumn();
        // delete icon
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Trash.ToIconString(), new Vector2(ImGui.GetFrameHeight()),
        "Delete this mod from associations", !ImGui.GetIO().KeyShift, true)) {
            removedMod = mod;
        }
        // the name of the appended mod
        ImGui.TableNextColumn();
        ImGui.Selectable($"{mod.Name}##name");
        if(ImGui.IsItemHovered()) { ImGui.SetTooltip("Mod to be enabled when restraint set it turned on."); }
        // if we should enable or disable this mod list (all buttons should sync)
        ImGui.TableNextColumn();
        var iconText = _manager._restraintSets[_manager._selectedIdx]._disableModsWhenInactive[_manager._selectedIdx] ? FontAwesomeIcon.Check : FontAwesomeIcon.Times;
        var helpText = _manager._restraintSets[_manager._selectedIdx]._disableModsWhenInactive[_manager._selectedIdx] ? "Mods are disabled when set is disabled" : "Mods will stay enabled after set is turned off";
        if (ImGuiUtil.DrawDisabledButton(iconText.ToIconString(), new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetFrameHeight()),
        helpText, false, true)) {
            _manager._restraintSets[_manager._selectedIdx]._disableModsWhenInactive[_manager._selectedIdx] 
            = !_manager._restraintSets[_manager._selectedIdx]._disableModsWhenInactive[_manager._selectedIdx];
            _manager.Save();
        }
        // button to update the status the mod from penumbra
        ImGui.TableNextColumn();
        ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.RedoAlt.ToIconString(), new Vector2(ImGui.GetFrameHeight()),
        "Update the settings of this mod association", false, true);
        if (ImGui.IsItemHovered()) {
            var (_, newSettings) = _penumbra.GetMods().FirstOrDefault(m => m.Mod == mod);
            if (ImGui.IsItemClicked()) {
                updatedMod = (mod, newSettings);
            }
            
            using var style = ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 2 * ImGuiHelpers.GlobalScale);
            using var tt = ImRaii.Tooltip();
            ImGui.Separator();
            var namesDifferent = mod.Name != mod.DirectoryName;
            ImGui.Dummy(new Vector2(300 * ImGuiHelpers.GlobalScale, 0));
            using (ImRaii.Group()) {
                if (namesDifferent)
                    ImGui.TextUnformatted("Directory Name");
                ImGui.TextUnformatted("Enabled");
                ImGui.TextUnformatted("Priority");
                ModCombo.DrawSettingsLeft(newSettings);
            }

            ImGui.SameLine(Math.Max(ImGui.GetItemRectSize().X + 3 * ImGui.GetStyle().ItemSpacing.X, 150 * ImGuiHelpers.GlobalScale));
            using (ImRaii.Group()) {
                if (namesDifferent)
                    ImGui.TextUnformatted(mod.DirectoryName);
                ImGui.TextUnformatted(newSettings.Enabled.ToString());
                ImGui.TextUnformatted(newSettings.Priority.ToString());
                ModCombo.DrawSettingsRight(newSettings);
            }
        }
    }

    private static void DrawAssociatedModTooltip(ModSettings settings)
    {
        if (settings is not { Enabled: true, Settings.Count: > 0 } || !ImGui.IsItemHovered())
            return;

        using var t = ImRaii.Tooltip();
        ImGui.TextUnformatted("This will also try to apply the following settings to the current collection:");

        ImGui.NewLine();
        using (var _ = ImRaii.Group())
        {
            ModCombo.DrawSettingsLeft(settings);
        }

        ImGui.SameLine(ImGui.GetContentRegionAvail().X / 2);
        using (var _ = ImRaii.Group())
        {
            ModCombo.DrawSettingsRight(settings);
        }
    }

    private void DrawNewModRow()
    {
        var currentName = _modCombo.CurrentSelection.Mod.Name;
        ImGui.TableNextColumn();
        var tt = currentName.IsNullOrEmpty()
            ? "Please select a mod first."
            : _manager._restraintSets[_manager._selectedIdx]._associatedMods.ContainsKey(_modCombo.CurrentSelection.Mod)
                ? "The design already contains an association with the selected mod."
                : string.Empty;

        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Plus.ToIconString(), new Vector2(ImGui.GetFrameHeight()), tt, tt.Length > 0,
                true))
            _manager.AddMod(_manager._selectedIdx, _modCombo.CurrentSelection.Mod, _modCombo.CurrentSelection.Settings);
        ImGui.TableNextColumn();
        _modCombo.Draw("##new", currentName.IsNullOrEmpty() ? "Select new Mod..." : currentName, string.Empty,
            ImGui.GetContentRegionAvail().X, ImGui.GetTextLineHeight());
    }
}