# Security and privacy

## Publication checklist

- Review `git diff --cached` and `git ls-files` before each commit. Ignore rules do not protect already tracked files or forced additions.
- Include only the two reviewed demo CSVs. Never publish real exports, host records, databases, credentials, logs, or screenshots containing imported data.
- Check author and committer metadata with `git log --all --format=fuller`. Use a privacy-safe Git identity before committing.
- Publish via Git, not by uploading a ZIP of the working folder. Ignored files and local Git recovery history can contain private information.
- Review all branches and tags before pushing. Re-scan whenever files or history change.

## Application boundaries

Imported CSVs and their URLs are untrusted. Non-HTTP(S) links and URLs containing credentials are blocked. Web links require confirmation and images are not fetched automatically. A permitted web link is not a guarantee that a website is trustworthy.

SQLite writes use bound parameters. The local database is not encrypted and can contain host names, vehicle identifiers, listing URLs and source file information. Clearing the visible session does not erase it. Protect the database and its backups using operating-system access controls.

Run the Release build, smoke checks, and `dotnet list TuroCarAnalyser/TuroCarAnalyser.csproj package --vulnerable --include-transitive` before release. Advisory results depend on the available vulnerability feed and do not prove absence of vulnerabilities.

This repository has had a focused source/publication review, not a penetration test. Large or malicious CSVs can still affect availability; import size limits and broader fuzz testing remain future work.

Do not put real data or secrets in public issue reports. Reproduce issues with synthetic data. If a credential is ever published, revoke it before cleaning Git history.
