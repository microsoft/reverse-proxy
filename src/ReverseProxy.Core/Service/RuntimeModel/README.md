# IslandGateway.RuntimeModel namespace

Classes in this folder define the internal representation
of IslandGateway's runtime state used in perf-critical code paths.

All classes should be immutable, and all members and members of members
MUST be either:

   A) immutable
   B) `AtomicHolder<T>` wrapping an immutable type `T`.
   C) Thread-safe (e.g. `AtomicCounter`)

This ensures we can easily handle hot-swappable configurations
without explicit synchronization overhead across threads,
and each thread can operate safely with up-to-date yet consistent information
(always the latest and consistent snapshot available when processing of a request starts).

## Class naming conventions

* Classes named `*Info` (`RouteInfo`, `BackendInfo`, `EndpointInfo`)
  represent the 3 primary abstractions in Island Gateway (Routes, Backends and Endpoints);

* Classes named `*Config` (`RouteConfig`, `BackendConfig`, `EndpointConfig`)
  represent portions of the 3 abstractions that only change in reaction to 
  Island Gateway config changes.
  For example, when the health check interval for a backend is updated,
  a new instance of `BackendConfig` is created with the new values,
  and the corresponding `AtomicHolder` in `BackendInfo` is updated to point at the new instance;

* Classes named `*DynamicState` (`BackendDynamicState`, `EndpointDynamicState`)
  represent portions of the 3 abstractions that change in reaction to
  Island Gateway's runtime state.
  For example, when new endpoints are discovered for a backend,
  a new instance of `BackendDynamicState` is created with the new values,
  and the corresponding `AtomicHolder` in `BackendInfo` is updated to point at the new instance;
