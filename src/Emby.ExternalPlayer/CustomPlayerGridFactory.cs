using System;
using Emby.ExternalPlayer.Localization;
using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.DxGrid;

namespace Emby.ExternalPlayer;

internal static class CustomPlayerGridFactory
{
    public static CaptionItem CreateCaption() => new(PluginStrings.CustomPlayers);

    public static LabelItem CreateDescription() => new(PluginStrings.CustomPlayersDescription);

    public static DxDataGrid CreateGrid()
    {
        return new DxDataGrid(new DxGridOptions
        {
            columnAutoWidth = true,
            columnHidingEnabled = true,
            columns = new DxGridColumnList
            {
                new()
                {
                    dataField = nameof(CustomPlayerOptions.Enabled),
                    caption = PluginStrings.CustomPlayerEnabled,
                    dataType = DxGridColumn.ColumnDataType.boolean,
                    width = 80,
                },
                new()
                {
                    dataField = nameof(CustomPlayerOptions.ApplicationName),
                    caption = PluginStrings.ApplicationName,
                    dataType = DxGridColumn.ColumnDataType.@string,
                    minWidth = 150,
                },
                new()
                {
                    dataField = nameof(CustomPlayerOptions.Platform),
                    caption = PluginStrings.Platform,
                    dataType = DxGridColumn.ColumnDataType.@string,
                    minWidth = 110,
                    lookup = new DxGridLookup
                    {
                        dataSource = CreatePlatformOptions(),
                        displayExpr = nameof(GridLookupOption.Name),
                        valueExpr = nameof(GridLookupOption.Value),
                    },
                },
                new()
                {
                    dataField = nameof(CustomPlayerOptions.UrlTemplate),
                    caption = PluginStrings.UrlTemplate,
                    dataType = DxGridColumn.ColumnDataType.@string,
                    minWidth = 220,
                },
            },
            editing = new DxGridEditing
            {
                allowAdding = true,
                allowDeleting = true,
                allowUpdating = true,
                mode = DxGridEditing.GridEditMode.row,
                useIcons = true,
                texts = new DxGridEditingTexts
                {
                    addRow = PluginStrings.CustomPlayerAdd,
                    editRow = PluginStrings.CustomPlayerEdit,
                    deleteRow = PluginStrings.CustomPlayerDelete,
                    confirmDeleteMessage = PluginStrings.CustomPlayerDeleteConfirm,
                    confirmDeleteTitle = PluginStrings.CustomPlayerDeleteTitle,
                    saveRowChanges = PluginStrings.CustomPlayerSave,
                    cancelRowChanges = PluginStrings.CustomPlayerCancel,
                },
            },
            noDataText = PluginStrings.CustomPlayersEmpty,
            paging = new DxGridPaging
            {
                enabled = false,
            },
            rowAlternationEnabled = true,
            showBorders = true,
            showColumnLines = false,
            showRowLines = true,
            wordWrapEnabled = true,
        });
    }

    private static GridLookupOption[] CreatePlatformOptions() =>
        new[]
        {
            new GridLookupOption(CustomPlayerPlatform.Any, PluginStrings.AnyPlatform),
            new GridLookupOption(CustomPlayerPlatform.Windows, PluginStrings.Windows),
            new GridLookupOption(CustomPlayerPlatform.MacOS, PluginStrings.MacOS),
            new GridLookupOption(CustomPlayerPlatform.IOS, PluginStrings.IOS),
            new GridLookupOption(CustomPlayerPlatform.Android, PluginStrings.Android),
            new GridLookupOption(CustomPlayerPlatform.Linux, PluginStrings.Linux),
        };

    private sealed class GridLookupOption
    {
        public GridLookupOption(CustomPlayerPlatform value, string name)
        {
            Value = value.ToString();
            Name = name;
        }

        public string Value { get; }

        public string Name { get; }
    }
}
