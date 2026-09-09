using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DeadSpace.Thief;
using Robust.Shared.Containers;

namespace Content.Shared.PDA
{
    public abstract class SharedPdaSystem : EntitySystem
    {
        [Dependency] protected readonly ItemSlotsSystem ItemSlotsSystem = default!;
        [Dependency] protected readonly SharedAppearanceSystem Appearance = default!;
        [Dependency] private readonly SharedJobStatusSystem _jobStatus = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<PdaComponent, ComponentInit>(OnComponentInit);
            SubscribeLocalEvent<PdaComponent, ComponentRemove>(OnComponentRemove);

            SubscribeLocalEvent<PdaComponent, EntInsertedIntoContainerMessage>(OnItemInserted);
            SubscribeLocalEvent<PdaComponent, EntRemovedFromContainerMessage>(OnItemRemoved);

            SubscribeLocalEvent<PdaComponent, GetAdditionalAccessEvent>(OnGetAdditionalAccess);

            // DS14: refuse to insert a tool into the tool slot of any PDA that isn't
            // the thief's own marked PDA. Done on both server and client so the
            // insertion is rejected predictively without a popup.
            SubscribeLocalEvent<PdaComponent, ItemSlotInsertAttemptEvent>(OnToolSlotInsertAttempt);
        }

        private void OnToolSlotInsertAttempt(Entity<PdaComponent> ent, ref ItemSlotInsertAttemptEvent args)
        {
            if (args.Slot.ID != PdaComponent.PdaToolSlotId)
                return;

            // The thief's own marked PDA accepts tools.
            if (HasComp<ThiefPdaComponent>(ent.Owner))
                return;

            args.Cancelled = true;
        }
        protected virtual void OnComponentInit(EntityUid uid, PdaComponent pda, ComponentInit args)
        {
            if (pda.IdCard != null)
                pda.IdSlot.StartingItem = pda.IdCard;

            ItemSlotsSystem.AddItemSlot(uid, PdaComponent.PdaIdSlotId, pda.IdSlot);
            ItemSlotsSystem.AddItemSlot(uid, PdaComponent.PdaPenSlotId, pda.PenSlot);
            ItemSlotsSystem.AddItemSlot(uid, PdaComponent.PdaPaiSlotId, pda.PaiSlot);
            // DS14: tool slot used to register the ВорПРО program.
            ItemSlotsSystem.AddItemSlot(uid, PdaComponent.PdaToolSlotId, pda.ToolSlot);

            UpdatePdaAppearance(uid, pda);
        }

        private void OnComponentRemove(EntityUid uid, PdaComponent pda, ComponentRemove args)
        {
            ItemSlotsSystem.RemoveItemSlot(uid, pda.IdSlot);
            ItemSlotsSystem.RemoveItemSlot(uid, pda.PenSlot);
            ItemSlotsSystem.RemoveItemSlot(uid, pda.PaiSlot);
            ItemSlotsSystem.RemoveItemSlot(uid, pda.ToolSlot); // DS14
        }

        protected virtual void OnItemInserted(EntityUid uid, PdaComponent pda, EntInsertedIntoContainerMessage args)
        {
            if (args.Container.ID == PdaComponent.PdaIdSlotId)
                pda.ContainedId = args.Entity;

            UpdatePdaAppearance(uid, pda);
            UpdateJobStatus(uid);
        }

        protected virtual void OnItemRemoved(EntityUid uid, PdaComponent pda, EntRemovedFromContainerMessage args)
        {
            if (args.Container.ID == pda.IdSlot.ID)
                pda.ContainedId = null;

            UpdatePdaAppearance(uid, pda);
            UpdateJobStatus(uid);
        }

        private void OnGetAdditionalAccess(EntityUid uid, PdaComponent component, ref GetAdditionalAccessEvent args)
        {
            if (component.ContainedId is { } id)
                args.Entities.Add(id);
        }

        private void UpdatePdaAppearance(EntityUid uid, PdaComponent pda)
        {
            Appearance.SetData(uid, PdaVisuals.IdCardInserted, pda.ContainedId != null);
        }

        // update the status icon of the player that has the pda currently equipped
        private void UpdateJobStatus(EntityUid uid)
        {
            // Only the player who has the pda currently equipped can insert or remove Ids
            var parent = Transform(uid).ParentUid;
            _jobStatus.UpdateStatus(parent);
        }

        public virtual void UpdatePdaUi(EntityUid uid, PdaComponent? pda = null)
        {
            // This does nothing yet while I finish up PDA prediction
            // Overriden by the server
        }
    }
}
