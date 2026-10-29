# Felinoir

Felinoir lists the films showing in Italian cinemas, their screenings, and lets people plan
group outings to them. All cinemas and users are in Italy.

## Language

**City**:
An Italian city that a **Cinema** is listed under and that a user browses. Every **Cinema**
belongs to exactly one **City**, normally the one it is in, but a cinema just outside may be
listed under the city it serves (the Velletri multiplex is a Rome **Cinema**). Today Rome is
the only **City**.
_Avoid_: Area, market, region, locality (the postal town in a cinema's address is not its **City**)

**Cinema**:
A venue that shows films, located in one **City**.
_Avoid_: Theatre, venue

**Movie**:
A film in the national catalogue, shared by every **City**.
_Avoid_: Film (in code), title

**Screening**:
One showing of a **Movie** at a **Cinema**, at an Italian local time. A cinema never shows the
same movie twice at the same time.
_Avoid_: Showing, showtime, event

## Lobbies

**Lobby**:
A shareable group outing to one **Screening**. It expires 48 hours after its screening starts
and is then treated as if it never existed.
_Avoid_: Event, group, outing, room

**Screening Snapshot**:
The copy of a **Screening**'s film, **Cinema** and time taken when a **Lobby** is created. It
never changes afterwards, even if the screening is rescheduled or leaves the schedule.

**Participant**:
A named entry in a **Lobby**. Names are unique within a lobby, ignoring case. A
**Participant** is either claimed by one **Device** or unclaimed.
_Avoid_: Member, attendee, guest

**Device**:
The browser a person uses, identified by an anonymous token. A **Device** has at most one
**Participant** in each **Lobby**; there are no user accounts.
_Avoid_: User, account

**Claim**:
Making an existing **Participant** this **Device**'s entry, which releases any other
**Participant** the **Device** held in that **Lobby**.

## Felix

**Felix**:
The chat assistant that recommends what to watch in a **City**, answering only from that
city's **Corpus**.
_Avoid_: Bot, AI, chatbot

**Corpus**:
The text description of every **Movie** in programmazione in a **City**, with its upcoming
**Screenings**, that **Felix** answers from.

**In programmazione**:
A **Movie** with at least one **Screening** in the **City** that hasn't started yet.
_Avoid_: Now showing, current, active

**Felix Context**:
The **Corpus** currently serving a **City**, together with the cached copy of it held at the
model provider. Each **City** has at most one.
