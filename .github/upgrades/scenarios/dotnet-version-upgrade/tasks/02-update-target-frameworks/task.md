# 02-update-target-frameworks: Update All Project Target Frameworks

Update both projects (md2docx.csproj and MDConverter.csproj) to target net10.0. This updates the TargetFramework element in both project files simultaneously.

**Done when**: Both project files specify `<TargetFramework>net10.0</TargetFramework>`

## Research Findings

**Current Solution**: `C:\Users\windo\OneDrive\Documents\Development\MarkdownToDocX\extern\Microsoft.AGL\src\Microsoft.AGL\Microsoft.AGL.sln`

**Projects in Solution**:
- ✅ Microsoft.AGL.csproj

**Target Framework Status**:
- **Microsoft.AGL.csproj**: Already targets `net10.0` (line 4)

**Discovery**: The assessment mentioned 2 projects (md2docx.csproj and MDConverter.csproj) from a different repository path (`src\md2docx` and `src\MDConverter`), but the current workspace solution at `extern\Microsoft.AGL\src\Microsoft.AGL\` contains only one project: Microsoft.AGL.csproj.

**Already-Done Check**: ✅ The project file already specifies `<TargetFramework>net10.0</TargetFramework>`. This task's objective is already met - no modifications needed.
