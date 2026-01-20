# Copilot Instructions

## General Guidelines
- Maintain a clear separation of concerns in your code.
- Keep UI and logic together for better maintainability.

## Code Style
- Use specific formatting rules.
- Follow naming conventions.

## Project-Specific Rules
- Move `CreateServerWizard` logic from the code-behind class into the `.razor` component to keep UI and logic together for this session.
- Use `Server.Settings` to store list-like data as newline-separated strings under specific keys such as "OPS" or "WHITELIST". `GameServer.Lists` and `GameTypeDefinition.DefaultLists` have been removed; `StepGameSettings` must not reference these lists and should use `Server.Settings` only.
- The ports list is fixed in the UI: do not allow adding or removing port mappings. When a `PortMapping`'s `PublishedPort` is 0, it should default to the `ContainerPort` value.
- The memory limit is fixed in the UI: do not allow adding or removing memory mappings. When a `MemoryMapping`'s `PublishedMemory` is 0, it should default to the `ContainerMemory` value.