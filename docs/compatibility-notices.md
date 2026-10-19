# Compatibility notices

Compatibility notices explain a specific change to Wake-Up in ordinary language.
A player should be able to tell which mod is involved, which feature is affected,
what Wake-Up stopped doing, what still happens, and whether any action is needed.
The current redesign is an unpublished source change; its checks are offline.
It has not been visually exercised in a running game or deployed to an installation.

## Player-facing presentation

The automatic window uses cards with a feature heading, known mod names, a short
status, and **What changed**, **What still works**, and **What to do** paragraphs.
Routine cooperation is informational. Optional setting changes are distinguished
from settings warnings and failures that need attention. A limited improvement is
not presented as a failure of the whole mod list, nor as proof of universal safety.

The normal popup never displays raw exception messages, method/class names,
Harmony patch descriptions or compatibility-check codes. Those diagnostics stay
in logs and the explicitly labelled **Technical compatibility details...** window.
Known settings are quoted by their actual labels. Optional instructions must say
the user can keep their current configuration when that remains a valid choice.

The semantic formatter is `CompatibilityNoticePresentation`. It uses the existing
operation ID, decision code and provider. Pretty mod names are display metadata,
not new decision identifiers. An integration can name its affected mod without
blaming that mod for an unknown incompatibility. Unknown provenance is stated only
when another mod's involvement was actually established; an unsupported version
does not imply another mod caused the limitation.

## Grouping and acknowledgement

Repeated notices with the same cause, mod, feature area, explanation and current/
historical status form one card. For example, eleven affected definition-search
operations can share one card, and five image-loading options yielded to Image Opt
can share another. Partial losses do not claim all searches are disabled.
Different causes or user actions stay separate.

Each card retains every original acknowledgement key. The key remains
`operation|decision-code|provider`; parse with at most three pieces, because a
provider label can contain the separator. Copy edits, prettier labels and later
identification of a known mod do not reset acknowledgements. The two background
setup call sites that previously lost supplier context retain their legacy keys;
their newly available supplier name and code are presentation-only metadata.

The window captures a snapshot when opened. Only keys in that snapshot can be
acknowledged; a later decision cannot inherit the acknowledgement. The player must
have displayed each card before acknowledging all of them. Visible fragments are
tracked so a card taller than the viewport can be read by scrolling; skipping its
middle does not count. Acknowledgement still requires a successful durable write.
Failure leaves notices pending and shows a retry/defer explanation.

## Older saved notices

The version-1 ledger and existing keys are preserved. Pending ledger text is treated
as diagnostic history, never replayed directly in the popup. The current formatter
recreates the explanation from the stable key and any optional presentation fields.
Malformed or unknown decisions receive a safe generic explanation.

A loaded pending notice is labelled as an earlier-launch decision until the same
decision is observed again. A repeated current decision refreshes its explanation
and mod attribution even when its key already exists. A decision superseded during
this launch is labelled as previously recorded within this launch, rather than
incorrectly described as coming from an earlier launch. If the operation is now
available, off or not applicable, the old notice says so and does not tell the
player to repair the old condition. Dismissing history does not alter settings.

## Focused validation

`CompatibilityNoticePresentationTests` covers known/unknown mods, information and
advisories, exact setting names, image and definition grouping, saved-text migration,
stable acknowledgements, current recovery, display-only attribution and scrolling
coverage. Existing operation-ledger, supplier-compatibility and definition-handoff
tests exercise the surrounding behavior. These are code and persistence checks;
they do not substitute for an in-game visual assessment.
