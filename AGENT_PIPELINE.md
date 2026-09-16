# Automated Issue Agent Pipeline

## Overview

This repository uses GitHub Actions to process issues and coordinate comment-based requests to GitHub Copilot agents.

## How It Works

### Workflow Trigger

The issue pipeline is triggered in two ways:

1. **Automatically** when a new issue is opened (via `on: issues: types: [opened]`)
2. **Manually** via workflow dispatch with a required positive `issue_number` input (for testing or reprocessing existing issues)

Manual dispatch example:

```bash
gh workflow run issue-agent-pipeline.yml -f issue_number=123
```

The workflow resolves and validates the issue number before any agent request. It does not accept pull request numbers.

### Pipeline Stages

#### 1. Resolve and prepare issue context
- Resolves the issue from the opened-issue event or the manual `issue_number` input.
- Validates that the value is a positive integer and refers to an issue, not a pull request.
- Prepares planning context for the downstream implementation request. A planner output is not presumed to exist.

#### 2. Validate Handball Context
- Checks if the issue contains handball-related terminology
- Gates the pipeline - only issues with sports context proceed
- Example keywords: "handball", "goalkeeper", "pivot", "wing", "6m", "9m", "7m", etc.

#### 3. Gate Check
- Decides whether to proceed based on validation results
- Posts status comment to the issue

#### 4. Implementation Agent ⭐
- Posts a comment-based request to the issue mentioning `@copilot` and the `implementation-coder` custom agent.
- Passes issue details and clearly labels the planning text as unapproved context that the implementation agent must verify.
- The agent creates a branch, implements changes, and opens a PR with `Fixes #<issue_number>`.
- Every request and completion/failure marker includes the current workflow run identifier.

#### 5. Wait for PR
- Polls the issue timeline for a bounded period and accepts only an open PR with an expected issue-closing reference.
- Looks for a cross-referenced PR linked to the issue
- Fails if no PR is created within the time window

#### 6. Review Agent
- Posts a comment on the PR requesting code review
- Uses `@copilot` mention with `pr-reviewer` custom agent

#### 7. Fix Comments Agent
- Posts a comment requesting fixes for review feedback
- Uses `@copilot` mention with `review-comment-fixer` custom agent

#### 8. Completion and manual review
- Waits for the comment-fixing agent to report completion.
- Posts a run-scoped completion comment on the PR when all pipeline stages finish.
- Leaves the PR open for maintainer review and manual merge; automatic merging is not enabled.

## Key Changes (2026-07-20)

### Comment-based operation

The pipeline intentionally uses issue and pull-request comments to request each custom agent. It does not use Copilot assignment/task APIs or a special user-to-server token. Agent requests require the normal repository permissions and the expected Copilot integration to be available.

Completion is observable through run-scoped comments such as:

```text
<!-- hka-agent-complete: implementation run:123456789-1 -->
```

Polling is bounded and fails explicitly when an agent reports a run-scoped failure marker or does not produce the expected marker in time. A successful pipeline reports completion on the PR, but does not merge it; maintainers must review and merge manually.

## Custom Agents

Custom agents are defined in `.github/agents/`:
- `Handballl.agent.md` - Domain expertise in handball and software development
- `handball-knowledge-validator.agent.md` - Validates features against handball rules
- `implementation-coder.agent.md` - Implements code changes and creates PRs
- `issue-planner.agent.md` - Creates implementation plans
- `pr-reviewer.agent.md` - Reviews PRs for correctness
- `review-comment-fixer.agent.md` - Resolves review feedback
- `ui-polish-coder.agent.md` - Visual and UX polish

## Testing

To test the pipeline:

1. Manually trigger via workflow dispatch:
   ```bash
   gh workflow run issue-agent-pipeline.yml -f issue_number=<number>
   ```

2. Or create a test issue with handball context

## Requirements

- **GitHub Token Permissions**: The workflow requires:
  - `contents: write` - For creating branches and commits
  - `pull-requests: write` - For creating and updating PRs
  - `issues: write` - For posting comments

- **Copilot access**: Agent requests are comment-based. No Copilot assignment/task API or special user-to-server token is required by this workflow.

## Troubleshooting

### Agent Not Triggering
- Check workflow run logs and the issue/PR comments for the run identifier.
- Verify token permissions and that the repository's Copilot integration can act on `@copilot` comments.

### No PR Created
- Verify the agent completed its work (may take several minutes)
- Check that the PR includes `Fixes #<issue_number>` for auto-linking

### Pipeline Timing Out
- The wait-for-pr and completion-marker steps use bounded polling windows
- If consistently timing out, the agent may be failing silently
- Check Copilot agent logs for errors

## Weekly Improvement Issues

The repository also has a `weekly-improvement-issue.yml` workflow that:
- Runs every Tuesday at 22:45 UTC (via cron)
- Creates a themed improvement issue (rotates between Visual polish, Game flow, Bug fixing)
- Automatically dispatches the issue-agent-pipeline for the new issue

## Future Enhancements

Potential improvements:
- Poll agent task status via API instead of waiting for PR linkage
- Add more granular error handling and retry logic
- Support custom agent selection via issue labels
- Add metrics and monitoring for pipeline success rates
