# Task 10 analyser results

The settings below were enabled locally for the whole solution and then removed,
as required by the brief. `dotnet format analyzers` and a build with the latest
.NET analysis level produced these requested findings:

| Diagnostic | Findings |
| --- | ---: |
| `IDE0051` unused private member | 0 |
| `IDE0052` unread private field | 0 |
| `IDE0060` unused parameter | 0 |
| `CA1822` member can be static | 0 |

No source was removed. The broad latest-level build also reported unrelated
analyser findings; they are outside this task's four requested diagnostics.
The temporary analyser severities are not committed.
