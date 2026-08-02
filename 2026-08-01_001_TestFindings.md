## Date: 2026-08-01 
## Test Run: 1

## Findings
- Create a list of issues seen
- On the Create Game Server Page the Volume Bindings should link each GameType Volume Definition with Mount Point.  
  - Only the Volume Name and Mount Type should be editable
  - The Container Path should take the value of the GameTypeVolume
  - Driver, DriverOptionsJson, OwnerGid, OwnerUID, Permissions, ReadOnly, Required, Source, Usage, & EnsureNfsPathExists columns aren't needed in the GameServerVolumes Table/Object
  - On the Settings section the Enum Data Type doesn't appear to be working
    - There should be a document created to Describe how the Enum Data Type works with the ValuesMappingsJson
    - The Boolean Data types should be a CheckBox or On/Off switch rather than a true false drop down.

- On the Edit GameType page, on the Volumes tab, none of the values are being saved when changed, after clicking the Save button, and reloading the page the orignal values are all returned.
- On the Mount Type Configuration Editor, the Live Preview
  - the {Source} varable a "formatted" version of the "GameType's" Volume's Container Path
  - If the Container Path starts or end with a slash that character is ignored
  - any other slash is replaced with a dash -
  - Live Preview should be displayed above Options
  - The Resoved Volume name should be a name not a path so it should not contain any chars that cannot be in a folder name
  


### Exmplain/Remember
