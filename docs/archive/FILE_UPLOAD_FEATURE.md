# File Upload Functionality Added to File Manager

## Summary
Implemented complete file upload functionality in the ServerFileManager component with progress tracking, multi-file support, and proper error handling.

## Changes Made

### 1. ✅ Upload Dialog Implementation
**Location**: `src\GameServer.Web\Components\Server\ServerFileManager.razor`

Replaced the placeholder upload functionality with a fully working implementation:

**Features**:
- Opens a modal dialog when "Upload" button is clicked
- Uses Blazor's `InputFile` component for file selection
- Supports multiple file uploads (up to 10 files)
- Shows current path where files will be uploaded
- Real-time progress tracking for each file
- Visual indicators for success/error states

### 2. ✅ Upload Progress Tracking

Added `UploadProgress` class to track each file's upload state:

```csharp
private class UploadProgress
{
    public string FileName { get; set; } = "";
    public long TotalBytes { get; set; }
    public long UploadedBytes { get; set; }
    public int Percentage => TotalBytes > 0 ? (int)((UploadedBytes * 100) / TotalBytes) : 0;
    public bool IsComplete { get; set; }
    public string? Error { get; set; }
}
```

**Tracks**:
- File name and size
- Upload progress percentage
- Completion status
- Error messages (if any)

### 3. ✅ File Upload Handler

`HandleFileUpload(InputFileChangeEventArgs e)` method:

**Process**:
1. Reads each selected file into memory
2. Limits file size to 100MB max per file
3. Shows progress as file is read
4. Constructs proper file path (respects current directory)
5. Uploads to server via `ServerApi.UploadFileAsync()`
6. Shows success/error notifications
7. Updates progress UI in real-time

**Features**:
- Multi-file support (up to 10 files at once)
- Large file support (100MB max per file)
- Proper error handling per file
- Non-blocking UI updates with `StateHasChanged()`

### 4. ✅ Dialog Flow

**User Experience**:
1. User clicks "Upload" button
2. Dialog opens showing current path
3. User selects one or more files
4. Progress bars show upload status for each file
5. Green checkmark shows when complete
6. Red error icon shows if upload fails
7. User clicks "Close" when done
8. File list automatically refreshes

### 5. ✅ UI Styling

Added CSS for upload dialog: `src\GameServer.Web\Components\Server\ServerFileManager.razor.css`

```css
.upload-progress-container {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  max-height: 400px;
  overflow-y: auto;
}

.upload-item {
  padding: 1rem;
  background: var(--rz-base-50);
  border-radius: 8px;
  border: 1px solid var(--rz-border-color);
}
```

**Styling**:
- Clean progress cards for each file
- Scrollable container for many files
- File name and size display
- Progress bar visualization
- Success/error states with icons

## Technical Details

### File Size Limits
- **Per file**: 100MB max
- **Total files**: 10 files max per upload operation
- Configurable via `OpenReadStream(maxAllowedSize)` parameter

### File Path Construction
Files are uploaded to the current directory:
- Root path (`/`): `/{fileName}`
- Subdirectory: `{currentPath}/{fileName}`

### API Integration
Uses existing `ServerApi.UploadFileAsync()`:
```csharp
await ServerApi.UploadFileAsync(
    ServerId,           // Server ID
    selectedVolume.Target,  // Volume target (e.g., "/data")
    filePath,           // Full file path
    new FileParameter(stream, fileName)  // File content
);
```

### Permission Handling
Files uploaded through this component will:
- Be created with proper container UID/GID ownership (via impersonation feature)
- Have permissions set correctly for container access
- Be immediately accessible by the game server

## User Workflow

### Single File Upload:
1. Navigate to Files tab
2. Select volume and directory
3. Click "Upload" button
4. Select file from local system
5. Watch progress bar
6. See success notification
7. File appears in file list

### Multiple File Upload:
1. Click "Upload" button
2. Select multiple files (Ctrl+Click or Shift+Click)
3. Watch progress for each file individually
4. See which files succeeded/failed
5. All successful files appear in list

### Error Handling:
- **File too large**: Shows error before upload
- **Network failure**: Shows error after upload attempt
- **Permission issues**: Shows error with message
- **Partial failure**: Some files succeed, some fail (independent)

## Notifications

### Success:
```
✓ Upload Complete
{fileName} uploaded successfully
```

### Error:
```
✗ Upload Failed
Failed to upload {fileName}: {error message}
```

## Testing Checklist

- [ ] Upload single file to root directory
- [ ] Upload single file to subdirectory
- [ ] Upload multiple files at once
- [ ] Upload large file (close to 100MB)
- [ ] Try to upload file > 100MB (should error)
- [ ] Try to upload > 10 files (should limit)
- [ ] Cancel dialog without uploading
- [ ] Upload to different volumes
- [ ] Verify file appears in list after upload
- [ ] Verify file has correct ownership (UID/GID)
- [ ] Verify progress bar updates correctly
- [ ] Verify success/error icons show correctly
- [ ] Upload file with same name as existing (overwrite)

## Known Behaviors

### File Overwriting
- Uploading a file with same name as existing file **overwrites** it
- No confirmation dialog (may want to add in future)
- Previous file is replaced completely

### Memory Usage
- Files are read into memory before upload
- Large files (100MB) will consume significant memory
- Multiple large files may impact browser performance
- Consider streaming for very large files in future

### Upload Speed
- Speed depends on network and server processing
- Progress bar shows file read progress (instant)
- Actual upload to server happens after read
- No streaming progress during upload to server

## Future Enhancements

1. **Streaming Uploads** - Stream files directly without loading into memory
2. **Overwrite Confirmation** - Ask before overwriting existing files
3. **Drag & Drop** - Support drag-and-drop file uploads
4. **Upload Queue** - Process uploads sequentially to reduce memory
5. **Pause/Resume** - Allow pausing and resuming uploads
6. **Upload from URL** - Download file from URL and upload to server
7. **File Preview** - Show preview of file before uploading (images, text)
8. **Bulk Actions** - Upload entire folders/zip files

## Related Files

- `src\GameServer.Web\Components\Server\ServerFileManager.razor` - Main component
- `src\GameServer.Web\Components\Server\ServerFileManager.razor.css` - Styling
- `src\GameServer.Docker\Services\GameServerFileManagerService.cs` - Server-side file handling
- `src\GameServer.Docker\Controllers\GameServerController.cs` - Upload API endpoint

## Integration with Existing Features

### Works with:
- ✅ **UID/GID Impersonation** - Files have correct ownership
- ✅ **Volume Selection** - Upload to any configured volume
- ✅ **Directory Navigation** - Upload to current directory
- ✅ **File List Refresh** - List updates automatically
- ✅ **Notifications** - Success/error notifications shown
- ✅ **File Editor** - Can edit uploaded files immediately
- ✅ **File Download** - Can download previously uploaded files

## Screenshots (Visual Description)

### Upload Dialog (No Files Selected):
```
┌──────────────────────────────────────────┐
│ Upload File                         [X]  │
├──────────────────────────────────────────┤
│ Select a file to upload to /config      │
│                                          │
│ [Choose Files...]                        │
│                                          │
│                              [Close]     │
└──────────────────────────────────────────┘
```

### Upload in Progress:
```
┌──────────────────────────────────────────┐
│ Upload File                         [X]  │
├──────────────────────────────────────────┤
│ Select a file to upload to /config      │
│                                          │
│ [Choose Files...]                        │
│                                          │
│ ┌────────────────────────────────────┐  │
│ │ server.properties         2.5 KB   │  │
│ │ [████████████████░░░░] 80%        │  │
│ └────────────────────────────────────┘  │
│                                          │
│                              [Close]     │
└──────────────────────────────────────────┘
```

### Upload Complete:
```
┌──────────────────────────────────────────┐
│ Upload File                         [X]  │
├──────────────────────────────────────────┤
│ Select a file to upload to /config      │
│                                          │
│ [Choose Files...]                        │
│                                          │
│ ┌────────────────────────────────────┐  │
│ │ server.properties         2.5 KB   │  │
│ │ ✓ Complete                         │  │
│ └────────────────────────────────────┘  │
│                                          │
│                              [Close]     │
└──────────────────────────────────────────┘
```

### Multiple Files with Error:
```
┌──────────────────────────────────────────┐
│ Upload File                         [X]  │
├──────────────────────────────────────────┤
│ ┌────────────────────────────────────┐  │
│ │ config.yml               1.2 KB    │  │
│ │ ✓ Complete                         │  │
│ └────────────────────────────────────┘  │
│ ┌────────────────────────────────────┐  │
│ │ world.dat               45.8 MB    │  │
│ │ ✓ Complete                         │  │
│ └────────────────────────────────────┘  │
│ ┌────────────────────────────────────┐  │
│ │ large.zip              156.2 MB    │  │
│ │ ✗ File too large (max 100MB)      │  │
│ └────────────────────────────────────┘  │
│                                          │
│                              [Close]     │
└──────────────────────────────────────────┘
```
