# Cobble Modernization and Update Roadmap

> **Status:** Proposed roadmap  
> **Last reviewed:** August 12, 2026  
> **Current repository baseline:** Minecraft Java 1.16.3, protocol 753, .NET 5

## Executive summary

Cobble should evolve from a small 2020 protocol proof of concept into an embeddable, ASP.NET Core-native Minecraft Java server engine:

> **Cobble is an embeddable Minecraft Java server engine configured through ordinary Microsoft dependency injection.**

The distinguishing use case is not another standalone vanilla server. A web application should be able to host Minecraft on a separate Kestrel TCP endpoint, own the game loop when desired, submit commands safely from HTTP or background services, read immutable game snapshots, and react to game events through DI.

The modernization should preserve the interesting part of the original design—hosting a non-HTTP protocol on Kestrel—while replacing almost every protocol and lifecycle assumption in the proof of concept. The recommended foundation is .NET 10 LTS, first-party Kestrel `ConnectionHandler` APIs, and a small Minecraft-specific `System.IO.Pipelines` transport engine. Bedrock.Framework should be removed.

Version-specific data should be generated rather than hand-maintained. A new Minecraft release should trigger an automated draft pull request containing verified Mojang metadata, generated packet ID and registry tables, generated codecs where schemas are known, a compatibility diff, and test results. Automation must distinguish *catalogued* from *actually compatible*: no release should claim Login or Play compatibility until the corresponding vanilla-client scenarios pass.

This is a substantial rewrite, not a retargeting exercise. The first useful milestone is a pinned modern Java version that supports Status, offline and online Login, Configuration, and a flat-world Play vertical slice with a vanilla client. A useful custom-server framework is likely 4–8 engineer-months; broad survival mechanics are a multi-engineer-year effort.

## Goals

### Product goals

1. Provide an embeddable Minecraft Java server engine for ASP.NET Core applications.
2. Use standard Microsoft DI, options, logging, hosting, health checks, and metrics.
3. Let an embedding application own the game loop through an external ticking mode.
4. Keep mutable game state single-owner and safe from concurrent HTTP and network access.
5. Support one explicitly pinned modern Java protocol well before considering broad multi-version support.
6. Automate mechanical version churn and make semantic protocol changes visible and reviewable.
7. Offer a tested vertical slice useful for custom game modes and application-driven worlds.
8. Keep protocol and game internals private until their extension contracts are stable.
9. Prefer deterministic, bounded, fuzz-tested protocol code over premature feature breadth.
10. Remain MIT-licensed and avoid dependencies whose licenses constrain Cobble consumers.

### Engineering goals

- Fragmentation-safe framing over `System.IO.Pipelines`.
- Explicit protocol state and direction in every packet lookup.
- Strict frame, string, collection, NBT, and decompression limits.
- Compression and encryption transitions at exact protocol boundaries.
- Per-connection DI scopes and bounded inbound/outbound work queues.
- Deterministic game-loop tests using `TimeProvider`.
- Checked-in, reproducible protocol inputs with source provenance and hashes.
- Generated packet registrations, registry IDs, block states, and codecs where possible.
- Compatibility reports that explain what changed between supported Minecraft versions.
- Real vanilla-client integration tests in addition to unit and round-trip tests.

## Non-goals

The initial modernization will not include:

- A plugin system.
- Plugin manifests, assembly discovery, dynamic assembly loading, hot unloading, or a plugin ABI.
- Immediate vanilla gameplay parity.
- Snapshot or pre-release support in published stable packages.
- Automatic support claims based only on packet IDs or successful compilation.
- HTTP and Minecraft multiplexed on the same port.
- Mutable world objects exposed directly to HTTP request threads.
- Public extension points for raw packet handlers, codecs, replication internals, or arbitrary state mutation.
- Multiple Java protocol versions in the first production milestone unless version automation proves that support is nearly free.
- Bedrock Edition support; it uses a materially different RakNet/UDP protocol.

## Current-state assessment

Cobble currently contains roughly 584 tracked lines and was developed during October 2020. It targets Minecraft Java 1.16.3, protocol 753, on .NET 5 using Kestrel and an old alpha Bedrock.Framework package from a custom Feedz source.

### What is worth preserving

- Kestrel-based foreign-protocol hosting remains a sound and interesting premise.
- ASP.NET Core co-hosting naturally provides DI, structured logging, options, HTTP APIs, and service lifecycle management.
- The initial separation between connection handling, protocol states, and packet records is small and understandable.
- The repository is MIT-licensed.
- Kestrel exposes `ConnectionContext.Transport` as an `IDuplexPipe`, which remains an appropriate low-level boundary for a Minecraft protocol engine.

### Critical correctness defects

The current protocol implementation cannot be safely extended without first replacing its transport and parsing foundation.

1. **TCP fragmentation is not handled correctly.**
   - An incomplete VarInt may be treated as complete.
   - Primitive readers slice buffers without first proving enough bytes are available.
   - Correct behavior must distinguish incomplete input from malformed input without consuming partial values.

2. **Packet frame boundaries are not enforced.**
   - The declared packet length is read but not used to constrain packet parsing.
   - Unknown fields, unknown packets, or trailing data can desynchronize the entire stream.
   - Every packet parser must receive an exact frame slice and either consume exactly that slice or fail.

3. **UTF-8 byte lengths are calculated incorrectly.**
   - Serialization uses `string.Length` instead of `Encoding.UTF8.GetByteCount`.
   - Non-ASCII usernames, descriptions, and messages can be truncated or corrupt following data.

4. **VarLong parsing is incorrect.**
   - The continuation condition in `ReadVarLong()` is reversed.

5. **Login transitions into an unimplemented Play state.**
   - The next Play packet throws `NotImplementedException`.
   - Login state is changed before Login Success has definitively crossed the outbound state boundary.

6. **Handshake state is insufficiently validated.**
   - Arbitrary enum values are accepted.
   - A peer can request a direct transition into Play.
   - The protocol version is not validated against a supported version set.

7. **Protocol safety limits are absent.**
   - There are no maximum frame, decompressed frame, string, collection, or NBT limits.
   - There is no controlled disconnect policy for malformed or unexpected traffic.

8. **Modern protocol requirements are absent.**
   - No Configuration state.
   - No compression.
   - No encryption or online authentication.
   - No useful Play implementation.

9. **The public packet surface is misleading.**
   - Public packet types exist whose serialization methods throw by design.

10. **Server behavior is hardcoded or unstable.**
    - Status is hardcoded.
    - Login UUIDs are random rather than derived, authenticated, or loaded from player data.

11. **The nominally performance-oriented design allocates excessively.**
    - Packet serialization creates temporary arrays and spans repeatedly.
    - Allocation optimization is secondary to correctness but should be addressed by the replacement codec design.

12. **The dependency and runtime baseline are obsolete.**
    - .NET 5 is end-of-life.
    - The referenced Bedrock.Framework package is an old alpha.
    - The custom Feedz source is an operational and supply-chain risk.

### Repository gaps

- No automated tests.
- No CI.
- No benchmarks.
- No protocol fixtures or fuzzing.
- No substantial documentation beyond a one-line README.
- No supported-version policy.
- No release/update automation.
- No operational health or metrics surface.

### Baseline build status

During the August 2026 architecture review, the legacy build was attempted with:

```sh
dotnet build src/Cobble.sln --configuration Release
```

That review environment did not have a .NET SDK installed, so verification stopped at `dotnet: command not found`. This records the review's evidence rather than a permanent repository condition: it is not evidence that the legacy project restores or builds successfully. Modernization work should establish a pinned .NET 10 SDK and CI before using build status as a migration baseline.

## Target platform and compatibility policy

### .NET baseline

Target .NET 10 LTS, supported through November 14, 2028. Pin the SDK in `global.json` and use repeatable CI images.

### Minecraft baseline

Start with one pinned stable Minecraft Java release. Do not interpret “latest” dynamically at runtime and do not publish snapshot support as stable support.

As an observed planning baseline on August 12, 2026, Mojang's official launcher metadata and the `version.json` embedded in the hash-verified Java 26.2 server JAR reported:

- Protocol version: 776
- Data version: 4903
- Data-pack version: 107.1
- Resource-pack version: 88.0
- Required Java major version: 25

Source: Mojang's `version_manifest_v2.json`, the linked 26.2 version manifest, and server JAR SHA-1 `823e2250d24b3ddac457a60c92a6a941943fcd6a`. These values are time-stamped examples of release metadata, not permanent constants. The checked-in version lock must be authoritative for each implemented Cobble version.

### Compatibility policy

Cobble should separate these concepts:

- **Engine version:** the Cobble package/release.
- **Minecraft display version:** for example `26.2`.
- **Protocol version:** for example `776`.
- **Data version:** for persisted world/data formats.
- **Data-pack and resource-pack versions.**
- **Compatibility level:** what was actually verified.

The server should reject unsupported protocol versions cleanly during Handshake/Login while still returning an informative Status response where the protocol allows it.

## Target project structure

A pragmatic initial solution layout is:

```text
Cobble.Abstractions
  Public commands, events, snapshots, options, IDs, runtime contracts.

Cobble.Protocol
  Framing, primitives, packet codecs, packet registry, NBT,
  compression, encryption, version packs; no Kestrel dependency.

Cobble.Hosting.Kestrel
  ConnectionHandler, endpoint registration, per-connection scopes,
  session lifecycle, reader/writer loops, operational connection metrics.

Cobble.Game
  Runtime, game loop, worlds, players, entities, command processing,
  snapshot publication, replication orchestration.

Cobble.Persistence.Anvil
  Region files, chunks, player data, migration/version checks.

Cobble.Server
  Standalone/reference ASP.NET Core host and end-to-end test target.
```

This can begin with fewer physical projects if build overhead becomes distracting, but dependency direction must remain clear:

```text
Abstractions <- Game <- Server
Abstractions <- Hosting.Kestrel <- Server
Protocol     <- Hosting.Kestrel
Abstractions <- Persistence.Anvil <- Game/Server composition
```

`Cobble.Protocol` should not reference Kestrel, ASP.NET Core hosting, game state, or persistence. Its useful low-level boundary is `IDuplexPipe` plus pure span/sequence codec APIs, allowing a raw-socket adapter later without rewriting the protocol.

### Migration from the current solution

The current solution contains only `Cobble` and `Cobble.Example`. Migrate it deliberately rather than leaving two competing architectures:

| Current project or area | Target disposition |
|---|---|
| `src/Cobble/Cobble.csproj` | Retire after protocol, hosting, and public abstractions have moved into their target projects. It may temporarily reference the new projects as a compatibility facade, but the facade should have an explicit removal milestone. |
| `CobbleConnectionHandler` and `MinecraftOptionsSetup` | Replace with `Cobble.Hosting.Kestrel` endpoint registration, singleton-safe handler, and per-connection scope creation. |
| `CobbleConnection` and `Protocols/*` | Replace with the new protocol-independent connection runner, explicit session state machine, and generated/state-aware codecs. Do not carry forward parser-object replacement as the state model. |
| `Packets.cs` and primitive extensions | Replace with `Cobble.Protocol` primitives, logical packet identities, version registrations, and generated/explicit codecs. |
| `Models/ResponsePayload.cs` | Move the public status model to `Cobble.Abstractions` or keep it internal behind `IServerStatusProvider`, depending on the final API review. |
| `src/Cobble.Example` | Evolve into `Cobble.Server`, the standalone reference ASP.NET Core host. Add a separate minimal embedding sample later only if it demonstrates an API not already clear in the reference host. |
| `src/Cobble.sln` | Retain and update to include the new projects and tests. |

The migration should keep `main` buildable at phase boundaries. Prefer vertical replacement—new framing plus Status, then Login/Configuration, then Play—over moving legacy files into new project names without changing their contracts.

## Transport architecture

### Decision

Keep first-party Kestrel `ConnectionHandler`; remove Bedrock.Framework and implement a small Minecraft-specific pipelines engine directly over `ConnectionContext.Transport.Input` and `.Output`.

ASP.NET Core 10 continues to support non-HTTP TCP endpoints using:

- `ConnectionHandler`
- `UseConnectionHandler<T>()`
- Kestrel connection middleware
- `ConnectionContext.Transport` as `IDuplexPipe`

HTTP and Minecraft should listen on separate ports while sharing the same host and service provider.

### Why not Bedrock.Framework

Bedrock.Framework's ideas remain useful, and its abstractions influenced first-party connection APIs. It is not the best dependency for the modernized implementation:

- NuGet releases remain alpha and historically target obsolete runtimes.
- Repository activity and maintenance are sparse.
- Old protocol-layer issues and pull requests remain open.
- `ProtocolReader` and `ProtocolWriter` are thin wrappers around pipelines; they do not solve Minecraft framing, compression, encryption, or version/state dispatch.
- The current repository depends on an even older build through a custom feed.
- Direct ownership makes consumed/examined semantics, bounds, transition points, and fuzzing easier to test.

### Inbound pipeline

```text
Kestrel PipeReader
  -> optional stateful stream decryption
  -> bounded, complete VarInt-length frame extraction
  -> optional compression envelope parsing and bounded decompression
  -> packet ID parsing
  -> version/state/direction codec lookup
  -> exact payload codec
  -> session dispatcher
  -> bounded command/event handoff
```

### Outbound pipeline

```text
bounded outbound Channel<T>
  -> version/state/direction packet lookup
  -> packet ID + payload serialization
  -> optional compression
  -> outer length prefix
  -> optional stateful stream encryption
  -> Kestrel PipeWriter
```

### Connection invariants

- Exactly one reader loop per connection.
- Exactly one writer loop per connection.
- Exactly one bounded outbound channel per connection.
- No concurrent writes to `PipeWriter`.
- No world mutation from the reader loop.
- No partial VarInt consumption.
- Exact packet-frame slicing.
- Explicit maximum frame and decompressed lengths.
- Explicit protocol state and direction on every dispatch.
- Explicit transition barriers for compression, encryption, and protocol state.
- Controlled disconnects rather than uncaught protocol exceptions.
- Cancellation and completion propagate in both directions.
- The slower side cannot create unbounded memory growth.

### Framing contract

A frame reader should return one of three outcomes:

1. Complete frame available.
2. More data required; consume nothing relevant and set `examined` correctly.
3. Malformed or over-limit frame; disconnect with a categorized protocol error.

After obtaining a complete frame:

- Slice exactly the declared length.
- Parse packet ID from that slice.
- Give the remaining payload slice to the codec.
- Require the codec to consume exactly all expected bytes unless that packet explicitly defines an opaque trailing payload.
- Advance the outer reader only after successful frame ownership is established.

### Bounds

All limits should be configurable within safe hard ceilings. At minimum:

- Maximum encoded frame length.
- Maximum decompressed packet length.
- Maximum compressed payload length and compression ratio.
- Maximum UTF-8 byte length and character length.
- Maximum array/list/map length per field category.
- Maximum NBT depth, tag count, string length, and total bytes.
- Maximum custom payload length.
- Maximum queued outbound bytes/messages.
- Maximum pending commands per connection and globally.

The parser must reject negative lengths, overlong VarInts, non-canonical encodings where required, integer overflow, truncated payloads, invalid enum discriminators, and impossible state/direction combinations.

### Encryption and compression transitions

Minecraft changes transport behavior during Login. These transitions must be modeled as ordered writer/reader control operations, not mutable booleans observed opportunistically.

Examples:

- Encryption begins only after the encryption response is validated and the shared secret is established at the correct byte boundary.
- Compression begins only after Set Compression has been written in the old framing mode; subsequent packets use the compression envelope.
- Login Success and Login Acknowledged must transition into Configuration according to the pinned protocol.
- Finish Configuration must not allow Play packets to race ahead of the state transition.

Represent such changes as writer-queue barriers/control messages so all prior packets are flushed under the old mode and all following packets use the new mode.

## Session state machine

State should not be represented by replacing loosely related parser objects. Use an explicit validated state machine whose transitions are auditable and testable:

```text
Handshake
  -> Status
  -> Login

Login
  -> Encryption negotiation (online mode)
  -> Compression negotiation (optional/configured)
  -> Login success
  -> Configuration

Configuration
  -> registry/feature/tag exchange
  -> finish acknowledgement
  -> Play

Play
  -> Configuration (only if supported by the pinned protocol)
  -> Disconnect
```

Each transition should define:

- Legal triggering packet or server action.
- Required prior conditions.
- Outbound packets that must complete first.
- New accepted packet set.
- Timeout.
- Failure/disconnect behavior.
- Metrics and structured log event.

Unknown packet policy should be state-sensitive. An unknown but correctly framed optional Play packet may be logged and ignored under an explicit policy. An unknown required Login or Configuration packet must fail compatibility and disconnect; it cannot be silently skipped during support certification.

## Dependency injection and application integration

### Public runtime model

The embedding application should interact through stable abstractions rather than packet or mutable world objects:

```csharp
public interface IGameRuntime
{
    ValueTask StartAsync(CancellationToken cancellationToken = default);
    ValueTask TickAsync(CancellationToken cancellationToken = default);
    ValueTask SaveAsync(CancellationToken cancellationToken = default);
    ValueTask StopAsync(CancellationToken cancellationToken = default);
}
```

The final API may add tick context or timing information, but the ownership intent should remain clear.

Core application-facing contracts:

- `IGameRuntime` — start, tick, save, and stop.
- `IGameCommandBus` — submit all external mutations.
- `IGameSnapshotReader` — read immutable published state.
- `IGameEventHandler<TEvent>` — DI-registered application reactions.
- `IGameCommandHandler<TCommand>` — DI-registered domain command behavior where appropriate.

### Tick modes

Support two modes eventually:

1. **Managed mode** — Cobble provides a `BackgroundService` that targets 20 ticks per second.
2. **External mode** — the application calls `IGameRuntime.TickAsync()` from its own loop.

External mode is the primary differentiator and should be implemented first or alongside managed mode. Runtime startup must validate that exactly one tick owner is configured.

Use `TimeProvider`, monotonic time, explicit catch-up policy, and tick-duration metrics. Do not run multiple world ticks concurrently if one tick overruns.

### Concurrency and ownership

```text
Minecraft TCP readers + HTTP endpoints + background services
    -> bounded command queues
    -> single owning game loop
    -> mutable world/player/entity state
    -> immutable snapshots + integration events + outbound replication
```

Rules:

- The game loop owns mutable game state.
- Network readers validate and translate protocol input into commands; they do not mutate worlds.
- HTTP endpoints submit commands through `IGameCommandBus`.
- Snapshot publication uses immutable/read-only models.
- Long-running I/O does not block the tick loop.
- Asynchronous integration event handlers use explicit scopes and do not hold up simulation unless the event contract says they are synchronous.
- Bounded queues expose backpressure, rejection, timeout, and metrics rather than silently growing.

### DI lifetimes

Recommended singleton services:

- Protocol/version registries and generated data.
- Authentication client and server-key provider.
- World manager.
- Player directory.
- Command bus and snapshot publisher.
- Metrics instruments.
- `TimeProvider`.
- Persistence coordinators that are designed to be thread-safe.

Recommended per-connection scoped services:

- Connection runner.
- Session/state machine.
- Packet dispatcher.
- Connection rate limiter.
- Outbound queue and replication context.
- Connection-local authentication state.

`UseConnectionHandler<T>()` does not automatically create a DI scope per accepted connection. The singleton-safe `ConnectionHandler` must create an `AsyncServiceScope` for every connection, resolve scoped services inside it, await the connection runner, and dispose the scope when the connection completes.

Do not capture application scoped services in singletons. Integration-event dispatch that needs scoped application services should create a scope away from the tick-critical path.

### Initial DI extension points

Expose only behavior that an application reasonably needs to customize:

- `IServerStatusProvider`
- `IAuthenticationService`
- `IPlayerDataStore`
- `IChunkStorage`
- `IWorldGenerator`
- `IGameCommandHandler<TCommand>`
- `IGameEventHandler<TEvent>`
- `TimeProvider`
- An access/whitelist policy abstraction

Keep packet codecs, packet handlers, replication internals, entity tracking, and mutable world storage private until use cases demonstrate a stable abstraction.

## Protocol model and code generation

### Packet identity

Numeric packet IDs are not intrinsic to a shared packet record. Register codecs using all relevant dimensions:

```text
Minecraft protocol version
+ protocol state
+ direction
+ packet ID
= codec/packet identity
```

Logical packet identity should be stable and independent of its numeric ID, for example:

```text
minecraft:serverbound_use_item_on
minecraft:clientbound_registry_data
```

Application code should never depend on a numeric packet ID.

### Codec design

Prefer generated or explicit codecs over reflection-based runtime serialization. A codec should expose:

- Exact encoded-length calculation where practical.
- Serialization to `IBufferWriter<byte>` or a purpose-built writer.
- Parsing from an exact `ReadOnlySequence<byte>`/span-backed reader.
- Field-specific bounds.
- Deterministic error categories.
- Version applicability.
- Tests generated from the same schema plus independent fixtures.

Use pooled buffers only after correctness is established. Avoid returning mutable `Span<byte>` backed by newly allocated arrays as a public packet API.

### Normalized protocol intermediate representation

Generated C# should not be the sole source of truth. Define a checked-in, language-neutral or tool-readable Cobble protocol IR from which code, tests, and documentation can be generated.

Conceptually:

```yaml
packet: minecraft:serverbound_move_player_position
state: play
direction: serverbound
versions:
  - protocols: 775..776
    fields:
      - { name: x, type: f64 }
      - { name: y, type: f64 }
      - { name: z, type: f64 }
      - { name: flags, type: u8 }
```

The exact format may be JSON rather than YAML. The important properties are:

- Stable logical packet names.
- Explicit state and direction.
- Explicit version ranges.
- Reusable primitive and composite types.
- Conditional fields and discriminated unions.
- Length/count relationships.
- Per-field limits.
- Provenance for imported facts.
- A controlled override mechanism for source errors or unsupported constructs.

The pipeline is:

```text
Pinned source artifacts
  -> source adapters
  -> normalized Cobble protocol IR
  -> validation and previous-version diff
  -> generated C# codecs/registrations/data
  -> generated tests/docs/compatibility manifest
```

## Automating Minecraft version updates

### Desired outcome

When Mojang releases a new stable Java version, Cobble automation should detect it and open a draft update pull request without claiming compatibility prematurely.

The update should be a review of semantic deltas, not a manual hunt for hundreds of shifted numeric constants.

### Authoritative and supplementary sources

#### Mojang launcher manifest and version metadata

Use Mojang's official launcher metadata to discover releases and obtain:

- Version identity and release type.
- Release timestamp.
- Server/client JAR URLs.
- Mojang-provided hashes and sizes.
- Required Java runtime.
- Mappings when Mojang publishes them.

Never download an unpinned “latest JAR” during normal builds. Resolve it during an explicit update operation, then record immutable URLs and hashes.

#### Server JAR `version.json`

The server JAR provides protocol, world/data, resource-pack, data-pack, Java, and stability metadata. Validate it against launcher metadata and write it into the version lock.

#### Mojang data generator reports

Modern server JARs include `net.minecraft.data.Main`. A version update job can run the bundled data generator, commonly through:

```sh
java \
  -DbundlerMainClass=net.minecraft.data.Main \
  -jar server.jar \
  --reports \
  --output generated
```

Exact arguments can change and must be versioned in the update tool. Generated reports can authoritatively supply much of the mechanical version data, including:

- Packet names and IDs by state and direction through `packets.json`.
- Registry names and numeric IDs through `registries.json`.
- Block and block-state data.
- Command tree data.
- Item component data.
- Tags and vanilla data-pack content.

#### PrismarineJS `minecraft-data`

PrismarineJS provides an MIT, language-independent `protocol.json` and extensive generated game data. Its protocol representation is suitable as an input to a Cobble source adapter and is explicitly intended for generated protocol implementations.

It must remain supplementary rather than the sole trigger/source:

- Third-party updates can lag Mojang releases.
- Protocol field schemas have historically required manual correction and source review.
- Data provenance and the exact upstream commit must be pinned.
- Cobble must preserve reviewed local overrides and test every imported change.

As one observed example on August 12, 2026, `minecraft-data` included Java 26.1 protocol data while Java 26.2 had already been released. Mechanical Mojang reports were therefore available before a complete third-party protocol schema.

#### Version-controlled generated data

The `misode/mcmeta` project demonstrates a useful pattern: run Mojang's generator, keep a commit per version, tag branch-specific outputs, and provide diffs. It is a useful validation/reference source for registries, blocks, commands, assets, and data-pack changes, but it does not replace packet field schemas.

#### Mojang mappings and source analysis

Where mappings are published, pin and use them to compare packet codecs and state transitions. Some newer releases may omit downloadable mappings from launcher metadata; the update workflow must handle that as an explicit reduced-evidence condition, not silently assume old layouts.

Source/decompiler review remains necessary when a packet is new, its field schema changes, a new serializer primitive appears, or runtime semantics change.

### What can be fully automated

- Stable-release detection.
- Metadata/JAR download and hash verification.
- Protocol/data/pack version extraction.
- Mojang report generation.
- Packet ID tables.
- State/direction packet catalogs.
- Registry and block-state tables.
- Added/removed/reordered data diffs.
- Codec generation when the normalized field schema is known.
- Packet factory and registration generation.
- Build, unit, round-trip, fuzz, and integration test execution.
- Draft pull request creation and compatibility report generation.
- Detection of unimplemented or ambiguous schema constructs.

### What cannot safely be assumed or fully automated

Mojang's packet report identifies packet names and numeric IDs but does not necessarily describe every packet's complete wire field layout. Knowing that an ID maps to `minecraft:use_item_on` does not prove the types, order, conditions, limits, or semantics of its fields.

Human review is still needed for:

- New packet field layouts not represented in an available trusted schema.
- Changed field ordering, conditions, encodings, or limits.
- New composite types or registry-dependent encodings.
- New Login/Configuration sequencing.
- Encryption, signing, authentication, or compression changes.
- Chunk, light, item component, metadata, and NBT format changes.
- Gameplay semantics behind new or changed packets.
- Upstream data errors or conflicting sources.

An LLM may assist with source diffs and schema proposals, but generated changes require the same tests and review as human-authored changes.

### Version pack layout

A generated and reviewed version pack could be stored as:

```text
protocol-data/
  java-26.2/
    source.lock.json
    version.json
    packets.json
    registries.json
    blocks.json
    tags.json
    packet-schemas.json
    capabilities.json
```

Large raw source artifacts do not all need to be committed if they are reproducibly downloadable by pinned hash. Commit normalized inputs required for deterministic generation and enough provenance to reproduce them.

A lock file should record at least:

```json
{
  "minecraftVersion": "26.2",
  "protocolVersion": 776,
  "dataVersion": 4903,
  "sources": {
    "serverJar": {
      "url": "https://piston-data.mojang.com/.../server.jar",
      "sha1": "823e2250d24b3ddac457a60c92a6a941943fcd6a"
    },
    "packetIds": {
      "kind": "mojang-generated-report"
    },
    "packetSchemas": {
      "kind": "cobble-reviewed-schema",
      "basedOnProtocol": 775
    }
  }
}
```

The real schema should also pin generator versions, Java runtime, report hashes, upstream commit SHAs, licenses, and local override identifiers.

### New-version workflow

```text
Scheduled/manual release watcher
  -> detect new stable release in Mojang manifest
  -> download and verify metadata and JARs
  -> read version metadata and required Java version
  -> run Mojang data reports
  -> import available supplementary schemas/data by pinned commit
  -> normalize into Cobble IR
  -> compare with current supported version
  -> generate C# codecs, tables, fixtures, and docs
  -> run all validation levels
  -> open a draft update PR with a machine-readable and human-readable report
```

The pull request should summarize:

```text
Minecraft:       26.2 -> 26.3
Protocol:        776 -> 777

Packets:
  Added:         3
  Removed:       1
  ID changed:    141
  Shape changed: 4
  Unknown shape: 1

Registries:
  Added blocks:  12
  Added items:   7
  Reordered IDs: yes

Required flows:
  Status:        PASS
  Offline login: PASS
  Online login:  PASS
  Configuration: FAIL - unknown required packet
  Flat-world join: NOT RUN
```

### Automation safety rules

- Update automation opens a draft PR; it does not push directly to the default branch.
- Generated files contain a source lock/provenance reference.
- Generation is deterministic and culture/time-zone independent.
- CI regenerates output and fails on a dirty diff.
- No unknown schema node is downgraded to raw bytes silently.
- No new protocol version inherits a previous packet schema merely because the packet name stayed the same.
- Schema reuse is allowed only after structural evidence and tests establish equivalence.
- Unknown required Login or Configuration packets block compatibility.
- No package is published as Play-compatible without the pinned vanilla-client scenario passing.
- Upstream source and generated-data licenses are recorded and reviewed.

### Compatibility evidence levels

Do not use one ambiguous `Supports26_3` flag. Track evidence:

| Level | Meaning |
|---|---|
| Detected | Mojang release metadata was downloaded and verified. |
| Catalogued | Packet IDs and registries were generated and validated. |
| Codec-generated | Required wire schemas are available and generated code compiles. |
| Protocol-tested | Primitive, framing, codec, round-trip, and fuzz tests pass. |
| Status-compatible | A vanilla client/status probe completes Status. |
| Login-compatible | Required offline/online Login and Configuration scenarios pass. |
| Play-compatible | A vanilla client joins and completes Cobble's supported Play scenario. |
| Released | The reviewed version pack and matching Cobble package were published. |

Capabilities should also be granular. A version may support Status and Login but not world join; status output and documentation must describe that honestly.

### Expected update latency

- **ID/registry-only release:** draft PR within roughly an hour; same-day support is plausible after tests.
- **Small additive protocol change:** generation handles most work; reviewed schema and behavior changes may take one to several days.
- **Major protocol redesign:** automation identifies and scopes the break, but implementation can take days or weeks.

The promise is rapid, explainable updates—not unsafe zero-day compatibility for every release.

## Authentication and identity

### Offline mode

- Derive deterministic offline UUIDs using the vanilla-compatible algorithm.
- Apply username length and character validation for the pinned protocol.
- Make offline mode an explicit security configuration with warnings.
- Do not conflate transport identity, player profile, and persisted player record.

### Online mode

Implement the complete pinned-version flow:

- Server key management.
- Encryption request/response.
- Verify-token validation.
- Shared secret establishment.
- AES stream encryption at the exact boundary.
- Mojang session-server authentication.
- Profile/skin properties validation and storage.
- Authentication timeout and cancellation.
- Safe HTTP client configuration, retry policy, and metrics.

Expose `IAuthenticationService` for controlled replacement/testing. Never log shared secrets, access tokens, verify tokens, full authentication responses, or sensitive profile data.

### Access policy

A DI-provided access policy should evaluate authenticated identity, remote endpoint, bans, whitelist, capacity, and application rules. Return a structured result that can be translated into an appropriate disconnect reason.

## Game runtime architecture

### Core model

The first runtime should favor a simple single-owner architecture over elaborate ECS or actor frameworks. A 20 TPS loop can own:

- Worlds and loaded chunks.
- Connected/active players.
- Entity IDs and minimal entity state.
- Command processing.
- Scheduled tasks.
- Snapshot publication.
- Replication decisions.

Partitioning worlds or regions across loops can be revisited only after measurement.

### Command flow

External commands should include metadata such as:

- Correlation ID.
- Submission source.
- Optional player/world target.
- Deadline/cancellation intent.
- Completion/result channel where needed.

The bounded command bus should define overload behavior explicitly: reject, time out, or wait up to a configured bound. HTTP callers should receive a clear overload response rather than holding unbounded requests.

### Snapshots

Publish immutable snapshots at a documented cadence. They may include:

- Server/tick status.
- Player summaries.
- World summaries.
- Selected block/entity views.
- Queue and persistence health.

Snapshots are read models, not live object graphs. They should not retain pooled buffers or mutable collections.

### Events

Separate simulation-domain events from asynchronous application integration events:

- Tick-critical events run synchronously under strict rules, if exposed at all.
- Application handlers run outside the critical section, can create DI scopes, and are isolated from one another.
- Slow or failed handlers are measured and follow a retry/dead-letter policy where applicable.
- Event delivery guarantees are documented; initial in-process delivery should not pretend to be durable messaging.

## World, chunks, and persistence

### First world target

Implement a deterministic flat or pregenerated world before general terrain generation. It is sufficient to prove:

- Registry/configuration exchange.
- Join sequence.
- Chunk and light serialization.
- View distance and chunk load/unload.
- Movement.
- Block placement and breaking.
- Persistence across restart.

### Chunk storage

Expose `IChunkStorage` and keep the game-loop representation separate from Anvil serialization. Chunk loading/saving is asynchronous, but completion is applied to mutable world state only by the owning loop.

### Anvil persistence

`Cobble.Persistence.Anvil` should implement:

- Region file headers and sector allocation.
- Compression variants used by the pinned version.
- NBT parsing/writing with strict limits.
- Chunk status/version checks.
- Atomic or crash-aware writes.
- Player data storage.
- Tests against curated vanilla fixtures.

Do not promise arbitrary old-world migration initially. Reject unsupported data versions with a clear error and preserve files.

### Save model

- Track dirty chunks/player data.
- Snapshot or serialize without allowing concurrent mutation to corrupt output.
- Bound save work per tick or perform safe immutable handoff to I/O workers.
- Expose explicit save and shutdown drains.
- Test abrupt cancellation and restart behavior.

## First usable gameplay scope

The initial custom-server framework should prioritize protocol completeness for a narrow vertical slice:

### Status

- Application-provided `IServerStatusProvider`.
- Correct version/protocol advertisement.
- Player counts and optional sample.
- Description component and favicon validation.
- Ping/pong.
- Rate limits and timeouts.

### Login and Configuration

- Offline and online authentication.
- Encryption and compression.
- Login Success/Acknowledged sequencing.
- Known packs, registries, features, and tags required by the pinned client.
- Configuration keepalive and finish sequencing.
- Controlled disconnects.

### Essential Play

- Join game and initial synchronization.
- Keepalive and timeout handling.
- Position/rotation and teleport confirmation.
- Client settings.
- Chunk and light data, view position/distance, load/unload.
- Basic entity/player spawn and removal as required.
- Block placement and breaking.
- Basic inventory slots and selected hotbar item.
- System chat.
- Command tree and a small command set.
- Respawn/disconnect behavior needed by the vertical slice.

### World

- One deterministic flat or pregenerated world.
- Minimal collision/ground rules sufficient for movement validation.
- Persistent chunks and player position/inventory.

### Explicitly deferred gameplay

- Hostile/passive mob AI.
- Redstone parity.
- Fluids and broad block tick behavior.
- Full crafting and recipe parity.
- Complete combat/enchanting/potions.
- Villagers, raids, advancements, dimensions, portals, weather parity.
- Broad world generation and structure generation.
- Full vanilla command set.

## Testing strategy

Tests are a release feature, not cleanup work after protocol implementation.

### Primitive tests

Cover every primitive and boundary:

- VarInt/VarLong min/max, negative values, overlong, incomplete, malformed.
- Signed/unsigned big-endian values.
- UTF-8 ASCII and multi-byte values, invalid input policy, byte/character limits.
- UUID format.
- Position packing.
- Arrays, optionals, enums, bit sets, identifiers.
- NBT depth/size/type limits.

### Framing tests

Feed identical packets using every relevant segmentation pattern:

- One byte at a time.
- VarInt split at each byte.
- Frame body split at each byte.
- Multiple frames in one read.
- Empty read followed by completion.
- Cancellation and EOF at every boundary.
- Malformed length and oversized frame.
- Consumed/examined position assertions.

### Compression and encryption tests

- Below/at/above threshold behavior.
- Incorrect declared decompressed length.
- Zip-bomb/compression-ratio limits.
- Truncated and corrupt zlib streams.
- Encryption split across arbitrary pipe segments.
- Transition packet written under old mode and next packet under new mode.
- Known cryptographic vectors and end-to-end Login captures.

### Codec tests

For every generated packet codec:

- Serialize/parse round trip.
- Exact payload consumption.
- Truncated input at every byte boundary for representative packets.
- Per-field limit violations.
- Known-good independent fixture.
- Previous/current version registration and ID assertions.
- No duplicate `(version, state, direction, ID)` registrations.

Round trips alone are insufficient because a serializer and parser can share the same bug.

### State-machine tests

- Every legal transition.
- Every illegal packet/state combination.
- Timeouts.
- Compression/encryption barriers.
- Unknown-packet policy by state.
- Disconnect and resource cleanup paths.

### Fuzzing

Fuzz at least:

- Outer frame parser.
- Compression envelope.
- Primitive reader.
- NBT parser.
- Generated packet dispatch and representative complex codecs.

Properties include no hangs, no unbounded allocation, no uncaught process-level exceptions, no reads outside the exact frame, and deterministic categorized failure.

### Game-loop tests

Use `TimeProvider` and in-memory stores to test:

- Exactly-once command application.
- Queue ordering and overload policy.
- No concurrent tick execution.
- Tick overrun/catch-up policy.
- Snapshot immutability.
- Save/shutdown drains.
- Player connect/disconnect races.

### Vanilla compatibility tests

Run a pinned vanilla client or protocol-driving harness against `Cobble.Server` in CI where licensing and environment permit. Required scenarios should include:

1. Status and ping.
2. Offline Login through Configuration.
3. Online-mode integration in a controlled environment or a clearly separated credentialed pipeline.
4. Join flat world.
5. Receive chunks and light without disconnect.
6. Keepalive exchange.
7. Move and confirm server position handling.
8. Place and break a block.
9. Receive system chat and execute a basic command.
10. Disconnect and reconnect with persisted state.

Capture server logs and packet traces on failure while redacting sensitive authentication data.

### Persistence tests

- Read curated vanilla Anvil fixtures.
- Write, reopen, and compare semantic chunk data.
- Sector reuse and growth.
- Corrupt/truncated region handling.
- Unsupported data version behavior.
- Crash-interrupted write scenarios where feasible.

### Performance tests

Add benchmarks after correctness tests exist:

- VarInt/string/NBT primitives.
- Frame extraction under common segmentation.
- Representative packet codecs.
- Compression thresholds and payload sizes.
- Chunk serialization.
- Connection queue throughput.

Track allocations and throughput, but do not trade bounds or state correctness for benchmark wins.

## Security and resilience

Treat every connection as hostile.

- Enforce all length/count/depth limits before allocation.
- Use connection and packet rate limiting.
- Time out Handshake, Login, Configuration, idle Play, authentication, and writes.
- Bound channels and queued bytes.
- Categorize disconnect reasons without exposing internal exceptions.
- Avoid logging raw packet contents by default.
- Redact usernames/profile data according to configurable privacy policy.
- Verify downloaded update artifacts by official hashes.
- Pin all generator inputs and package dependencies.
- Review third-party generated-data licenses.
- Run dependency and secret scanning in CI.
- Ensure one malicious connection cannot terminate the host.
- Test cancellation, half-close, reset, slowloris, decompression abuse, and malformed NBT.

## Observability and operations

### Structured logging

Use event IDs and structured fields for:

- Connection accepted/closed.
- Protocol version and state transitions.
- Authentication outcome.
- Disconnect category.
- Queue rejection/backpressure.
- Tick overrun.
- Chunk load/save failures.
- Version-pack identity.

Avoid per-packet info logs in production; use sampled/debug tracing when needed.

### Metrics

Expose OpenTelemetry-compatible instruments for:

- Active and accepted connections.
- Connections by protocol/state/outcome.
- Inbound/outbound bytes and packets.
- Protocol errors by category.
- Authentication duration/outcomes.
- Queue depth, rejection, and wait duration.
- Tick duration, lag, and overruns.
- Loaded chunks/players/entities.
- Chunk load/generation/save duration.
- Compression ratio and decompression rejection.

Avoid unbounded-cardinality labels such as username, UUID, IP address, packet ID strings from untrusted sources, or world coordinates.

### Health checks

Provide separate checks for:

- Host/runtime started.
- Game loop advancing.
- Command queue not persistently saturated.
- Persistence availability.
- Required version pack loaded and valid.
- Optional authentication dependency health without making transient Mojang outages restart the process unnecessarily.

### Graceful shutdown

Shutdown should:

1. Stop accepting new Minecraft sessions.
2. Notify or disconnect active players within a deadline.
3. Stop new external commands.
4. Drain or cancel connection loops.
5. Complete the current tick safely.
6. Save dirty state within the configured deadline.
7. Dispose scopes, cryptographic state, pipes, and stores.
8. Report if the deadline forced an incomplete save.

## Comparable projects and reuse guidance

No active project was found that clearly combines all three of:

1. A substantially complete modern Minecraft Java server.
2. A Kestrel listener.
3. An embeddable Microsoft DI-first application model.

That combination remains Cobble's differentiator.

### ObsidianMC/Obsidian

- Active C# Java server targeting modern .NET.
- Implements substantial gameplay including chunks, movement, inventory/crafting, weather, generation, and fluids.
- Uses raw sockets/`SocketAsyncEventArgs` and some Microsoft DI.
- Lacks full mob AI/redstone parity.
- GPL-3.0: useful as an architectural/protocol reference, but copying or linking code requires careful license analysis incompatible with an uncomplicated MIT strategy.

### CoPokBl/MinecraftDotnet

- MIT and modern .NET.
- Provides packet codecs, compression, encryption, Mojang authentication, and a managed server used for custom game modes.
- Uses `TcpListener`, not Kestrel, and is not centered on the desired Microsoft DI embedding model.
- Its code generator runs Mojang reports and generates packet ID/registry registration, demonstrating that mechanical update work can be automated.
- Packet field implementations and some source URL/version maintenance still require reviewed changes.
- Worth a formal build-vs-adopt spike for protocol components, with API quality, correctness, test depth, and version-update cost measured rather than inferred from feature lists.

### Titlehhhh/McProtoNet

- MIT, modern .NET, `System.IO.Pipelines`, tests, benchmarks, source generation, compression, and encryption infrastructure.
- Primarily protocol/client-oriented; its test server is not a game server engine.
- Strong reference for modern pipelines, primitive serialization, packet factories, and generated plumbing.
- Version-specific packet ranges and field schemas still contain maintained protocol knowledge.
- Worth evaluating as a protocol dependency or source of design patterns, but Cobble should not surrender control of framing/state transitions without compatibility evidence.

### caunt/Void

- Active MIT, modern .NET Minecraft proxy.
- Useful for multi-version handling, custom channels/streams, and proxy networking architecture.
- Does not provide world/gameplay implementation.

### PrismarineJS ecosystem

- `minecraft-data` provides a broad machine-readable protocol and game-data corpus.
- Its automated generator opens version PRs and produces substantial data, but its own process documents manual fixes for breaking Minecraft source changes.
- Best treated as a pinned import/validation source rather than a live runtime dependency.

### General networking alternatives

- **Raw sockets/`TcpListener`:** viable for standalone servers and Generic Host DI, but duplicates Kestrel lifecycle/acceptance and weakens Cobble's defining integration.
- **Kestrel transport internals/`SocketTransportFactory`:** avoid; more fragile and provides no needed advantage over public `ConnectionHandler` APIs.
- **SuperSocket:** credible modern generic socket framework with DI and host integration, but adds session/pipeline abstractions that do not solve Minecraft's dynamic framing.
- **DotNetty:** powerful, but introduces a separate channel/buffer/event-loop model and is a poor fit for an ASP.NET-native component.

### Reuse decision criteria

Before adopting any protocol library/component, prototype and measure:

- License compatibility.
- Support for the chosen current protocol.
- Fragmentation and exact-frame correctness.
- Bounds and malformed-input behavior.
- Compression/encryption transition control.
- Packet schema completeness.
- Ability to operate over `IDuplexPipe` without a competing host lifecycle.
- Generated data provenance.
- Update latency after a new Mojang release.
- Test and fuzz coverage.
- Allocation profile.
- Maintenance activity and bus factor.

## Delivery phases

Phases are ordered to retire the highest risks first. Dates should be set after staffing and the .NET 10 toolchain are available.

### Phase 0: repository and toolchain baseline

**Objective:** make modernization work reproducible.

Deliverables:

- Pin .NET 10 SDK.
- Establish solution-wide build settings, nullable reference types, analyzers, formatting, and warnings policy.
- Add CI for restore, build, test, format/analyzers, and generated-diff checks.
- Remove the custom Feedz source as soon as Bedrock is no longer needed.
- Add architecture decisions and contribution/update documentation.
- Define supported platform/runtime matrix.

Exit criteria:

- Clean clone builds and tests in CI.
- No dependency requires the legacy custom feed.
- Build artifacts and test results are reproducible.

### Phase 1: protocol foundation spike

**Objective:** prove safe direct pipelines transport before broad packet work.

Deliverables:

- `Cobble.Protocol` boundary.
- Safe primitive reader/writer.
- Bounded VarInt frame reader over in-memory `IDuplexPipe`.
- Exact payload slicing and consumption checks.
- Initial fuzz/property tests.
- Error taxonomy.
- Benchmarks for framing/primitives after correctness.

Exit criteria:

- All segmentation and malformed-input tests pass.
- No partial VarInt consumption.
- Limits prevent unbounded allocation.
- The protocol project has no Kestrel dependency.

### Phase 2: Kestrel hosting and Status

**Objective:** restore useful network behavior on the new foundation.

Deliverables:

- Remove Bedrock.Framework.
- `Cobble.Hosting.Kestrel` connection adapter.
- Per-connection `AsyncServiceScope`.
- One reader and writer loop with bounded outbound channel.
- Handshake validation and Status state.
- `IServerStatusProvider`.
- Timeouts, controlled disconnects, metrics, and health check.
- Vanilla status integration test.

Exit criteria:

- Vanilla status/ping succeeds under arbitrary TCP segmentation.
- Unsupported versions are handled cleanly.
- Connection resources/scopes are disposed on all tested paths.

### Phase 3: version data and generator

**Objective:** eliminate hand-maintained protocol IDs and registries before Play expands.

Deliverables:

- Source lock/provenance format.
- Mojang manifest/JAR/report downloader and verifier.
- Normalized version-pack format.
- Packet ID, registry, block-state, and tag generators.
- Initial packet-schema IR and C# codec generator.
- Previous-version compatibility report.
- Deterministic regeneration CI.
- Scheduled/manual draft version-update workflow.

Exit criteria:

- Current target version data is generated reproducibly.
- No numeric packet ID is embedded in application packet types.
- Duplicate/missing registrations fail generation.
- The update tool can catalogue a newly detected release and report unresolved schemas without claiming support.

### Phase 4: Login, encryption, compression, and Configuration

**Objective:** complete the modern pre-Play lifecycle.

Deliverables:

- Offline deterministic identity.
- Online authentication and server key management.
- AES stream encryption.
- Compression envelope and thresholds.
- Ordered transport transition barriers.
- Modern Login and Configuration codecs/state machine.
- Required registry/feature/tag exchange.
- Access policy.
- Authentication and protocol metrics.
- Vanilla offline and online compatibility tests.

Exit criteria:

- Pinned vanilla client completes Login and Configuration.
- Encryption/compression split-boundary tests pass.
- Unknown required Configuration packets block certification.
- Authentication secrets are not logged.

### Phase 5: game runtime and application ownership

**Objective:** establish the embeddable concurrency model before world features.

Deliverables:

- `Cobble.Abstractions` commands, events, snapshots, IDs, and options.
- `IGameRuntime` external ticking.
- Bounded `IGameCommandBus`.
- Immutable `IGameSnapshotReader` models.
- Event dispatch and DI scope rules.
- `TimeProvider`-based deterministic tests.
- Managed ticking mode if it does not delay the external model.
- Example HTTP endpoints that submit commands and read snapshots safely.

Exit criteria:

- HTTP/network threads cannot obtain mutable world objects.
- Queue overload behavior is tested and observable.
- Tick execution is deterministic and never concurrent.

### Phase 6: flat-world Play vertical slice

**Objective:** allow a vanilla client to join and interact with a minimal world.

Deliverables:

- Join and initial Play synchronization.
- Keepalive and timeout.
- Movement and teleport confirmation.
- Chunk/light serialization and view management.
- Flat/pregenerated world.
- Basic player/entity replication.
- Block place/break.
- Minimal inventory/hotbar.
- System chat and simple commands.
- Vanilla end-to-end scenario.

Exit criteria:

- Client joins, receives stable chunks/light, moves, places and breaks a block, chats, and remains connected through keepalives.
- All network input becomes validated commands before world mutation.
- Tick and replication metrics are available.

### Phase 7: persistence and restart

**Objective:** make the vertical slice durable.

Deliverables:

- Bounded NBT implementation or validated dependency.
- Anvil region/chunk storage.
- Player data store.
- Dirty tracking and safe save pipeline.
- Graceful shutdown drain.
- Vanilla fixture and restart tests.

Exit criteria:

- Blocks, player position, and minimal inventory survive restart.
- Unsupported/corrupt data fails safely without destructive rewrite.
- Shutdown reports incomplete saves honestly.

### Phase 8: framework hardening and first supported release

**Objective:** publish a credible custom-server framework.

Deliverables:

- Public API review and compatibility policy.
- Operational docs and complete reference server.
- Load, soak, slow-client, and abuse tests.
- Security review.
- Package/release automation and SBOM.
- Supported-version/capability manifest.
- Upgrade and version-update operator documentation.

Exit criteria:

- All claimed compatibility evidence levels pass in release CI.
- Public abstractions do not expose unstable packet/game internals.
- A sample ASP.NET application owns the loop and safely controls game state.

### Phase 9: selective gameplay expansion

Prioritize features driven by custom-server consumers rather than a blanket vanilla-parity checklist. Candidates:

- Rich inventories and containers.
- Crafting/recipes.
- Combat and health.
- More entity types and metadata.
- Additional world generators.
- Resource packs and custom data packs.
- Scoreboards, teams, boss bars, and richer commands.
- Additional dimensions.

Every feature requires protocol tests, game-loop ownership tests, persistence impact review, and explicit capability documentation.

## Estimated effort

These are order-of-magnitude estimates, not commitments:

- Protocol foundation, Status, and hosting: several weeks.
- Login, encryption, compression, and Configuration: additional weeks.
- Vanilla-client flat-world vertical slice: roughly 3–5 months for an experienced engineer, depending heavily on reuse and test depth.
- Useful custom-server framework: approximately 4–8 engineer-months.
- Broad survival implementation: 1–2+ engineer-years.
- Near-vanilla parity: ongoing team work rather than a finite one-person milestone.

The version automation investment should begin early because every later packet, registry, and block feature otherwise increases update cost.

## Risks and mitigations

### Protocol data is incomplete or late

**Risk:** third-party packet schemas lag a Mojang release or contain errors.  
**Mitigation:** use official reports for IDs/registries, pin all third-party imports, preserve reviewed local IR, compare source/mappings, and block compatibility when required schemas are unresolved.

### Generated code creates false confidence

**Risk:** code compiles and round trips against itself but is wire-incompatible.  
**Mitigation:** independent fixtures, exact-consumption checks, fuzzing, and vanilla-client evidence levels.

### Version abstraction becomes over-engineered

**Risk:** designing for every historical version delays one working modern version.  
**Mitigation:** build a version-aware registry and IR, but publish only one pinned version initially. Add another supported version only after the first vertical slice proves the abstractions.

### Game-loop and web concurrency leaks

**Risk:** DI convenience exposes mutable worlds to arbitrary threads.  
**Mitigation:** public command bus and immutable snapshots only; enforce dependency boundaries and test ownership.

### Kestrel assumptions change

**Risk:** non-HTTP hosting APIs evolve.  
**Mitigation:** isolate Kestrel in `Cobble.Hosting.Kestrel`; keep protocol on `IDuplexPipe`; use only public APIs.

### Persistence corrupts worlds

**Risk:** incomplete Anvil implementation damages user data.  
**Mitigation:** begin read-only against fixtures, use crash-aware writes/backups, reject unsupported versions, and never migrate in place without explicit tooling.

### Scope expands toward vanilla parity

**Risk:** the project becomes permanently unfinished.  
**Mitigation:** define a custom-server vertical slice, capability manifest, and selective feature process. Treat vanilla parity as a separate long-term program.

### Dependency adoption constrains architecture or license

**Risk:** a convenient protocol/server library brings GPL obligations, competing lifecycle, or slow update cadence.  
**Mitigation:** perform a time-boxed evidence-based spike using the reuse criteria above; keep Kestrel and game abstractions independent.

### Update automation becomes a supply-chain path

**Risk:** compromised upstream data or automation writes executable code.  
**Mitigation:** pin hashes/commits, run generation in restricted CI, require review, generate draft PRs only, scan diffs/dependencies, and never execute unreviewed generated binaries in release infrastructure.

## Architecture decisions to record

Create ADRs as implementation begins for at least:

1. .NET 10 and supported OS/runtime policy.
2. Kestrel `ConnectionHandler` plus direct pipelines; removal of Bedrock.Framework.
3. `IDuplexPipe` as the protocol/transport adapter boundary.
4. Explicit session state machine and ordered transport transitions.
5. Per-connection DI scope lifecycle.
6. Single-owner mutable game state and bounded command queues.
7. External versus managed ticking ownership.
8. Version-pack and protocol IR source-of-truth policy.
9. Generated-code provenance and compatibility evidence levels.
10. NBT implementation/dependency choice.
11. Anvil persistence write-safety strategy.
12. Authentication and server-key management.

## Immediate next actions

1. Provision and pin a .NET 10 SDK in local development and CI.
2. Preserve the current proof of concept in Git history; do not attempt piecemeal bug fixes as the long-term architecture.
3. Create the new project boundaries and dependency tests.
4. Remove Bedrock.Framework and the custom Feedz source as part of the first transport milestone.
5. Implement and fuzz safe primitives and bounded framing over in-memory `IDuplexPipe`.
6. Add the Kestrel adapter, per-connection scopes, and bounded reader/writer loops.
7. Restore modern Status with `IServerStatusProvider`.
8. Build the Mojang report/version-pack spike before implementing dozens of packet IDs manually.
9. Time-box a protocol reuse evaluation of MinecraftDotnet and McProtoNet using the stated criteria.
10. Implement Login, encryption, compression, Configuration, and their vanilla compatibility tests.
11. Add external `IGameRuntime.TickAsync()`, command bus, and immutable snapshots.
12. Proceed to the flat-world Play and persistence vertical slice.

## Definition of the first major release

The first modern Cobble release is complete when an ASP.NET Core application can:

- Configure Cobble using ordinary Microsoft DI and options.
- Host HTTP and Minecraft on separate Kestrel endpoints.
- Supply server status, authentication/access policy, player data, chunk storage, and world generation services.
- Own a deterministic 20 TPS game loop through `IGameRuntime.TickAsync()`.
- Submit bounded game commands from HTTP/background work.
- Read immutable player/world snapshots.
- Receive scoped application events without blocking the tick loop.
- Accept the pinned vanilla Java client through Status, Login, Configuration, and the documented Play scenario.
- Support offline and online authentication, encryption, and compression.
- Join a persistent flat/pregenerated world, move, receive chunks/light, place/break blocks, use a basic inventory, chat, and execute basic commands.
- Expose health checks, structured logs, metrics, and graceful shutdown behavior.
- Reproduce all protocol/version data from pinned, verified sources.
- Generate a draft compatibility PR and report when the next stable Minecraft release appears.

The release documentation must state the exact Minecraft display version, protocol version, data version, Cobble capabilities, and compatibility evidence that passed. Anything not tested is not advertised as supported.

## Closing direction

Cobble's opportunity is not to out-implement mature vanilla servers immediately. It is to make Minecraft a well-behaved, embeddable workload inside the modern .NET hosting ecosystem.

The project should therefore optimize for:

- Correct, bounded protocol handling.
- Clear ownership and lifecycle.
- First-class ASP.NET Core and DI integration.
- A narrow but complete game vertical slice.
- Honest compatibility evidence.
- Reproducible, largely automated Minecraft version updates.

If those foundations are established first, gameplay breadth can grow without repeating the proof of concept's framing, state, concurrency, and maintenance problems.
