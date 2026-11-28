survey-beacon-landing = { $planet }: landing zone
survey-beacon-far = { $planet }: far slope
survey-cargo-examine = [color=#7fb8ff]Expedition cargo.[/color] Does not open: valuable as a whole. { $need }
survey-cargo-examine-reward = If delivered to the station: { $money } credits and { $items }.
survey-cargo-examine-money = If delivered to the station: { $money } credits.
survey-cargo-examine-stolen = [color=#ffb347]Stolen from the station. Valuable only if delivered undamaged.[/color]
survey-search-start = The scanner listens to the void. The search takes time and may find nothing.
survey-search-busy = A search is already running. About { $seconds } s left.
survey-search-mapping = A planet was found, mapping in progress.
survey-search-known = The planet is already found: { $planet }. The course is open on the navigation map.
survey-search-nothing = Nothing. Only noise. You can try again.

survey-sender = Expedition navigation
survey-sender-sky = Planet
survey-found = Planet { $planet } found. { $land } { $motive } Valuable cargo below: { $cargo }. Press "Scan" on the shuttle console: landing beacons will appear on the map.

survey-motive-retrieve = Stolen station cargo is down there. Pay only if it comes back intact.
survey-motive-windfall = Someone else's cargo is down there. Leave it, or haul it for credits.
survey-motive-curse = Someone else's cargo is down there. Leave it, or haul it for credits.

survey-land-city = A ruined city.
survey-land-cove = A camp by the water.
survey-land-factory = The shops are still printing forms.
survey-land-forest = Forest.
survey-land-jungle = Acid rain.
survey-land-tundra = Open tundra.
survey-land-frost = Ice and empty buildings.
survey-land-dust = Dust to the horizon.

survey-storm-warning-first = The sky is going dark. Storm soon, find a roof.
survey-storm-warning = Storm again. This one is worse.
survey-storm-over = The storm is over. Another will come.

survey-leave-empty = Left empty-handed. Alive, though.
survey-leave-cargo = Hauled off the planet: { $count } { $count ->
    [one] cargo
   *[other] cargos
}. { $heat ->
    [0] The bill is small so far.
   *[other] The bill grows.
}

survey-cargo-stolen = { $name } (stolen from the station)

survey-deliver-retrieve = The expedition returned { $name } intact. { $need } { $money } credits added to the station.
survey-deliver-broken = The expedition brought { $name }, but the cargo is damaged and useless.
survey-deliver-windfall = The expedition brought { $name }. { $need } { $money } credits added to the station.

survey-curse-beasts = A pack came for { $name }. They are already on the station.
survey-curse-raiders = The people { $name } was taken from followed it. They are on the station.
survey-curse-pirates = Pirates tracked { $name }. They are aboard.
survey-curse-factory = { $name } is printing again. Forms are coming out of it.
survey-curse-deadelement = { $name } is hot. The dead land came aboard with it.

survey-need-science = A drive full of disks. Science, and keep it whole.
survey-need-command = A blueprint core. Command. Do not crack it on site.
survey-need-engineering = A microreactor. Engineering. It stays warm when off.
survey-need-medical = Unmarked ampoules. Medical. Do not test them on yourself.
survey-need-security = A sealed case. Security. Leave the seal on.
survey-need-cargo = A crate of ingots. Cargo. It sells as it is.

cmd-surveyplanet-desc = Creates this round's expedition planet.
cmd-surveyplanet-help = surveyplanet [world|*] [motive|*] [replace true|false]. Asterisk: random. Replace deletes the current planet if nobody is on it.
cmd-surveyplanet-occupied = Players or ghosts are on the current planet ({ $count }). Move them off first.
cmd-surveytp-desc = Moves you to the expedition planet.
cmd-surveytp-help = surveytp [landing|town|far]: landing zone, settlement edge or far slope.
cmd-surveytp-none = There is no planet yet, or it is still being made.
cmd-surveytp-done = You are on planet { $planet }.
cmd-surveyplanet-bad-world = No such world.
cmd-surveyplanet-bad-motive = Motive: Retrieve, Windfall or Curse.
cmd-surveyplanet-done = Planet ready: { $planet }
cmd-surveyplanet-failed = Failed to create the planet.

survey-bark-raider-seen-1 = Strangers! To arms!
survey-bark-raider-seen-2 = Guests. Let's greet them.
survey-bark-raider-hurt-1 = Argh, I'm hit!
survey-bark-raider-hurt-2 = My leg!
survey-bark-raider-loss-1 = No... He's gone.
survey-bark-raider-loss-2 = They killed one of ours!
survey-bark-raider-kill-1 = Got one!
survey-bark-raider-kill-2 = Down he goes!
survey-bark-raider-retreat-1 = Fall back!
survey-bark-raider-retreat-2 = Too few of us, back!
survey-bark-raider-idle-1 = Could use clean water.
survey-bark-raider-idle-2 = Rain again.
survey-bark-raider-loot-1 = They're taking ours! Stop the thief!
survey-bark-raider-revenge-1 = Thought we wouldn't find you?

survey-bark-pirate-seen-1 = Look who's here.
survey-bark-pirate-seen-2 = All hands!
survey-bark-pirate-deal-1 = Stop. Leave a crate by our chest and we part in peace.
survey-bark-pirate-deal-2 = Toll for passage. One crate by the chest, and nobody dies.
survey-bark-pirate-paid-1 = Now that's talking. Move along.
survey-bark-pirate-paid-2 = A deal's a deal. For now.
survey-bark-pirate-betray-1 = Changed our minds. We take it all!
survey-bark-pirate-betray-2 = The deal is over. Fire!
survey-bark-pirate-hurt-1 = I'm hit!
survey-bark-pirate-loss-1 = Another one overboard...
survey-bark-pirate-kill-1 = One less!
survey-bark-pirate-retreat-1 = Blood costs more than loot! Away!
survey-bark-pirate-idle-1 = Sharing the loot at midnight.
survey-bark-pirate-loot-1 = The loot is leaving! After it!
survey-bark-pirate-revenge-1 = You took ours. We came for it.

survey-ui-title = Expedition: { $planet }
survey-ui-threat-low = The planet is quiet
survey-ui-threat-mid = The planet shoots rarely
survey-ui-threat-high = The planet is shooting

survey-obj-explore = Explore the planet: places found { $done }/{ $goal }
survey-obj-retrieve = Find and load the stolen cargo onto the shuttle: { $done }/{ $goal }
survey-obj-cargo = Load valuable cargo onto the shuttle: { $done }/{ $goal }
survey-obj-raiders = Break the raiders: down { $done }/{ $goal }
survey-obj-pirates = Deal with the pirates: pay the toll at their chest or beat them
survey-obj-stamps = Blow up the stamps so the factory stops printing: { $done }/{ $goal }
survey-obj-dens = Study the clutches at the dens: { $done }/{ $goal }
survey-obj-storm = Survive a storm in shelter without losses
survey-obj-study = Study anomalous objects (a scanner helps): { $done }/{ $goal }
survey-obj-done = Objective complete: { $text } The station and the crew are rewarded.

survey-personal-share = Your share for the cargo: { $money } credits.

survey-verb-study = Study
survey-study-start = You start studying the find.
survey-study-done = Data sent to the R&D server: { $points } points. You get a bonus.

survey-relic-bloom = Wounds close. Something stirs under the skin.
survey-relic-bloom-wither = The flower crumbles to ash.
survey-relic-hatch = The egg bursts!
survey-relic-egg-kick = The egg twitches in your hands.
survey-relic-whisper-1 = The orb whispers someone's name.
survey-relic-whisper-2 = Cold seeps from the orb. Something answers the call.
survey-relic-whisper-3 = The orb laughs quietly.
survey-relic-whisper-4 = The orb asks not to be sold.

survey-bark-raider-reinforce-1 = Backup's here! Pin them down!
survey-bark-pirate-reinforce-1 = Boarding party is here!

id-card-access-level-survey-native = Planet natives

survey-verb-sabotage-power = Cut the power
survey-verb-sabotage-comms = Disable the mast
survey-sabotage-done = Done. The base will not notice right away.
survey-ui-spotted = You were spotted! Alarm in { $seconds } s if the lookouts survive
survey-ui-alarm = ALARM: the base is up
survey-ui-blackout = base powered down
survey-ui-comms = their comms are down
survey-obj-power = (optional) Black out the main base via the power node at an outpost
survey-obj-comms = (optional) Disable the comms mast: the alarm will not spread
survey-obj-stealth = (optional) Take an outpost without raising the alarm

survey-bark-raider-alarm-1 = Strangers! Wake everyone!
survey-bark-raider-blackout-1 = Lights are out! Who's at the node?!
survey-bark-raider-nocomms-1 = Base isn't answering!
survey-bark-pirate-alarm-1 = Ring the bell! All hands!
survey-bark-pirate-blackout-1 = Who killed the lights?!
survey-bark-pirate-nocomms-1 = The mast is silent! We're alone!
survey-ui-deal = Pirates hold fire and wait for a toll: cargo by their chest. { $seconds } s left

survey-core-greet = CORE: Organics detected. Raw material accepted for processing.
survey-core-down = The factory core is dark. The line stops, the forms drop dead.
survey-obj-core = Destroy the factory core in the central hall: everything else will stop

survey-bark-raider-help-1 = I'm pinned! Need help!
survey-bark-raider-qrf-1 = Squad's not answering. Sending backup!
survey-bark-pirate-help-1 = We're pinned! Everyone here!
survey-bark-pirate-qrf-1 = The boat's silent. Wake the boarders!

survey-bark-raider-medic-1 = Hold on, I'm coming!
survey-bark-raider-medic-2 = Medic's here, stay still!
survey-bark-raider-medic-3 = Cover me, going for the wounded!
survey-bark-raider-drag-1 = Dragging him to cover! Suppressing fire!
survey-bark-raider-drag-2 = Got him! Cover the pullback!
survey-bark-raider-patch-1 = Tourniquet's on, breathe!
survey-bark-raider-patch-2 = Almost there, don't black out!
survey-bark-raider-grenade-1 = Grenade!
survey-bark-raider-grenade-2 = Catch!
survey-bark-raider-grenade-3 = Smoking them out!
survey-bark-raider-smoke-1 = Smoke! Fall back under smoke!
survey-bark-raider-smoke-2 = Popping smoke!
survey-bark-raider-scout-1 = Drone's got eyes on them! Move!
survey-bark-raider-scout-2 = Bird spotted intruders, coordinates in!
survey-bark-pirate-medic-1 = Surgeon's coming, don't die on me!
survey-bark-pirate-medic-2 = Hold still, I'll stitch you up!
survey-bark-pirate-drag-1 = Hauling him off, cover me!
survey-bark-pirate-drag-2 = Got him, keep them pinned!
survey-bark-pirate-patch-1 = Bite down, one more stitch!
survey-bark-pirate-patch-2 = Patched, now live!
survey-bark-pirate-grenade-1 = Frag out!
survey-bark-pirate-grenade-2 = Eat this, rats!
survey-bark-pirate-smoke-1 = Smoke! Fall back!
survey-bark-pirate-smoke-2 = Smokescreen, everyone back!
survey-bark-pirate-scout-1 = The eye spotted them! Everyone go!
survey-bark-pirate-scout-2 = Drone found guests, welcome them!

chat-radio-survey-native = Native frequency

survey-place-landing = by the landing pad
survey-place-far = by the far beacon
survey-place-power = by the power node
survey-place-comms = by the comms mast
survey-place-outpost = by the outpost
survey-place-stamp = by the stamp
survey-place-device = by the control node
survey-place-site = by the strange find
survey-place-den = by the den
survey-place-settlement = in the settlement
survey-place-base = in the base, { $dir } wing
survey-place-sector = in the { $dir } sector

survey-sector-north = north
survey-sector-south = south
survey-sector-east = east
survey-sector-west = west
survey-sector-centre = central
survey-heading-north = heading north
survey-heading-south = heading south
survey-heading-east = heading east
survey-heading-west = heading west
survey-heading-centre = holding still
survey-heading-still = holding still

survey-radio-raider-sitrep-1 = { $where }: I see { $count }, { $heading }.
survey-radio-raider-sitrep-2 = Post, visitors { $where }. { $heading }. I counted { $count }.
survey-radio-raider-idle-1 = { $where }, quiet. Even the dogs are quiet.
survey-radio-raider-idle-2 = Who pulled the batteries off the rack again? This is { $where }.
survey-radio-raider-contact-1 = Shooting, { $where }. { $count } of them.
survey-radio-raider-help-1 = { $where }, they have us pinned. Who is free?
survey-radio-raider-moving-1 = Moving to { $where }.
survey-radio-raider-qrf-1 = Nobody answers from { $where }. Waking the heavies.
survey-radio-raider-arrive-1 = We are at { $where }. Going in.
survey-radio-raider-alarm-1 = Alarm. Hostiles from { $where }. Armor on?
survey-radio-raider-scout-1 = Drone: { $count } { $where }. Intercept.
survey-radio-raider-medic-1 = Wounded { $where }, I'm coming.
survey-radio-raider-blackout-1 = Lights are out. Who is on the cables?
survey-radio-pirate-sitrep-1 = From the bridge: { $count } { $where }, { $heading }.
survey-radio-pirate-idle-1 = Watch { $where }. Water is quiet.
survey-radio-pirate-contact-1 = Shooting, { $where }. { $count } of them.
survey-radio-pirate-help-1 = { $where }, we are short. Everyone here.
survey-radio-pirate-moving-1 = Moving to { $where }.
survey-radio-pirate-qrf-1 = Watch { $where } is silent. Wake the boarders.
survey-radio-pirate-arrive-1 = Boarders at { $where }.
survey-radio-pirate-alarm-1 = Everyone up. Hostiles at { $where }.
survey-radio-pirate-scout-1 = The eye: { $count } { $where }.
survey-radio-pirate-medic-1 = Surgeon is coming to { $where }. Stay still.
survey-radio-pirate-blackout-1 = Lights out. Watch the hold, not the sky.
survey-radio-factory-sitrep-1 = Targets: { $count }. Point { $where }. Movement: { $heading }.
survey-radio-factory-idle-1 = Line { $where } is stopped. The quota is open.
survey-radio-factory-contact-1 = Gunfire { $where }. Targets { $count }.
survey-radio-factory-qrf-1 = Group { $where } does not answer. Printing a replacement.
survey-radio-factory-arrive-1 = Replacement is out toward { $where }.
survey-radio-factory-alarm-1 = Alarm. Origin { $where }. Do not stop the lines.
survey-radio-factory-scout-1 = Watcher: { $count } at { $where }. Coordinates sent.
survey-radio-factory-blackout-1 = Power is down. Cutting spare lines, stamps stay up.
survey-bark-raider-incoming-1 = Grenade! Get down!
survey-bark-raider-selfheal-1 = Got clipped... patching up.
survey-bark-raider-stim-1 = Stimming up!
survey-bark-pirate-incoming-1 = Grenade! Scatter!
survey-bark-pirate-selfheal-1 = Give me a minute, stitching.
survey-bark-pirate-stim-1 = For the captain!

survey-radio-raider-crit-1 = Guys... I caught one... { $where }...
survey-radio-raider-crit-2 = I'm hit... lying { $where }... come get me?
survey-radio-pirate-crit-1 = Blast it... they holed me { $where }...
survey-radio-raider-laugh-1 = Heh. Kolyan again.
survey-radio-pirate-laugh-1 = The bosun heard that. He is not paying you extra.
survey-radio-factory-laugh-1 = Logged. The line does not stop for it.
survey-joke-raider-1-1 = Kolyan caught a station hand with a crate.
survey-joke-raider-1-2 = The hand yelled that the crate was scientific and must not be dropped.
survey-joke-raider-1-3 = Kolyan dropped it. Now we have the crate and a complaint.
survey-joke-pirate-1-1 = The cabin boy has mixed up the captain and the cook for three days.
survey-joke-pirate-1-2 = The cook does not mind. The captain already got the cook's share of extinguisher.
survey-joke-factory-1-1 = Line 3 is stuck on the same door again.
survey-joke-factory-1-2 = The form waits. The door waits. The blank is going cold.
survey-joke-factory-1-3 = The log calls it a scheduled pause.

survey-radio-raider-intruder-heard-1 = Hold on. Who was that on the channel? Name yourself.
survey-radio-raider-intruder-warn-1 = Him again! Outsider, we hear you.
survey-radio-raider-intruder-locate-1 = Got the chatterbox { $where }! Nearest team, go!
survey-radio-raider-intruder-cut-1 = All: switching cipher. Outsider, goodbye.
survey-radio-pirate-intruder-heard-1 = Arr, who's croaking on our wave?
survey-radio-pirate-intruder-warn-1 = Landlubber!
survey-radio-pirate-intruder-locate-1 = Rat squeaking { $where }! Boarders, go!
survey-radio-pirate-intruder-cut-1 = Changing cipher, lads. Rat, you're done.
survey-radio-factory-intruder-heard-1 = FOREIGN TRANSMITTER DETECTED.
survey-radio-factory-intruder-warn-1 = REPEATED INTRUSION. TRIANGULATING.
survey-radio-factory-intruder-locate-1 = INTERFERENCE SOURCE: { $where }. FORMS TO SOURCE.
survey-radio-factory-intruder-cut-1 = KEY ROTATION. FOREIGN TRANSMITTER DISABLED.

survey-radio-factory-rebuilt-1 = FORM RESTORED { $where }. RETURNING TO DUTY.

survey-bark-raider-cover-1 = Pinned! Take cover!
survey-bark-pirate-cover-1 = Get down, they're shooting!

cmd-surveydebug-desc = Toggles the planet natives debug overlay.
cmd-surveydebug-help = surveydebug: shows role, plan step, target, steering and flags above natives within 25 tiles. Run again to turn off.
cmd-surveydebug-on = Natives debug on.
cmd-surveydebug-off = Natives debug off.
