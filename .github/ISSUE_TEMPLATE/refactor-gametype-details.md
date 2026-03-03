---
name: Refactor GameTypeDetails into Components
about: Split large GameTypeDetails.razor file into smaller, maintainable components
title: 'Refactor: Split GameTypeDetails.razor into focused components'
labels: ['refactoring', 'technical-debt', 'enhancement']
assignees: ''
---

## 📋 Overview

The `GameTypeDetails.razor` file has grown to 1900+ lines, making it difficult to maintain and test. This issue tracks the work to split it into smaller, focused components.

**Current:** 1 file, 1900+ lines  
**Target:** 5-6 components, ~300-500 lines each  
**Estimated Effort:** 3-4 hours

---

## 🎯 Goals

- ✅ Improve code maintainability
- ✅ Enable isolated component testing
- ✅ Reduce cognitive load when making changes
- ✅ Improve code reusability
- ✅ Follow single responsibility principle

---

## 📁 Proposed Component Structure

```
src/GameServer.Web/Components/Pages/GameTypes/
├── GameTypeDetails.razor                    (~200 lines - coordinator)
└── Components/
    ├── GameTypeBasicInfo.razor              (~250 lines)
    ├── GameTypePortsEditor.razor            (~300 lines)
    ├── GameTypeVolumesEditor.razor          (~200 lines)
    └── GameTypeSettingsEditor.razor         (~800 lines)
        └── Components/
            ├── SettingsList.razor           (~200 lines)
            ├── SettingDetailEditor.razor    (~400 lines)
            └── SettingMetadataEditor.razor  (~200 lines)
```

---

## 📝 Tasks

### Phase 1: Preparation (30 min)
- [ ] Create feature branch: `refactor/split-gametype-details`
- [ ] Create component folder structure
- [ ] Backup current GameTypeDetails.razor
- [ ] Verify all existing tests pass

### Phase 2: Extract Basic Info Component (45 min)
- [ ] Create `GameTypeBasicInfo.razor`
- [ ] Move basic info UI (Key, DisplayName, Description, Image, etc.)
- [ ] Move basic info validation logic
- [ ] Add Parameters and EventCallbacks
- [ ] Replace section in GameTypeDetails with component
- [ ] Test: Create/edit game type basic info works
- [ ] Commit: `refactor: Extract GameTypeBasicInfo component`

### Phase 3: Extract Ports Editor Component (45 min)
- [ ] Create `GameTypePortsEditor.razor`
- [ ] Move ports tab UI
- [ ] Move AddPort, RemovePort, port validation methods
- [ ] Add Parameters and EventCallbacks
- [ ] Replace ports tab with component
- [ ] Test: Add/edit/remove ports works
- [ ] Commit: `refactor: Extract GameTypePortsEditor component`

### Phase 4: Extract Volumes Editor Component (30 min)
- [ ] Create `GameTypeVolumesEditor.razor`
- [ ] Move volumes tab UI
- [ ] Move AddVolume, RemoveVolume methods
- [ ] Add Parameters and EventCallbacks
- [ ] Replace volumes tab with component
- [ ] Test: Add/edit/remove volumes works
- [ ] Commit: `refactor: Extract GameTypeVolumesEditor component`

### Phase 5: Extract Settings Editor Component (90 min)
- [ ] Create `GameTypeSettingsEditor.razor`
- [ ] Move settings tab UI
- [ ] Move settings-related methods
- [ ] Consider sub-components (SettingsList, SettingDetailEditor)
- [ ] Add Parameters and EventCallbacks
- [ ] Replace settings tab with component
- [ ] Test: Full settings workflow works
- [ ] Commit: `refactor: Extract GameTypeSettingsEditor component`

### Phase 6: Finalize & Deploy (30 min)
- [ ] Remove backup file
- [ ] Update documentation if needed
- [ ] Run full test suite
- [ ] Performance check (rendering, memory)
- [ ] Code review
- [ ] Merge to main

---

## 🧪 Testing Checklist

### Manual Testing
- [ ] Create new game type (all tabs)
- [ ] Edit existing game type (all tabs)
- [ ] Add/remove ports
- [ ] Add/remove volumes
- [ ] Add/remove settings
- [ ] Edit setting metadata (all fields)
- [ ] Save and verify in database
- [ ] Navigation between tabs preserves changes
- [ ] Validation errors display correctly
- [ ] UI rendering matches original

### Automated Testing
- [ ] All existing tests pass
- [ ] Add component-specific tests (optional)
- [ ] Integration tests pass

---

## 📖 Reference Documentation

See detailed refactoring plan: [`docs/refactoring/REFACTOR-GameTypeDetails-Components.md`](../docs/refactoring/REFACTOR-GameTypeDetails-Components.md)

This document includes:
- Detailed component responsibilities
- Code examples
- Testing strategy
- Rollback plan

---

## ⚠️ Important Notes

### Breaking Changes
- **None expected** - This is a pure refactoring with no functional changes
- All existing functionality should work identically

### Rollback Plan
If issues arise post-merge:
1. Revert the merge commit
2. Investigate and fix in refactor branch
3. Re-deploy after validation

### Dependencies
- No external dependencies
- No database changes
- No API changes

---

## 💡 Future Improvements (Post-Refactor)

Consider these after successful refactoring:
- [ ] Add unit tests for each component
- [ ] Extract common validation into shared service
- [ ] Add loading skeletons for better UX
- [ ] Consider Blazor EditForm for validation
- [ ] Optimize re-rendering with ShouldRender

---

## 📊 Success Criteria

✅ All existing functionality works identically  
✅ Each component file < 500 lines  
✅ All tests pass  
✅ No performance degradation  
✅ Code is easier to understand  
✅ Future changes are easier to make  

---

## 📅 Timeline

**Estimated Time:** 3-4 hours  
**Priority:** Medium (Technical Debt)  
**Target Version:** Next minor release

---

## 👥 Related Issues

- Closes #[issue-number] (if applicable)
- Related to: Technical debt cleanup initiative

---

## 💬 Comments

<!-- Add any additional context, concerns, or questions here -->
