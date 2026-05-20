# File Permission Fix - Using Linux UID/GID Impersonation

## Problem
File save operations were failing with "Access denied" errors when trying to write files to container volumes via direct filesystem access.

## Root Cause
The GameServer web service writes files directly to the host filesystem (volume mount points), but the files are created with the web service's user/group ownership instead of the container's user/group. When the container tries to access these files, it gets permission denied errors.

## Solution
Modified `GameServerFileManagerService` to **impersonate** the container's User ID (UID) and Group ID (GID) **during file creation** using Linux syscalls (`setfsuid` and `setfsgid`). This ensures files are created with the correct ownership from the start, avoiding the need for post-write permission fixes.

### Changes Made

#### 1. Added System.Runtime.InteropServices namespace
- Required for P/Invoke to call Linux syscalls

#### 2. Added Linux Syscall Interop: `NativeMethods`
```csharp
private static class NativeMethods
{
    [DllImport("libc", SetLastError = true)]
    public static extern int setfsuid(int uid);

    [DllImport("libc", SetLastError = true)]
    public static extern int setfsgid(int gid);
}
```
- `setfsuid`: Sets the filesystem UID for the current thread
- `setfsgid`: Sets the filesystem GID for the current thread
- Thread-safe: only affects the calling thread

#### 3. New Method: `GetContainerUserGroupAsync()`
```csharp
private async Task<(int uid, int gid)?> GetContainerUserGroupAsync(string serverId)
```
- Queries Docker Swarm service for container's User specification
- Parses formats like:
  - `"1000:1000"` (uid:gid)
  - `"1000"` (uid only, uses same for gid)
- Returns null if no user is specified (container runs as root)

#### 4. New Method: `ExecuteWithImpersonationAsync()`
```csharp
private async Task ExecuteWithImpersonationAsync((int uid, int gid)? userGroup, Func<Task> action)
```
- **Saves** current thread's filesystem UID/GID
- **Sets** filesystem UID/GID to container's values
- **Executes** the file operation (write, create directory)
- **Restores** original filesystem UID/GID
- Linux only: skips on Windows/macOS

#### 5. Modified: `UploadFileAsync()`
- Wraps file write in `ExecuteWithImpersonationAsync()`
- Files are created with container's UID/GID from the start

#### 6. Modified: `CreateDirectoryAsync()`
- Wraps directory creation in `ExecuteWithImpersonationAsync()`
- Directories are created with container's UID/GID from the start

## How It Works

### Flow for File Upload:
1. User edits file in browser
2. File content sent to `UploadFileAsync()`
3. Service queries Docker for container's UID/GID (e.g., `1000:1000`)
4. Service calls `setfsuid(1000)` and `setfsgid(1000)` (thread-local)
5. File written to host filesystem **as UID 1000, GID 1000**
6. Service restores original UID/GID
7. Container can immediately read/write the file ✅

### Sequence Diagram:
```
User → Web Service: Save file
Web Service → Docker API: Get service spec (User: "1000:1000")
Web Service → Linux kernel: setfsuid(1000), setfsgid(1000)
Web Service → Filesystem: Write file (owned by 1000:1000)
Web Service → Linux kernel: setfsuid(original), setfsgid(original)
Container → Filesystem: Access file ✅ (correct ownership)
```

## Benefits

✅ **Cleaner solution** - No external `chown` process needed  
✅ **Files created correctly from the start** - No post-write permission fixes  
✅ **Thread-safe** - Uses thread-local filesystem UID/GID  
✅ **More efficient** - One syscall vs spawning a process  
✅ **Keeps direct filesystem access** - No architecture changes  
✅ **Automatic & transparent** - Works without UI/API changes  
✅ **Safe fallback** - Skips impersonation on non-Linux or if container has no user spec  

## Platform Support

- ✅ **Linux**: Full support (primary deployment target)
- ⚠️ **macOS**: Skips impersonation (different permission model)
- ⚠️ **Windows**: Skips impersonation (uses ACLs instead of UID/GID)

## Technical Details

### Why `setfsuid`/`setfsgid` instead of `setuid`/`setgid`?

- **`setuid`/`setgid`**: Change the process's real/effective/saved UID/GID (affects everything)
- **`setfsuid`/`setfsgid`**: Change only filesystem permission checks (thread-local, safer)

### Security & Permissions

- Requires `CAP_SETUID` and `CAP_SETGID` capabilities (or root)
- Web service must have permission to impersonate target UID/GID
- UIDs are obtained from Docker API (trusted source)
- Thread-local: doesn't affect other requests/threads

### Performance

- **Impersonation overhead**: ~microseconds (2 syscalls)
- **vs chown approach**: Saves process spawn + wait + cleanup
- **Async-safe**: Works correctly with async/await

## Testing

### Test Cases to Verify:

1. **Edit existing file**
   - Open file in editor → Make changes → Save
   - Verify: `ls -l` shows file owned by container UID/GID
   - Verify: Container can read/write the file

2. **Create new file**
   - Upload new file
   - Verify: File owned by container UID/GID
   - Verify: Container can access the file

3. **Create directory**
   - Create new folder → Upload file to folder
   - Verify: Both directory and file owned by container UID/GID

4. **Container with user spec (`User: "1000:1000"`)**
   - Verify files: `uid=1000, gid=1000`

5. **Container without user spec (root)**
   - Verify files: owned by web service user (fallback)

6. **Concurrent file operations**
   - Upload multiple files simultaneously
   - Verify: All have correct ownership
   - Verify: No race conditions

## Monitoring

### Log Messages:

**Success:**
```
[DEBUG] Container user spec: 1000:1000
[DEBUG] Impersonating UID 1000, GID 1000 for filesystem operations
[DEBUG] Successfully impersonated UID 1000, GID 1000 (original: 0:0)
[INFO] Successfully uploaded file: /config/server.properties
[DEBUG] Restored original UID 0
[DEBUG] Restored original GID 0
```

**Fallback (no user spec):**
```
[DEBUG] No user specified for service abc123, files will use default ownership
[DEBUG] No UID/GID specified, executing without impersonation
```

**Warnings:**
```
[WARNING] Failed to set filesystem UID/GID, continuing without impersonation
[WARNING] Failed to get container user/group for service abc123
```

## Comparison: Impersonation vs Chown

| Aspect | Impersonation (NEW) | Chown (OLD) |
|--------|---------------------|-------------|
| **When ownership set** | During write | After write |
| **Performance** | Fast (syscalls) | Slower (process spawn) |
| **Correctness** | Files always correct | Race window |
| **External dependencies** | None (kernel syscalls) | Requires `chown` binary |
| **Error handling** | Inline | Post-write cleanup |
| **Complexity** | Low (P/Invoke) | Medium (Process management) |

## Future Improvements

1. **Cache UID/GID** - Store per service to avoid repeated Docker API calls
2. **Capability checking** - Verify `CAP_SETUID`/`CAP_SETGID` before attempting
3. **Metrics** - Track impersonation success/failure rates
4. **Extended attributes** - Consider setting SELinux/AppArmor contexts

## Environment Requirements

### Linux Host:
- Kernel with `setfsuid`/`setfsgid` syscalls (all modern kernels)
- Web service must have appropriate capabilities:
  - Running as root, OR
  - Has `CAP_SETUID` and `CAP_SETGID` capabilities

### Docker Configuration:
- Services must specify `User` in ContainerSpec (e.g., `User: "1000:1000"`)
- Volume mounts must support Unix permissions (not FAT32)

## Troubleshooting

### Files still owned by wrong user:
- Check logs for "Failed to set filesystem UID/GID"
- Verify web service has `CAP_SETUID` capability: `getcap /path/to/executable`
- Check if running as root: `id`

### Impersonation not happening:
- Verify service has `User` specified: `docker service inspect <service>`
- Check platform: Impersonation only works on Linux

### Permission denied errors persist:
- Verify volume mount permissions
- Check if filesystem supports Unix permissions
- Ensure UID/GID exists on the host system
