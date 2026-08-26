# Feature Specification: Durable storage for customers and subscriptions

**Feature Branch**: `alexeytopchilko/ale-9-persist-data-in-a-real-database-instead-of-in-memory`

**Created**: 2026-08-26

**Status**: Draft

**Input**: Tracked as ALE-9. Customers and their subscriptions must survive an application restart, with no observable change to the API contract other than one ordering change declared in this document.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Work is not lost when the application restarts (Priority: P1)

An account manager records a new customer and the subscription they just signed. Later that day the application is restarted — a deployment, a crash, a laptop reboot. When they come back, the customer and the subscription are exactly as they left them.

Today this is not true. Every restart discards everything and the application returns to the same three demonstration records. Anything entered since the last start is gone, silently and irrecoverably.

**Why this priority**: This is the entire feature. Without it the product cannot be used for anything beyond a single uninterrupted session, which makes every other capability decorative. Nothing else in this specification delivers value on its own.

**Independent Test**: Record a customer and a subscription, stop the application, start it again, and read them back. Fully testable on its own; delivers the whole of the feature's value.

**Acceptance Scenarios**:

1. **Given** a running application, **When** a customer is created and the application is then stopped and started again, **Then** that customer is still present with every field identical to what was recorded.
2. **Given** a customer with a subscription, **When** the application is restarted, **Then** the subscription is still attached to that same customer, with its plan, price, billing cycle, dates, status and notes unchanged.
3. **Given** a customer that was deleted before a restart, **When** the application is restarted, **Then** that customer is still absent — deletions persist as reliably as creations.
4. **Given** a subscription whose status was changed before a restart, **When** the application is restarted, **Then** the status reads as the value it was changed to, not the value it was created with.
5. **Given** an application starting for the very first time against empty storage, **When** it finishes starting, **Then** it is immediately usable and its state is well-defined rather than partially initialised.

---

### User Story 2 - Existing clients keep working, unmodified (Priority: P2)

The web interface, and anything else already speaking to this API, continues to work without being touched. No request has to be reshaped, no response field disappears or changes type, no status code moves.

**Why this priority**: P2 rather than P1 because it delivers no new capability — but it is the constraint that makes P1 safe to ship. A durable store reached by breaking every existing caller is not an improvement. It ranks immediately below P1 because a contract break here is silent: the application starts, requests return success, and the interface renders the wrong thing.

**Independent Test**: Exercise every endpoint against the new implementation and compare each status code and response body against the recorded reference of current behaviour. Testable without reference to persistence at all.

**Acceptance Scenarios**:

1. **Given** the recorded reference of current API behaviour, **When** every endpoint is exercised against the new implementation, **Then** each status code and response shape matches it, except for the single ordering change declared in this specification.
2. **Given** a monetary amount recorded as `4900`, **When** it is read back, **Then** it is still the number `4900` and has not acquired a decimal fraction or become a string.
3. **Given** a subscription status, **When** it is read back, **Then** it is the status *name* as text, never a numeric code.
4. **Given** an optional field left unset, **When** the record is read back, **Then** the field is present with an empty value rather than omitted from the response.
5. **Given** a billing cycle submitted in upper case, **When** the record is read back, **Then** the stored and returned value is lower case, as before.
6. **Given** each way a request can be rejected — a missing required field, an amount below zero, an unrecognised billing cycle, an end date not later than its start date, an email already belonging to another customer — **When** that request is made, **Then** it is rejected with the same outcome as before, and a duplicate email in particular is still reported as a conflict rather than as a malformed request or an internal failure.
7. **Given** the web interface, **When** it is used to browse customers, add a subscription and change a status, **Then** every flow works with no change to the interface's code.

---

### User Story 3 - Collections come back in a predictable order (Priority: P3)

Someone opening a customer sees that customer's subscriptions in a stable, sensible order, and the order does not shuffle between page loads or between application restarts.

**Why this priority**: P3 because no user is currently blocked by it, but it must be decided *here* rather than discovered later. The present order is an accident of how records happen to be held in memory; once records are stored and queried, an order that is not explicitly requested is not guaranteed at all. Leaving it unstated would turn a design decision into an unexplained difference found during verification.

**Independent Test**: Read a customer's subscriptions repeatedly, across restarts, and confirm the order is identical every time and matches the declared rule.

**Acceptance Scenarios**:

1. **Given** a customer with several subscriptions, **When** that customer is read repeatedly, **Then** the subscriptions appear in the same order every time.
2. **Given** several customers, **When** the customer list is read, **Then** they appear oldest-created first.
3. **Given** a customer with several subscriptions, **When** the customer is read, **Then** the subscriptions appear oldest-created first.
4. **Given** two records created at the very same instant, **When** the collection is read, **Then** the order between them is still identical on every read rather than arbitrary.

---

### Edge Cases

- **Storage unreachable at startup.** The application must not appear healthy while being unable to serve or retain anything. Failing to start visibly is preferable to starting into a state where every request fails for reasons a caller cannot interpret.
- **Storage becomes unreachable while running.** A request that cannot be completed must be reported as a server-side failure with no detail leaked about the storage layer — the existing behaviour for unexpected failures. It must not be reported as a validation error, which would tell the caller their input was at fault when it was not.
- **A write that is interrupted part-way.** A customer and its subscriptions form one unit. A subscription must never become durable in a state its parent customer does not reflect; after any interruption, a reader sees either all of a change or none of it.
- **Two changes to the same customer at once.** Simultaneous modifications must not silently discard one another, and must not leave the customer's subscription list internally inconsistent — for example holding a subscription twice, or losing one that was added.
- **Deleting a customer that still has subscriptions.** Those subscriptions must not survive their parent. A subscription reachable only through a customer that no longer exists is unreachable by definition, and must not linger in storage.
- **Very first start against empty storage.** Whether demonstration records are present must be a stated outcome, not an accident of which code path ran first.
- **Restart with no changes made.** Restarting must not alter anything by itself. Reading the same collection before and after an idle restart returns identical content in an identical order.

## Requirements *(mandatory)*

### Functional Requirements

**Durability**

- **FR-001**: The system MUST retain customers and their subscriptions across a complete stop and start of the application, including creations, updates, status changes and deletions.
- **FR-002**: The system MUST treat a customer together with its subscriptions as a single unit of change: after any failure or interruption, a reader MUST see either the whole of a change or none of it.
- **FR-003**: The system MUST NOT retain a subscription that is not reachable through an existing customer. Deleting a customer MUST remove its subscriptions.
- **FR-004**: The system MUST reach a well-defined, immediately usable state when started against empty storage, and MUST state whether demonstration records are present in that state.
- **FR-005**: The system MUST fail visibly at startup if its storage cannot be reached, rather than starting into a state where requests fail for uninterpretable reasons.
- **FR-006**: The system MUST report a request it cannot complete for storage reasons as a server-side failure, disclosing nothing about the storage layer, and MUST NOT report it as a caller input error.

**Contract preservation**

- **FR-007**: The system MUST keep every endpoint's status code identical to the recorded reference of current behaviour.
- **FR-008**: The system MUST keep every response's field names, field presence and value types identical to that reference. An optional field with no value MUST remain present and empty rather than being omitted.
- **FR-009**: The system MUST return monetary amounts with the same numeric representation as before, without introducing a fractional part or a change of type.
- **FR-010**: The system MUST return subscription status as its name in text form, never as a numeric code.
- **FR-011**: The system MUST continue to normalise billing cycle to lower case on write, and MUST continue to accept only the two recognised cycles.
- **FR-012**: The system MUST continue to derive a new subscription's status from its start date — not yet started if the start date is in the future, otherwise active — and MUST continue to ignore any status supplied by the caller on creation.
- **FR-013**: The system MUST continue to leave a subscription's status untouched when the subscription is updated as a whole. Status MUST remain changeable only through the dedicated status operation.
- **FR-014**: The system MUST continue to enforce the status transitions exactly as they stand: from not-yet-started to active or cancelled; from active to paused or cancelled; from paused to active or cancelled; cancelled permits no onward transition; and a transition to the status already held MUST continue to succeed as a no-op, including from cancelled to cancelled.
- **FR-015**: The system MUST continue to reject the same requests with the same outcomes: a customer without a name or without an email, a subscription without a plan, an amount below zero, an unrecognised billing cycle, and an end date not strictly later than its start date. A customer email already in use MUST continue to be reported as a conflict, distinctly from a malformed request and from an internal failure.
- **FR-016**: The system MUST continue to report expected failures as outcomes decided by the application rather than as unexpected errors, so that callers keep receiving the same specific codes and messages rather than generic ones.
- **FR-017**: The web interface MUST continue to work with no modification.

**Access path**

- **FR-018**: A subscription MUST remain unreachable without first resolving the customer that owns it. No means of retrieving or modifying a subscription without identifying its customer may be introduced, regardless of how records are stored.

**Ordering**

- **FR-019**: Collection order MUST be part of the contract and MUST be deterministic. Reading the same collection twice, with or without a restart in between, MUST produce the same order.
- **FR-020**: Customers MUST be returned oldest-created first. Subscriptions within a customer MUST be returned oldest-created first.
- **FR-021**: Where two records share the same creation instant, a further stable tiebreaker MUST be applied so the order is still identical on every read.

### Declared contract change — the one permitted difference

FR-020 changes one observable thing, and it is declared here so that verification treats it as intended rather than as a regression.

One demonstration customer currently holds two subscriptions in an order that does **not** match their creation times: the later-created subscription is listed first. That order reflects the sequence in which the demonstration records were appended in memory, nothing more. Under FR-020 those two subscriptions swap places.

- **Expected**: this specific pair swapping, visible in the three reference cases that read customer collections.
- **Not expected**: any other difference anywhere in the reference. Every other difference is a regression and MUST be fixed rather than explained.

The recorded reference MUST be regenerated as part of this feature, and its own change MUST contain only this swap.

Rationale for choosing a stated order over reproducing the current one: the present order is an artifact, not a decision. Reproducing it exactly would require storing a sequence number whose only purpose is to preserve the order in which two demonstration records happened to be appended — enshrining an accident as a contract. Declaring a meaningful order costs one pre-announced difference and leaves the contract explainable.

### Key Entities

- **Customer**: A person or organisation that holds subscriptions. Identified uniquely, and additionally distinguished by an email address that no other customer may share. Carries a name, an email, an optional company, an optional phone number, and the times at which it was created and last changed. Owns its subscriptions: they exist only in its context and do not outlive it.
- **Subscription**: A commercial arrangement belonging to exactly one customer. Carries a plan name, a monetary amount, a billing cycle, a start date, an optional end date that must fall strictly after the start, optional notes, a status drawn from a fixed set governed by the transition rules in FR-014, and the times at which it was created and last changed. Reachable only through its owning customer.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Records entered before an application restart are 100% present and unchanged after it — zero loss across creations, updates, status changes and deletions.
- **SC-002**: Of the 29 recorded behaviour cases, 28 match exactly, and the 29th differs only by the pre-declared ordering swap. No other difference is present.
- **SC-003**: The web interface completes every flow it completes today — browsing customers, adding a subscription, changing a status, deleting — with zero changes to its own code.
- **SC-004**: Reading any collection ten times in succession, with a restart in the middle, yields the same order all ten times.
- **SC-005**: A first start against empty storage reaches a usable state with no manual intervention, and a subsequent start against populated storage neither duplicates nor discards anything.
- **SC-006**: When storage is unreachable, an operator can tell that from the application's own startup behaviour without inspecting the storage layer directly.

## Assumptions

- **Scale is small.** Tens to low thousands of customers. No requirement is stated here that would only matter at high volume, and no performance target is set beyond the existing user-facing expectation that reads feel immediate.
- **A single instance.** No requirement for several application instances sharing one store concurrently. FR-019's determinism and the concurrency edge case are about correctness within one instance, not about distributed coordination.
- **Nothing to migrate.** The application is not deployed anywhere and holds no data anyone relies on. There is no existing data to carry across, no downtime to plan, and nothing to roll back — which is what makes this feature low-risk despite touching every read and write path.
- **Demonstration records stay.** The three demonstration customers exist so the interface is not empty on a fresh start and so the recorded behaviour reference has something to read. This specification assumes they remain available on a fresh start; how that is arranged is an implementation matter.
- **Timestamps are already recorded.** Creation and modification times exist on both entities today, so FR-020 needs no new field.
- **The recorded reference is trustworthy.** `docs/api-baseline.md` was verified to reproduce identically across repeated runs and across a restart before being relied upon here. Its regeneration is part of this feature.
- **One known defect is deliberately left alone.** Not-found responses currently come in two different shapes, tracked separately. Correcting it inside this feature would mix an intended contract change into the very comparison that proves the contract did not change, so FR-007 and FR-008 preserve the inconsistency as it stands.
