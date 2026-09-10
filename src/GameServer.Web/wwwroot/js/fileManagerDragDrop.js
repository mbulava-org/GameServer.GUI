// File Manager drag-and-drop helper.
// Supports dropping individual files and entire folder trees (recursive) using
// the HTML5 webkitGetAsEntry API. Dropped File objects are cached and exposed
// to Blazor as IJSStreamReference-compatible Blobs via getFileStream(id).
window.gameServerFileDragDrop = (function () {
    const zones = new Map();     // element -> handler bag
    const fileStore = new Map(); // id -> File
    let nextId = 1;

    function readAllEntries(dirReader) {
        return new Promise((resolve, reject) => {
            const all = [];
            const readBatch = () => {
                dirReader.readEntries((batch) => {
                    if (batch.length === 0) {
                        resolve(all);
                    } else {
                        all.push(...batch);
                        readBatch();
                    }
                }, reject);
            };
            readBatch();
        });
    }

    async function walkEntry(entry, relPath, out) {
        if (entry.isFile) {
            const file = await new Promise((res, rej) => entry.file(res, rej));
            const id = String(nextId++);
            fileStore.set(id, file);
            out.push({
                id: id,
                name: file.name,
                size: file.size,
                relativePath: relPath,
                isDirectory: false
            });
        } else if (entry.isDirectory) {
            out.push({
                id: '',
                name: entry.name,
                size: 0,
                relativePath: relPath,
                isDirectory: true
            });
            const reader = entry.createReader();
            const children = await readAllEntries(reader);
            for (const child of children) {
                const childRel = relPath ? relPath + '/' + child.name : child.name;
                await walkEntry(child, childRel, out);
            }
        }
    }

    async function handleDrop(dotNetRef, dataTransfer) {
        const collected = [];
        const entries = [];

        if (dataTransfer.items && dataTransfer.items.length > 0) {
            for (let i = 0; i < dataTransfer.items.length; i++) {
                const it = dataTransfer.items[i];
                if (it.kind !== 'file') continue;
                const entry = it.webkitGetAsEntry ? it.webkitGetAsEntry() : null;
                if (entry) {
                    entries.push(entry);
                } else {
                    const file = it.getAsFile();
                    if (file) {
                        const id = String(nextId++);
                        fileStore.set(id, file);
                        collected.push({
                            id: id,
                            name: file.name,
                            size: file.size,
                            relativePath: file.name,
                            isDirectory: false
                        });
                    }
                }
            }
        } else if (dataTransfer.files) {
            for (const file of dataTransfer.files) {
                const id = String(nextId++);
                fileStore.set(id, file);
                collected.push({
                    id: id,
                    name: file.name,
                    size: file.size,
                    relativePath: file.name,
                    isDirectory: false
                });
            }
        }

        for (const entry of entries) {
            await walkEntry(entry, entry.name, collected);
        }

        if (collected.length > 0) {
            try {
                await dotNetRef.invokeMethodAsync('OnFilesDropped', collected);
            } catch (err) {
                console.error('OnFilesDropped failed', err);
                // Clean up any leftover file references on failure.
                for (const item of collected) {
                    if (item.id) fileStore.delete(item.id);
                }
            }
        }
    }

    return {
        attach: function (element, dotNetRef) {
            if (!element || zones.has(element)) return;
            let dragCounter = 0;

            const onDragEnter = (e) => {
                if (!e.dataTransfer || !Array.from(e.dataTransfer.types || []).includes('Files')) return;
                e.preventDefault();
                e.stopPropagation();
                dragCounter++;
                element.classList.add('file-manager-dragover');
            };
            const onDragOver = (e) => {
                if (!e.dataTransfer || !Array.from(e.dataTransfer.types || []).includes('Files')) return;
                e.preventDefault();
                e.stopPropagation();
                e.dataTransfer.dropEffect = 'copy';
            };
            const onDragLeave = (e) => {
                dragCounter--;
                if (dragCounter <= 0) {
                    dragCounter = 0;
                    element.classList.remove('file-manager-dragover');
                }
            };
            const onDrop = (e) => {
                if (!e.dataTransfer || !Array.from(e.dataTransfer.types || []).includes('Files')) return;
                e.preventDefault();
                e.stopPropagation();
                dragCounter = 0;
                element.classList.remove('file-manager-dragover');
                handleDrop(dotNetRef, e.dataTransfer);
            };

            element.addEventListener('dragenter', onDragEnter);
            element.addEventListener('dragover', onDragOver);
            element.addEventListener('dragleave', onDragLeave);
            element.addEventListener('drop', onDrop);
            zones.set(element, { onDragEnter, onDragOver, onDragLeave, onDrop });
        },
        detach: function (element) {
            const z = zones.get(element);
            if (!z) return;
            element.removeEventListener('dragenter', z.onDragEnter);
            element.removeEventListener('dragover', z.onDragOver);
            element.removeEventListener('dragleave', z.onDragLeave);
            element.removeEventListener('drop', z.onDrop);
            element.classList.remove('file-manager-dragover');
            zones.delete(element);
        },
        getFileStream: function (id) {
            return fileStore.get(id);
        },
        releaseFile: function (id) {
            fileStore.delete(id);
        }
    };
})();
