# SAP-Dynmaics-Smart-Mapping
This project demonstrates the use of Azure Open AI to map SAP schema fields to Dynamics 365 fields.

## Fix for merge/cache corruption errors
If you see errors like:
- `Problem reading ... obj/project.nuget.cache : '<' is an invalid start of a property name`
- `Files has invalid value "<<<<<<< HEAD". Illegal characters in path.`

those files contain unresolved Git merge markers or corrupted generated cache content.

### Quick fix
Run:

```bash
./fix-merge-issues.sh
```

Then restore and rebuild dependencies:

```bash
dotnet restore
```

### Manual fix checklist
1. Search for merge markers in source files: `<<<<<<<`, `=======`, `>>>>>>>`.
2. Resolve each conflict and remove marker lines.
3. Delete `bin/` and `obj/` directories.
4. Re-run `dotnet restore` and `dotnet build`.
