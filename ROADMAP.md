# Parallel You Roadmap

## Vision

A thought-organizer based on the idea of multi-threading. Opinionated limited hierarchy that fits well with Agile software methodology: Saga -> Story; Sprint will encompass multiple stories as per process. Allow for collaboration by providing basic IAM and RBAC, allow assignments of team members to each level of the organization. Also broadcast events across multiple interfaces.

## Principles

1. **Guardrails, not absolutes** The human mind has predictable capacity, but it still varies from person to person. The process should guide you toward providing healthy and productive boundaries, while still being customizable to you. To this end, a lot of things can be tweaked:
    * the number of parallel processes, 
    * definable energy level (Deep, Medium, Light)
    * quantum per thread 
    * the number of active todo items per thread.

2. **Simplicity First** This is meant to be a tool, not a master. Use it sparingly, but understand the need for it. Pure chaos needs guidance.  But, the chaos should flow freely when it needs to.

3. **Colloboration Focus** Projects work best when contributors collaborate. The interface will allow for multiple clients to receive the same events, so all contributors have realtime views of the threads.

4. **Free Software** This software is Free (as in speech, not beer). It is developed from the principle that knowledge must be preserved for us to progress as a species. To this end, we choose software that embodies the same spirit.

## Tech Stack

1. **Linux** The OG Free system. GNU/Linux is the legendary rebel in a cute tuxedo.
2. **Python** An elegant and adaptable language. From solid OOP, to simple scripts, Python is very productive.
3. **TUI** Textual and Rich provide a simple markup-based approach to developing console-based applications. Rapid prototyping could not be easier.
4. **MongoDB** Thread documents are best stored in a document database to keep structure simple.
5. **RabbitMQ** Fanout event handling easily handled over AMQP.
6. **Secure Websockets** to bring the events to a web UI
7. **WebUI** Based on Vue.js and Typescript, a simple web client to mirror the TUI client will make the collaboration even easier.
8. **Keycloak** Will provide SSO support with OAuth2 and SAML authentication options.

## Roadmap

1. v0.3 - Complete re-architecture, tested backend components: pluggable storage repo, and message broker with pluggable transport
2. v1.0 - Polish and UI complete. Feature parity with v0.2.1 with complete rearchitecture.
3. v1.1 - RabbitMQ instrospection to self-assemble Web API to expose over WSS and HTTPS
4. v1.2 - Multi-user support using PAM backend
5. v1.3 - Keycloak integration via LDAPS
