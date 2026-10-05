# FT-991A preview profile and emulator

The implemented subset is identification, frequency A/B read/write, MAIN RX mode read/write, and VFO frequency swap. These operations have manufacturer-fixture and independent emulator evidence (`ProtocolTested`), **not physical radio verification**. Other model names remain discoverable; their old feature flags are experimental catalog information. Unsupported FT-991A operations fail before I/O, and binary/placeholder protocols remain disabled.

`YaesuFT991A` implements `IRadioOperations`. `ExecuteAsync(RadioOperationRequest, CancellationToken)` returns outcome, completion evidence and an observation stamped by the sole session owner with time and connection generation. Legacy frequency/mode setters and swap automatically use this path. `ResilientRadio` forwards it once without retries. `RadioModelInfo.Capabilities` reports per-operation evidence separately from the old feature mask.

## Startup and recovery

Startup writes `AI0;`, waits for a 100 ms quiet interval while the one reader drains startup traffic, queries `AI;` and requires `AI0;`, then queries `ID;` and requires `ID0670;`. The overall startup budget is two seconds. Traffic restarts the quiet interval; the deadline prevents endless startup. Readiness requires the complete sequence. This intentionally changes the device's automatic-information setting to OFF and leaves it OFF; no restore command is guessed during shutdown.

The AI command is defined on printed page 4 / PDF index 4 of the [Yaesu reference manual](https://www.yaesu.com/Files/4CB893D7-1018-01AF-FA97E9E9AD48B50C/FT-991A_CAT_OM_ENG_1711-D.pdf). The 100 ms quiet interval is a configurable transaction policy in the session API, not a documented device guarantee. Closing, draining and re-identifying cannot distinguish every arbitrarily delayed untagged response. This preview assumes exclusive control of the CAT connection and reports observations under that limitation.

A timed-out/cancelled ambiguous transaction closes the generation, rejects queued work and never replays it. Call `ReconnectAsync` explicitly for the already-owned byte transport, or reconnect the same serial-port object. Old reader/parser buffers cannot become new observations; a physically delayed untagged reply remains subject to the separation limit above. `ConnectAsync(IByteTransport, string)` transfers lifetime to the same session as the serial adapter; callers must not read/write/open/close the transferred transport.

## Verified operations

Frequency and mode setters issue the documented write, then query and parse actual device state. Success requires the requested postcondition; a mismatch returns `ProtocolError` with the observed state. A lost reply after mutation can return `OutcomeUnknown`. No setter treats silence as evidence of device acceptance.

Swap holds one queue entry and deadline across `FA;`, `FB;`, `SV;`, `FA;`, `FB;`. Other commands cannot interleave. A failure before SV does not imply a swap occurred; a failure after SV can leave its effect unknown. Equal initial frequencies cannot prove an exchange, so SV is sent once and the result remains `OutcomeUnknown` with the observed pair. The implementation does not invent active-VFO selection. Status identifies the active VFO as unknown, carries explicit validity flags for unobserved transmit/power state, preserves those flags through the service projection and displays Unknown. The universal projection omits those fields. Legacy boolean properties are retained for source compatibility and require the validity flags before use.

## Independent emulator

`SharpCAT2.Emulator` depends on Core only. It separately implements documented CAT parsing and radio state; it does not call the production profile, builders or reply parsers. Its in-memory byte transport supports fragmented replies, silence, held/delayed replies, malformed replacements, unsolicited frames and EOF/disconnect. Faults and write barriers let tests coordinate without arbitrary sleeps. State persists across transport generations, while old queued/held output is discarded on reconnect.

The emulator is a development oracle, not a firmware model or USB/serial driver test. It does not establish physical tuning, latency, front-panel interactions or real cable reliability. PTY demo hosting and unattended configuration remain Phase 5; remote message framing remains Phase 4. The independent fixture sources and exact bytes are in [FT991A_PROTOCOL.md](FT991A_PROTOCOL.md).
