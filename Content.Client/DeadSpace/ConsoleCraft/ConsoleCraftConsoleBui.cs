// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.ConsoleCraft;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client.DeadSpace.ConsoleCraft;

[UsedImplicitly]
public sealed partial class ConsoleCraftConsoleBui : BoundUserInterface
{
    [Dependency] private IEntityManager _entMan = default!;

    private ConsoleCraftConsoleWindow? _window;

    public ConsoleCraftConsoleBui(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<ConsoleCraftConsoleWindow>();
        _window.OnBlueprintSelected += OnBlueprintSelected;
        _window.OnBackPressed += OnBackPressed;
        _window.OnCraftPressed += OnCraftPressed;
        _window.OnEjectPressed += OnEjectPressed;
        _window.OnDismantlePressed += OnDismantlePressed;
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not ConsoleCraftConsoleState craftState)
            return;

        _window?.UpdateState(craftState, _entMan);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _window?.Dispose();
    }

    private void OnBlueprintSelected(string recipeId)
    {
        SendMessage(new ConsoleCraftSelectBlueprintMessage { RecipeId = recipeId });
    }

    private void OnBackPressed()
    {
        SendMessage(new ConsoleCraftBackMessage());
    }

    private void OnCraftPressed()
    {
        SendMessage(new ConsoleCraftStartMessage());
    }
    private void OnEjectPressed()
    {
        SendMessage(new ConsoleCraftEjectMessage());
    }

    private void OnDismantlePressed()
    {
        SendMessage(new ConsoleCraftDismantleMessage());
    }
}
