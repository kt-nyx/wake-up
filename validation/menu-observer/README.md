# Fixture menu observer

The observer source and package metadata live here. Current setup, launch, event interpretation, automatic exit and failure handling are documented in [Development and fixture](../../docs/development.md#authorized-launch-and-capture).

The observer is fixture-only and independent of RLO. It measures the first completed main-menu repaint and, when selected, requests normal exit afterward. It must match between performance comparison arms, including RLO-absent runs.
