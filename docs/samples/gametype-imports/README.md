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
- `minecraft-bedrock.portable.json`
  - Based on `itzg/minecraft-bedrock-server`
  - Includes the Bedrock UDP port, `/data` volume, and common Bedrock server property environment variables
- `minecraft-java.portable.json`
  - Based on `itzg/minecraft-server`
  - Includes the standard Java TCP port, `/data` volume, and common Java server settings

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

