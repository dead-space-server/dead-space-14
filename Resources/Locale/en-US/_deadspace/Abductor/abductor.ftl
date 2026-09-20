# Abductor localization. Values ported verbatim from Goob-Station,
# except: roles-antag-abductor-victim and tiles-abductor-floor were dangling
# locale keys in Goob (not defined in any FTL) and are filled here.

# Species
species-name-abductor = alien

# Reagent / flavor
reagent-name-alien-blood = alien blood
reagent-desc-alien-blood = The creature this bled from is not of this galaxy. Maybe it's grape flavoured.
reagent-physical-desc-alien = alien
flavor-base-alienblood = alien

# Tile
tiles-abductor-floor = Abductor Floor

# Roles / ghost roles
abductor-lone-ghost-role-name = Lone Abductor
abductor-lone-ghost-role-desc = Kidnap people, and stuff them with experimental organs of dubious origin, all by yourself.
abductor-scientist-ghost-role-name = Abductor Scientist
abductor-scientist-ghost-role-desc = Teleport people your partner kidnapped onto your ship and stuff them with experimental organs of dubious origin.
abductor-agent-ghost-role-name = Abductor Agent
abductor-agent-ghost-role-desc = Kidnap people for your partner to stuff them with experimental organs of dubious origin.
abductor-victim-role-name = Abductee
abductor-victim-role-name-freeagent = Abductee (Free Agent)
abductors-ghost-role-rules = You are an [color=red][bold]Abductor[/bold][/color].

# Antag objectives
roles-antag-abductor-objective = Kidnap station crew and perform your experiments on them!
roles-antag-abductor-victim = You have been changed by the Mothership. Survive your ordeal, return to the station, and tell the crew the truth.

# Antag briefings (game rules)
abductor-role-greeting = You are a professional combat scientist of a high-tech race. Your task is to abduct humans, conduct experiments on them, and return them alive for the purity of the experiment. It is not in your interest to destroy the station, kill, or assist the crew.
abductor-victim-role-greeting = You have seen things you shouldn't have. The world must know the truth.

# Mind role subtypes
role-subtype-abductor = Abductor
role-subtype-abductor-victim = Abducted

# Objectives
objective-issuer-abductors = [color=#FD0098]Mothership[/color]
objective-issuer-voices = [color=#FD0098]The Voices[/color]
objective-condition-abduct-title = Abduct {$count} person.
objective-condition-abduct-description = (use the Gizmo on a subdued victim, then use the Gizmo on the abductor console and select the attract action), then replace their heart with one of the glands, put them in the experimenter, and press complete experiment.