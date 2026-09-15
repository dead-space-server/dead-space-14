admin-player-actions-window-ert = SOC Management

ert-response-balance-label =
    Balance: { $cost }

ert-response-caused-messager =
    Central Command has received the request.
    { $team } is moving to the station.
    Estimated arrival time: minimum possible.

ert-response-caused-messager-gamma =
    Attention, station! Early command channels have relayed alarming reports of a threat to corporate interests. This unacceptable development has placed the asset under full SOC control. Gamma threat level with martial law protocols has been initiated on the station. ERT forces are being deployed toward the threat vector. We are coming for you. God save the Directorate!

ert-response-caused-messager-red =
    Attention, asset! Early communications have delivered alarming reports of a threat to corporate interests. This unacceptable development has resulted in the asset being placed under partial SOC control. ERT deployment toward the threat vector has begun. Expect the arrival of Red Code Emergency Response Team forces as soon as possible. We are coming for you. God save the Directorate!

ert-response-caused-messager-amber =
    Attention, asset! The Special Operations Corps has reviewed the situation aboard the asset and authorized deployment of ERT unit Amber. The team is en route to stabilize the situation and reinforce station security. Expect arrival as soon as possible. Glory to NanoTrasen!

ert-response-caused-messager-engineers =
    Attention, asset! The Special Operations Corps has authorized deployment of an ERT engineering detachment to restore critical asset systems. Specialists are en route and will begin infrastructure stabilization immediately upon arrival. Expect arrival as soon as possible. Glory to NanoTrasen!

ert-response-caused-messager-bsaa =
    Attention, station! Early command channels have relayed alarming reports of a critical biological catastrophe threat. The risk of sector-wide and external contamination has placed the asset under full SOC control. Sierra threat level with martial law protocols has been initiated on the station. BSAA forces are being deployed toward the threat vector. By fire and sword. God save the Directorate!

ert-response-caused-messager-official =
    Attention station command! Sector Central Command Headquarters on the line!
    Due to operational necessity, Sector Central Command Headquarters has decided to dispatch a Central Command Official
    to your asset to conduct an inquiry. Expect arrival in the near future. Glory to NanoTrasen!

ert-response-caused-messager-sierra =
    Attention, station! Early command channels have relayed alarming reports of a biological catastrophe threat. The risk of sector contamination has placed the asset under full SOC control. Sierra threat level with martial law protocols has been initiated on the station. NBC forces are being deployed toward the threat vector. By fire and sword. God save the Directorate!

ert-response-caused-messager-cburn =
    Attention, station! Early command channels have relayed reports of a biological catastrophe threat aboard the asset. NBC forces are being deployed to contain the contamination and clear hazardous zones. Expect the team's arrival shortly. By fire and sword. God save the Directorate!

ert-response-cso-sender = Special Operations Corps

ert-computer-window-title = ERT Computer
ert-computer-evac-title = start evacuation
ert-computer-evac-cancle-title = cancel evacuation
ert-computer-time-until-eval = { $time } seconds until evacuation.

station-event-response-team-arrival = The emergency response team has begun its mission on the station.

station-event-response-team-arrival-gamma =
    Gamma-class Emergency Response Team has arrived in the station sector.
    Crew are ordered to remain calm and report the location of the active threat.

station-event-response-team-arrival-red =
    Red-class Emergency Response Team has arrived in the station sector.
    Crew are ordered to remain calm and report the location of the active threat.

station-event-response-team-arrival-cburn =
    NBC-class Emergency Response Team has arrived in the station sector.
    Crew are ordered to remain calm and report the location of the active threat.

station-event-response-team-arrival-cburn-sierra =
    NBC-class Emergency Response Team has arrived in the station sector.
    Crew are ordered to remain calm and report the location of the active threat.

station-event-centcomm-official-arrival =
    Attention station command! Sector Central Command Headquarters on the line!
    The Central Command Official has arrived in the station sector. Await further instructions over standard communication channels. Glory to NanoTrasen!

ert-call-fail-already-waiting = Already requested
ert-call-fail-prototype-missing = The team is unavailable.
ert-call-fail-code-blacklist = Current alert level ({$level}) forbids calling this team.
ert-call-fail-not-enough-points = Not enough call resources (need {$price}, available {$balance}).
ert-call-fail-cooldown = Another call can be made in {$seconds} seconds.
ert-response-call-submitted = The ERT request has been submitted to the Special Operations Corps.
ert-console-request-submitted-announcement = Attention Station Command! The Special Operations Corps has received the asset's request for Emergency Response Team deployment. The request will be processed shortly. Glory to NanoTrasen!
ert-console-auth-required = Access requires two cards: captain + captain or captain + head of security.
ert-console-auth-card-invalid = Only captain and head of security ID cards can be inserted into this console.
ert-console-requester-unknown = unknown operator
ert-console-requester-name-with-job = { $name } ({ $job })
ert-console-request-rejected-announcement = Attention Station Command! The Special Operations Corps has denied the Emergency Response Team request. Handle the situation with your own forces. Glory to NanoTrasen!
ert-response-team-changed-announcement = Attention, station! The Special Operations Corps has reconsidered the previous Emergency Response Team decision. A decision has been made to dispatch { $team }. God save the Directorate!
ert-console-auth-slot-a = authorization card A
ert-console-auth-slot-b = authorization card B
ert-admin-requester-system = system request

ert-admin-window-subtitle = Requests, auto spawn, and manual approval for emergency response teams.
ert-admin-settings-title = ERT Call Settings
ert-admin-cooldown-label = Cooldown (sec):
ert-admin-points-label = Call points balance:
ert-admin-actions-label = Actions
ert-admin-refresh-button = Refresh
ert-admin-apply-button = Apply
ert-admin-pending-tab = Pending
ert-admin-approved-tab = Auto Spawn
ert-admin-manual-approved-tab = Manual Approval

ert-admin-pending-title = Pending Requests
ert-admin-selected-requester = Requester:
ert-admin-selected-reason = Call reason:
ert-admin-reject-button = Reject
ert-admin-approve-manual-button = Approve with manual spawn
ert-admin-approve-auto-button = Approve with auto spawn
ert-admin-notify-checkbox = Make announcement?

ert-admin-approved-title = Auto-approved Requests
ert-admin-arrival-label = Arrival time (sec):
ert-admin-approved-reason-label = Call reason:
ert-admin-approved-team-label = Auto-spawn team:
ert-admin-set-reason-button = Set reason
ert-admin-set-team-button = Change team
ert-admin-send-now-button = Send team now
ert-admin-cancel-auto-button = Cancel auto spawn
ert-admin-move-to-manual-button = Move to manual approval

ert-admin-promote-manual-button = Start auto spawn without notifications
ert-admin-queue-auto-button = Add to auto spawn
ert-admin-queue-auto-silent-button = Add without announcement
