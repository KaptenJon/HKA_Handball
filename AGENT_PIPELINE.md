# Automated Issue Agent Pipeline

## Overview

This repository uses GitHub Actions to automatically process issues and create pull requests using GitHub Copilot agents.

## How It Works

### Workflow Trigger

The issue pipeline is triggered in two ways:

1. **Automatically** when a new issue is opened (via `on: issues: types: [opened]`)
2. **Manually** via workflow dispatch (for testing or reprocessing existing issues)

### Pipeline Stages

#### 1. Plan Issue
- Extracts issue details (title, body, number)
- Prepares context for downstream agents

#### 2. Validate Handball Context
- Checks if the issue contains handball-related terminology
- Gates the pipeline - only issues with sports context proceed
- Example keywords: "handball", "goalkeeper", "pivot", "wing", "6m", "9m", "7m", etc.

#### 3. Gate Check
- Decides whether to proceed based on validation results
- Posts status comment to the issue

#### 4. Implementation Agent ⭐
- **NEW**: Uses the GitHub Copilot Agent Tasks API to programmatically trigger a coding agent
- API endpoint: `POST /agents/repos/{owner}/{repo}/tasks`
- Passes comprehensive instructions including:
  - Issue details
  - Planner context
  - Requirements (handball rules, offline-first, Android build, etc.)
- Agent creates a branch, implements changes, and opens a PR with `Fixes #<issue_number>`

#### 5. Wait for PR
- Polls the issue timeline for up to 40 minutes (80 attempts × 30 seconds)
- Looks for a cross-referenced PR linked to the issue
- Fails if no PR is created within the time window

#### 6. Review Agent
- Posts a comment on the PR requesting code review
- Uses `@copilot` mention with `pr-reviewer` custom agent

#### 7. Fix Comments Agent
- Posts a comment requesting fixes for review feedback
- Uses `@copilot` mention with `review-comment-fixer` custom agent

#### 8. Auto-Merge
- Enables auto-merge on the PR (squash method)
- PR will merge automatically once all checks pass

## Key Changes (2026-07-20)

### Problem
The original workflow posted `@copilot` comments to trigger agents, but this approach didn't actually invoke the agents. Comments alone don't start Copilot agent sessions - they're just notifications that require manual user interaction.

### Solution
Updated the `implementation-agent` job to use the **GitHub Copilot Agent Tasks REST API**:

```javascript
const response = await github.request('POST /agents/repos/{owner}/{repo}/tasks', {
  owner: context.repo.owner,
  repo: context.repo.repo,
  prompt: prompt,  // Comprehensive task instructions
  base_ref: context.ref || 'master',
  create_pull_request: true,
  headers: {
    'X-GitHub-Api-Version': '2026-03-10'
  }
});
```

### Benefits
- ✅ Agents are **actually executed** programmatically
- ✅ PRs are **automatically created** with proper issue linkage
- ✅ No manual intervention required
- ✅ Fallback to comment-based approach if API fails
- ✅ Full error handling and status reporting

### Removed
- Removed `ui-polish-agent` step - the main implementation agent now handles the complete task in one pass to ensure a single, cohesive PR

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

- **API Access**: The Copilot Agent Tasks API requires:
  - Personal access token, OAuth token, or GitHub App user-to-server token
  - Installation tokens are NOT supported

## Troubleshooting

### Agent Not Triggering
- Check workflow run logs for API errors
- Verify token permissions
- Look for fallback comment in the issue (indicates API failure)

### No PR Created
- Check the agent task was created successfully (look for Task ID in comments)
- Verify the agent completed its work (may take 5-30 minutes)
- Check that the PR includes `Fixes #<issue_number>` for auto-linking

### Pipeline Timing Out
- The wait-for-pr step allows 40 minutes for agent completion
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
