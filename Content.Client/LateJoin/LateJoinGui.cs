using System.Linq;
using System.Numerics;
using Content.Client.CrewManifest;
using Content.Client.DeadSpace.Stylesheets;
using Content.Client.GameTicking.Managers;
using Content.Client.Lobby;
using Content.Client.UserInterface.Controls;
using Content.Client.Players.PlayTimeTracking;
using Content.Shared.CCVar;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.StatusIcon;
using Robust.Client.Console;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.LateJoin
{
    public sealed class LateJoinGui : DefaultWindow
    {
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly IClientConsoleHost _consoleHost = default!;
        [Dependency] private readonly IConfigurationManager _configManager = default!;
        [Dependency] private readonly IEntitySystemManager _entitySystem = default!;
        [Dependency] private readonly JobRequirementsManager _jobRequirements = default!;
        [Dependency] private readonly IClientPreferencesManager _preferencesManager = default!;
        [Dependency] private readonly ILogManager _logManager = default!;

        public event Action<(NetEntity, string)> SelectedId;

        private readonly ClientGameTicker _gameTicker;
        private readonly SpriteSystem _sprites;
        private readonly CrewManifestSystem _crewManifest;
        private readonly ISawmill _sawmill;

        private readonly Dictionary<NetEntity, Dictionary<string, List<JobButton>>> _jobButtons = new();
        private readonly Dictionary<NetEntity, Dictionary<string, BoxContainer>> _jobCategories = new();
        private readonly List<Control> _jobLists = new();
        private readonly List<ContainerButton> _stationButtons = new(); // DS14

        private readonly Control _base;

        public LateJoinGui()
        {
            MinSize = SetSize = new Vector2(500, 620); // DS14-resize
            IoCManager.InjectDependencies(this);
            _sprites = _entitySystem.GetEntitySystem<SpriteSystem>();
            _crewManifest = _entitySystem.GetEntitySystem<CrewManifestSystem>();
            _gameTicker = _entitySystem.GetEntitySystem<ClientGameTicker>();
            _sawmill = _logManager.GetSawmill("latejoin.panel");

            Title = Loc.GetString("late-join-gui-title");

            _base = new BoxContainer()
            {
                Orientation = LayoutOrientation.Vertical,
                VerticalExpand = true,
                Margin = new Thickness(20), // DS14: preserve the old shell plus content inset without nesting windows.
                SeparationOverride = 8,
            };

            ContentsContainer.AddChild(_base); // DS14

            _jobRequirements.Updated += RebuildUI;
            RebuildUI();

            SelectedId += x =>
            {
                var (station, jobId) = x;
                _sawmill.Info($"Late joining as ID: {jobId}");
                _consoleHost.ExecuteCommand($"joingame {CommandParsing.Escape(jobId)} {station}");
                Close();
            };

            _gameTicker.LobbyJobsAvailableUpdated += JobsAvailableUpdated;
        }

        private void RebuildUI()
        {
            _base.RemoveAllChildren();
            _jobLists.Clear();
            _stationButtons.Clear(); // DS14
            _jobButtons.Clear();
            _jobCategories.Clear();

            if (!_gameTicker.DisallowedLateJoin && _gameTicker.StationNames.Count == 0)
                _sawmill.Warning("No stations exist, nothing to display in late-join GUI");

            foreach (var (id, name) in _gameTicker.StationNames)
            {
                var jobList = new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    Margin = new Thickness(0, 0, 5f, 0),
                };

                var collapseButton = new ContainerButton()
                {
                    HorizontalAlignment = HAlignment.Right,
                    ToggleMode = true,
                    MinSize = new Vector2(36, 28),
                    // DS14-start
                    StyleBoxOverride = new StyleBoxFlat
                    {
                        BackgroundColor = Color.Transparent,
                    },
                    // DS14-end
                    Children =
                    {
                        new TextureRect
                        {
                            StyleClasses = { OptionButton.StyleClassOptionTriangle },
                            Margin = new Thickness(8, 0),
                            HorizontalAlignment = HAlignment.Center,
                            VerticalAlignment = VAlignment.Center,
                        }
                    }
                };

                // DS14-start
                _base.AddChild(new PanelContainer
                {
                    StyleClasses = { DeadSpaceStyleClass.ListHeader },
                    Children =
                    {
                        new BoxContainer
                        {
                            Orientation = LayoutOrientation.Horizontal,
                            SeparationOverride = 8,
                            HorizontalExpand = true,
                            Children =
                            {
                                new Label()
                                {
                                    StyleClasses = { DeadSpaceStyleClass.ListHeader },
                                    Text = name,
                                    HorizontalExpand = true,
                                    Align = Label.AlignMode.Center,
                                    VerticalAlignment = VAlignment.Center,
                                },
                                collapseButton
                            }
                        }
                    }
                });
                // DS14-end

                if (_configManager.GetCVar(CCVars.CrewManifestWithoutEntity))
                {
                    var crewManifestButton = new Button()
                    {
                        Text = Loc.GetString("crew-manifest-button-label")
                    };
                    crewManifestButton.OnPressed += _ => _crewManifest.RequestCrewManifest(id);

                    _base.AddChild(crewManifestButton);
                }

                var jobListScroll = new ScrollContainer()
                {
                    VerticalExpand = true,
                    Children = { jobList },
                };

                // DS14-start
                var jobListPanel = new PanelContainer
                {
                    StyleClasses = { DeadSpaceStyleClass.Inset },
                    VerticalExpand = true,
                    Visible = false,
                    Children = { jobListScroll },
                };
                // DS14-end

                if (_jobLists.Count == 0)
                {
                    jobListPanel.Visible = true;
                    collapseButton.Pressed = true; // DS14
                }

                _jobLists.Add(jobListPanel);
                _stationButtons.Add(collapseButton); // DS14

                _base.AddChild(jobListPanel);

                collapseButton.OnToggled += args =>
                {
                    if (!args.Pressed)
                    {
                        jobListPanel.Visible = false;
                        return;
                    }

                    foreach (var section in _jobLists)
                        section.Visible = false;

                    foreach (var button in _stationButtons)
                    {
                        if (button != collapseButton)
                            button.Pressed = false;
                    }

                    jobListPanel.Visible = true;
                };

                var firstCategory = true;
                var departments = _prototypeManager.EnumeratePrototypes<DepartmentPrototype>().ToArray();
                Array.Sort(departments, DepartmentUIComparer.Instance);

                _jobButtons[id] = new Dictionary<string, List<JobButton>>();

                foreach (var department in departments)
                {
                    var departmentName = Loc.GetString(department.Name);
                    _jobCategories[id] = new Dictionary<string, BoxContainer>();
                    var stationAvailable = _gameTicker.JobsAvailable[id];
                    var jobsAvailable = new List<JobPrototype>();

                    foreach (var jobId in department.Roles)
                    {
                        if (!stationAvailable.ContainsKey(jobId))
                            continue;

                        jobsAvailable.Add(_prototypeManager.Index<JobPrototype>(jobId));
                    }

                    if (JobUIComparer.TryCreate(
                            _prototypeManager,
                            _gameTicker.JobWeightsByStation.GetValueOrDefault(id),
                            out var comparer))
                    {
                        jobsAvailable.Sort(comparer);
                    }

                    // Do not display departments with no jobs available.
                    if (jobsAvailable.Count == 0)
                        continue;

                    var category = new BoxContainer
                    {
                        Orientation = LayoutOrientation.Vertical,
                        Name = department.ID,
                        ToolTip = Loc.GetString("late-join-gui-jobs-amount-in-department-tooltip",
                            ("departmentName", departmentName))
                    };

                    if (firstCategory)
                    {
                        firstCategory = false;
                    }
                    else
                    {
                        category.AddChild(new Control
                        {
                            MinSize = new Vector2(0, 12),
                        });
                    }

                    // DS14-start
                    category.AddChild(new PanelContainer
                    {
                        StyleClasses = { DeadSpaceStyleClass.ListHeader },
                        Margin = new Thickness(0, 0, 0, 4),
                        Children =
                        {
                            new Label
                            {
                                StyleClasses = { DeadSpaceStyleClass.ListHeader },
                                Text = Loc.GetString("late-join-gui-department-jobs-label", ("departmentName", departmentName)),
                                Align = Label.AlignMode.Center,
                                HorizontalExpand = true,
                            }
                        }
                    });
                    // DS14-end

                    _jobCategories[id][department.ID] = category;
                    jobList.AddChild(category);

                    var rowIndex = 0; // DS14
                    foreach (var prototype in jobsAvailable)
                    {
                        var value = stationAvailable[prototype.ID];

                        var jobLabel = new Label
                        {
                            Margin = new Thickness(5f, 0, 0, 0),
                            VerticalAlignment = VAlignment.Center,
                        };

                        var jobButton = new JobButton(jobLabel, prototype.ID, prototype.LocalizedName, value);
                        jobButton.AddStyleClass(GetJobRowStyleClass(rowIndex++)); // DS14

                        var jobSelector = new BoxContainer
                        {
                            Orientation = LayoutOrientation.Horizontal,
                            HorizontalExpand = true,
                            SeparationOverride = 6,
                        };

                        var jobIcon = _prototypeManager.Index(prototype.Icon);
                        var icon = CreateJobIcon(_sprites.Frame0(jobIcon.Icon)); // DS14
                        jobSelector.AddChild(icon);

                        jobSelector.AddChild(jobLabel);
                        jobButton.AddChild(jobSelector);
                        category.AddChild(jobButton);

                        jobButton.OnPressed += _ => SelectedId.Invoke((id, jobButton.JobId));

                        if (!_jobRequirements.IsAllowed(prototype, (HumanoidCharacterProfile?)_preferencesManager.Preferences?.SelectedCharacter, out var reason))
                        {
                            jobButton.Disabled = true;

                            if (!reason.IsEmpty)
                            {
                                var tooltip = new Tooltip();
                                tooltip.SetMessage(reason);
                                jobButton.TooltipSupplier = _ => tooltip;
                            }

                            jobSelector.AddChild(new TextureRect
                            {
                                TextureScale = new Vector2(0.4f, 0.4f),
                                Stretch = TextureRect.StretchMode.KeepCentered,
                                Texture = _sprites.Frame0(new SpriteSpecifier.Texture(new ("/Textures/Interface/Nano/lock.svg.192dpi.png"))),
                                HorizontalExpand = true,
                                HorizontalAlignment = HAlignment.Right,
                            });
                        }
                        else if (value == 0)
                        {
                            jobButton.Disabled = true;
                        }

                        if (!_jobButtons[id].ContainsKey(prototype.ID))
                        {
                            _jobButtons[id][prototype.ID] = new List<JobButton>();
                        }

                        _jobButtons[id][prototype.ID].Add(jobButton);
                    }
                }
            }
        }

        // DS14-start
        internal static string GetJobRowStyleClass(int rowIndex)
        {
            // Every row must use identical list geometry. Previously only odd rows received a list class, so even
            // rows inherited the wider/shorter generic button margins and moved both the icon and the label.
            return rowIndex % 2 == 0
                ? DeadSpaceStyleClass.ListItem
                : DeadSpaceStyleClass.ListItemAlternate;
        }

        internal static TextureRect CreateJobIcon(Texture texture)
        {
            // Job icon prototypes are not required to share a source resolution. A fixed cell prevents custom icons
            // from changing the label offset or row height while KeepAspectCentered preserves their proportions.
            return new TextureRect
            {
                Texture = texture,
                TextureScale = new Vector2(2, 2),
                MinSize = new Vector2(16, 16),
                MaxSize = new Vector2(16, 16),
                CanShrink = true,
                Stretch = TextureRect.StretchMode.KeepAspectCentered,
                VerticalAlignment = VAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0),
            };
        }
        // DS14-end

        private void JobsAvailableUpdated(IReadOnlyDictionary<NetEntity, Dictionary<ProtoId<JobPrototype>, int?>> updatedJobs)
        {
            if (!_jobButtons.Keys.ToHashSet().SetEquals(updatedJobs.Keys) ||
                updatedJobs.Any(station => station.Value.Keys.Any(job => !_jobButtons[station.Key].ContainsKey(job))))
            {
                RebuildUI();
                return;
            }

            foreach (var stationEntries in updatedJobs)
            {
                if (_jobButtons.ContainsKey(stationEntries.Key))
                {
                    var jobsAvailable = stationEntries.Value;

                    var existingJobEntries = _jobButtons[stationEntries.Key];
                    foreach (var existingJobEntry in existingJobEntries)
                    {
                        if (jobsAvailable.ContainsKey(existingJobEntry.Key))
                        {
                            var updatedJobValue = jobsAvailable[existingJobEntry.Key];
                            foreach (var matchingJobButton in existingJobEntry.Value)
                            {
                                if (matchingJobButton.Amount != updatedJobValue)
                                {
                                    matchingJobButton.RefreshLabel(updatedJobValue);
                                    matchingJobButton.Disabled = matchingJobButton.Amount == 0 ||
                                        !_jobRequirements.IsAllowed(_prototypeManager.Index<JobPrototype>(matchingJobButton.JobId),
                                            (HumanoidCharacterProfile?)_preferencesManager.Preferences?.SelectedCharacter, out _);
                                }
                            }
                        }
                        else
                        {
                            foreach (var matchingJobButton in existingJobEntry.Value)
                            {
                                matchingJobButton.RefreshLabel(0);
                                matchingJobButton.Disabled = true;
                            }
                        }
                    }
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                _jobRequirements.Updated -= RebuildUI;
                _gameTicker.LobbyJobsAvailableUpdated -= JobsAvailableUpdated;
                _jobButtons.Clear();
                _jobCategories.Clear();
            }
        }
    }

    sealed class JobButton : ContainerButton
    {
        public Label JobLabel { get; }
        public string JobId { get; }
        public string JobLocalisedName { get; }
        public int? Amount { get; private set; }
        private bool _initialised = false;

        public JobButton(Label jobLabel, ProtoId<JobPrototype> jobId, string jobLocalisedName, int? amount)
        {
            JobLabel = jobLabel;
            JobId = jobId;
            JobLocalisedName = jobLocalisedName;
            RefreshLabel(amount);
            AddStyleClass(StyleClassButton);
            _initialised = true;
        }

        public void RefreshLabel(int? amount)
        {
            if (Amount == amount && _initialised)
            {
                return;
            }
            Amount = amount;

            JobLabel.Text = Amount != null ?
                Loc.GetString("late-join-gui-job-slot-capped", ("jobName", JobLocalisedName), ("amount", Amount)) :
                Loc.GetString("late-join-gui-job-slot-uncapped", ("jobName", JobLocalisedName));
        }
    }
}
