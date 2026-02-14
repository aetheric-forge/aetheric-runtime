v1.0.0 - Re-architect for development simplicity

### High-Level Summary
Primary goals of this release:
1.  **Re-establish architectural clarity**: Documenting the "why" and "how" of the system to help developers (including the original author) re-onboard.
2.  **Improve test robustness and readability**: Moving away from ad-hoc test setups to a structured "Test Matrix" and "Test Models" approach.
3.  **Strengthen Abstractions**: Decoupling services from specific transport implementations by moving towards the `IBroker` interface.
4.  **Expand the system**: Introducing new services (LogService) and refining existing ones.

---

### Key Changes Breakdown

#### 1. Architectural Documentation (`ARCHITECTURE.md`)
The `ARCHITECTURE.md` file received a major overhaul. It now explicitly defines:
*   **Philosophy**: "Message-shaped reality" (loose coupling via messages) and "Intentional minimalism."
*   **Concepts**: Explains the **Envelope Protocol**, **Command/Event Model**, and **Dynamic API Generation** (where FastAPI routes are derived from RabbitMQ bindings).
*   **Nervous System Analogy**: Describes the system as nodes and packets, similar to biological nervous systems.

#### 2. Test Infrastructure Refactoring
*   **`TestMatrix.cs`**: Introduced a centralized way to run the same test suite against different backends (e.g., testing Repos against both `InMemory` and `Mongo` if a connection string is available).
*   **`TestModels.cs`**: A new helper class that provides factory methods for domain objects (`Domain`, `Saga`, `Story`) and their corresponding messages, ensuring consistent test data across the project.
*   **`MessageTests.cs`**: Added dedicated tests for the base `Message` class, specifically verifying how routing keys (e.g., `domain.created`) are automatically derived from class names (e.g., `DomainCreated`).
*   **Cleaned up `RepoTests.cs`**: Refactored to be backend-agnostic by using the `TestMatrix` cases.

#### 3. Service and Logic Changes
*   **`ParallelYou.LogService`**: A brand new microservice was added to handle logging commands/events, demonstrating how the system can be extended by simply adding a new consumer to the bus.
*   **`ThreadService` Refinement**: The `ThreadCommandWorker` was refactored to use the `IBroker` abstraction instead of interacting with the transport layer directly. This aligns with the "Service Pattern" described in the updated documentation.
*   **Decoupling**: The commit removed several direct dependencies and hardcoded logic in favor of the message-driven approach.

#### 4. Project Infrastructure
*   **Solution/Projects**: Updated `ParallelYou.sln` to include the new `LogService`.
*   **Dependencies**: Synchronized versions and references across `.csproj` files.
*   **Cleanup**: Removed unused files like `global.json` and consolidated redundant classes (e.g., moving `Envelope.cs` to shared namespaces).
 