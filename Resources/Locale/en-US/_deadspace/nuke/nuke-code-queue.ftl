# Nuke code queue
ert-admin-main-tab = ERT
nuke-codes-admin-tab = Nuke codes
nuke-codes-admin-title = Nuke Code Dispatch Queue
nuke-codes-admin-stations-title = Stations
nuke-codes-admin-reasons-title = Dispatch reasons
nuke-codes-admin-pending-title = Pending dispatches
nuke-codes-admin-selected-requester = Requester:
nuke-codes-admin-selected-station = Station:
nuke-codes-admin-selected-reason = Reason:
nuke-codes-admin-queue-button = Add to queue
nuke-codes-admin-approve-button = Approve now
nuke-codes-admin-cancel-button = Cancel

nuke-codes-reason-manual = Manual administrator decision
nuke-codes-reason-blob-critical-mass = Blob critical mass
nuke-codes-reason-spider-terror-critical = Spider Terror critical takeover
nuke-codes-requester-auto-timeout = automatic timeout
nuke-codes-requester-auto-no-admin = automatic dispatch: no available KSO administrator
nuke-codes-requester-system = game system
nuke-codes-requester-server-console = server console

nuke-codes-admin-queued = Nuke code dispatch request added to the queue.
nuke-codes-admin-invalid-station = Invalid station.
nuke-codes-admin-invalid-reason = Invalid nuke code dispatch reason.
nuke-codes-admin-already-pending = This station already has a pending nuke code dispatch request.
nuke-codes-admin-queue-failed = Failed to queue nuke code dispatch request.
nuke-codes-admin-permission-denied = You do not have permission to manage nuke code dispatch requests.
nuke-codes-admin-request-missing = Nuke code dispatch request was not found.
nuke-codes-admin-cancelled = Nuke code dispatch request cancelled.
nuke-codes-admin-sent = Nuke codes were sent.
nuke-codes-admin-send-failed = Nuke codes were not sent: no authorized fax machine received them.
nuke-codes-admin-awaiting-decision-short = KSO decision
nuke-codes-admin-seconds-short = { $seconds } sec

nuke-codes-admin-alert-game-queued = The game added nuke code dispatch request #{ $requestId } for { $station } to the queue. Reason: { $reason }.
nuke-codes-admin-decision-reminder = KSO decision required for nuke code dispatch request #{ $requestId } for { $station }. Reason: { $reason }. Open the KSO panel and approve or cancel the dispatch.
nuke-codes-admin-alert-admin-queued = { $admin } added nuke code dispatch request #{ $requestId } for { $station } to the queue. Reason: { $reason }.
nuke-codes-admin-alert-cancelled = { $admin } cancelled nuke code dispatch request #{ $requestId } for { $station }.
nuke-codes-admin-alert-sent = Nuke code dispatch request #{ $requestId } for { $station } was approved by { $admin }. Reason: { $reason }.
nuke-codes-admin-alert-send-failed = Nuke code dispatch request #{ $requestId } for { $station } was approved by { $admin }, but codes were not sent. Reason: { $reason }.

nuke-codes-announcement-manual =
    Attention, command and crew of station object { $station }, this is the Special Operations Corps.

    Pursuant to an approved order, nuclear authentication codes have been sent to the Captain's fax aboard the object.

    By order of the Special Operations Officer, station self-destruction protocols must be initiated, followed by evacuation of the crew by any available means. God preserve the Director!

blob-alert-critical =
    Attention, command and crew of station object { $station }, this is the Special Operations Corps.

    Local station systems have registered a high level of biological contamination due to the critical mass of a level-five biological threat. Pursuant to an approved order, nuclear authentication codes have been sent to the Captain's fax aboard the object.

    By order of the Special Operations Officer, station self-destruction protocols must be initiated, followed by evacuation of the crew by any available means. God preserve the Director!
spider-terror-centcomm-announcement-station-was-nuke =
    Attention, command and crew of station object { $station }, this is the Special Operations Corps.

    Local station systems have registered a high level of biological contamination due to uncontrolled Spider Terror growth. Pursuant to an approved order, nuclear authentication codes have been sent to the Captain's fax aboard the object.

    By order of the Special Operations Officer, station self-destruction protocols must be initiated, followed by evacuation of the crew by any available means. God preserve the Director!
