# Nonshipping XML prefix experiment

This is the R1 experiment withdrawn from the normal product after parent review.
The real-content prefix restored correctly but cost about 770 ms versus 22 ms
for ordinary patching. A cost refusal still incurred validation overhead, so
neither that refusal nor the default-off setting justified shipping its bridges.
No useful independent XML cache is delivered.

These sources compile **only into WakeUp.Tests**, through that existing project's
explicit source include. They are outside the product source directory, have no
automatic Prepatcher registration, and are not initialized by Wake-Up. Historical
selector and maintenance routines remain here solely to preserve the experiment;
the normal product has no setting, selector handler or cache maintenance for it.
The test DLL is not an installed runtime payload. Do not move these files back
into the product merely to expose the old option.

Existing tests retain the state, source-attribution, corruption and fallback
evidence. The callsite contract test accounts only for the bridge assembly's
relocation from WakeUp to WakeUp.Tests; the historical reviewed method constants
remain unchanged. Ordinary-product tests verify that all five experiment types
and the setting are absent from the compiled product, and that the experimental
prepatch has no registration attribute.

See the [R1 report](../../docs/archive/ecosystem-replacement-20260910/r1-independent-xml.md) for
historical source/build identities, the negative result and the bounded R4
follow-up premise. This directory adds no project, runner or dependency.
