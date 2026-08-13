using System;
using System.Collections;
using System.Collections.Generic;
using Emby.ExternalPlayer.Localization;
using Emby.Web.GenericEdit;
using MediaBrowser.Model.GenericEdit;
using MediaBrowser.Model.LocalizationAttributes;

namespace Emby.ExternalPlayer;

public enum CustomPlayerPlatform
{
    [DescriptionL(nameof(PluginStrings.AnyPlatform), typeof(PluginStrings))]
    Any,

    [DescriptionL(nameof(PluginStrings.Windows), typeof(PluginStrings))]
    Windows,

    [DescriptionL(nameof(PluginStrings.MacOS), typeof(PluginStrings))]
    MacOS,

    [DescriptionL(nameof(PluginStrings.IOS), typeof(PluginStrings))]
    IOS,

    [DescriptionL(nameof(PluginStrings.Android), typeof(PluginStrings))]
    Android,

    [DescriptionL(nameof(PluginStrings.Linux), typeof(PluginStrings))]
    Linux,
}

[Flags]
public enum PlayerPlatforms
{
    None = 0,
    Windows = 1,
    MacOS = 2,
    IOS = 4,
    Android = 8,
    Linux = 16,
    All = Windows | MacOS | IOS | Android | Linux,
}

public sealed class CustomPlayerOptions : EditableOptionsBase
{
    public override string EditorTitle => string.IsNullOrWhiteSpace(ApplicationName)
        ? PluginStrings.CustomPlayerAdd
        : ApplicationName;

    [System.ComponentModel.Browsable(false)]
    public string Id { get; set; } = string.Empty;

    [DisplayNameL(nameof(PluginStrings.CustomPlayerEnabled), typeof(PluginStrings))]
    public bool Enabled { get; set; }

    [DisplayNameL(nameof(PluginStrings.ApplicationName), typeof(PluginStrings))]
    [DescriptionL(nameof(PluginStrings.ApplicationNameDescription), typeof(PluginStrings))]
    public string ApplicationName { get; set; } = string.Empty;

    [System.ComponentModel.Browsable(false)]
    public CustomPlayerPlatform Platform { get; set; } = CustomPlayerPlatform.Any;

    [System.ComponentModel.Browsable(false)]
    public PlayerPlatforms Platforms { get; set; }

    public bool ShouldSerializePlatform() => false;

    public PlayerPlatforms GetEffectivePlatforms() => Platforms != PlayerPlatforms.None
        ? Platforms
        : Platform switch
        {
            CustomPlayerPlatform.Windows => PlayerPlatforms.Windows,
            CustomPlayerPlatform.MacOS => PlayerPlatforms.MacOS,
            CustomPlayerPlatform.IOS => PlayerPlatforms.IOS,
            CustomPlayerPlatform.Android => PlayerPlatforms.Android,
            CustomPlayerPlatform.Linux => PlayerPlatforms.Linux,
            _ => PlayerPlatforms.All,
        };

    [DisplayNameL(nameof(PluginStrings.UrlTemplate), typeof(PluginStrings))]
    [DescriptionL(nameof(PluginStrings.UrlTemplateDescription), typeof(PluginStrings))]
    public string UrlTemplate { get; set; } = string.Empty;
}

public sealed class CustomPlayerOptionsCollection :
    List<CustomPlayerOptions>,
    IEditableObjectCollection
{
    public CustomPlayerOptionsCollection()
    {
    }

    public CustomPlayerOptionsCollection(IEnumerable<CustomPlayerOptions> collection)
        : base(collection)
    {
    }

    IEnumerator<IEditableObject> IEnumerable<IEditableObject>.GetEnumerator() =>
        GetEditableObjects().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => base.GetEnumerator();

    private IEnumerable<IEditableObject> GetEditableObjects()
    {
        for (var index = 0; index < Count; index++)
        {
            yield return this[index];
        }
    }
}
