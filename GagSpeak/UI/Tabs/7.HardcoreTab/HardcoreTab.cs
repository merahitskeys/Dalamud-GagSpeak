﻿using System;
using ImGuiNET;
using OtterGui.Widgets;
using Dalamud.Interface.Utility;

namespace GagSpeak.UI.Tabs.HardcoreTab;
/// <summary> This class is used to handle the Hardcore Tab. </summary>
public class HardcoreTab : ITab
{
    private readonly    HardcoreSelector  _selector;
    private readonly    HardcoreMainPanel     _panel;

    public HardcoreTab(HardcoreSelector selector, HardcoreMainPanel panel) {
        _selector = selector;
        _panel = panel;
    }

    public void DrawContent()
    {
        _selector.Draw(GetSetSelectorSize());
        ImGui.SameLine();
        _panel.Draw();
    }

    public float GetSetSelectorSize() {
        return 140f * ImGuiHelpers.GlobalScale;
    }

    public ReadOnlySpan<byte> Label => "Hardcore"u8; // apply the tab label


}