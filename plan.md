1. Update `review-agent` and `fix-comments-agent` steps to use the Copilot Agent Tasks API (`POST /agents/repos/{owner}/{repo}/tasks`) instead of creating `@copilot` comments.
2. Update the `auto-merge` step to use a `github-script` that explicitly merges the PR using `github.rest.pulls.merge` instead of trying to enable repo-level auto-merge (which is disabled on this repo).
3. Ensure error handling for API calls.
