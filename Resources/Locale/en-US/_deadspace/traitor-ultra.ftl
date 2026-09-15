traitor-ultra-title = Traitors ULTRA
traitor-ultra-description = More agents, harder contracts, and a chance to escalate into open corporate warfare.
traitor-ultra-round-end-agent-name = traitor ULTRA

role-subtype-traitor-ultra = Traitor ULTRA
roles-antag-syndicate-agent-ultra-name = Traitor ULTRA
roles-antag-syndicate-agent-ultra-objective = You accepted an escalation contract. Survive the hunt and complete your new orders.
store-category-ultra = Ultra

traitor-ultra-objectives-complete-popup = Final objective completion registered, calculating...
traitor-ultra-contract-action-name = Open contract
traitor-ultra-contract-action-description = Open the intercepted escalation contract. If no decision is made within two minutes, the contract will be automatically withdrawn.
ent-ActionTraitorUltraOpenContract = { traitor-ultra-contract-action-name }
    .desc = { traitor-ultra-contract-action-description }
traitor-ultra-extra-objective-contract-action-name = Open bonus contract
traitor-ultra-extra-objective-contract-action-description = Open the standing offer for an additional ultra objective.
ent-ActionTraitorUltraOpenExtraObjectiveOffer = { traitor-ultra-extra-objective-contract-action-name }
    .desc = { traitor-ultra-extra-objective-contract-action-description }
traitor-ultra-offer-ready-popup = The intercepted contract is ready. Use the action to open the offer.
traitor-ultra-offer-title = Contract Intercepted
traitor-ultra-offer-body = {$newCorp} has reviewed your completed contract with {$oldCorp}. They are offering a hostile buyout: louder methods, larger assets, and protection only while you remain useful. A response is required within two minutes; silence will be treated as refusal.
traitor-ultra-offer-gains =
    You receive:
    - permission for loud action
    - major sabotage authority
    - a new uplink assortment
    - additional telecrystals
    - a contract with a new corporation
traitor-ultra-offer-losses =
    You lose:
    - the right to a quiet shift
    - immunity from validhunt
    - your previous corporate contract
traitor-ultra-offer-accept = Accept
traitor-ultra-offer-decline = Refuse
traitor-ultra-offer-declined-popup = You remain faithful to your principles and return to a peaceful shift end.
traitor-ultra-offer-expired-popup = Contract withdrawn: no decision was made in time.
traitor-ultra-extra-objective-offer-ready-popup = An additional assignment is available.
traitor-ultra-extra-objective-offer-title = Additional Assignment
traitor-ultra-extra-objective-offer-body = {$corp} confirms completion of a major directive and offers one more open order. The offer is permanent and will remain available until an explicit decision is made.
traitor-ultra-extra-objective-offer-body-immediate = {$corp} issued you an unusually entertaining objective, but believes that is not enough. Would you like to bring NanoTrasen and its personnel even more suffering?
traitor-ultra-extra-objective-offer-objective = Additional objective: {$objective}
traitor-ultra-extra-objective-offer-objective-pending = being selected
traitor-ultra-extra-objective-offer-reward = Reward on acceptance: {$amount} TC.
traitor-ultra-extra-objective-offer-accept = Accept
traitor-ultra-extra-objective-offer-decline = Refuse
traitor-ultra-extra-objective-offer-accepted-popup = Additional assignment accepted. The reward has been transferred to your uplink.
traitor-ultra-extra-objective-offer-declined-popup = Additional assignment refused.
traitor-ultra-extra-objective-offer-failed-popup = Assignment temporarily unavailable: the handler could not select an executable objective.
traitor-ultra-upgrade-briefing = Contract transfer confirmed. {$oldCorp} no longer recognizes you as protected property. {$newCorp} has opened the escalation budget and attached a new directive package. Your old objectives remain recorded; your new orders are available in your character menu.
traitor-ultra-role-briefing-memory = Escalation contract: former handler {$oldCorp}; active handler {$newCorp}. The original uplink channel remains valid.

traitor-ultra-bounty-announcer =
    Syndicate

traitor-ultra-bounty-announcement =
    Attention on all channels. {$agent} has changed the terms of the contract. Starting from the date of this notification, the Syndicate is offering a large reward on an individual basis for the assassination of this person. Reminder: any cooperation with this agent will result in immediate elimination. Death to NanoTrasen!
# traitor-ultra-bounty-announcement-cybersun =
#     Cybersun Industries black protocol is active. {$agent} has defected from {$oldCorp} to {$newCorp} and is no longer considered recoverable property. The asset is marked for disposal. Confirmed liquidation will be rewarded: shadow-market agents will receive individually calculated payouts, and {$oldCorp} will contact independent contractors directly.
# traitor-ultra-bounty-announcement-gorlex =
#     Gorlex contract broadcast. {$agent} broke oath with {$oldCorp} and sold their violence to {$newCorp}. The name is open season. Confirmed liquidation will be rewarded: shadow-market agents will receive individually calculated payouts, and {$oldCorp} will contact independent contractors directly.
# traitor-ultra-bounty-announcement-interdyne =
#     Interdyne Pharmaceutics containment notice. {$agent} has left {$oldCorp} custody for {$newCorp} and is reclassified as an uncontrolled hostile sample. Confirmed liquidation will be rewarded: shadow-market agents will receive individually calculated payouts, and {$oldCorp} will contact independent contractors directly.
# traitor-ultra-bounty-announcement-donk =
#     Donk.Co loss-prevention bulletin. {$agent} violated an active {$oldCorp} contract and accepted cover from {$newCorp}. The asset is cleared for termination. Confirmed liquidation will be rewarded: shadow-market agents will receive individually calculated payouts, and {$oldCorp} will contact independent contractors directly.
# traitor-ultra-bounty-announcement-waffle =
#     WaffleCorp enforcement dispatch. {$agent} abandoned {$oldCorp} for {$newCorp}; the account is hostile, overdue, and collectible by force. Confirmed liquidation will be rewarded: shadow-market agents will receive individually calculated payouts, and {$oldCorp} will contact independent contractors directly.
traitor-ultra-bounty-traitor-kill-message = Contract closure confirmed. Your individual payout is being calculated and queued.
traitor-ultra-bounty-crew-kill-message = Your initiative has been noticed. The previous handler is preparing a private offer.
traitor-ultra-bounty-security-kill-message = Contract violator neutralized. Executor loyalty to their Corporation confirmed; 10,000 credits have been transferred to the NanoTrasen Security account.
traitor-ultra-bounty-captain-kill-message = Contract violator neutralized. Enemy asset Captain rank confirmed; 10,000 credits have been transferred to your personal account. Excellent work, Captain...
traitor-ultra-recruit-title = Private Contract Offer
traitor-ultra-recruit-body = {$corp} confirms your liquidation claim and offers a limited field contract. Accepting opens a funded uplink and a standard order package.
traitor-ultra-recruit-accept = Accept
traitor-ultra-recruit-decline = Refuse
traitor-ultra-recruit-briefing = You are now a limited contractor for {$corp}. Your uplink has been funded with a small operational budget. Complete the issued orders and avoid unnecessary exposure.
traitor-ultra-recruit-failed-no-objective = Contract withdrawn: the handler could not select an executable objective.

traitor-ultra-objective-kill-security-title = Annihilate Security
traitor-ultra-objective-kill-security-description = Kill {$count} of {$total} Security personnel assigned to this station. Fear is useful; survivors are not.
traitor-ultra-objective-destroy-atmos-title = Break the atmospheric backbone
traitor-ultra-objective-destroy-atmos-description = Destroy or dismantle every gas miner in the station atmospherics department.
traitor-ultra-objective-destroy-ame-title = Destroy the AME controller
traitor-ultra-objective-destroy-ame-description = Destroy or dismantle the station AME controller.
traitor-ultra-objective-destroy-servers-title = Burn the station nervous system
traitor-ultra-objective-destroy-servers-description = Destroy or dismantle crew monitoring, at least one camera router, and at least three telecommunication servers.

ent-TraitorUltraHijackShuttleObjective = Hijack the evacuation shuttle
    .desc = Leave the sector on the evacuation shuttle with no loyal NanoTrasen crew aboard. Any method is authorized.

ent-TraitorUltraSurviveObjective = Survive
    .desc = Stay alive until the end of the shift.

traitor-ultra-objective-destroy-station-ai-core-title = Destroy the station AI core
traitor-ultra-objective-destroy-station-ai-core-description = Destroy the station's assigned AI core. Purchased or foreign AI cores do not count.
traitor-ultra-objective-hijack-trade-title = Hijack the trade uplink
traitor-ultra-objective-hijack-trade-description = Deploy a trade interdictor on the station trade hub and keep it online until the trade channel is severed.

ent-TraitorUltraDestroyStationAiCoreObjective = Destroy the station AI core
    .desc = Destroy the station's assigned AI core.

ent-TraitorUltraHijackTradeObjective = Hijack the trade uplink
    .desc = Deploy a trade interdictor on the station trade hub and keep it online until the trade channel is severed.

trade-interdiction-announcer = Station Automated Systems
trade-interdiction-announcement-started = Attention Security personnel, unauthorized interference has been detected at the facility trade outpost. Immediate liquidation of hostile interference is required. Estimated time until access to trade systems is obtained: 3 minutes.
trade-interdiction-announcement-cancelled = Attention! Interference in the trade hub has been neutralized. External order channels are operating normally. Thank you for serving NanoTrasen!
trade-interdiction-announcement-completed = Attention! Quarantine initiated. Isolation protocol "SIERRA-1". Trade hub quarantine is now in effect. External order channels for the Corporate Facility are no longer available. Thank you for working for NanoTrasen!
trade-interdiction-beacon-examine-idle = The interdictor is folded and inactive.
trade-interdiction-beacon-examine-active = Trade hijack progress: {$percentage}%.
trade-interdiction-beacon-examine-complete = The trade channel has been severed.
trade-interdiction-beacon-popup-invalid-location = The interdictor must be deployed on the station trade hub.
trade-interdiction-beacon-popup-no-objective = You do not have an active trade hijack contract for this station.
trade-interdiction-beacon-popup-already-active = A trade hijack is already in progress on this station.
trade-interdiction-beacon-popup-already-complete = This station's trade channel is already severed.
