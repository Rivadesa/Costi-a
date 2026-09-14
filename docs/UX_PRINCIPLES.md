# UX Principles — Sala, Kitchen, Chef

## 1. Primary rule

During live service, frequent actions should require **one interaction whenever possible**.

Backoffice may be information-dense. Live service surfaces must optimize speed, certainty and visibility.

## 2. Operator surfaces

### Waiter / Sala
Questions answered immediately:
- which table?
- which course?
- what state?
- what is the next valid action?
- any critical restriction/alert?

Primary contextual action should dominate visually:
- SEND COURSE;
- MARK SERVED;
- ADD CONSUMPTION.

Exceptional actions belong behind a secondary menu.

### Kitchen station
KDS should show only actionable kitchen information:
- table;
- course;
- preparation;
- quantity;
- guest-specific adaptation;
- critical restriction;
- elapsed time;
- START / READY / INCIDENT.

Do not show price/account/customer marketing information.

### Chef / Pass
The chef/pass surface must answer:
- are all components of this course ready?
- which station is blocking?
- which tables are ahead/behind?
- what restrictions/incidents exist?

### Global service board
Must be understandable at a glance:
- table;
- pax;
- current course / total;
- state;
- elapsed time;
- alerts.

A user should not need to open all tables to understand the room.

## 3. Do not require redundant state taps

Avoid manual actions such as:
`SERVED → EATING → FINISHED`.

Preferred:
- sala taps SERVED;
- system derives `Eating · elapsed` until next course fire.

Only persist a state if it changes operational behavior or provides data worth the interaction cost.

## 4. Restriction visibility

Critical allergy information:
- always includes text/iconography;
- never relies on color alone;
- appears on affected preparation/card;
- remains visible while action is relevant.

## 5. Contextual action pattern

Example table view:

```text
Course 5 served · 08:42
[ SEND COURSE 6 ]
```

When kitchen is working:

```text
Course 6 preparing · 03:11
(no destructive primary action)
```

When ready:

```text
Course 6 READY
[ MARK SERVED ]
```

## 6. Exceptions vs routine

Routine actions remain visible.

Secondary/exception menu may include:
- pause rhythm;
- change table;
- change pax;
- skip course;
- add extra course;
- substitute preparation;
- add note;
- cancel service (permission restricted).

## 7. Consumption entry

Optimize for recurring items:
- frequent products;
- recent items on this table;
- +1 shortcuts;
- category shortcuts;
- search only when needed.

Do not force waiter through complete catalogue navigation for every water/coffee repeat.

## 8. Confirmation dialogs

Do not confirm every normal action. Confirmation is appropriate when:
- action is difficult to reverse;
- financial/destructive consequence is significant;
- user is entering exceptional path;
- allergy/safety policy requires explicit acknowledgement.

Excess confirmations destroy service speed and train users to ignore them.

## 9. Touch targets

Live service UI must assume:
- touch screens;
- hurried interaction;
- possible gloves/wet hands in kitchen;
- variable display sizes.

Use large targets and avoid tiny icon-only controls for critical actions.

## 10. Realtime uncertainty

UI must distinguish:
- command accepted;
- command pending/retrying;
- server disconnected;
- Internet/cloud disconnected.

Never show an optimistic READY/FIRED state as final if local server did not acknowledge it.

## 11. Offline/cloud indicators

Users should see simple infrastructure status without technical jargon:
- Local server: Connected / Disconnected;
- Internet: Online / Offline;
- Cloud sync: Synced / Pending / Error.

Internet offline is not a service-critical red alert when local operation is healthy.

## 12. Product validation

UX is considered wrong if real staff repeatedly bypass it through verbal coordination because the system requires too many steps.

Observe real services and remove interactions before adding analytics-only states.
