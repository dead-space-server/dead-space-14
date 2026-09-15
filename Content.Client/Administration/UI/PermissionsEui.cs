using System.Linq;
using System.Numerics;
using Content.Client.Administration.Managers;
using Content.Client.DeadSpace.Stylesheets;
using Content.Client.Eui;
using Content.Client.Stylesheets;
using Content.Shared.Administration;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Utility;
using static Content.Shared.Administration.PermissionsEuiMsg;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.Administration.UI
{
    [UsedImplicitly]
    public sealed class PermissionsEui : BaseEui
    {
        private const int NoRank = -1;

        [Dependency] private readonly IClientAdminManager _adminManager = default!;

        private readonly Menu _menu;
        private readonly List<BaseWindow> _subWindows = new();

        // DS14-start
        private PermissionsEuiState? _lastState;
        private const float TableActionColumnWidth = 160f;
        private const float AdminNameColumnWidth = 200f;
        private const float AdminRankColumnWidth = 145f;
        private const float AdminTitleColumnWidth = 145f;
        private const float AdminFlagsColumnMinWidth = 160f;
        private const float RankNameColumnWidth = 220f;
        private const float RankFlagsColumnMinWidth = 220f;
        private const int TableGapWidth = 8;
        private const float TableTextApproxGlyphWidth = 8f;
        private const float TableTextPaddingWidth = 24f;
        private const float AdminTableMinWidth =
            TableActionColumnWidth + AdminNameColumnWidth + AdminRankColumnWidth + AdminTitleColumnWidth + AdminFlagsColumnMinWidth + TableGapWidth * 4;
        private const float RankTableMinWidth =
            TableActionColumnWidth + RankNameColumnWidth + RankFlagsColumnMinWidth + TableGapWidth * 2;
        private const float FlagChoiceColumnWidth = 72f;
        private const float AdminFlagSpacerColumnWidth = 12f;
        private const float AdminFlagNameColumnWidth = 260f;
        private const float AdminFlagTableMinWidth = FlagChoiceColumnWidth * 3 + AdminFlagSpacerColumnWidth + AdminFlagNameColumnWidth;
        // DS14-end

        private Dictionary<int, PermissionsEuiState.AdminRankData> _ranks =
            new();

        public PermissionsEui()
        {
            IoCManager.InjectDependencies(this);

            _menu = new Menu(this);
            _menu.AddAdminButton.OnPressed += AddAdminPressed;
            _menu.AddAdminRankButton.OnPressed += AddAdminRankPressed;
            _menu.OnClose += CloseEverything;
        }

        public override void Closed()
        {
            base.Closed();

            SendMessage(new CloseEuiMessage());
            CloseEverything();
        }

        private void CloseEverything()
        {
            foreach (var subWindow in _subWindows.ToArray())
            {
                subWindow.Close();
            }

            _menu.Close();
        }

        private void AddAdminPressed(BaseButton.ButtonEventArgs obj)
        {
            OpenEditWindow(null);
        }

        private void AddAdminRankPressed(BaseButton.ButtonEventArgs obj)
        {
            OpenRankEditWindow(null);
        }


        private void OnEditPressed(PermissionsEuiState.AdminData admin)
        {
            OpenEditWindow(admin);
        }

        private void OpenEditWindow(PermissionsEuiState.AdminData? data)
        {
            var window = new EditAdminWindow(this, data);
            window.SaveButton.OnPressed += _ => SaveAdminPressed(window);
            window.OpenCentered();
            window.OnClose += () => _subWindows.Remove(window);
            if (data != null)
            {
                window.RemoveButton!.OnPressed += _ => RemoveButtonPressed(window);
            }

            _subWindows.Add(window);
        }


        private void OpenRankEditWindow(KeyValuePair<int, PermissionsEuiState.AdminRankData>? rank)
        {
            var window = new EditAdminRankWindow(this, rank);
            window.SaveButton.OnPressed += _ => SaveAdminRankPressed(window);
            window.OpenCentered();
            window.OnClose += () => _subWindows.Remove(window);
            if (rank != null)
            {
                window.RemoveButton!.OnPressed += _ => RemoveRankButtonPressed(window);
            }

            _subWindows.Add(window);
        }

        private void RemoveButtonPressed(EditAdminWindow window)
        {
            SendMessage(new RemoveAdmin { UserId = window.SourceData!.Value.UserId });

            window.Close();
        }

        private void RemoveRankButtonPressed(EditAdminRankWindow window)
        {
            SendMessage(new RemoveAdminRank { Id = window.SourceId!.Value });

            window.Close();
        }

        private void SaveAdminPressed(EditAdminWindow popup)
        {
            popup.CollectSetFlags(out var pos, out var neg);

            int? rank = popup.RankButton.SelectedId;
            if (rank == NoRank)
            {
                rank = null;
            }

            var title = string.IsNullOrWhiteSpace(popup.TitleEdit.Text) ? null : popup.TitleEdit.Text;
            var suspended = popup.SuspendedCheckbox.Pressed;

            if (popup.SourceData is { } src)
            {
                SendMessage(new UpdateAdmin
                {
                    UserId = src.UserId,
                    Title = title,
                    PosFlags = pos,
                    NegFlags = neg,
                    RankId = rank,
                    Suspended = suspended,
                });
            }
            else
            {
                DebugTools.AssertNotNull(popup.NameEdit);

                SendMessage(new AddAdmin
                {
                    UserNameOrId = popup.NameEdit!.Text,
                    Title = title,
                    PosFlags = pos,
                    NegFlags = neg,
                    RankId = rank,
                    Suspended = suspended,
                });
            }

            popup.Close();
        }


        private void SaveAdminRankPressed(EditAdminRankWindow popup)
        {
            var flags = popup.CollectSetFlags();
            var name = popup.NameEdit.Text;

            if (popup.SourceId is { } src)
            {
                SendMessage(new UpdateAdminRank
                {
                    Id = src,
                    Flags = flags,
                    Name = name,
                });
            }
            else
            {
                SendMessage(new AddAdminRank
                {
                    Flags = flags,
                    Name = name
                });
            }

            popup.Close();
        }

        public override void Opened()
        {
            _menu.OpenCentered();
        }

        public override void HandleState(EuiStateBase state)
        {
            var s = (PermissionsEuiState) state;

            if (s.IsLoading)
            {
                return;
            }

            _ranks = s.AdminRanks;
            _lastState = s;

            RebuildLists();
        }

        // DS14-start
        private void RebuildLists()
        {
            if (_lastState == null)
            {
                return;
            }

            RebuildAdminsList(_lastState);
            RebuildRanksList(_lastState);
        }

        private void RebuildAdminsList(PermissionsEuiState state)
        {
            var query = _menu.AdminSearch.Text.Trim();
            _menu.AdminsList.RemoveAllChildren();

            var rows = new List<(PermissionsEuiState.AdminData Admin, string Name, string Title, bool TitleInherited, string Rank, bool RankInherited, string FlagsText, AdminFlags CombinedFlags)>();
            foreach (var admin in state.Admins.OrderBy(d => d.UserName ?? d.UserId.ToString()))
            {
                var name = admin.UserName ?? admin.UserId.ToString();
                var title = admin.Title ?? Loc.GetString("permissions-eui-edit-admin-title-control-text").ToLowerInvariant();
                var titleInherited = admin.Title == null;
                bool italic;
                string rank;
                var combinedFlags = admin.PosFlags;
                if (admin.RankId is { } rankId)
                {
                    if (state.AdminRanks.TryGetValue(rankId, out var rankData))
                    {
                        italic = false;
                        rank = rankData.Name;
                        combinedFlags |= rankData.Flags;
                    }
                    else
                    {
                        italic = true;
                        rank = Loc.GetString("permissions-eui-edit-no-rank-text").ToLowerInvariant();
                    }
                }
                else
                {
                    italic = true;
                    rank = Loc.GetString("permissions-eui-edit-no-rank-text").ToLowerInvariant();
                }

                var flagsText = AdminFlagsHelper.PosNegFlagsText(admin.PosFlags, admin.NegFlags);

                if (!MatchesFilter(query, name, admin.UserId.ToString(), title, rank, flagsText))
                    continue;

                rows.Add((admin, name, title, titleInherited, rank, italic, flagsText, combinedFlags));
            }

            var flagsWidth = rows.Count == 0
                ? AdminFlagsColumnMinWidth
                : rows.Max(row => GetTextColumnWidth(row.FlagsText, AdminFlagsColumnMinWidth));

            _menu.AdminsList.AddChild(MakeAdminHeader(flagsWidth));

            var rowIndex = 0;
            foreach (var row in rows)
            {
                _menu.AdminsList.AddChild(MakeAdminRow(
                    row.Admin,
                    row.Name,
                    row.Title,
                    row.TitleInherited,
                    row.Rank,
                    row.RankInherited,
                    row.FlagsText,
                    row.CombinedFlags,
                    flagsWidth,
                    rowIndex++));
            }

            if (rows.Count == 0)
            {
                _menu.AdminsList.AddChild(MakeEmptySearchRow(GetAdminTableWidth(flagsWidth)));
            }
        }

        private void RebuildRanksList(PermissionsEuiState state)
        {
            var query = _menu.RankSearch.Text.Trim();
            _menu.AdminRanksList.RemoveAllChildren();

            var rows = new List<(KeyValuePair<int, PermissionsEuiState.AdminRankData> Rank, string Name, string FlagsText)>();
            foreach (var kv in state.AdminRanks.OrderBy(kv => kv.Value.Name))
            {
                var rank = kv.Value;
                var flagsText = string.Join(' ', AdminFlagsHelper.FlagsToNames(rank.Flags).Select(f => $"+{f}"));

                if (!MatchesFilter(query, rank.Name, flagsText))
                    continue;

                rows.Add((kv, rank.Name, flagsText));
            }

            var flagsWidth = rows.Count == 0
                ? RankFlagsColumnMinWidth
                : rows.Max(row => GetTextColumnWidth(row.FlagsText, RankFlagsColumnMinWidth));

            _menu.AdminRanksList.AddChild(MakeRankHeader(flagsWidth));

            var rowIndex = 0;
            foreach (var row in rows)
            {
                _menu.AdminRanksList.AddChild(MakeRankRow(row.Rank, row.Name, row.FlagsText, flagsWidth, rowIndex++));
            }

            if (rows.Count == 0)
            {
                _menu.AdminRanksList.AddChild(MakeEmptySearchRow(GetRankTableWidth(flagsWidth)));
            }
        }

        private Control MakeAdminRow(
            PermissionsEuiState.AdminData admin,
            string name,
            string title,
            bool titleInherited,
            string rank,
            bool rankInherited,
            string flagsText,
            AdminFlags combinedFlags,
            float flagsWidth,
            int row)
        {
            var editButton = new Button
            {
                Text = Loc.GetString("permissions-eui-edit-title-button"),
                SetWidth = TableActionColumnWidth,
                TextAlign = Label.AlignMode.Center,
                VerticalAlignment = Control.VAlignment.Center,
            };
            editButton.OnPressed += _ => OnEditPressed(admin);

            if (!_adminManager.HasFlag(combinedFlags))
            {
                editButton.Disabled = true;
                editButton.ToolTip = Loc.GetString("permissions-eui-do-not-have-required-flags-to-edit-admin-tooltip");
            }

            return MakeListPanel(row, MakeTableRow(
                GetAdminTableWidth(flagsWidth),
                editButton,
                MakeTableLabel(name, DeadSpaceStyleClass.SectionTitle, AdminNameColumnWidth),
                MakeTableLabel(rank, null, AdminRankColumnWidth, rankInherited),
                MakeTableLabel(title, null, AdminTitleColumnWidth, titleInherited),
                MakeTableLabel(flagsText, DeadSpaceStyleClass.Subtitle, flagsWidth)));
        }

        private Control MakeRankRow(KeyValuePair<int, PermissionsEuiState.AdminRankData> rank, string name, string flagsText, float flagsWidth, int row)
        {
            var editButton = new Button
            {
                Text = Loc.GetString("permissions-eui-edit-admin-rank-button"),
                SetWidth = TableActionColumnWidth,
                TextAlign = Label.AlignMode.Center,
                VerticalAlignment = Control.VAlignment.Center,
            };
            editButton.OnPressed += _ => OnEditRankPressed(rank);

            if (!_adminManager.HasFlag(rank.Value.Flags))
            {
                editButton.Disabled = true;
                editButton.ToolTip = Loc.GetString("permissions-eui-do-not-have-required-flags-to-edit-rank-tooltip");
            }

            return MakeListPanel(row, MakeTableRow(
                GetRankTableWidth(flagsWidth),
                editButton,
                MakeTableLabel(name, DeadSpaceStyleClass.SectionTitle, RankNameColumnWidth),
                MakeTableLabel(flagsText, DeadSpaceStyleClass.Subtitle, flagsWidth)));
        }

        private static PanelContainer MakeListPanel(int row, Control child)
        {
            return new PanelContainer
            {
                HorizontalExpand = true,
                MinSize = child.MinSize,
                StyleClasses = { row % 2 == 0 ? DeadSpaceStyleClass.ListItem : DeadSpaceStyleClass.ListItemAlternate },
                Children = { child },
            };
        }

        private static Control MakeEmptySearchRow(float minWidth)
        {
            return new PanelContainer
            {
                HorizontalExpand = true,
                MinSize = new Vector2(minWidth, 0),
                StyleClasses = { DeadSpaceStyleClass.ListItem },
                Children =
                {
                    new Label
                    {
                        Text = Loc.GetString("permissions-eui-no-search-results"),
                        StyleClasses = { DeadSpaceStyleClass.Subtitle },
                        HorizontalExpand = true,
                        Align = Label.AlignMode.Center,
                    }
                }
            };
        }

        private static Control MakeAdminHeader(float flagsWidth)
        {
            return MakeHeaderPanel(MakeTableRow(
                GetAdminTableWidth(flagsWidth),
                MakeHeaderLabel("permissions-eui-list-column-action", TableActionColumnWidth),
                MakeHeaderLabel("permissions-eui-list-column-admin", AdminNameColumnWidth),
                MakeHeaderLabel("permissions-eui-list-column-rank", AdminRankColumnWidth),
                MakeHeaderLabel("permissions-eui-list-column-title", AdminTitleColumnWidth),
                MakeHeaderLabel("permissions-eui-list-column-extra-flags", flagsWidth)));
        }

        private static Control MakeRankHeader(float flagsWidth)
        {
            return MakeHeaderPanel(MakeTableRow(
                GetRankTableWidth(flagsWidth),
                MakeHeaderLabel("permissions-eui-list-column-action", TableActionColumnWidth),
                MakeHeaderLabel("permissions-eui-list-column-rank", RankNameColumnWidth),
                MakeHeaderLabel("permissions-eui-list-column-flags", flagsWidth)));
        }

        private static PanelContainer MakeHeaderPanel(Control child)
        {
            return new PanelContainer
            {
                HorizontalExpand = true,
                MinSize = child.MinSize,
                StyleClasses = { DeadSpaceStyleClass.ListHeader },
                Children = { child },
            };
        }

        private static float GetAdminTableWidth(float flagsWidth)
        {
            return TableActionColumnWidth + AdminNameColumnWidth + AdminRankColumnWidth + AdminTitleColumnWidth + flagsWidth + TableGapWidth * 4;
        }

        private static float GetRankTableWidth(float flagsWidth)
        {
            return TableActionColumnWidth + RankNameColumnWidth + flagsWidth + TableGapWidth * 2;
        }

        private static float GetTextColumnWidth(string text, float minWidth)
        {
            if (string.IsNullOrEmpty(text))
            {
                return minWidth;
            }

            return Math.Max(minWidth, text.Length * TableTextApproxGlyphWidth + TableTextPaddingWidth);
        }

        private static BoxContainer MakeTableRow(float minWidth, params Control[] children)
        {
            var box = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                HorizontalExpand = true,
                MinSize = new Vector2(minWidth, 0),
                SeparationOverride = TableGapWidth,
            };

            foreach (var child in children)
            {
                box.AddChild(child);
            }

            return box;
        }

        private static Label MakeHeaderLabel(string locKey, float width)
        {
            return new Label
            {
                Text = Loc.GetString(locKey),
                ClipText = true,
                SetWidth = width,
                ToolTip = Loc.GetString(locKey),
                VerticalAlignment = Control.VAlignment.Center,
                StyleClasses = { DeadSpaceStyleClass.ListHeader },
            };
        }

        private static Label MakeFlagHeaderLabel(string locKey, float width)
        {
            return new Label
            {
                Text = Loc.GetString(locKey),
                ClipText = true,
                SetWidth = width,
                ToolTip = Loc.GetString(locKey),
                Align = Label.AlignMode.Center,
                VerticalAlignment = Control.VAlignment.Center,
                StyleClasses = { DeadSpaceStyleClass.ListHeader },
            };
        }

        private static Label MakeTableLabel(string text, string? styleClass, float width, bool italic = false)
        {
            var label = new Label
            {
                Text = text,
                ClipText = true,
                SetWidth = width,
                ToolTip = text,
                VerticalAlignment = Control.VAlignment.Center,
            };

            if (styleClass != null)
                label.AddStyleClass(styleClass);

            if (italic)
            {
                label.StyleClasses.Add(StyleClass.Italic);
            }

            return label;
        }

        private static bool MatchesFilter(string filter, params string?[] values)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return true;

            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value) &&
                    value.Contains(filter, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
        // DS14-end

        private void OnEditRankPressed(KeyValuePair<int, PermissionsEuiState.AdminRankData> rank)
        {
            OpenRankEditWindow(rank);
        }

        private sealed class Menu : DefaultWindow
        {
            private readonly PermissionsEui _ui;
            // DS14-start
            public readonly BoxContainer AdminsList;
            public readonly BoxContainer AdminRanksList;
            public readonly LineEdit AdminSearch;
            public readonly LineEdit RankSearch;
            // DS14-end
            public readonly Button AddAdminButton;
            public readonly Button AddAdminRankButton;

            public Menu(PermissionsEui ui)
            {
                _ui = ui;
                // DS14-start
                MinSize = new Vector2(760, 460);
                SetSize = new Vector2(920, 560);
                // DS14-end
                Title = Loc.GetString("permissions-eui-menu-title");

                // DS14-start
                var tab = new TabContainer
                {
                    HorizontalExpand = true,
                    VerticalExpand = true,
                };
                // DS14-end

                AddAdminButton = new Button
                {
                    Text = Loc.GetString("permissions-eui-menu-add-admin-button"),
                    HorizontalAlignment = HAlignment.Right,
                    StyleClasses = { DeadSpaceStyleClass.ControlPositive }, // DS14
                };

                AddAdminRankButton = new Button
                {
                    Text = Loc.GetString("permissions-eui-menu-add-admin-rank-button"),
                    HorizontalAlignment = HAlignment.Right,
                    StyleClasses = { DeadSpaceStyleClass.ControlPositive }, // DS14
                };

                // DS14-start
                AdminSearch = MakeSearch(Loc.GetString("permissions-eui-admin-search-placeholder"));
                AdminSearch.OnTextChanged += _ => _ui.RebuildLists();
                AdminsList = new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    VerticalExpand = true,
                    HorizontalExpand = true,
                    MinSize = new Vector2(AdminTableMinWidth, 0),
                    SeparationOverride = 4,
                };
                var adminVBox = new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    HorizontalExpand = true,
                    VerticalExpand = true,
                    SeparationOverride = 8,
                    Children =
                    {
                        AdminSearch,
                        MakeListScroll(AdminsList),
                        AddAdminButton,
                    },
                };
                TabContainer.SetTabTitle(adminVBox, Loc.GetString("permissions-eui-menu-admins-tab-title"));

                RankSearch = MakeSearch(Loc.GetString("permissions-eui-rank-search-placeholder"));
                RankSearch.OnTextChanged += _ => _ui.RebuildLists();
                AdminRanksList = new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    VerticalExpand = true,
                    HorizontalExpand = true,
                    MinSize = new Vector2(RankTableMinWidth, 0),
                    SeparationOverride = 4,
                };
                var rankVBox = new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    HorizontalExpand = true,
                    VerticalExpand = true,
                    SeparationOverride = 8,
                    Children =
                    {
                        RankSearch,
                        MakeListScroll(AdminRanksList),
                        AddAdminRankButton,
                    }
                };
                TabContainer.SetTabTitle(rankVBox, Loc.GetString("permissions-eui-menu-admin-ranks-tab-title"));

                tab.AddChild(adminVBox);
                tab.AddChild(rankVBox);

                ContentsContainer.AddChild(tab);
            }

            private static LineEdit MakeSearch(string placeholder)
            {
                return new LineEdit
                {
                    PlaceHolder = placeholder,
                    SelectAllOnFocus = true,
                    HorizontalExpand = true,
                };
            }

            private static ScrollContainer MakeListScroll(BoxContainer list)
            {
                return new ScrollContainer
                {
                    HorizontalExpand = true,
                    VerticalExpand = true,
                    HScrollEnabled = true,
                    VScrollEnabled = true,
                    Children = { list },
                };
            }
            // DS14-end
        }

        private sealed class EditAdminWindow : DefaultWindow
        {
            public readonly PermissionsEuiState.AdminData? SourceData;
            public readonly LineEdit? NameEdit;
            public readonly LineEdit TitleEdit;
            public readonly OptionButton RankButton;
            public readonly Button SaveButton;
            public readonly Button? RemoveButton;
            public readonly CheckBox SuspendedCheckbox;

            public readonly Dictionary<AdminFlags, (Button inherit, Button sub, Button plus)> FlagButtons
                = new();

            public EditAdminWindow(PermissionsEui ui, PermissionsEuiState.AdminData? data)
            {
                // DS14-start
                MinSize = new Vector2(700, 460);
                SetSize = new Vector2(760, 520);
                // DS14-end
                SourceData = data;

                Control nameControl;

                if (data is { } dat)
                {
                    var name = dat.UserName ?? dat.UserId.ToString();
                    Title = Loc.GetString("permissions-eui-edit-admin-window-edit-admin-label",
                                          ("admin", name));

                    // DS14-start
                    nameControl = new Label
                    {
                        Text = name,
                        ClipText = true,
                        ToolTip = name,
                        StyleClasses = { DeadSpaceStyleClass.SectionTitle },
                    };
                    // DS14-end
                }
                else
                {
                    Title = Loc.GetString("permissions-eui-menu-add-admin-button");

                    nameControl = NameEdit = new LineEdit
                    {
                        PlaceHolder = Loc.GetString("permissions-eui-edit-admin-window-name-edit-placeholder"),
                        SelectAllOnFocus = true,
                    };
                }

                TitleEdit = new LineEdit
                {
                    PlaceHolder = Loc.GetString("permissions-eui-edit-admin-window-title-edit-placeholder"),
                    SelectAllOnFocus = true,
                };
                // DS14-start
                if (data?.Title is { } adminTitle)
                {
                    TitleEdit.Text = adminTitle;
                }
                // DS14-end

                RankButton = new OptionButton
                {
                    HorizontalExpand = true,
                };
                SaveButton = new Button
                {
                    Text = Loc.GetString("permissions-eui-edit-admin-window-save-button"),
                    HorizontalAlignment = HAlignment.Right,
                    StyleClasses = { DeadSpaceStyleClass.ControlPositive }, // DS14
                };

                SuspendedCheckbox = new CheckBox
                {
                    Text = Loc.GetString("permissions-eui-edit-admin-window-suspended"),
                    Pressed = data?.Suspended ?? false,
                };

                RankButton.AddItem(Loc.GetString("permissions-eui-edit-admin-window-no-rank-button"), NoRank);
                foreach (var (rId, rank) in ui._ranks)
                {
                    RankButton.AddItem(rank.Name, rId);
                }

                RankButton.SelectId(data?.RankId ?? NoRank);
                RankButton.OnItemSelected += RankSelected;

                var permGrid = new GridContainer
                {
                    Columns = 5, // DS14
                    HSeparationOverride = 0,
                    VSeparationOverride = 0,
                    MinSize = new Vector2(AdminFlagTableMinWidth, 0) // DS14
                };

                // DS14-start
                permGrid.AddChild(MakeFlagHeaderLabel("permissions-eui-flag-column-inherit", FlagChoiceColumnWidth));
                permGrid.AddChild(MakeFlagHeaderLabel("permissions-eui-flag-column-deny", FlagChoiceColumnWidth));
                permGrid.AddChild(MakeFlagHeaderLabel("permissions-eui-flag-column-grant", FlagChoiceColumnWidth));
                permGrid.AddChild(new Control { MinSize = new Vector2(AdminFlagSpacerColumnWidth, 0) });
                permGrid.AddChild(MakeFlagHeaderLabel("permissions-eui-flag-column-right", AdminFlagNameColumnWidth));
                // DS14-end

                foreach (var flag in AdminFlagsHelper.AllFlags)
                {
                    // Can only grant out perms you also have yourself.
                    // Primarily intended to prevent people giving themselves +HOST with +PERMISSIONS but generalized.
                    var disable = !ui._adminManager.HasFlag(flag);
                    var flagName = flag.ToString().ToUpper();

                    var group = new ButtonGroup();

                    // DS14-start
                    var inherit = new Button
                    {
                        Text = "I",
                        SetWidth = FlagChoiceColumnWidth,
                        TextAlign = Label.AlignMode.Center,
                        StyleClasses = { StyleClass.ButtonOpenRight },
                        Disabled = disable,
                        Group = group,
                    };
                    var sub = new Button
                    {
                        Text = "-",
                        SetWidth = FlagChoiceColumnWidth,
                        TextAlign = Label.AlignMode.Center,
                        StyleClasses = { StyleClass.ButtonOpenBoth },
                        Disabled = disable,
                        Group = group
                    };
                    var plus = new Button
                    {
                        Text = "+",
                        SetWidth = FlagChoiceColumnWidth,
                        TextAlign = Label.AlignMode.Center,
                        StyleClasses = { StyleClass.ButtonOpenLeft },
                        Disabled = disable,
                        Group = group
                    };
                    inherit.OnPressed += _ => SetSelectedFlagButton(inherit, sub, plus, inherit);
                    sub.OnPressed += _ => SetSelectedFlagButton(inherit, sub, plus, sub);
                    plus.OnPressed += _ => SetSelectedFlagButton(inherit, sub, plus, plus);
                    // DS14-end

                    if (data is { } d)
                    {
                        if ((d.NegFlags & flag) != 0)
                        {
                            sub.Pressed = true;
                        }
                        else if ((d.PosFlags & flag) != 0)
                        {
                            plus.Pressed = true;
                        }
                        else
                        {
                            inherit.Pressed = true;
                        }
                    }
                    else
                    {
                        inherit.Pressed = true;
                    }

                    RefreshSelectedFlagButton(inherit, sub, plus); // DS14
                    permGrid.AddChild(inherit);
                    permGrid.AddChild(sub);
                    permGrid.AddChild(plus);
                    permGrid.AddChild(new Control { MinSize = new Vector2(AdminFlagSpacerColumnWidth, 0) }); // DS14
                    // DS14-start
                    permGrid.AddChild(new Label
                    {
                        Text = flagName,
                        ClipText = true,
                        SetWidth = AdminFlagNameColumnWidth,
                        ToolTip = flagName,
                        VerticalAlignment = Control.VAlignment.Center,
                    });
                    // DS14-end

                    FlagButtons.Add(flag, (inherit, sub, plus));
                }

                var bottomButtons = new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    // DS14-start
                    HorizontalExpand = true,
                    SeparationOverride = 8,
                    // DS14-end
                };
                if (data != null)
                {
                    RemoveButton = new Button
                    {
                        Text = Loc.GetString("permissions-eui-edit-admin-window-remove-flag-button"),
                        StyleClasses = { StyleClass.Negative }, // DS14
                    };
                    bottomButtons.AddChild(RemoveButton);
                }

                bottomButtons.AddChild(new Control { HorizontalExpand = true }); // DS14
                bottomButtons.AddChild(SaveButton);

                // DS14-start
                ContentsContainer.AddChild(new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    HorizontalExpand = true,
                    VerticalExpand = true,
                    SeparationOverride = 8,
                    Children =
                    {
                        new BoxContainer
                        {
                            Orientation = LayoutOrientation.Horizontal,
                            SeparationOverride = 8,
                            HorizontalExpand = true,
                            VerticalExpand = true,
                            Children =
                            {
                                new PanelContainer
                                {
                                    SetWidth = 260,
                                    StyleClasses = { DeadSpaceStyleClass.Inset },
                                    Children =
                                    {
                                        new BoxContainer
                                        {
                                            Orientation = LayoutOrientation.Vertical,
                                            SeparationOverride = 6,
                                            Children =
                                            {
                                                nameControl,
                                                TitleEdit,
                                                RankButton,
                                                SuspendedCheckbox,
                                            }
                                        }
                                    }
                                },
                                new PanelContainer
                                {
                                    HorizontalExpand = true,
                                    VerticalExpand = true,
                                    StyleClasses = { DeadSpaceStyleClass.Inset },
                                    Children =
                                    {
                                        new ScrollContainer
                                        {
                                            HorizontalExpand = true,
                                            VerticalExpand = true,
                                            HScrollEnabled = true,
                                            VScrollEnabled = true,
                                            Children = { permGrid },
                                        }
                                    }
                                }
                            }
                        },
                        bottomButtons
                    }
                });
                // DS14-end
            }

            // DS14-start
            private static void RefreshSelectedFlagButton(Button inherit, Button sub, Button plus)
            {
                if (inherit.Pressed)
                {
                    SetSelectedFlagButton(inherit, sub, plus, inherit);
                }
                else if (sub.Pressed)
                {
                    SetSelectedFlagButton(inherit, sub, plus, sub);
                }
                else if (plus.Pressed)
                {
                    SetSelectedFlagButton(inherit, sub, plus, plus);
                }
                else
                {
                    ClearSelectedFlagButton(inherit, sub, plus);
                }
            }

            private static void SetSelectedFlagButton(Button inherit, Button sub, Button plus, Button selected)
            {
                ClearSelectedFlagButton(inherit, sub, plus);
                selected.AddStyleClass(DeadSpaceStyleClass.ControlPositive);
            }

            private static void ClearSelectedFlagButton(Button inherit, Button sub, Button plus)
            {
                inherit.RemoveStyleClass(DeadSpaceStyleClass.ControlPositive);
                sub.RemoveStyleClass(DeadSpaceStyleClass.ControlPositive);
                plus.RemoveStyleClass(DeadSpaceStyleClass.ControlPositive);
            }
            // DS14-end

            private void RankSelected(OptionButton.ItemSelectedEventArgs obj)
            {
                RankButton.SelectId(obj.Id);
            }

            public void CollectSetFlags(out AdminFlags pos, out AdminFlags neg)
            {
                pos = default;
                neg = default;

                foreach (var (flag, (_, s, p)) in FlagButtons)
                {
                    if (s.Pressed)
                    {
                        neg |= flag;
                    }
                    else if (p.Pressed)
                    {
                        pos |= flag;
                    }
                }
            }
        }

        private sealed class EditAdminRankWindow : DefaultWindow
        {
            public readonly int? SourceId;
            public readonly LineEdit NameEdit;
            public readonly Button SaveButton;
            public readonly Button? RemoveButton;
            // DS14-start
            public readonly Button ClearAllButton;
            public readonly Button GrantAllButton;
            // DS14-end
            public readonly Dictionary<AdminFlags, CheckBox> FlagCheckBoxes = new();

            public EditAdminRankWindow(PermissionsEui ui, KeyValuePair<int, PermissionsEuiState.AdminRankData>? data)
            {
                // DS14-start
                MinSize = new Vector2(500, 460);
                SetSize = new Vector2(560, 520);
                // DS14-end
                Title = Loc.GetString("permissions-eui-edit-admin-rank-window-title");
                SourceId = data?.Key;

                NameEdit = new LineEdit
                {
                    PlaceHolder = Loc.GetString("permissions-eui-edit-admin-rank-window-name-edit-placeholder"),
                    SelectAllOnFocus = true,
                };

                if (data != null)
                {
                    NameEdit.Text = data.Value.Value.Name;
                }

                SaveButton = new Button
                {
                    Text = Loc.GetString("permissions-eui-menu-save-admin-rank-button"),
                    HorizontalAlignment = HAlignment.Right,
                    StyleClasses = { DeadSpaceStyleClass.ControlPositive }, // DS14
                };
                // DS14-start
                ClearAllButton = new Button
                {
                    Text = Loc.GetString("permissions-eui-edit-admin-rank-clear-all-button"),
                };
                ClearAllButton.OnPressed += _ => SetAvailableFlags(false);

                GrantAllButton = new Button
                {
                    Text = Loc.GetString("permissions-eui-edit-admin-rank-grant-all-button"),
                    StyleClasses = { DeadSpaceStyleClass.ControlPositive },
                };
                GrantAllButton.OnPressed += _ => SetAvailableFlags(true);
                // DS14-end

                var flagsBox = new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    // DS14-start
                    MinSize = new Vector2(AdminFlagTableMinWidth, 0),
                    SeparationOverride = 4,
                    // DS14-end
                };

                foreach (var flag in AdminFlagsHelper.AllFlags)
                {
                    // Can only grant out perms you also have yourself.
                    // Primarily intended to prevent people giving themselves +HOST with +PERMISSIONS but generalized.
                    var disable = !ui._adminManager.HasFlag(flag);
                    var flagName = flag.ToString().ToUpper();

                    var checkBox = new CheckBox
                    {
                        Disabled = disable,
                        Text = flagName
                    };

                    if (data != null && (data.Value.Value.Flags & flag) != 0)
                    {
                        checkBox.Pressed = true;
                    }

                    FlagCheckBoxes.Add(flag, checkBox);
                    flagsBox.AddChild(checkBox);
                }

                var bottomButtons = new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    // DS14-start
                    HorizontalExpand = true,
                    SeparationOverride = 8,
                    // DS14-end
                };
                if (data != null)
                {
                    RemoveButton = new Button
                    {
                        Text = Loc.GetString("permissions-eui-menu-remove-admin-rank-button"),
                        StyleClasses = { StyleClass.Negative }, // DS14
                    };
                    bottomButtons.AddChild(RemoveButton);
                }

                bottomButtons.AddChild(new Control { HorizontalExpand = true }); // DS14
                // DS14-start
                bottomButtons.AddChild(ClearAllButton);
                bottomButtons.AddChild(GrantAllButton);
                // DS14-end
                bottomButtons.AddChild(SaveButton);

                // DS14-start
                ContentsContainer.AddChild(new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    HorizontalExpand = true,
                    VerticalExpand = true,
                    SeparationOverride = 8,
                    Children =
                    {
                        NameEdit,
                        new PanelContainer
                        {
                            HorizontalExpand = true,
                            VerticalExpand = true,
                            StyleClasses = { DeadSpaceStyleClass.Inset },
                            Children =
                            {
                                new ScrollContainer
                                {
                                    HorizontalExpand = true,
                                    VerticalExpand = true,
                                    HScrollEnabled = true,
                                    VScrollEnabled = true,
                                    Children = { flagsBox },
                                }
                            }
                        },
                        bottomButtons
                    }
                });
                // DS14-end
            }

            public AdminFlags CollectSetFlags()
            {
                AdminFlags flags = default;
                foreach (var (flag, chk) in FlagCheckBoxes)
                {
                    if (chk.Pressed)
                    {
                        flags |= flag;
                    }
                }

                return flags;
            }

            // DS14-start
            private void SetAvailableFlags(bool pressed)
            {
                foreach (var checkBox in FlagCheckBoxes.Values)
                {
                    if (!checkBox.Disabled)
                    {
                        checkBox.Pressed = pressed;
                    }
                }
            }
            // DS14-end
        }
    }
}
