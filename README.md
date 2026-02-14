# Parallel You

**Parallel You** is a saga-driven personal operations system. It treats your long-running goals, recurring responsibilities, and creative processes the same way a distributed system handles complex, multi-step workflows.

You get:

* **Threads** for ongoing life domains
* **Sagas** for long-running arcs inside those domains
* **Stories** for bite-sized steps
* **Events** that record every state change

Parallel You gives you a structured way to think, plan, and act -- without forcing artificial methodolgies on top. It's your internal "process engine", made explicit.

Built on a clean event model and real async orchestration (C#), Parallel You uses RabbitMQ for distributed messaging, and an in-memory broker for local use. It's fast, low-ceremony, and brutally honest on what's happening in your life and why.

## Philosphy

I wrote Parallel You to help myself think. As life and code both scale in complexity, the mental load starts to feel like juggling plates on a moving train. Parallel You brings structure to that chaos -- _without_ dictating how you're supposed to work.

At a certain point, raw creativity becomes its own management problem. The more projects you accumulate (and there is always more than one), the harder it becomes to track energy, intent, timing, and momentum. This isn't a glorified todo list. It's a way to track your **actual cognitive bandwidth** -- estimated session length, real energy expenditure, priority, timing, and the events that shape a work arc.

Parallel You stays subtle. It remembers what you're working on, how long you've been at it, how much focus you've burned, and nudges you when you've reached a healthy limit. Not commands -- just signals. A quiet metronome for your inner process.

The secondary purpose of this project is architectural: to demonstrate what it looks like to design a scalable system from zero, evolve the architecture naturally, and eventually distribute it across real messaging infrastructure. Parallel You serves as both a personal tool and a modern example of clear, event-driven design.

## Core Concepts

Parallel You treats your work the way a multi-processor treats computation: as **threads**. Each thread is a unique stream of taks, events, and intentions. The terminology is deliberate -- it pushes you toward a more orderly mental model, the same way a CPU scheduler creates order out of competing workloads. Your time blocks (called **quantums**) aren't rigid cycles, though. They're creative sessions -- focused bursts of attention you allocate intentionally.

Like any processor, your mind can run _hot_. Parallel You tracks your cognitive load using three broad **energy bands**. A deep band means you spent the quantum in full concentration; moderate is sustained, but comfortable focus; light is shallow-effort or administrative work. Seeing upcoming tasks through the lens of energy cost helps you plan more realistically and work more efficiently.

Theads aren't isolated. They fork, merge, block, and wait on each other -- just like real concurrent systems. Parallel You tracks these relationships using explicit **states** and **blockers**. A new thread begins in the _Ready_ state. When you enter a quantum, it transitions to _Running_. When you wrap up, you _yield_ -- recording context, saving momentum, and preparing the way for your next thread.

## How it Works

Parallel You is designed to be simple and unobtrusive. A thought comes to mind -- a project idea, a task, a responsibility -- and you capture it as a **thread**. You give it a rough shape: how much energy it will likely cost, and how long you expect to work on it when its turn comes. Over time, these individual threads naturally gather into **sagas** (long-running arcs), and then into broader **domains**.

Take the Aetheric Forge as an example. It has multiple domains -- the IaC/Architecture work, the Aetheric Press satire engine, and the Baator world-building efforts. Each domain contains its own sagas, and each saga contains stories. Parallel You itself is a saga within the Aetheric Forge domain, while its version releases are individual stories. The structure emerges organically as you work.

Threads inevitably intertwine. Work on Parallel You shapes the tools and patterns used by Aetheric Forge or Baator; some domains can't move forward until a certain architectural milestone is reached. Once that architecture exists, it accelerates every other project -- feeding improvements back into Parallel You on the next cycle. Creativity and refinement reinforce each other instead of competing for headspace.

Using this system has helped turn my high-level intentions into concrete taks that fit within realistic time and energy boundaries. Instead of restricting creativity, it channels it -- and it gives me visibility into my cognitive load so I can rest when needed and maintain momentum without burning out.

## Architectural Overview

Parallel You is an event-driven application built with clean separation of concerns, CQRS-style messaging, a lightweight microservice mindset, and a domain-driven core. The result is an architecture with four major components:

1. **Repository** -- a place to store and update threads. The system supports multiple repository types; an in-memory version (ideal for testing and development) and a MongoDB implementation are included. New repositories can be added without disturbing the domain.
2. **Message Broker** -- the central nervous system of the application. All events flow through the broker, which delivers them to the subscribers that care about each topic.
3. **Message Transport** -- the "wire" the messages travel over. Transports define how events actually move to and from the broker. The system currently supports an in-memory transport and RabbitMQ with room for more.
4. **Domain Model** -- the conceptual map of the system, organized into domains, evetns, and bounded contexts. This forms the foundation for future microservice decomposition.

### Repository

The repository is intentionally minimal: broad primitives like `upsert` and `list` instead of a jungle of specialized methods. The caller is responsible for shaping the returned data into higher-level domain objects. This keeps the repository layer clean, predictable, and easy to replace.

### Message Broker

The broker coordinates communication between components -- for example between the TUI and the underlying storage. It also slects and manages the message transport. Nothing in the applications talks to the transport directly; every event must pass through the broker, keeping the architecture consistent.

### Message Transport

Transports handle the raw delivery of messages: acknowledgements, retries, fan-out, and the mechanics of moving events across boundaries. In-memory transport is perfect for simple scenarios; RabbitMQ gives true distributed messaging for multi-interface or multi-service setups.

### Domain Model

The domain model captures the shape of the real-world problem the application solves. Classes and their attributes form a conceptual map, grouped into domains and subdivided into bounded contexts. The boundaries are the natural seams for future microservices -- each with its own event types and responsibilities.

Further technical detail on each component is available in the [ARCHITECTURE](ARCHITECTURE.md) document.
