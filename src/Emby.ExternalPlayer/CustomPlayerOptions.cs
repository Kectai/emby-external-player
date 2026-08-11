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

public sealed class CustomPlayerOptions : EditableOptionsBase
{
    public override string EditorTitle => string.IsNullOrWhiteSpace(ApplicationName)
        ? PluginStrings.CustomPlayer
        : ApplicationName;

    [DisplayNameL(nameof(PluginStrings.CustomPlayerEnabled), typeof(PluginStrings))]
    public bool Enabled { get; set; }

    [DisplayNameL(nameof(PluginStrings.ApplicationName), typeof(PluginStrings))]
    [DescriptionL(nameof(PluginStrings.ApplicationNameDescription), typeof(PluginStrings))]
    public string ApplicationName { get; set; } = string.Empty;

    [DisplayNameL(nameof(PluginStrings.Platform), typeof(PluginStrings))]
    public CustomPlayerPlatform Platform { get; set; } = CustomPlayerPlatform.Any;

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
