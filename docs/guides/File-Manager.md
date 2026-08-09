# File Manager Guide

The file manager lets you browse, upload, download, and edit files inside a game server's volumes directly from the browser.

## Current Implementation

**Component:** `src/GameServer.Web/Components/Server/ServerFileManager.razor`  
Route: used inside the legacy server details page (`/servers/{id}`) Files tab.

> Note: The component is marked `[Obsolete]` because it depends on the legacy `IGameServerApi` and `GameServer` model. A V2-aligned file manager will be needed before the legacy path can be removed.

## Features

- **Browse volumes** — Select a mounted volume and navigate directories.
- **Upload files** — Upload files to the current directory.
- **Download files** — Download individual files.
- **Edit text files** — Open text files in a built-in editor.
- **Create folders** — Add new directories inside the selected volume.
- **Delete files/folders** — Remove items from the volume.

## How to Use

1. Open a server details page that includes the file manager tab.
2. Select a volume from the dropdown.
3. Navigate folders by double-clicking directories.
4. Use the toolbar buttons to upload, create folders, or refresh the list.
5. Click the edit or download action buttons on a file row.

## Limitations

- The file manager currently works through the legacy V1 API surface.
- It does not yet operate against the V2 persistence path.
- Advanced operations such as bulk upload, drag-and-drop, or archive extract are not implemented.

## Roadmap

- Port `ServerFileManager` to use the V2 server model and V2 file API.
- Add V2 volume browsing directly from `GameServerDetailsV2`.
- Add folder operations (rename, move) and bulk upload.

## Related Documentation

- [Current Features](../CURRENT-FEATURES.md)
- [V2 GameServer Lifecycle](V2-GameServer-Lifecycle.md)
