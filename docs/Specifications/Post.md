# Article III — Post

## Definition

Post is the conveyance of Messages between Participants within the Runtime. It exists independently of the mechanisms by which Messages are represented, transported, or delivered.

## Principles

- Post exists independently of any transport technology.
- Messages are conveyed according to their semantic intent rather than their physical destination.
- Post preserves the independence of Producers and Consumers.
- The Runtime may employ one or more Transports to realize Post.
- Delivery infrastructure is an implementation concern.

## Relationships

Post may:

- convey Messages;
- communicate Knowledge;
- originate from or be received by an Identity;
- originate from or be received by an Institution;
- transport Commands, Events, and Requests;
- employ one or more Transports;
- utilize Serialization;
- establish Correlation between related Messages;
- participate within the Runtime.

## Canons

- Messages SHALL remain independent of transport implementation.
- Producers SHALL NOT depend upon the identity or existence of Consumers.
- Consumers SHALL depend only upon the Message Contract.
- The semantic meaning of a Message SHALL remain invariant regardless of transport.
- Transport-specific concerns SHALL NOT appear within Message Contracts.
- A Runtime MAY substitute one Transport for another without altering application behavior.