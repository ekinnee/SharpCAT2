# SharpCAT2 TCP protocol version 1

Status: Phase 1 envelope contracts and specification. The active server/client still use the old protocol; Phase 4 will implement framing, connection state and dispatch. The pure DTO validation added here does not open sockets or execute commands.

## Framing and negotiation

One UTF-8 JSON object is terminated by LF. CRLF is accepted; embedded line breaks in strings must be escaped. The maximum encoded frame is 65,536 bytes including the terminator. The decoder must reject malformed UTF-8, incomplete/oversized frames, missing required fields, duplicate JSON properties, unknown envelope members and unsupported versions. Phase 4 must set bounded partial-frame and hello deadlines; no socket read boundary is a message boundary.

The first request is hello. Requests use `v:1`, `type:request`, a canonical positive UInt64 decimal `id` string, `op`, and an object `args`. Optional `deadlineMs` is 1 through 5,000, measured from server admission and including queue time. Local configuration may tighten that maximum. Hello identifies the client using `args.clientName`; dispatch validates its nonempty value and limits it to 128 characters. Successful hello returns supported version and server capabilities in a response value. Only then may operations be admitted. A second hello is rejected.

Request IDs increase strictly for the connection's lifetime. A rejected request does not advance the last admitted ID. IDs cannot be reused after completion, cancellation or abandonment, and numeric exhaustion requires a new connection. A client pending-call key includes its connection generation. Responses without an active matching key are ignored; bounded abandoned-ID history must never enable reuse. Request IDs provide correlation, not durable deduplication or exactly-once execution.

## Operation arguments

| Operation | Arguments | Dispatch meaning |
| --- | --- | --- |
| hello | clientName string | Negotiate once before radio operations |
| listModels, listPorts | empty object | Return one array value with capabilities where applicable |
| getConnection, getStatus | empty object | Return connection state or supported observed fields with validity |
| getFrequency | vfo A or B | Query that VFO |
| setFrequency | vfo A or B, hz positive integer | Model validates range; atomic write/read-back |
| getMode | empty object | Query the main receiver mode |
| setMode | mode nonempty string | Model validates names; atomic write/read-back |
| swapVfo | empty object | Non-replayable VFO swap; profile defines available evidence |
| reconnect | empty object | Explicit session recovery; affects shared radio, not a private client port |
| cancel | targetId string | Request cancellation of an earlier request on this connection |

Envelope validation is separate from argument validation in the operation handler. Do not convert invalid input to a default frequency, receiver, mode or profile. Model selection is startup configuration for this preview. Transmit/PTT and arbitrary raw commands are absent.

## Response and event shapes

```json
{"v":1,"type":"request","id":"42","op":"getFrequency","args":{"vfo":"A"}}
{"v":1,"type":"response","id":"42","outcome":"Succeeded","evidence":"ReplyReceived","value":{"hz":14250000}}
{"v":1,"type":"event","event":"connection","value":{"state":"Recovering"}}
```

A response terminates exactly one request. Outcome/evidence are case-sensitive enum names; integer enum values are rejected by the contract serializer. Outcome and evidence have the meanings in PHASE1_CONTRACTS.md. A `value` is optional and operation-specific; lists contain an array rather than multiple START/END text fragments. `diagnostic` is optional and must not expose credentials. Notifications have no request ID and cannot complete a pending call. Control operations such as hello/cancel may succeed with NotSent evidence because they do not write to a radio; radio operation results enforce their separate completion invariants.

Phase 4 will use one reader and one bounded output writer per connection, with a maximum of 16 admitted outstanding operations and the session-wide budget of 64. Slow consumers are disconnected when their output budget is exhausted. A response cannot be interleaved with a broadcast at the byte level. Resource and operation argument errors use a correlated response when an ID is valid; invalid protocol envelopes can close the connection after a bounded diagnostic. Never allocate based solely on a client-supplied length/value.

## Cancellation and disconnect

Cancel has its own increasing ID and targets an earlier ID on the same connection. Its success response value contains `status` Requested, AlreadyCompleted or Unknown; acknowledgment does not establish the target's terminal outcome. Completion can win a cancellation race. Queued work removed before writing returns Cancelled/NotSent on the target ID. Cancellation after a write preserves transaction ownership until reply consumption or recovery; an unconfirmed mutation returns OutcomeUnknown with its available write evidence.

The receive loop must continue admitting bounded control messages while radio requests run. Cancel bypasses the radio queue, not the connection's resource policy. Client disconnect cancels queued work; written operations follow the same consumption/recovery rules. A local client timeout is abandonment, and the client may send a best-effort cancel but must not automatically replay the mutation.

## Compatibility and migration

Updated client-library methods will automatically map supported legacy calls into typed operations and these envelopes. There is one radio execution owner. An old compiled TCP client must upgrade its software because the server cannot repair its single-read response parsing. New clients fail negotiation with an old server explicitly rather than fall back to raw writes. Old plain-text requests receive a bounded incompatibility message and disconnect. There is no simultaneous legacy execution stack in this preview.

Release version selection, framing implementation, actual cancellation races and connection-generation routing are future phase gates, not claims proven by the Phase 1 DTO tests.
