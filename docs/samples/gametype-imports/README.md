# Sample Portable GameType Imports

These files are starter `PortableGameTypePackage` JSON documents for import from the V2 GameType list page at `/gametypes-v2`.

## Files

- `conan-exiles-dedicated.portable.json`
  - Based on `othrayte/docker-conanexiles` and SteamCMD App ID `443030` (`ConanSandboxServer.exe`)
  - Includes game port (`7777/udp`), raw ping port (`7778/udp`), Steam query port (`27015/udp`), and RCON port (`25575/tcp`)
  - Configures the `/conanws` persistent volume for `game.db`, logs, and `WindowsServer` ini configurations

- `palworld-dedicated.portable.json`
  - Based on `thijsvanloef/palworld-server-docker`
  - Includes the common game/query/RCON/REST API ports, `/palworld` data volume, and core server settings
- `aska-dedicated.portable.json`
  - Windows Host Agent GameType for ASKA Dedicated Server (SteamCMD App ID `3246670` using Steam GSLT auth token for Base App ID `1898300`)
  - Includes game port (`7777/udp`) and Steam query port (`27015/udp`)
  - Configures volume access for the server root folder `/` and the game save folder `/savegame`
- `minecraft-bedrock.portable.json`
  - Based on `itzg/minecraft-bedrock-server`
  - Includes the Bedrock UDP port, `/data` volume, and common Bedrock server property environment variables
- `minecraft-java.portable.json`
  - Based on `itzg/minecraft-server`
  - Includes the standard Java TCP port, `/data` volume, and common Java server settings

## ASKA Dedicated Server Hosting Guidance

When hosting an ASKA dedicated server on a Windows agent:

1. **Steam Game Server Login Token (GSLT / Auth Token)**:
   - Go to [Steam Game Server Account Management](https://steamcommunity.com/dev/managegameservers).
   - Enter **App ID: `1898300`** (the base ASKA app ID) and generate a token.
   - In the GameServer settings, set **`ASKA_AUTHENTICATION_TOKEN`** to your generated token. The server will write this token as `authentication token` in `server properties.txt`.

2. **File Manager Access**:
   - **Server Root Folder (`/`)**: Browse and edit `server properties.txt`, server binaries (`AskaServer.exe`), BepInEx plugin folders (`BepInEx/plugins`), and logs.
   - **Game Saves Folder (`/savegame`)**: Access and back up world save files.

3. **Required Network Ports**:
   - `7777/udp`: Main game traffic
   - `27015/udp`: Steam query port

4. **SteamCMD Installation**:
   - SteamCMD downloads and updates the server files using App ID `3246670` with anonymous login.

## Conan Exiles Hosting Guidance

When hosting a Conan Exiles dedicated server:

1. **Required Network Ports (Inbound Firewall & NAT Rules)**:
   - `7777/udp`: Main game traffic
   - `7778/udp`: Server browser ping / raw UDP
   - `27015/udp`: Steam Master Server query
   - `25575/tcp`: Remote Console (RCON, optional)

2. **Hosting on the Same Machine as Game Client**:
   - If running both the game client and the dedicated server on the same physical PC, avoid port conflicts with the client process.
   - Use `MULTIHOME` setting or assign alternate ports (e.g. `7779/udp`, `7780/udp`, `27016/udp`).
   - Connect locally via **Direct Connect** using your local IP (`127.0.0.1` or LAN IP) and configured game port.

3. **Key Configuration Files**:
   - `Engine.ini`: Controls network binding and port overrides (`Port`, `GameServerQueryPort`).
   - `ServerSettings.ini`: Controls server rules, player limits, and the required `AdminPassword`.

4. **Process Launching**:
   - Run via the agent or `ConanSandboxServer.exe -log`. Do not launch the server executable through the Steam client UI.

## Notes

- These are starter imports, not final production presets.
- No persisted integer ids are included.
- Review passwords, allowlists, operators, memory sizing, and public exposure settings before publishing a revision.
- Upstream container projects evolve independently, so settings may need refresh over time.

