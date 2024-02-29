using System;
using GagSpeak.Services;
using Newtonsoft.Json.Linq;
using System.IO;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using GagSpeak.Events;
using GagSpeak.Wardrobe;
using Dalamud.Game.ClientState.Keys;
using GagSpeak.CharacterData;
using System.Threading.Tasks;
namespace GagSpeak.Hardcore;
public partial class HardcoreManager : ISavable, IDisposable
{
    // this makes sure all of our options are independant for each player
    public List<HC_PerPlayerConfig> _perPlayerConfigs;

    // the list of entries to auto select no from when forced to stay is active
    public TextFolderNode StoredEntriesFolder { get; private set; } = new TextFolderNode { Name = "ForcedDeclineList" };
    
#region Ignores
    [JsonIgnore] // reflects the index of the whitelisted player that the active set is currently pulling its config from
    public int ActivePlayerCfgListIdx;
    
    [JsonIgnore] // reflects the index of the active set (currently enabled)
    public int ActiveHCsetIdx;

    [JsonIgnore] // the multipler added to cooldowns when stimulation is active, default is 1.0
    public double StimulationMultipler = 1.0;
    
    [JsonIgnore] // stores the last seen dialog text, will always be temp, and localized here as a source location to pull info from
    internal Tuple<string, List<string>> LastSeenDialogText { get; set; } = Tuple.Create(string.Empty, new List<string>());
    
    [JsonIgnore] // stores the last seen list target, will always be temp, and localized here as a source location to pull info from
    public string LastSeenListTarget { get; set; } = string.Empty;
    
    [JsonIgnore] // stores the last seen list selection, will always be temp, and localized here as a source location to pull info from
    public string LastSeenListSelection { get; set; } = string.Empty;


    [JsonIgnore]
    private readonly SaveService _saveService;
    [JsonIgnore]
    private readonly RestraintSetManager _restraintSetManager;
    [JsonIgnore]
    private readonly CharacterHandler _characterHandler;
    [JsonIgnore]
    private readonly RestraintSetListChanged _restraintSetListChanged;
    [JsonIgnore]
    private readonly RS_ToggleEvent _rsToggleEvent;
    [JsonIgnore]
    private readonly RS_PropertyChangedEvent _rsPropertyChanged;
    [JsonIgnore]
    private readonly InitializationManager _manager;
#endregion Ignores

    #pragma warning disable CS8618
    public HardcoreManager(SaveService saveService, RestraintSetListChanged restraintSetListChanged,
    InitializationManager manager, RS_PropertyChangedEvent propertyChanged, CharacterHandler handler, 
    RestraintSetManager restraintSetManager, RS_ToggleEvent rsToggleEvent) {
        _saveService = saveService;
        _characterHandler = handler;
        _rsPropertyChanged = propertyChanged;
        _manager = manager;
        _restraintSetListChanged = restraintSetListChanged;
        _restraintSetManager = restraintSetManager;
        _rsToggleEvent = rsToggleEvent;
        // setup a blank list
        _perPlayerConfigs = new List<HC_PerPlayerConfig>();
        // load the information from our storage file
        Load();
        // if the size of the list is still 0, set it to the size of our whitelist
        if (_perPlayerConfigs.Count == 0) {
            _perPlayerConfigs = _characterHandler.whitelistChars.Select(chara => new HC_PerPlayerConfig(_rsPropertyChanged)).ToList();
        }
        // save the information
        Save();
        // subscribe to the events
        _restraintSetListChanged.SetListModified += OnRestraintSetListModified;
        // subscribe to the toggle event
        _rsToggleEvent.SetToggled += OnRestraintSetToggled;
        // subscribe to the initializer, so we can finish setting everything up once we are ready
        _manager.RS_ManagerInitialized += ManagerReadyForHardcoreManager;
        // set completion task to true
        _manager._hardcoreManagerReadyForEvent.SetResult(true);
    }
    #pragma warning restore CS8618
#region Manager Helpers
    public void ManagerReadyForHardcoreManager() {
        GagSpeak.Log.Debug("======================== [ Completing Hardcore Manager Initialization ] ========================");
        // run size integrity check
        IntegrityCheck(_restraintSetManager._restraintSets.Count);
        
        // set the actively enabled set index to if one is
        ActiveHCsetIdx = _restraintSetManager._restraintSets.FindIndex(set => set._enabled);
        // find who it was that enabled the set, if it is enabled
        if(ActiveHCsetIdx != -1) {
            var EnabledBy = _restraintSetManager._restraintSets[ActiveHCsetIdx]._wasEnabledBy;
            GagSpeak.Log.Debug($"[HardcoreManager]  Active set was enabled by: {EnabledBy}");
            // find the index of whitelisted chars which contains the same name as the wasenabled by name, if it is not "self"
            ActivePlayerCfgListIdx = EnabledBy == "self" ? 0 : _characterHandler.whitelistChars.FindIndex(chara => chara._name == EnabledBy);
            // if the index if not -1, set up the multiplier
            if (ActivePlayerCfgListIdx != -1) {
                ApplyMultipler();
            } else {
                StimulationMultipler = 1.0;
            }
        } else {
            ActivePlayerCfgListIdx = 0;
            StimulationMultipler = 1.0;
        }
        // prune empty TextFolderNode enteries
        StoredEntriesFolder.CheckAndInsertRequired();
        StoredEntriesFolder.PruneEmpty();
        // save the information
        Save();
        // invoke the hardcoreManagerFinished method
        _manager.CompleteStep(InitializationSteps.HardcoreManagerInitialized);
    }
    public void Dispose() {
        // unsubscribe from the events
        _rsToggleEvent.SetToggled -= OnRestraintSetToggled;
        _manager.RS_ManagerInitialized -= ManagerReadyForHardcoreManager;
        _restraintSetListChanged.SetListModified -= OnRestraintSetListModified;
    }
#endregion Manager Helpers

    public string ToFilename(FilenameService filenameService)
        => filenameService.HardcoreSettingsFile;

    public void Save(StreamWriter writer) {
        using var j = new JsonTextWriter(writer);
        j.Formatting = Formatting.Indented;
        Serialize().WriteTo(j);
    }

    public void Save()
        => _saveService.DelaySave(this);

    public JObject Serialize() {
        // serialize the selectedIdx
        var array = new JArray();
        // for each of our restraint sets, serialize them and add them to the array
        foreach (var HC_SettingsforPlayer in _perPlayerConfigs) {
            array.Add(HC_SettingsforPlayer.Serialize());
        }
        return new JObject() {
            ["CharacterSettings"] = array,
            ["StoredEntriesFolder"] = JObject.FromObject(StoredEntriesFolder, new JsonSerializer { TypeNameHandling = TypeNameHandling.Auto })
        };
    }

    public void Load() {
        var file = _saveService.FileNames.HardcoreSettingsFile;
        if (!File.Exists(file)) {
            return;
        }
        try {
            var text = File.ReadAllText(file);
            var jsonObject = JObject.Parse(text);
            var characterSettingsArray = jsonObject["CharacterSettings"]?.Value<JArray>();
            _perPlayerConfigs = new List<HC_PerPlayerConfig>();
            if (characterSettingsArray != null) {
                foreach (var item in characterSettingsArray) {
                    var HC_SettingsforPlayer = new HC_PerPlayerConfig(_rsPropertyChanged);
                    HC_SettingsforPlayer.Deserialize(item.Value<JObject>());
                    _perPlayerConfigs.Add(HC_SettingsforPlayer);
                }
            }
            // stored entries
            var storedEntriesFolder = jsonObject["StoredEntriesFolder"]?.ToObject<TextFolderNode>();
            if (storedEntriesFolder != null) {
                StoredEntriesFolder = storedEntriesFolder;
            }
        } catch (Exception ex) {
            GagSpeak.Log.Error($"[HardcoreManager] Error loading HardcoreManager.json: {ex}");
        } finally {
            GagSpeak.Log.Debug($"[HardcoreManager] HardcoreManager.json loaded!");

        }
    }
}