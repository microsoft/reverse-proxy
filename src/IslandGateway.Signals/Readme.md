# IslandGateway.Signals

## Overview

Signals serve as an abstraction over data that changes over time, and whose
latest value can be read cheaply and without blocking at any time.

Signals come with a built-in notifications mechanism, so that signals can be derived
from other signals, and updates to one signal are propagated to dependent signals
deterministically and reliably.

Writing a value to a signal incurs the cost of propagating that change to all derived signals,
and all writes are synchronized to ensure thread-safety and determinism. Signals are attached
to a `SignalContext` from creation, and signals must belong to the same context
to interopoerate. In many cases, a single context suffices, and `SignalFactory.Default` helps
create signals in a shared context.

Reading a signal is non-blocking and always produces the latest value for that signal.
It does not wait for ongoing propagations to complete, and notifications achieve guaranteed delivery
through the notion of signal snapshots (see `IReadableSignal{T}.GetSnapshot()`).

Signals are in many ways related to observables (think `rx`, `Reactive` or
`Functional Reactive Programming` paradigms). The power of this abstraction
comes from the ability to apply primitives (see `SignalExtensions`)
to produce derived signals from existing signals.

Island Gateway leverages signals to optimize performance on hot paths
by precomputing necessary routing information such as the set of healthy endpoints in a backend.
This is done by writing a LINQ-query over other signals to create a new signal
which is kept up to date automagically. In the hot path, it suffices to read the current value
of the derived signal, which will always have a materialized immutable result.

A data flow diagram of one of the places where signals are used in Island Gateway
is shown below. Double arrows denote data flows implemented with signals:

```
┌───────────────┐
│               │
│  BackendInfo  │
│               ├── DynamicState ◄═══════════════════════════════════════╗
│               │            ▲  ▲                                        ║
│               │            ║  ║                                        ║
│               ├── Config ══╝  ║                                        ║
│               │               ║           ┌──────────────────┐         ║
│               ├── EndpointsManager ─────> │ ┌──────────────────┐       ║
└───────────────┘                           │ │                  │       ║
                                            │ │                  │       ║
                                            │ │  EndpointInfo's  │       ║
                                            │ │                  │       ║
                                            │ │                  │       ║
                                            │ │                  ├── DynamicState
                                            └─│                  │
                                              └──────────────────┘
```

`BackendInfo.DynamicState`, which is a `IReadableSignal<BackendDynamicState>`,
reacts to updates from `BackendInfo.Config`, `BackendInfo.EndpointsManager`, and
[`EndpointInfo.DynamicState` from each of the tracked associated endpoints].

During routing, `BackendInfo.DynamicState.Value` can be accessed efficiently
since the result was precomputed and only in reaction to changes.

## Examples

1. Derived signal with `Select`

```cs
var signal = SignalFactory.Default.CreateSignal(2);
var derived = signal.Select(i => i + 1);

int a = derived.Value; // a = 3

signal.Value = 5;
int b = derived.Value; // b = 6
```


2. Derived signal with `Flatten`

```cs
var signal1 = SignalFactory.Default.CreateSignal(1);
var signal2 = SignalFactory.Default.CreateSignal(2);
var selector = SignalFactory.Default.CreateSignal(signal1);
var derived = selector.Flatten();

int a = derived.Value; // a = 1

signal1.Value = 7;
signal2.Value = 42;
int b = derived.Value; // b = 7

selector.Value = signal2;
int c = derived.Value; // c = 42

signal2.Value = 31;
signal1.Value = 1234;
int d = derived.Value; // d = 31
```


3. Derived signals with `AnyChange`

```cs
var signal1 = SignalFactory.Default.CreateSignal<int>(1);
var signal2 = SignalFactory.Default.CreateSignal<int>(2);
var signal3 = SignalFactory.Default.CreateSignal<int>(3);
var derived = new[] { signal1, signal2, signal3 }.AnyChange();

int a = derived.Value; // a = 1 (initially takes value of the first signal)

signal1.Value = 42;
int b = derived.Value; // b = 42

signal3.Value = 4;
int c = derived.Value; // c = 4

signal2.Value = 5;
int d = derived.Value; // d = 5
```
