# Sample Portable GameType Imports

These files are starter `PortableGameTypePackage` JSON documents for import from the V2 GameType list page at `/gametypes-v2`.

## Files

- `palworld-dedicated.portable.json`
  - Based on `thijsvanloef/palworld-server-docker`
  - Includes the common game/query/RCON/REST API ports, `/palworld` data volume, and core server settings
- `minecraft-bedrock.portable.json`
  - Based on `itzg/minecraft-bedrock-server`
  - Includes the Bedrock UDP port, `/data` volume, and common Bedrock server property environment variables
- `minecraft-java.portable.json`
  - Based on `itzg/minecraft-server`
  - Includes the standard Java TCP port, `/data` volume, and common Java server settings

## Notes

- These are starter imports, not final production presets.
- No persisted integer ids are included.
- Review passwords, allowlists, operators, memory sizing, and public exposure settings before publishing a revision.
- Upstream container projects evolve independently, so settings may need refresh over time.
