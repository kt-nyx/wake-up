# Fixture menu observer

The observer source and package metadata live here. Current setup, launch, event interpretation, automatic exit and failure handling are documented in [Development and fixture](../../docs/development.md#authorized-launch-and-capture).

The observer is fixture-only and independent of Wake-Up. It measures the first completed main-menu repaint and, when selected, requests normal exit afterward. It must match between performance comparison arms, including Wake-Up-absent runs.

`--gameplay-smoke` selects the separate new-colony/tick/save/reload check after
the menu endpoint, and `--residual-probe` measures bounded early-menu calls and
late type searches. These are explicit fixture qualification modes, not routine
timing comparisons. See [post-startup qualification](../../docs/development.md#normal-activation-and-post-startup-qualification).
