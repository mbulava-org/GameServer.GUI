# Authentication & Authorization Guide

This guide covers the authentication and role-based access control (RBAC) system in **GameServer.Docker**, including JWT configuration, role hierarchies, group boundaries, password access controls, and admin management interfaces.

---

## 📋 Table of Contents

1. [Overview](#overview)
2. [Configuration & Settings](#configuration--settings)
3. [Default Admin Account](#default-admin-account)
4. [Roles & Permissions](#roles--permissions)
5. [Group-Based Server Access Boundaries](#group-based-server-access-boundaries)
6. [Server Creator Auditing & Permissions](#server-creator-auditing--permissions)
7. [Password Access Scoping](#password-access-scoping)
8. [Admin Management Screens](#admin-management-screens)
9. [API Endpoints](#api-endpoints)
10. [SignalR Hub Authentication](#signalr-hub-authentication)

---

## Overview

GameServer.Docker provides a centralized authentication and authorization model:
- **Backend API (`GameServer.API`)**: Uses ASP.NET Core JWT Bearer authentication to issue and validate signed tokens.
- **Frontend Web UI (`GameServer.Web`)**: Uses Blazor Server `JwtAuthenticationStateProvider` to manage user sessions, inject `Authorization: Bearer <token>` headers on API calls via `AuthTokenHandler`, and adapt navigation based on user roles.

---

## Configuration & Settings

JWT settings are configured in `appsettings.json` or through environment variables.

### `appsettings.json` Configuration

```json
{
  "Jwt": {
    "SecretKey": "GameServer-Default-Super-Secret-Key-Change-In-Production-2026!",
    "Issuer": "GameServer.API",
    "Audience": "GameServer.Web",
    "ExpirationHours": 24
  }
}
```

### Configuration Keys & Environment Variables

| Configuration Path | Environment Variable | Default Value | Description |
|--------------------|----------------------|---------------|-------------|
| `Jwt:SecretKey` | `Jwt__SecretKey` or `JWT_SECRET_KEY` | *(Built-in default)* | Symmetric key used to sign and verify JWTs. **Must be changed in production!** |
| `Jwt:Issuer` | `Jwt__Issuer` | `GameServer.API` | Valid token issuer. |
| `Jwt:Audience` | `Jwt__Audience` | `GameServer.Web` | Valid token audience. |
| `Jwt:ExpirationHours` | `Jwt__ExpirationHours` | `24` | Token lifetime in hours before expiration. |

> [!WARNING]
> In production and multi-node Docker Swarm environments, always specify a strong `Jwt:SecretKey` (minimum 256 bits / 32 characters) via environment variables or Docker secrets.

---

## Default Admin Account

When the database is initialized for the first time, a default administrator account is automatically seeded:

- **Username:** `admin`
- **Password:** `Admin123!`
- **Role:** `Admin`

> [!IMPORTANT]
> It is strongly recommended to change the default admin password immediately after first login via the user profile menu in the upper right corner of the web interface.

---

## Roles & Permissions

The system defines three standard roles:

| Role | Scope & Permissions |
|------|---------------------|
| **`Admin`** | **Full System Access**.<br>• View, edit, start, stop, delete all game servers across all groups.<br>• Create, edit, publish, delete GameTypes and Revisions.<br>• Create, edit, delete Mount Type Configurations.<br>• Manage Users, Role assignments, and Groups (`/admin/users`, `/admin/groups`). |
| **`GameManager`** | **Game Catalog Management**.<br>• Create, edit, publish, and delete GameTypes and Revisions.<br>• Server access is bounded by Group memberships and created servers. |
| **`User`** | **Standard End-User**.<br>• View and manage only the GameServers they created or have been granted access to via Groups.<br>• Cannot modify GameTypes or Mount Types. |

---

## Group-Based Server Access Boundaries

Groups define the boundaries of which GameServers standard users can see and interact with.

- **Group Memberships**: Users can belong to zero, one, or multiple groups.
- **Server Access Rules**: Each group can be assigned access to specific GameServers with one of two permission levels:
  - **`View` (View Only)**: Group members can view server status, overview, connection details, and monitor stats.
  - **`Edit` (View & Edit)**: Group members can edit server configuration, start/stop the server, access the terminal, manage files, and view logs.

```
┌─────────────────────────────────────────────────────────┐
│                       Group: "Gamers"                   │
│                                                         │
│   Members:                                              │
│   • user_alice (User)                                   │
│   • user_bob (User)                                     │
│                                                         │
│   Server Access Rules:                                  │
│   • Minecraft Survival (ID: mc-srv-1)   → [ View & Edit ] │
│   • Palworld Dedicated (ID: pw-srv-1)   → [ View Only   ] │
└─────────────────────────────────────────────────────────┘
```

---

## Server Creator Auditing & Permissions

When a user creates a GameServer:
- The server records `CreatedByUserId` and `CreatedByUsername`.
- The creating user retains full management permissions over their own created server.
- The creator can optionally assign a specific Group they belong to with permission to control the server, or keep it exclusive to themselves.

---

## Password Access Scoping

For sensitive GameType settings defined with `DataType = password` (such as Game Server Password or RCON Admin Password), the system provides granular password visibility controls:
- **Group Shared**: All members of the assigned group can reveal and copy the password (useful for general joining passwords).
- **Creator / Restricted**: Only the server creator or explicitly authorized users can view the password (useful for admin or RCON passwords).

---

## Admin Management Screens

Accessible only to users in the `Admin` role (`[Authorize(Roles = "Admin")]`):

### 1. User Management (`/admin/users`)
- **User Grid**: Lists all users with ID, username, email, role badge (`Admin`, `GameManager`, `User`), group badges, active status, and last login timestamp.
- **Create User Dialog**: Provision new users with username, email, initial password, role assignment, and group memberships.
- **Edit User Dialog**: Update email, role, active status, group memberships, or reset user password.
- **Delete User**: Delete user accounts with confirmation.

### 2. Group Management (`/admin/groups`)
- **Group Grid**: Lists all groups with member counts, server assignment counts, and creation timestamps.
- **Create / Edit Group Dialog**: Set group name and description.
- **Manage Members Dialog**: Assign/remove users from the group with visual role badges.
- **Manage Server Access Dialog**: Set permission levels (`No Access`, `View Only`, `View & Edit`) for each game server.

---

## API Endpoints

### Authentication (`/api/v2/auth`)
- `POST /api/v2/auth/login` — Authenticate with username and password, returns JWT token and profile.
- `GET /api/v2/auth/me` — Retrieve current authenticated user profile (`[Authorize]`).
- `POST /api/v2/auth/change-password` — Change password for current authenticated user (`[Authorize]`).

### User Management (`/api/v2/users` - `[Authorize(Roles = "Admin")]`)
- `GET /api/v2/users` — List all users.
- `GET /api/v2/users/{id}` — Get user details and group memberships.
- `POST /api/v2/users` — Create a new user.
- `PUT /api/v2/users/{id}` — Update user email, role, active status, password, or groups.
- `DELETE /api/v2/users/{id}` — Delete a user.

### Group Management (`/api/v2/groups` - `[Authorize(Roles = "Admin")]`)
- `GET /api/v2/groups` — List all groups.
- `GET /api/v2/groups/{id}` — Get group details, members, and server access rules.
- `POST /api/v2/groups` — Create a new group.
- `PUT /api/v2/groups/{id}` — Update group name and description.
- `DELETE /api/v2/groups/{id}` — Delete a group.
- `PUT /api/v2/groups/{id}/members` — Set members of a group.
- `PUT /api/v2/groups/{id}/servers` — Set server access permissions for a group.

---

## SignalR Hub Authentication

Real-time SignalR hubs (`/hubs/serverlogs`, `/hubs/terminal`, `/hubs/resources`, `/hubs/attach`) support authentication via query string:
- When connecting via WebSocket, the client passes `?access_token=<JWT>` in the query string.
- The JWT bearer event listener validates and unpacks claims for WebSocket sessions.
